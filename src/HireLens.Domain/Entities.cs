namespace HireLens.Domain;

public static class Roles { public const string Admin = "Admin"; public const string HR = "HR"; }

public class Analysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoleTitle { get; set; } = "";
    public string JdText { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CvResult> Results { get; set; } = new();
}

public enum CvStatus { Pending = 0, Completed = 1, Failed = 2 }

public class CvResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public Analysis? Analysis { get; set; }
    public string FileName { get; set; } = "";
    public string? StoredPath { get; set; }
    public CvStatus Status { get; set; }
    public string? Error { get; set; }
    public int AtsScore { get; set; }
    public int CvScore { get; set; }
    public int FitScore { get; set; }
    public string Verdict { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public string Model { get; set; } = "";
    public bool FromCache { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AnalysisCacheEntry
{
    public string Hash { get; set; } = "";   // SHA-256 hex of JD + CV + model
    public string ResultJson { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
