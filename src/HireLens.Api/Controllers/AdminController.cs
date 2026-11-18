using System.Security.Claims;
using FluentValidation;
using HireLens.Application.Analysis;
using HireLens.Domain;
using HireLens.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Controllers;

public record UserRow(string Id, string Email, string FullName, string[] Roles, DateTime CreatedAt);
public record CreateUserRequest(string Email, string FullName, string Password, string Role);
public record ModelRequest(string Model);
public record RetentionRequest(int Days);
public record UsageStats(int Analyses, int CvsAnalysed, long InputTokens, long OutputTokens, int CacheHits, double CacheHitRate, string DefaultModel);

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);
        RuleFor(x => x.Role).Must(r => r is Roles.HR or Roles.Admin).WithMessage("Role must be HR or Admin.");
    }
}

public class ModelValidator : AbstractValidator<ModelRequest>
{
    public ModelValidator() => RuleFor(x => x.Model).NotEmpty().MaximumLength(80).Matches("^[a-zA-Z0-9._-]+$");
}

[ApiController, Authorize(Roles = Roles.Admin), Route("api/admin")]
public class AdminController(UserManager<AppUser> users, AppDbContext db, CvAnalysisService svc, ICvAnalysisProvider provider, IConfiguration cfg,
    IValidator<CreateUserRequest> userValidator, IValidator<ModelRequest> modelValidator) : ControllerBase
{
    public const string RetentionKey = "RetentionDays";

    [HttpGet("users")]
    public async Task<List<UserRow>> Users()
    {
        var list = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        var rows = new List<UserRow>();
        foreach (var u in list) rows.Add(new UserRow(u.Id, u.Email!, u.FullName, (await users.GetRolesAsync(u)).ToArray(), u.CreatedAt));
        return rows;
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserRow>> CreateUser(CreateUserRequest req)
    {
        await userValidator.ValidateAndThrowAsync(req);
        var u = new AppUser { UserName = req.Email, Email = req.Email, FullName = req.FullName.Trim(), EmailConfirmed = true };
        var res = await users.CreateAsync(u, req.Password);
        if (!res.Succeeded) throw new ValidationException(string.Join(" ", res.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(u, req.Role);
        return Created($"api/admin/users/{u.Id}", new UserRow(u.Id, u.Email!, u.FullName, [req.Role], u.CreatedAt));
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier)) throw new ValidationException("You cannot delete your own account.");
        var u = await users.FindByIdAsync(id) ?? throw new KeyNotFoundException("User not found.");
        await users.DeleteAsync(u);
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<UsageStats> Stats()
    {
        var completed = db.CvResults.Where(r => r.Status == CvStatus.Completed);
        var n = await completed.CountAsync();
        var hits = await completed.CountAsync(r => r.FromCache);
        var inTok = await completed.SumAsync(r => (long)r.InputTokens);
        var outTok = await completed.SumAsync(r => (long)r.OutputTokens);
        return new UsageStats(await db.Analyses.CountAsync(), n, inTok, outTok, hits, n == 0 ? 0 : Math.Round(100.0 * hits / n, 1), await svc.GetModelAsync());
    }

    [HttpGet("settings")]
    public async Task<object> Settings() => new
    {
        defaultModel = await svc.GetModelAsync(),
        retentionDays = await RetentionDays(),
        provider = provider.UsesModelOverride ? "Claude" : "Mock",
    };

    [HttpPut("settings/model")]
    public async Task<object> SetModel(ModelRequest req)
    {
        await modelValidator.ValidateAndThrowAsync(req);
        await Upsert(CvAnalysisService.ModelSettingKey, req.Model);
        return new { defaultModel = req.Model };
    }

    [HttpPut("settings/retention")]
    public async Task<object> SetRetention(RetentionRequest req)
    {
        if (req.Days is < 0 or > 3650) throw new ValidationException("Days must be between 0 (keep forever) and 3650.");
        await Upsert(RetentionKey, req.Days.ToString());
        return new { retentionDays = req.Days };
    }

    private async Task<int> RetentionDays()
    {
        var s = await db.Settings.FindAsync(RetentionKey);
        return int.TryParse(s?.Value, out var d) ? d : cfg.GetValue("Analysis:RetentionDays", 30);
    }

    private async Task Upsert(string key, string value)
    {
        var s = await db.Settings.FindAsync(key);
        if (s is null) db.Settings.Add(new AppSetting { Key = key, Value = value }); else s.Value = value;
        await db.SaveChangesAsync();
    }
}
