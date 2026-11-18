using HireLens.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Infrastructure;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<CvResult> CvResults => Set<CvResult>();
    public DbSet<AnalysisCacheEntry> CacheEntries => Set<AnalysisCacheEntry>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Analysis>(e =>
        {
            e.Property(x => x.RoleTitle).HasMaxLength(200);
            e.Property(x => x.OwnerId).HasMaxLength(450);
            e.HasIndex(x => x.CreatedAt);
            e.HasMany(x => x.Results).WithOne(x => x.Analysis).HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CvResult>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.Verdict).HasMaxLength(40);
            e.Property(x => x.Model).HasMaxLength(80);
            e.Property(x => x.Status).HasConversion<int>();
        });
        b.Entity<AnalysisCacheEntry>(e => { e.HasKey(x => x.Hash); e.Property(x => x.Hash).HasMaxLength(64); e.Property(x => x.Model).HasMaxLength(80); });
        b.Entity<AppSetting>(e => { e.HasKey(x => x.Key); e.Property(x => x.Key).HasMaxLength(100); });
    }
}
