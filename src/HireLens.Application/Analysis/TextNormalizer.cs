using System.Text.RegularExpressions;

namespace HireLens.Application.Analysis;

public static partial class TextNormalizer
{
    [GeneratedRegex(@"[ \t ]+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex BlankLines();
    [GeneratedRegex(@"^\s*(page\s+\d+(\s+of\s+\d+)?|\d+\s*/\s*\d+|confidential|curriculum vitae|resume|r[eé]sum[eé])\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Boilerplate();

    /// <summary>Normalises whitespace, strips boilerplate lines and caps the length (saves tokens).</summary>
    public static string Normalize(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\0", "");
        t = Boilerplate().Replace(t, "");
        t = Spaces().Replace(t, " ");
        t = string.Join("\n", t.Split('\n').Select(l => l.Trim()));
        t = BlankLines().Replace(t, "\n\n").Trim();
        return t.Length > maxChars ? t[..maxChars] : t;
    }
}
