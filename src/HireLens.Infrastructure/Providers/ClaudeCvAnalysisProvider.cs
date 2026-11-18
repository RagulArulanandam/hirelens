using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HireLens.Application.Analysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Providers;

/// <summary>One Anthropic Messages API call per CV. Strict JSON, one retry on malformed output.</summary>
public class ClaudeCvAnalysisProvider(HttpClient http, IConfiguration cfg, ILogger<ClaudeCvAnalysisProvider> log) : ICvAnalysisProvider
{
    public const string Endpoint = "https://api.anthropic.com/v1/messages";
    public string ModelName => cfg["Anthropic:Model"] ?? "claude-haiku-5-5";
    public bool UsesModelOverride => true;

    private const string System =
        "You are an expert recruiter. Compare a CV to a job description. Reply with ONE JSON object only, no prose, no markdown. " +
        "Schema: {\"atsScore\":int,\"cvScore\":int,\"fitVerdict\":\"Strong fit\"|\"Possible fit\"|\"Not a fit\",\"fitScore\":int," +
        "\"matchedSkills\":[str],\"missingSkills\":[str],\"gaps\":[{\"area\":str,\"detail\":str,\"suggestion\":str}],\"strengths\":[str],\"redFlags\":[str],\"summary\":str(max 60 words)}. " +
        "Scores are 0-100. cvScore = CV writing quality and impact. fitScore = suitability for the role. Be concise: max 6 gaps, 6 strengths.";

    public async Task<ProviderResult> AnalyseAsync(ProviderRequest r, CancellationToken ct = default)
    {
        var key = cfg["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Anthropic:ApiKey is not configured.");

        var user = $"JOB DESCRIPTION:\n{r.JdText}\n\nCV:\n{r.CvText}\n\n" +
                   $"PRE-COMPUTED ATS (do not recompute): score={r.Ats.Score}, keyword coverage={r.Ats.KeywordCoveragePct}%, " +
                   $"matched=[{string.Join(", ", r.Ats.MatchedKeywords)}], missing=[{string.Join(", ", r.Ats.MissingKeywords)}]. " +
                   "Judge what keywords cannot: seniority, relevance, achievements, red flags.";

        int inTok = 0, outTok = 0;
        var messages = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = user } };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var (text, i, o) = await CallAsync(key, r.Model, messages, ct);
            inTok += i; outTok += o;
            try
            {
                var parsed = AnalysisJsonParser.Parse(text);
                parsed.AtsScore = r.Ats.Score; parsed.AtsBreakdown = r.Ats;
                log.LogInformation("Claude analysis ok model={Model} inputTokens={In} outputTokens={Out}", r.Model, inTok, outTok);
                return new ProviderResult(parsed, inTok, outTok, r.Model);
            }
            catch (InvalidAnalysisJsonException ex) when (attempt == 0)
            {
                log.LogWarning("Malformed JSON from model, retrying once: {Reason}", ex.Message);
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = text });
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = "Return valid JSON only, matching the schema exactly." });
            }
        }
        throw new InvalidAnalysisJsonException("Model returned invalid JSON twice.");
    }

    private async Task<(string text, int inTok, int outTok)> CallAsync(string key, string model, JsonArray messages, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = cfg.GetValue("Anthropic:MaxTokens", 1200),
            ["system"] = System,
            ["messages"] = JsonNode.Parse(messages.ToJsonString())
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            log.LogError("Anthropic API error {Status}", (int)resp.StatusCode);   // body not logged: may echo CV text
            throw new HttpRequestException($"Anthropic API returned {(int)resp.StatusCode}.");
        }
        var doc = JsonNode.Parse(raw)!;
        var text = doc["content"]?.AsArray().Select(c => c?["text"]?.GetValue<string>()).FirstOrDefault(t => t is not null) ?? "";
        return (text, doc["usage"]?["input_tokens"]?.GetValue<int>() ?? 0, doc["usage"]?["output_tokens"]?.GetValue<int>() ?? 0);
    }
}
