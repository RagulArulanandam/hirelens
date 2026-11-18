using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HireLens.Infrastructure;

public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("HIRELENS_DB")
                 ?? "Server=(localdb)\MSSQLLocalDB;Database=HireLens;Trusted_Connection=True;TrustServerCertificate=True";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    }
}
