using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HireLens.Domain;
using HireLens.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HireLens.Api.Services;

public class JwtService(IConfiguration cfg)
{
    public (string token, DateTime expires) Create(AppUser user, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var expires = DateTime.UtcNow.AddMinutes(cfg.GetValue("Jwt:ExpiryMinutes", 480));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Email, user.Email!), new(ClaimTypes.Name, user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var jwt = new JwtSecurityToken(cfg["Jwt:Issuer"], cfg["Jwt:Audience"], claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration cfg, ILogger log)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational()) await db.Database.MigrateAsync(); else await db.Database.EnsureCreatedAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { Roles.Admin, Roles.HR })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var email = cfg["Seed:AdminEmail"] ?? "admin@hirelens.local";
        var pw = cfg["Seed:AdminPassword"];
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;
        if (string.IsNullOrWhiteSpace(pw)) { log.LogWarning("Seed:AdminPassword not set; admin not seeded (use dotnet user-secrets)"); return; }

        var admin = new AppUser { UserName = email, Email = email, FullName = "Administrator", EmailConfirmed = true };
        var res = await users.CreateAsync(admin, pw);
        if (res.Succeeded) await users.AddToRoleAsync(admin, Roles.Admin);
        else log.LogError("Admin seed failed: {Errors}", string.Join(", ", res.Errors.Select(e => e.Code)));
    }
}
