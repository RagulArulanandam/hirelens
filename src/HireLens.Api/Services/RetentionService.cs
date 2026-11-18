using HireLens.Api.Controllers;
using HireLens.Domain;
using HireLens.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Services;

/// <summary>Privacy: deletes uploaded CV files older than N days (N from admin setting, 0 = keep). Scores stay; the original files go.</summary>
public class RetentionService(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<RetentionService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await SweepAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Retention sweep failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.Settings.FindAsync([AdminController.RetentionKey], ct);
        var days = int.TryParse(s?.Value, out var d) ? d : cfg.GetValue("Analysis:RetentionDays", 30);
        if (days <= 0) return 0;

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var old = await db.CvResults.Where(r => r.StoredPath != null && r.CreatedAt < cutoff).ToListAsync(ct);
        foreach (var r in old)
        {
            try { if (File.Exists(r.StoredPath)) File.Delete(r.StoredPath!); } catch (IOException) { continue; }
            r.StoredPath = null;
        }
        if (old.Count > 0) { await db.SaveChangesAsync(ct); log.LogInformation("Retention: removed {Count} CV file(s) older than {Days} days", old.Count, days); }
        return old.Count;
    }
}
