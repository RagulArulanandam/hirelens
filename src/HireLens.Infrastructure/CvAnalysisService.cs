using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireLens.Application.Analysis;
using HireLens.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure;

public record AnalysisRun(AnalysisOutput Output, string Model, bool FromCache, int InputTokens, int OutputTokens);

public class CvAnalysisService(AppDbContext db, ICvAnalysisProvider provider, IConfiguration cfg, ILogger<CvAnalysisService> log)
{
    public const string ModelSettingKey = "DefaultModel";

    public async Task<string> GetModelAsync(CancellationToken ct = default)
    {
        if (!provider.UsesModelOverride) return provider.ModelName;
        var s = await db.Settings.FindAsync([ModelSettingKey], ct);
        return string.IsNullOrWhiteSpace(s?.Value) ? provider.ModelName : s.Value;
    }

    public static string Hash(string jd, string cv, string model) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jd + "\u0001" + cv + "\u0001" + model))).ToLowerInvariant();

    /// <summary>Normalise, score deterministically, then (cache miss only) make one LLM call.</summary>
    public async Task<AnalysisRun> RunAsync(string jdRaw, string cvRaw, CancellationToken ct = default)
    {
        var jd = TextNormalizer.Normalize(jdRaw, cfg.GetValue("Analysis:MaxJdChars", 6000));
        var cv = TextNormalizer.Normalize(cvRaw, cfg.GetValue("Analysis:MaxCvChars", 12000));
        if (cv.Length < 50) throw new InvalidOperationException("Could not extract enough text from the CV (scanned image PDF?).");

        var model = await GetModelAsync(ct);
        var hash = Hash(jd, cv, model);
        var hit = await db.CacheEntries.FindAsync([hash], ct);
        if (hit is not null)
        {
            log.LogInformation("Cache hit model={Model}", model);
            return new AnalysisRun(JsonSerializer.Deserialize<AnalysisOutput>(hit.ResultJson, AnalysisJsonParser.JsonOptions)!, model, true, 0, 0);
        }

        var ats = AtsScorer.Score(jd, cv);
        var res = await provider.AnalyseAsync(new ProviderRequest(jd, cv, ats, model), ct);
        res.Output.AtsBreakdown = ats;
        res.Output.AtsScore = ats.Score;
        res.Output.FitVerdict = Verdicts.FromScore(res.Output.FitScore);

        db.CacheEntries.Add(new AnalysisCacheEntry
        {
            Hash = hash, Model = model, InputTokens = res.InputTokens, OutputTokens = res.OutputTokens,
            ResultJson = JsonSerializer.Serialize(res.Output, AnalysisJsonParser.JsonOptions)
        });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); } // concurrent insert of same hash
        log.LogInformation("Analysed model={Model} inputTokens={In} outputTokens={Out}", model, res.InputTokens, res.OutputTokens);
        return new AnalysisRun(res.Output, model, false, res.InputTokens, res.OutputTokens);
    }
}
