using System.Text.Json.Serialization;

namespace HireLens.Application.Analysis;

public record Gap(string Area, string Detail, string Suggestion);

/// <summary>Final per-CV analysis output (stored and displayed).</summary>
public class AnalysisOutput
{
    public int AtsScore { get; set; }
    public int CvScore { get; set; }
    public string FitVerdict { get; set; } = "Not a fit";
    public int FitScore { get; set; }
    public List<string> MatchedSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public List<Gap> Gaps { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> RedFlags { get; set; } = new();
    public string Summary { get; set; } = "";
    /// <summary>Explainable ATS computation (deterministic, computed in C#).</summary>
    public AtsReport? AtsBreakdown { get; set; }
}

public record AtsCheck(string Name, bool Passed, int Points, int MaxPoints, string Note);

public class AtsReport
{
    public int Score { get; set; }
    public double KeywordCoveragePct { get; set; }
    public int KeywordPoints { get; set; }
    public List<string> JdKeywords { get; set; } = new();
    public List<string> MatchedKeywords { get; set; } = new();
    public List<string> MissingKeywords { get; set; } = new();
    public List<AtsCheck> Checks { get; set; } = new();
}

public record ProviderRequest(string JdText, string CvText, AtsReport Ats, string Model);

public record ProviderResult(AnalysisOutput Output, int InputTokens, int OutputTokens, string Model);

public interface ICvAnalysisProvider
{
    /// <summary>Default model name used for caching when no admin override exists.</summary>
    string ModelName { get; }
    bool UsesModelOverride { get; }
    Task<ProviderResult> AnalyseAsync(ProviderRequest request, CancellationToken ct = default);
}

public interface ITextExtractor
{
    /// <summary>Extracts plain text from PDF, DOCX or TXT.</summary>
    string Extract(Stream content, string fileName);
}

public static class Verdicts
{
    public const string Strong = "Strong fit";
    public const string Possible = "Possible fit";
    public const string NotFit = "Not a fit";
    public static string FromScore(int fit) => fit >= 75 ? Strong : fit >= 50 ? Possible : NotFit;
}
