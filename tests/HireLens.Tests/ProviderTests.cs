using System.Net;
using System.Text;
using HireLens.Application.Analysis;
using HireLens.Infrastructure;
using HireLens.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HireLens.Tests;

public class ProviderTests
{
    private static IConfiguration Cfg(string? key = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Anthropic:ApiKey"] = key, ["Anthropic:Model"] = "claude-haiku-5-5" }).Build();

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Mock_IsDeterministic_AndRanksStrongAboveWeak()
    {
        var p = new MockCvAnalysisProvider();
        var s = await p.AnalyseAsync(new(Samples.Jd, Samples.Strong, AtsScorer.Score(Samples.Jd, Samples.Strong), "mock-v1"));
        var s2 = await p.AnalyseAsync(new(Samples.Jd, Samples.Strong, AtsScorer.Score(Samples.Jd, Samples.Strong), "mock-v1"));
        var w = await p.AnalyseAsync(new(Samples.Jd, Samples.Weak, AtsScorer.Score(Samples.Jd, Samples.Weak), "mock-v1"));
        Assert.Equal(s.Output.FitScore, s2.Output.FitScore);
        Assert.Equal("Strong fit", s.Output.FitVerdict);
        Assert.Equal("Not a fit", w.Output.FitVerdict);
        Assert.NotEmpty(w.Output.MissingSkills);
        Assert.Equal(0, s.InputTokens);
    }

    [Fact]
    public async Task Service_CachesByHash_SecondRunIsFree()
    {
        using var db = NewDb();
        var svc = new CvAnalysisService(db, new MockCvAnalysisProvider(), Cfg(), NullLogger<CvAnalysisService>.Instance);
        var a = await svc.RunAsync(Samples.Jd, Samples.Strong);
        var b = await svc.RunAsync(Samples.Jd, Samples.Strong);
        var c = await svc.RunAsync(Samples.Jd, Samples.Weak);
        Assert.False(a.FromCache); Assert.True(b.FromCache); Assert.False(c.FromCache);
        Assert.Equal(a.Output.FitScore, b.Output.FitScore);
        Assert.Equal(2, await db.CacheEntries.CountAsync());
        Assert.NotEqual(CvAnalysisService.Hash("a", "b", "m1"), CvAnalysisService.Hash("a", "b", "m2"));
    }

    [Fact]
    public async Task Service_RejectsEmptyCv()
    {
        using var db = NewDb();
        var svc = new CvAnalysisService(db, new MockCvAnalysisProvider(), Cfg(), NullLogger<CvAnalysisService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RunAsync(Samples.Jd, "   "));
    }

    private class FakeHandler(params string[] replies) : HttpMessageHandler
    {
        public int Calls; public string? LastKey, LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastKey = req.Headers.GetValues("x-api-key").Single();
            LastBody = await req.Content!.ReadAsStringAsync(ct);
            var text = replies[Math.Min(Calls++, replies.Length - 1)];
            var json = System.Text.Json.JsonSerializer.Serialize(new { content = new[] { new { type = "text", text } }, usage = new { input_tokens = 900, output_tokens = 300 } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private const string Good = "{\"atsScore\":10,\"cvScore\":70,\"fitVerdict\":\"Possible fit\",\"fitScore\":60,\"matchedSkills\":[\"C#\"],\"missingSkills\":[],\"gaps\":[],\"strengths\":[],\"redFlags\":[],\"summary\":\"ok\"}";

    [Fact]
    public async Task Claude_RetriesOnceOnMalformedJson_AndSumsTokens()
    {
        var h = new FakeHandler("sorry, here you go: not json", Good);
        var p = new ClaudeCvAnalysisProvider(new HttpClient(h), Cfg("test-key"), NullLogger<ClaudeCvAnalysisProvider>.Instance);
        var ats = AtsScorer.Score(Samples.Jd, Samples.Strong);
        var r = await p.AnalyseAsync(new(Samples.Jd, Samples.Strong, ats, "claude-haiku-5-5"));
        Assert.Equal(2, h.Calls);
        Assert.Equal(1800, r.InputTokens); Assert.Equal(600, r.OutputTokens);
        Assert.Equal(ats.Score, r.Output.AtsScore);          // deterministic score overrides the LLM's
        Assert.Equal("test-key", h.LastKey);
        Assert.Contains("claude-haiku-5-5", h.LastBody);
    }

    [Fact]
    public async Task Claude_FailsAfterTwoInvalidReplies()
    {
        var p = new ClaudeCvAnalysisProvider(new HttpClient(new FakeHandler("nope")), Cfg("k"), NullLogger<ClaudeCvAnalysisProvider>.Instance);
        await Assert.ThrowsAsync<InvalidAnalysisJsonException>(() => p.AnalyseAsync(new(Samples.Jd, Samples.Strong, AtsScorer.Score(Samples.Jd, Samples.Strong), "m")));
    }

    [Fact]
    public async Task Claude_WithoutKey_Throws()
    {
        var p = new ClaudeCvAnalysisProvider(new HttpClient(new FakeHandler(Good)), Cfg(), NullLogger<ClaudeCvAnalysisProvider>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.AnalyseAsync(new("a", "b", new AtsReport(), "m")));
    }
}
