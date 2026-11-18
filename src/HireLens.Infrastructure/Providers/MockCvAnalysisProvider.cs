using System.Text.RegularExpressions;
using HireLens.Application.Analysis;

namespace HireLens.Infrastructure.Providers;

/// <summary>Deterministic keyword-based scoring. Used with no API key and in tests.</summary>
public partial class MockCvAnalysisProvider : ICvAnalysisProvider
{
    public string ModelName => "mock-v1";
    public bool UsesModelOverride => false;

    [GeneratedRegex(@"\b\d+(\.\d+)?\s*(%|x|k|m|million|users|requests|staff|people|days|hours)\b|\b\d{2,}\b", RegexOptions.IgnoreCase)] private static partial Regex Metric();
    [GeneratedRegex(@"\b(built|led|delivered|designed|developed|migrated|reduced|increased|optimi[sz]ed|launched|mentored|introduced|improved|managed|deployed|created)\b", RegexOptions.IgnoreCase)] private static partial Regex Verb();

    public Task<ProviderResult> AnalyseAsync(ProviderRequest r, CancellationToken ct = default)
    {
        var ats = r.Ats;
        int metrics = Metric().Matches(r.CvText).Count, verbs = Verb().Matches(r.CvText).Count;
        int cv = Math.Clamp((int)Math.Round(ats.Checks.Sum(c => c.Points) * 0.9 + Math.Min(metrics, 6) * 2.0 + Math.Min(verbs, 8) * 1.0), 0, 100);
        int fit = Math.Clamp((int)Math.Round(ats.KeywordCoveragePct * 0.8 + cv * 0.2), 0, 100);

        var o = new AnalysisOutput
        {
            AtsScore = ats.Score, CvScore = cv, FitScore = fit, FitVerdict = Verdicts.FromScore(fit),
            MatchedSkills = ats.MatchedKeywords.Take(25).ToList(),
            MissingSkills = ats.MissingKeywords.Take(25).ToList(),
            Gaps = ats.MissingKeywords.Take(5).Select(k => new Gap($"Skill: {k}", $"The job description mentions '{k}' but the CV does not.", $"Add concrete evidence of {k} (project, outcome, years of use) if the candidate has it.")).ToList(),
            Strengths = new(), RedFlags = new()
        };
        if (metrics >= 3) o.Strengths.Add("Quantified achievements");
        if (verbs >= 4) o.Strengths.Add("Strong action verbs");
        if (ats.KeywordCoveragePct >= 70) o.Strengths.Add("High coverage of required keywords");
        foreach (var c in ats.Checks.Where(c => !c.Passed))
            if (c.Name.StartsWith("Contact") || c.Name.StartsWith("Dates")) o.RedFlags.Add($"{c.Name}: {c.Note}");
        if (metrics == 0) o.Gaps.Add(new Gap("Impact", "No measurable results found.", "Add numbers (%, volumes, savings) to achievements."));
        o.Summary = $"Keyword coverage {ats.KeywordCoveragePct:0}% ({ats.MatchedKeywords.Count}/{ats.JdKeywords.Count}). ATS {ats.Score}, CV quality {cv}. Verdict: {o.FitVerdict}.";
        o.AtsBreakdown = ats;
        return Task.FromResult(new ProviderResult(o, 0, 0, ModelName));
    }
}
