using System.Text.RegularExpressions;

namespace HireLens.Application.Analysis;

/// <summary>
/// Deterministic, explainable ATS scoring. 100 points:
/// keyword coverage 60, sections 15, contact info 10, dates 5, format 10.
/// </summary>
public static partial class AtsScorer
{
    public const int KeywordWeight = 60;

    // Curated skill vocabulary (lower-case). Multi-word entries are matched as phrases.
    private static readonly string[] SkillVocabulary =
    [
        "c#", ".net", "asp.net", "entity framework", "ef core", "linq", "sql server", "sql", "postgresql", "mysql", "mongodb", "redis",
        "azure", "aws", "gcp", "docker", "kubernetes", "terraform", "ci/cd", "jenkins", "github actions", "git",
        "angular", "react", "vue", "typescript", "javascript", "html", "css", "sass", "rxjs", "ngrx", "node.js", "next.js",
        "java", "spring", "python", "django", "flask", "go", "rust", "php", "ruby", "kotlin", "swift", "scala",
        "rest", "restful", "graphql", "grpc", "microservices", "rabbitmq", "kafka", "signalr", "oauth", "jwt", "openid",
        "unit testing", "xunit", "nunit", "jest", "cypress", "selenium", "tdd", "agile", "scrum", "kanban", "jira",
        "machine learning", "deep learning", "nlp", "pandas", "numpy", "tensorflow", "pytorch", "power bi", "tableau", "excel", "etl", "spark",
        "devops", "linux", "bash", "powershell", "design patterns", "clean architecture", "ddd", "solid", "oop",
        "project management", "stakeholder management", "leadership", "mentoring", "communication", "problem solving", "teamwork",
        "recruitment", "talent acquisition", "onboarding", "payroll", "hris", "employee relations", "sourcing", "interviewing",
        "salesforce", "sap", "figma", "seo", "marketing", "accounting", "budgeting", "forecasting", "customer service"
    ];

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","with","you","our","are","will","have","has","this","that","from","your","who","can","all","any","not","but","job","role","work",
        "team","years","year","experience","ability","strong","good","skills","skill","knowledge","required","preferred","must","should","including","etc",
        "about","across","other","such","also","well","new","use","using","able","looking","join","company","candidate","responsibilities","requirements",
        "plus","within","their","they","been","being","into","more","than","help","make","working","build","great","excellent","understanding"
    };

    private static readonly (string name, string[] aliases)[] Sections =
    [
        ("Experience", ["experience", "employment", "work history", "professional background"]),
        ("Education", ["education", "qualifications", "academic"]),
        ("Skills", ["skills", "technologies", "technical skills", "competencies"]),
        ("Summary", ["summary", "profile", "objective", "about me"])
    ];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")] private static partial Regex EmailRx();
    [GeneratedRegex(@"(\+?\d[\d\s().-]{7,}\d)")] private static partial Regex PhoneRx();
    [GeneratedRegex(@"\b(19|20)\d{2}\b")] private static partial Regex YearRx();
    [GeneratedRegex(@"\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?\s+(19|20)\d{2}\b", RegexOptions.IgnoreCase)] private static partial Regex MonthYearRx();
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+#./-]{2,}")] private static partial Regex WordRx();
    [GeneratedRegex(@"^\s*[-•*▪●◦]\s+", RegexOptions.Multiline)] private static partial Regex BulletRx();

    public static AtsReport Score(string jdText, string cvText)
    {
        var report = new AtsReport();
        var jdKeywords = ExtractKeywords(jdText);
        var cvLower = " " + cvText.ToLowerInvariant() + " ";
        report.JdKeywords = jdKeywords;
        foreach (var k in jdKeywords) (Contains(cvLower, k) ? report.MatchedKeywords : report.MissingKeywords).Add(k);

        report.KeywordCoveragePct = jdKeywords.Count == 0 ? 0 : Math.Round(100.0 * report.MatchedKeywords.Count / jdKeywords.Count, 1);
        report.KeywordPoints = (int)Math.Round(report.KeywordCoveragePct / 100.0 * KeywordWeight);

        int total = report.KeywordPoints;

        // Sections (15 pts)
        var found = Sections.Where(s => s.aliases.Any(a => Regex.IsMatch(cvLower, $@"(^|\n)\s*{Regex.Escape(a)}\b"))).Select(s => s.name).ToHashSet();
        var perSection = new Dictionary<string, int> { ["Experience"] = 5, ["Education"] = 4, ["Skills"] = 4, ["Summary"] = 2 };
        foreach (var (name, _) in Sections)
        {
            var ok = found.Contains(name);
            report.Checks.Add(new AtsCheck($"Section: {name}", ok, ok ? perSection[name] : 0, perSection[name], ok ? "Found" : "Not detected"));
            if (ok) total += perSection[name];
        }

        // Contact (10 pts)
        bool email = EmailRx().IsMatch(cvText), phone = PhoneRx().IsMatch(cvText);
        report.Checks.Add(new AtsCheck("Contact: email", email, email ? 5 : 0, 5, email ? "Found" : "Missing"));
        report.Checks.Add(new AtsCheck("Contact: phone", phone, phone ? 5 : 0, 5, phone ? "Found" : "Missing"));
        total += (email ? 5 : 0) + (phone ? 5 : 0);

        // Dates (5 pts)
        int years = YearRx().Matches(cvText).Count + MonthYearRx().Matches(cvText).Count;
        bool dates = years >= 2;
        report.Checks.Add(new AtsCheck("Dates present", dates, dates ? 5 : 0, 5, $"{years} date mention(s)"));
        total += dates ? 5 : 0;

        // Format (10 pts): length + bullets
        int words = WordRx().Matches(cvText).Count;
        bool lenOk = words is >= 200 and <= 1500;
        bool bullets = BulletRx().Matches(cvText).Count >= 3;
        report.Checks.Add(new AtsCheck("Length (200-1500 words)", lenOk, lenOk ? 6 : 0, 6, $"{words} words"));
        report.Checks.Add(new AtsCheck("Bulleted achievements", bullets, bullets ? 4 : 0, 4, bullets ? "Found" : "Fewer than 3 bullets"));
        total += (lenOk ? 6 : 0) + (bullets ? 4 : 0);

        report.Score = Math.Clamp(total, 0, 100);
        return report;
    }

    /// <summary>Skills from the vocabulary found in the JD, plus up to 12 repeated domain terms.</summary>
    public static List<string> ExtractKeywords(string jdText)
    {
        var lower = " " + jdText.ToLowerInvariant() + " ";
        var result = SkillVocabulary.Where(s => Contains(lower, s)).ToList();

        var freq = WordRx().Matches(lower).Select(m => m.Value.Trim('.', '-', '/'))
            .Where(w => w.Length >= 4 && !Stop.Contains(w))
            .GroupBy(w => w).Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count()).Select(g => g.Key);
        foreach (var w in freq)
        {
            if (result.Count >= 40) break;
            if (!result.Any(r => r.Contains(w) || w.Contains(r))) result.Add(w);
        }
        return result.Distinct().Take(40).ToList();
    }

    private static bool Contains(string haystackLower, string needle)
    {
        var idx = 0;
        while ((idx = haystackLower.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            var before = idx == 0 ? ' ' : haystackLower[idx - 1];
            var afterIdx = idx + needle.Length;
            var after = afterIdx >= haystackLower.Length ? ' ' : haystackLower[afterIdx];
            if (!char.IsLetterOrDigit(before) && !(char.IsLetterOrDigit(after) || (after is '#' or '+' && !needle.EndsWith('#') && !needle.EndsWith('+')))) return true;
            idx += needle.Length;
        }
        return false;
    }
}
