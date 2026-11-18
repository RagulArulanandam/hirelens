using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HireLens.Application.Analysis;
using UglyToad.PdfPig;

namespace HireLens.Infrastructure.Extraction;

public class TextExtractor : ITextExtractor
{
    public string Extract(Stream content, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => Pdf(content),
            ".docx" => Docx(content),
            ".txt" => new StreamReader(content, Encoding.UTF8, true).ReadToEnd(),
            _ => throw new NotSupportedException($"Unsupported file type '{ext}'. Use PDF, DOCX or TXT.")
        };
    }

    private static string Pdf(Stream s)
    {
        using var ms = new MemoryStream(); s.CopyTo(ms);
        using var doc = PdfDocument.Open(ms.ToArray());
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages()) { sb.AppendLine(page.Text); sb.AppendLine(); }
        return sb.ToString();
    }

    private static string Docx(Stream s)
    {
        using var doc = WordprocessingDocument.Open(s, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "";
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<Paragraph>()) sb.AppendLine(p.InnerText);
        return sb.ToString();
    }
}
