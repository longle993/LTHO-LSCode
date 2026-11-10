using System.Text.RegularExpressions;
namespace LSCode.Domain;
public sealed record ViewWidth(string Field, int Width, int[] Columns);
public static class XmlViewWidths
{
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
            var mask = Regex.Match(value.Value, @"^\s*([01]+)\s*:"); if (!mask.Success || widths.Length == 0) continue;
            var bits = mask.Groups[1].Value; if (bits[0] != '1' || bits.Length > widths.Length) continue;
            var fields = Regex.Matches(value.Value[mask.Length..], @"\[[^\]]+\](?:\.Label)?", RegexOptions.IgnoreCase);
            var starts = bits.Select((c,i)=>(c,i)).Where(x=>x.c=='1').Select(x=>x.i).ToArray();
            for (var f = 0; f < Math.Min(fields.Count, starts.Length); f++) {
                var field = fields[f]; var absolute = start + value.Index + mask.Length + field.Index;
                if (offset < absolute || offset > absolute + field.Length) continue;
                var stop = f + 1 < starts.Length ? starts[f+1] : bits.Length;
                var columns = widths[starts[f]..stop];
                try { return new(field.Value, columns.Sum(), columns); } catch (OverflowException) { return null; }
            }
        }
        return null;
    }
}
