using System.Text;
using HireLens.Application.Analysis;
using HireLens.Infrastructure.Extraction;
using Xunit;

namespace HireLens.Tests;

public static class Samples
{
    private static string Dir
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "samples"))) d = d.Parent;
            return Path.Combine(d!.FullName, "samples");
        }
    }
    public static string Jd => File.ReadAllText(Path.Combine(Dir, "jd-senior-dotnet-developer.txt"));
    public static string Strong => File.ReadAllText(Path.Combine(Dir, "cv-strong-alex-morgan.txt"));
    public static string Weak => File.ReadAllText(Path.Combine(Dir, "cv-weak-sam-lee.txt"));
}

public class AtsScorerTests
{
    [Fact]
    public void ExtractKeywords_FindsSkillsFromJd()
    {
        var k = AtsScorer.ExtractKeywords(Samples.Jd);
        Assert.Contains("c#", k); Assert.Contains("angular", k); Assert.Contains("sql server", k);
        Assert.Contains("docker", k); Assert.Contains("xunit", k);
    }

    [Fact]
    public void StrongCv_ScoresHigh_WeakCv_ScoresLow()
    {
        var strong = AtsScorer.Score(Samples.Jd, TextNormalizer.Normalize(Samples.Strong, 12000));
        var weak = AtsScorer.Score(Samples.Jd, TextNormalizer.Normalize(Samples.Weak, 12000));
        Assert.True(strong.Score >= 70, $"strong was {strong.Score}");
        Assert.True(weak.Score <= 25, $"weak was {weak.Score}");
        Assert.True(strong.KeywordCoveragePct > weak.KeywordCoveragePct);
    }

    [Fact]
    public void Breakdown_IsExplainable_AndSumsToScore()
    {
        var r = AtsScorer.Score(Samples.Jd, Samples.Strong);
        Assert.Equal(r.Score, Math.Min(100, r.KeywordPoints + r.Checks.Sum(c => c.Points)));
        Assert.All(r.Checks, c => Assert.True(c.Points <= c.MaxPoints));
        Assert.Equal(r.JdKeywords.Count, r.MatchedKeywords.Count + r.MissingKeywords.Count);
    }

    [Fact]
    public void ContactAndSectionChecks_Detected()
    {
        var r = AtsScorer.Score(Samples.Jd, Samples.Strong);
        Assert.True(r.Checks.Single(c => c.Name == "Contact: email").Passed);
        Assert.True(r.Checks.Single(c => c.Name == "Contact: phone").Passed);
        Assert.True(r.Checks.Single(c => c.Name == "Section: Experience").Passed);
        Assert.False(AtsScorer.Score(Samples.Jd, Samples.Weak).Checks.Single(c => c.Name == "Contact: email").Passed);
    }

    [Fact]
    public void KeywordMatching_RespectsWordBoundaries()
    {
        var r = AtsScorer.Score("We need Java developers. Java Java.", "I know javascript only.");
        Assert.Contains("java", r.MissingKeywords);
    }
}

public class NormalizerAndExtractorTests
{
    [Fact]
    public void Normalize_CollapsesWhitespace_StripsBoilerplate_AndCaps()
    {
        var t = TextNormalizer.Normalize("Resume\n\n\n\nHello    world\t\tfoo\nPage 1 of 2\n", 100);
        Assert.Equal("Hello world foo", t);
        Assert.Equal(5, TextNormalizer.Normalize("abcdefghij", 5).Length);
        Assert.Equal("", TextNormalizer.Normalize(null, 10));
    }

    [Fact]
    public void Extract_Txt_Works_AndUnsupportedThrows()
    {
        var ex = new TextExtractor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("hello cv"));
        Assert.Equal("hello cv", ex.Extract(ms, "a.txt"));
        Assert.Throws<NotSupportedException>(() => ex.Extract(new MemoryStream(), "a.exe"));
    }

    [Fact]
    public void Extract_Docx_RoundTrip()
    {
        using var ms = new MemoryStream();
        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new DocumentFormat.OpenXml.Wordprocessing.Body(
                new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("C# developer")))));
            main.Document.Save();
        }
        ms.Position = 0;
        Assert.Contains("C# developer", new TextExtractor().Extract(ms, "cv.docx"));
    }
}

public class JsonParserTests
{
    private const string Valid = """
        ```json
        {"atsScore":80,"cvScore":70,"fitVerdict":"Strong fit","fitScore":82,"matchedSkills":["C#"],"missingSkills":["Kafka"],
         "gaps":[{"area":"Messaging","detail":"No Kafka","suggestion":"Add a project"}],"strengths":["Leadership"],"redFlags":[],"summary":"Solid candidate."}
        ```
        """;

    [Fact]
    public void Parse_ValidJson_InsideFences()
    {
        var o = AnalysisJsonParser.Parse(Valid);
        Assert.Equal(80, o.AtsScore); Assert.Equal("Strong fit", o.FitVerdict);
        Assert.Single(o.Gaps); Assert.Equal("Messaging", o.Gaps[0].Area);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"atsScore\":150,\"cvScore\":1,\"fitVerdict\":\"Strong fit\",\"fitScore\":1,\"summary\":\"x\"}")]
    [InlineData("{\"atsScore\":1,\"cvScore\":1,\"fitVerdict\":\"Maybe\",\"fitScore\":1,\"summary\":\"x\"}")]
    [InlineData("{\"atsScore\":1,\"cvScore\":1,\"fitVerdict\":\"Strong fit\",\"fitScore\":1}")]
    [InlineData("{\"atsScore\":\"abc\"}")]
    public void Parse_Invalid_Throws(string raw) => Assert.Throws<InvalidAnalysisJsonException>(() => AnalysisJsonParser.Parse(raw));

    [Fact]
    public void Parse_TruncatesLongSummary()
    {
        var long80 = string.Join(' ', Enumerable.Repeat("word", 80));
        var o = AnalysisJsonParser.Parse($"{{\"atsScore\":1,\"cvScore\":1,\"fitVerdict\":\"Not a fit\",\"fitScore\":1,\"summary\":\"{long80}\"}}");
        Assert.Equal(60, o.Summary.Split(' ').Length);
    }
}
