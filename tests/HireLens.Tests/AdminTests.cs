using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireLens.Api.Services;
using HireLens.Domain;
using HireLens.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HireLens.Tests;

public class AdminTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string HrPw = "Hr!Passw0rd-456";

    private async Task<HttpClient> HrClient(HttpClient admin, string email)
    {
        var r = await admin.PostAsJsonAsync("/api/admin/users", new { email, fullName = "Hannah HR", password = HrPw, role = "HR" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await f.LoggedIn(email, HrPw);
    }

    private static MultipartFormDataContent Jd(string title)
    {
        var m = new MultipartFormDataContent();
        m.Add(new StringContent(title), "roleTitle"); m.Add(new StringContent(Samples.Jd), "jdText");
        return m;
    }

    [Fact]
    public async Task HrUser_CannotUseAdminEndpoints_AndOnlySeesOwnAnalyses()
    {
        var admin = await f.LoggedIn();
        var hr = await HrClient(admin, "hr1@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/admin/stats")).StatusCode);

        var adminAnalysis = (await (await admin.PostAsync("/api/analyses", Jd("Admin-only role"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync($"/api/analyses/{adminAnalysis}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.DeleteAsync($"/api/analyses/{adminAnalysis}")).StatusCode);
        Assert.Equal(0, (await hr.GetFromJsonAsync<JsonElement>("/api/analyses")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Admin_Settings_Stats_AndUserRules()
    {
        var admin = await f.LoggedIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users", new { email = "bad", fullName = "x", password = "short", role = "Boss" })).StatusCode);

        var s = await admin.GetFromJsonAsync<JsonElement>("/api/admin/stats");
        Assert.True(s.GetProperty("cacheHitRate").GetDouble() >= 0);

        // Mock provider ignores the override, so the effective model stays mock; the setting itself must persist.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/admin/settings/model", new { model = "claude-sonnet-5-5" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings/model", new { model = "bad model!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/admin/settings/retention", new { days = 7 })).StatusCode);
        Assert.Equal(7, (await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings")).GetProperty("retentionDays").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings/retention", new { days = -1 })).StatusCode);

        var me = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("email").GetString();
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/admin/users");
        var myId = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == me).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/admin/users/{myId}")).StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsPdf()
    {
        var c = await f.LoggedIn();
        var id = (await (await c.PostAsync("/api/analyses", Jd("PDF role"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var form = new MultipartFormDataContent { { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(Samples.Strong)), "file", "cv.txt" } };
        await c.PostAsync($"/api/analyses/{id}/cvs", form);
        var r = await c.GetAsync($"/api/analyses/{id}/export");
        if (!r.IsSuccessStatusCode) Assert.Fail(await r.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", r.Content.Headers.ContentType!.MediaType);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Retention_DeletesOldFiles_KeepsResults()
    {
        var c = await f.LoggedIn();
        await c.PutAsJsonAsync("/api/admin/settings/retention", new { days = 1 });
        var id = (await (await c.PostAsync("/api/analyses", Jd("Retention role"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var form = new MultipartFormDataContent { { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(Samples.Strong)), "file", "old.txt" } };
        await c.PostAsync($"/api/analyses/{id}/cvs", form);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cv = await db.CvResults.FirstAsync(r => r.AnalysisId == id);
        Assert.True(File.Exists(cv.StoredPath));
        cv.CreatedAt = DateTime.UtcNow.AddDays(-5);
        await db.SaveChangesAsync();

        var svc = new RetentionService(f.Services.GetRequiredService<IServiceScopeFactory>(), f.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RetentionService>.Instance);
        Assert.True(await svc.SweepAsync(default) >= 1);

        await db.Entry(cv).ReloadAsync();
        Assert.Null(cv.StoredPath);
        Assert.Equal(CvStatus.Completed, cv.Status);
    }
}
