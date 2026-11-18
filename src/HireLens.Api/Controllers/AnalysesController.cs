using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using HireLens.Application.Analysis;
using HireLens.Domain;
using HireLens.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Controllers;

public record CvResultDto(Guid Id, string FileName, string Status, string? Error, int AtsScore, int CvScore, int FitScore, string Verdict,
    bool FromCache, string Model, int InputTokens, int OutputTokens, AnalysisOutput? Output);
public record AnalysisSummaryDto(Guid Id, string RoleTitle, DateTime CreatedAt, int CvCount, int? BestFitScore, string? BestVerdict);
public record AnalysisDetailDto(Guid Id, string RoleTitle, string JdText, DateTime CreatedAt, List<CvResultDto> Results);
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public class CreateAnalysisForm
{
    public string RoleTitle { get; set; } = "";
    public string? JdText { get; set; }
    public IFormFile? JdFile { get; set; }
}

public class CreateAnalysisValidator : AbstractValidator<CreateAnalysisForm>
{
    public CreateAnalysisValidator()
    {
        RuleFor(x => x.RoleTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.JdText) || x.JdFile is not null).WithMessage("Provide the job description as text or a file.");
    }
}

[ApiController, Authorize, Route("api/analyses")]
public class AnalysesController(AppDbContext db, ITextExtractor extractor, CvAnalysisService svc, IConfiguration cfg,
    IValidator<CreateAnalysisForm> validator, ILogger<AnalysesController> log) : ControllerBase
{
    private static readonly string[] Allowed = [".pdf", ".docx", ".txt"];
    private const int MaxCvsPerAnalysis = 10;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin => User.IsInRole(Roles.Admin);
    private IQueryable<Analysis> Visible => IsAdmin ? db.Analyses : db.Analyses.Where(a => a.OwnerId == UserId);
    private long MaxBytes => cfg.GetValue("Analysis:MaxFileBytes", 5L * 1024 * 1024);

    /// <summary>Step 1: create the analysis with the JD (text or file).</summary>
    [HttpPost, Consumes("multipart/form-data")]
    public async Task<ActionResult<AnalysisDetailDto>> Create([FromForm] CreateAnalysisForm form, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(form, ct);
        var jd = form.JdText;
        if (form.JdFile is not null)
        {
            ValidateFile(form.JdFile);
            await using var s = form.JdFile.OpenReadStream();
            jd = extractor.Extract(s, form.JdFile.FileName);
        }
        jd = TextNormalizer.Normalize(jd, cfg.GetValue("Analysis:MaxJdChars", 6000));
        if (jd.Length < 30) throw new ValidationException("The job description is too short.");

        var a = new Analysis { RoleTitle = form.RoleTitle.Trim(), JdText = jd, OwnerId = UserId };
        db.Analyses.Add(a);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Analysis {Id} created", a.Id);
        return CreatedAtAction(nameof(Get), new { id = a.Id }, ToDetail(a));
    }

    /// <summary>Step 2 (repeat per CV so the UI can show progress): upload one CV and run the analysis.</summary>
    [HttpPost("{id:guid}/cvs"), Consumes("multipart/form-data"), EnableRateLimiting("analyse")]
    public async Task<ActionResult<CvResultDto>> AddCv(Guid id, IFormFile file, CancellationToken ct)
    {
        var a = await Visible.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Analysis not found.");
        if (a.Results.Count >= MaxCvsPerAnalysis) throw new ValidationException($"At most {MaxCvsPerAnalysis} CVs per analysis.");
        ValidateFile(file);

        var result = new CvResult { AnalysisId = a.Id, FileName = Path.GetFileName(file.FileName), Status = CvStatus.Pending };
        try
        {
            // Save original (auto-deleted after N days by the retention service), then extract text locally.
            var dir = Path.Combine(cfg["Analysis:UploadPath"] ?? "uploads", a.Id.ToString("N"));
            Directory.CreateDirectory(dir);
            var stored = Path.Combine(dir, result.Id.ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant());
            await using (var fs = System.IO.File.Create(stored)) await file.CopyToAsync(fs, ct);
            result.StoredPath = stored;

            string text;
            await using (var rs = System.IO.File.OpenRead(stored)) text = extractor.Extract(rs, file.FileName);

            var run = await svc.RunAsync(a.JdText, text, ct);
            result.Status = CvStatus.Completed;
            result.AtsScore = run.Output.AtsScore; result.CvScore = run.Output.CvScore; result.FitScore = run.Output.FitScore;
            result.Verdict = run.Output.FitVerdict; result.Model = run.Model; result.FromCache = run.FromCache;
            result.InputTokens = run.InputTokens; result.OutputTokens = run.OutputTokens;
            result.ResultJson = JsonSerializer.Serialize(run.Output, AnalysisJsonParser.JsonOptions);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or InvalidAnalysisJsonException or HttpRequestException or InvalidDataException or IOException)
        {
            log.LogWarning("CV analysis failed for analysis {Id}: {Type}", a.Id, ex.GetType().Name);  // never log CV contents
            result.Status = CvStatus.Failed;
            result.Error = ex is HttpRequestException ? "The AI service is unavailable. Try again." : ex.Message;
        }
        db.CvResults.Add(result);
        await db.SaveChangesAsync(ct);
        return ToDto(result);
    }

    [HttpGet]
    public async Task<PagedResult<AnalysisSummaryDto>> List([FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Visible.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(a => a.RoleTitle.Contains(search.Trim()));
        if (from is not null) q = q.Where(a => a.CreatedAt >= from);
        if (to is not null) q = q.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.RoleTitle, a.CreatedAt,
                Count = a.Results.Count,
                Best = a.Results.Where(r => r.Status == CvStatus.Completed).OrderByDescending(r => r.FitScore).Select(r => new { r.FitScore, r.Verdict }).FirstOrDefault()
            }).ToListAsync(ct);
        return new(rows.Select(r => new AnalysisSummaryDto(r.Id, r.RoleTitle, r.CreatedAt, r.Count, r.Best?.FitScore, r.Best?.Verdict)).ToList(), total, page, pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var a = await Visible.AsNoTracking().Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Analysis not found.");
        return ToDetail(a);
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var a = await Visible.AsNoTracking().Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Analysis not found.");
        var bytes = Services.PdfExporter.Render(ToDetail(a));
        return File(bytes, "application/pdf", $"HireLens-{a.Id:N}.pdf");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var a = await Visible.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Analysis not found.");
        foreach (var r in a.Results.Where(r => r.StoredPath is not null && System.IO.File.Exists(r.StoredPath))) System.IO.File.Delete(r.StoredPath!);
        db.Analyses.Remove(a);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Analysis {Id} deleted", id);
        return NoContent();
    }

    private void ValidateFile(IFormFile f)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) throw new ValidationException("Only PDF, DOCX or TXT files are allowed.");
        if (f.Length == 0) throw new ValidationException("The file is empty.");
        if (f.Length > MaxBytes) throw new ValidationException($"File exceeds {MaxBytes / 1024 / 1024} MB.");
        Span<byte> head = stackalloc byte[4];
        using var s = f.OpenReadStream();
        var n = s.Read(head);
        var ok = ext switch { ".pdf" => n >= 4 && head[0] == '%' && head[1] == 'P' && head[2] == 'D' && head[3] == 'F', ".docx" => n >= 2 && head[0] == 'P' && head[1] == 'K', _ => true };
        if (!ok) throw new ValidationException("File content does not match its extension.");
    }

    public static CvResultDto ToDto(CvResult r) => new(r.Id, r.FileName, r.Status.ToString(), r.Error, r.AtsScore, r.CvScore, r.FitScore, r.Verdict,
        r.FromCache, r.Model, r.InputTokens, r.OutputTokens,
        string.IsNullOrEmpty(r.ResultJson) ? null : JsonSerializer.Deserialize<AnalysisOutput>(r.ResultJson, AnalysisJsonParser.JsonOptions));

    public static AnalysisDetailDto ToDetail(Analysis a) => new(a.Id, a.RoleTitle, a.JdText, a.CreatedAt,
        a.Results.OrderByDescending(r => r.FitScore).ThenBy(r => r.CreatedAt).Select(ToDto).ToList());
}
