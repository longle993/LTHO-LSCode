using System.Text;
using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record SqlBatch(string Text, int StartLine);
public static partial class SqlBatches
{
    public static (int Line, int Column) ErrorLocation(int batchStartLine, int errorLine, int selectionLine = 1, int selectionColumn = 1)
    {
        if (errorLine <= 0) return (0, 0);
        var relativeLine = batchStartLine + errorLine - 1;
        return (selectionLine + relativeLine - 1, relativeLine == 1 ? selectionColumn : 1);
    }
    [GeneratedRegex(@"^\s*GO(?:\s+(\d+))?\s*(?:--.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex GoLine();
    public static IReadOnlyList<SqlBatch> Split(string sql)
    {
        var output = new List<SqlBatch>();
        var buffer = new StringBuilder();
        var state = 0; // 0 normal, 1 string, 2 quoted identifier, 3 bracket identifier
        var comments = 0;
        var lineNumber = 0;
        var start = 1;
        using var reader = new StringReader(sql);
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var match = state == 0 && comments == 0 ? GoLine().Match(line) : Match.Empty;
            if (match.Success)
            {
                var repeat = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 1;
                if (repeat is < 1 or > 100) throw new ArgumentException("GO repeat phải từ 1 đến 100.");
                if (!string.IsNullOrWhiteSpace(buffer.ToString()))
                    for (var n = 0; n < repeat; n++) output.Add(new(buffer.ToString(), start));
                buffer.Clear(); start = lineNumber + 1; continue;
            }
            buffer.AppendLine(line);
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i]; var next = i + 1 < line.Length ? line[i + 1] : '\0';
                if (comments > 0)
                {
                    if (c == '/' && next == '*') { comments++; i++; }
                    else if (c == '*' && next == '/') { comments--; i++; }
                    continue;
                }
                if (state != 0)
                {
                    var end = state == 1 ? '\'' : state == 2 ? '"' : ']';
                    if (c == end) { if (next == end) i++; else state = 0; }
                    continue;
                }
                if (c == '-' && next == '-') break;
                if (c == '/' && next == '*') { comments++; i++; }
                else if (c == '\'') state = 1;
                else if (c == '"') state = 2;
                else if (c == '[') state = 3;
            }
        }
        if (!string.IsNullOrWhiteSpace(buffer.ToString())) output.Add(new(buffer.ToString(), start));
        return output;
    }
}
