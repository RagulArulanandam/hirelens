using HireLens.Api.Controllers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HireLens.Api.Services;

public static class PdfExporter
{
    static PdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    private const string Indigo = "#3730a3", Teal = "#0d9488", Muted = "#6b7194";

    public static byte[] Render(AnalysisDetailDto a) => Document.Create(doc =>
    {
        doc.Page(p =>
        {
            p.Size(PageSizes.A4); p.Margin(36); p.DefaultTextStyle(t => t.FontSize(10));
            p.Header().Column(c =>
            {
                c.Item().Text(t => { t.Span("Hire").FontSize(20).Bold().FontColor(Indigo); t.Span("Lens").FontSize(20).Bold().FontColor(Teal); });
                c.Item().Text($"{a.RoleTitle}  ·  {a.CreatedAt:yyyy-MM-dd HH:mm} UTC  ·  {a.Results.Count} CV(s)").FontColor(Muted);
                c.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
            p.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(10);
                if (a.Results.Count > 1)
                {
                    col.Item().Text("Ranking").FontSize(13).Bold().FontColor(Indigo);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(24); c.RelativeColumn(3); c.ConstantColumn(40); c.ConstantColumn(40); c.ConstantColumn(40); c.RelativeColumn(1.5f); });
                        t.Header(h => { foreach (var x in new[] { "#", "File", "Fit", "ATS", "CV", "Verdict" }) h.Cell().BorderBottom(1).PaddingVertical(3).Text(x).Bold(); });
                        var i = 1;
                        foreach (var r in a.Results)
                        {
                            t.Cell().PaddingVertical(2).Text((i++).ToString()); t.Cell().Text(r.FileName);
                            t.Cell().Text(r.Status == "Completed" ? r.FitScore.ToString() : "-"); t.Cell().Text(r.Status == "Completed" ? r.AtsScore.ToString() : "-");
                            t.Cell().Text(r.Status == "Completed" ? r.CvScore.ToString() : "-"); t.Cell().Text(r.Status == "Completed" ? r.Verdict : "Failed");
                        }
                    });
                }
                foreach (var r in a.Results.Where(r => r.Output is not null))
                {
                    var o = r.Output!;
                    col.Item().PageBreak();
                    col.Item().Text(r.FileName).FontSize(14).Bold().FontColor(Indigo);
                    col.Item().Text(t => { t.Span($"Verdict: {o.FitVerdict}").Bold(); t.Span($"   ATS {o.AtsScore}  ·  CV quality {o.CvScore}  ·  Fit {o.FitScore}"); });
                    col.Item().Text(o.Summary);
                    col.Item().Text("Matched skills").Bold(); col.Item().Text(o.MatchedSkills.Count > 0 ? string.Join(", ", o.MatchedSkills) : "None");
                    col.Item().Text("Missing skills").Bold(); col.Item().Text(o.MissingSkills.Count > 0 ? string.Join(", ", o.MissingSkills) : "None");
                    if (o.Gaps.Count > 0)
                    {
                        col.Item().Text("Gaps to improve").Bold();
                        foreach (var g in o.Gaps) col.Item().Text(t => { t.Span($"{g.Area}: ").Bold(); t.Span($"{g.Detail} "); t.Span($"Suggestion: {g.Suggestion}").FontColor(Teal); });
                    }
                    if (o.Strengths.Count > 0) { col.Item().Text("Strengths").Bold(); col.Item().Text(string.Join("; ", o.Strengths)); }
                    if (o.RedFlags.Count > 0) { col.Item().Text("Red flags").Bold(); col.Item().Text(string.Join("; ", o.RedFlags)); }
                    if (o.AtsBreakdown is { } b)
                    {
                        col.Item().Text("ATS score breakdown").Bold();
                        col.Item().Text($"Keyword coverage: {b.MatchedKeywords.Count}/{b.JdKeywords.Count} ({b.KeywordCoveragePct}%) = {b.KeywordPoints}/60 points");
                        foreach (var c in b.Checks) col.Item().Text($"{(c.Passed ? "PASS" : "FAIL")} {c.Name}: {c.Points}/{c.MaxPoints} ({c.Note})");
                    }
                }
            });
            p.Footer().AlignCenter().Text(t => { t.Span("HireLens · confidential · page ").FontColor(Muted); t.CurrentPageNumber().FontColor(Muted); });
        });
    }).GeneratePdf();
}
