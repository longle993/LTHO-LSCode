using System.Text.RegularExpressions;
namespace LSCode.Core;
public sealed record ViewWidth(string Field, int Width, int[] Columns);
public static class XmlViewWidths
{
    internal static readonly Regex LayoutMask = new(@"^\s*(?<slots>[01-]+)\s*:");
    internal static readonly Regex LayoutFields = new(@"\[(?<name>[^\]\r\n]+)\](?:\s*\.(?:(?<label>Label)|(?<description>Description))\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static bool IsField(Match reference) =>
        !reference.Groups["label"].Success && !reference.Groups["description"].Success;
    internal static string FieldName(string name) =>
        name.Trim().EndsWith("%l", StringComparison.OrdinalIgnoreCase) ? name.Trim()[..^2] : name.Trim();
    internal static IEnumerable<(int Start, int Length)> Spans(string slots)
    {
        for (var start = 0; start < slots.Length; start++)
        {
            if (slots[start] != '1') continue;
            var stop = start + 1;
            while (stop < slots.Length && slots[stop] == '0') stop++;
            yield return (start, stop - start);
            start = stop - 1;
        }
    }
    public static ViewWidth? Find(string text, int offset)
    {
        if (text.Length == 0 || text.Length > 1000000 || offset < 0 || offset > text.Length) return null;
        text = Regex.Replace(text, @"<!--[\s\S]*?-->", m => new string(' ', m.Length));
        var start = text.LastIndexOf("<view ", Math.Max(0, Math.Min(offset, text.Length - 1)), StringComparison.OrdinalIgnoreCase);
        if (start < 0 || text.LastIndexOf("</view>", Math.Max(0, Math.Min(offset, text.Length - 1)), StringComparison.OrdinalIgnoreCase) > start) return null;
        var end = text.IndexOf("</view>", start, StringComparison.OrdinalIgnoreCase); if (end < 0) end = text.Length;
        var widths = Array.Empty<int>();
        foreach (Match item in Regex.Matches(text[start..end], "<item\\b[^>]*\\bvalue\\s*=\\s*[\"'](?<value>[^\"']*)[\"']", RegexOptions.IgnoreCase)) {
            var value = item.Groups["value"];
            if (Regex.IsMatch(value.Value, @"^\s*\d+(\s*,\s*\d+)*\s*$")) {
                try { widths = value.Value.Split(',').Select(v => int.Parse(v.Trim())).ToArray(); } catch (OverflowException) { widths = []; }
                continue;
            }
            var mask = LayoutMask.Match(value.Value); if (!mask.Success || widths.Length == 0) continue;
            var bits = mask.Groups["slots"].Value;
            var fields = LayoutFields.Matches(value.Value[mask.Length..]);
            foreach (var (span, field) in Spans(bits).Zip(fields)) {
                var absolute = start + value.Index + mask.Length + field.Index;
                if (offset < absolute || offset > absolute + field.Length) continue;
                var stop = span.Start + span.Length;
                if (stop > widths.Length) return null;
                var columns = widths[span.Start..stop];
                try { return new(field.Value, columns.Sum(), columns); } catch (OverflowException) { return null; }
            }
        }
        return null;
    }
}
