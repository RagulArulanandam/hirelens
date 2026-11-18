using System.Text.Json;

namespace HireLens.Application.Analysis;

public class InvalidAnalysisJsonException(string message) : Exception(message);

/// <summary>Parses and validates the strict-JSON output expected from the LLM.</summary>
public static class AnalysisJsonParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    public static JsonSerializerOptions JsonOptions => Options;

    public static AnalysisOutput Parse(string raw)
    {
        var json = ExtractJson(raw);
        AnalysisOutput? o;
        try { o = JsonSerializer.Deserialize<AnalysisOutput>(json, Options); }
        catch (JsonException ex) { throw new InvalidAnalysisJsonException("Malformed JSON: " + ex.Message); }
        if (o is null) throw new InvalidAnalysisJsonException("Empty JSON");

        foreach (var (name, v) in new[] { ("atsScore", o.AtsScore), ("cvScore", o.CvScore), ("fitScore", o.FitScore) })
            if (v is < 0 or > 100) throw new InvalidAnalysisJsonException($"{name} out of range 0-100: {v}");
        if (o.FitVerdict is not (Verdicts.Strong or Verdicts.Possible or Verdicts.NotFit))
            throw new InvalidAnalysisJsonException($"Invalid fitVerdict: {o.FitVerdict}");
        if (string.IsNullOrWhiteSpace(o.Summary)) throw new InvalidAnalysisJsonException("summary missing");

        o.MatchedSkills ??= new(); o.MissingSkills ??= new(); o.Gaps ??= new(); o.Strengths ??= new(); o.RedFlags ??= new();
        var words = o.Summary.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 60) o.Summary = string.Join(' ', words.Take(60));
        return o;
    }

    /// <summary>Strips markdown fences / surrounding prose and returns the outermost JSON object.</summary>
    public static string ExtractJson(string raw)
    {
        var s = raw?.Trim() ?? "";
        var start = s.IndexOf('{'); var end = s.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidAnalysisJsonException("No JSON object found");
        return s[start..(end + 1)];
    }
}
