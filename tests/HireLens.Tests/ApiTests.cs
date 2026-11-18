using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HireLens.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HireLens.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@hirelens.local", AdminPw = "Test!Passw0rd-123";
    private readonly string _db = Guid.NewGuid().ToString();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "hirelens-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-1234567890");
        b.UseSetting("Seed:AdminPassword", AdminPw);
        b.UseSetting("Anthropic:ApiKey", "");           // force mock provider
        b.UseSetting("Analysis:UploadPath", _uploads);
        b.UseSetting("RateLimit:PermitPerMinute", "1000");
        b.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            foreach (var d in s.Where(d => d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true).ToList()) s.Remove(d);
            s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_db));
        });
    }

    public async Task<HttpClient> LoggedIn(string email = AdminEmail, string pw = AdminPw)
    {
        var c = CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = pw });
        r.EnsureSuccessStatusCode();
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", j.GetProperty("token").GetString());
        return c;
    }
}

public class ApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static MultipartFormDataContent Form(params (string name, string value)[] fields)
    {
        var m = new MultipartFormDataContent();
        foreach (var (n, v) in fields) m.Add(new StringContent(v), n);
        return m;
    }

    private static MultipartFormDataContent FileForm(string content, string fileName)
    {
        var m = new MultipartFormDataContent();
        var sc = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        m.Add(sc, "file", fileName);
        return m;
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword_AndSucceeds_WithSeededAdmin()
    {
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = "nope-nope-nope" })).StatusCode);
        var authed = await f.LoggedIn();
        var me = await authed.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Admin", me.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Endpoints_RequireAuth_AndErrorsAreProblemDetails_WithCorrelationId()
    {
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/analyses")).StatusCode);
        var bad = await c.PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("application/problem+json", bad.Content.Headers.ContentType!.MediaType);
        Assert.True(bad.Headers.Contains("X-Correlation-ID"));
        Assert.True(bad.Headers.Contains("X-Response-Time-ms"));
    }

    [Fact]
    public async Task FullFlow_Create_UploadCvs_Rank_List_Search_Delete()
    {
        var c = await f.LoggedIn();
        var create = await c.PostAsync("/api/analyses", Form(("roleTitle", "Senior .NET Developer"), ("jdText", Samples.Jd)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var strong = await c.PostAsync($"/api/analyses/{id}/cvs", FileForm(Samples.Strong, "strong.txt"));
        var weak = await c.PostAsync($"/api/analyses/{id}/cvs", FileForm(Samples.Weak.PadRight(120, ' ') + " more text to pass the minimum length check of the extractor.", "weak.txt"));
        Assert.Equal(HttpStatusCode.OK, strong.StatusCode);
        Assert.Equal("Completed", (await strong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);

        // identical CV again -> cache hit, zero tokens
        var again = await (await c.PostAsync($"/api/analyses/{id}/cvs", FileForm(Samples.Strong, "strong-copy.txt"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(again.GetProperty("fromCache").GetBoolean());

        var detail = await c.GetFromJsonAsync<JsonElement>($"/api/analyses/{id}");
        var results = detail.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(3, results.Count);
        Assert.True(results[0].GetProperty("fitScore").GetInt32() >= results[^1].GetProperty("fitScore").GetInt32()); // ranked

        var list = await c.GetFromJsonAsync<JsonElement>("/api/analyses?search=.NET");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
        Assert.Equal(0, (await c.GetFromJsonAsync<JsonElement>("/api/analyses?search=zzz-none")).GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/analyses/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/analyses/{id}")).StatusCode);
    }

    [Fact]
    public async Task Validation_RejectsBadInput()
    {
        var c = await f.LoggedIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/analyses", Form(("roleTitle", "")))).StatusCode);
        var id = (await (await c.PostAsync("/api/analyses", Form(("roleTitle", "Dev"), ("jdText", Samples.Jd)))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync($"/api/analyses/{id}/cvs", FileForm("x", "malware.exe"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync($"/api/analyses/{id}/cvs", FileForm("not a pdf", "fake.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/analyses/{Guid.NewGuid()}/cvs", FileForm("x", "a.txt"))).StatusCode);
    }
}
