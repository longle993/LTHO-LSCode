using System.Text;

namespace LSCode.Core;

/// <summary>Bounded TSV exchange. Quotes preserve embedded tabs, newlines and empty cells.</summary>
public static class CatalogClipboard
{
    public const int MaxRows = 1000;
    public const int MaxBytes = 5 * 1024 * 1024;

    public static void CheckSize(string text)
    {
        if (text.Length > MaxBytes || Encoding.UTF8.GetByteCount(text) > MaxBytes)
            throw new InvalidOperationException("Clipboard vượt giới hạn 5 MB mỗi đợt.");
    }

    public static string Write(IEnumerable<IEnumerable<string?>> rows)
    {
        var result = new StringBuilder();
        int count = 0;
        foreach (var row in rows)
        {
            if (++count > MaxRows) throw new InvalidOperationException("Chỉ sao chép tối đa 1.000 dòng mỗi đợt.");
            if (count > 1) result.Append("\r\n");
            result.AppendJoin('\t', row.Select(value => "\"" + (value ?? "").Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
            if (result.Length > MaxBytes) throw new InvalidOperationException("Clipboard vượt giới hạn 5 MB mỗi đợt.");
        }
        var text = result.ToString();
        CheckSize(text);
        return text;
    }

    public static List<string[]> Read(string text)
    {
        CheckSize(text);
        var result = new List<string[]>();
        if (text.Length == 0) return result;
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false, closed = false, started = false;
        void EndCell()
        {
            cells.Add(cell.ToString()); cell.Clear(); closed = started = false;
            if (cells.Count > 4096) throw new InvalidOperationException("Clipboard có quá nhiều cột.");
        }
        void EndRow()
        {
            EndCell(); result.Add(cells.ToArray()); cells.Clear();
            // One extra row may be a header, selected explicitly by the caller.
            if (result.Count > MaxRows + 1) throw new InvalidOperationException("Chỉ dán tối đa 1.000 dòng mỗi đợt.");
        }
        for (int i = 0; i < text.Length; i++)
        {
            char value = text[i];
            if (quoted)
            {
                if (value == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else cell.Append(value);
                continue;
            }
            if (value == '\t') { EndCell(); continue; }
            if (value is '\r' or '\n')
            {
                if (value == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow(); continue;
            }
            if (closed) throw new InvalidOperationException("TSV không hợp lệ: có ký tự sau dấu ngoặc kép đóng.");
            if (value == '"' && !started) { quoted = started = true; continue; }
            cell.Append(value); started = true;
        }
        if (quoted) throw new InvalidOperationException("TSV không hợp lệ: thiếu dấu ngoặc kép đóng.");
        if (started || closed || cells.Count > 0 || cell.Length > 0) EndRow();
        return result;
    }

    // Cell editors use these tokens for NULL/DEFAULT/empty. Literal clipboard text must not
    // acquire their meaning (including when the literal already starts with the escape prefix).
    public static string Literal(string value) => value.StartsWith(":", StringComparison.Ordinal) ? ":text:" + value : value;
    public static string DecodeLiteral(string value) => value.StartsWith(":text:", StringComparison.Ordinal) ? value[6..] : value;
}
