namespace LSCode.Core;

public sealed record DiffViewRow(int? LeftLine, int? RightLine, string Kind, string Left, string Right)
{
    public string Content => Kind == "−" ? Left : Right;
}

public static class DiffPresentation
{
    public static IReadOnlyList<DiffViewRow> Create(IReadOnlyList<DiffRow> source, bool split, bool context)
    {
        var rows = new List<DiffViewRow>();
        for (var i = 0; i < source.Count;)
        {
            var row = source[i];
            if (!split || row.Kind == "=") { rows.Add(new(row.LeftLine, row.RightLine, row.Kind, row.Left, row.Right)); i++; continue; }
            var removed = new List<DiffRow>(); var added = new List<DiffRow>();
            while (i < source.Count && source[i].Kind != "=")
            {
                (source[i].Kind == "−" ? removed : added).Add(source[i++]);
            }
            for (var n = 0; n < Math.Max(removed.Count, added.Count); n++)
            {
                var a = n < removed.Count ? removed[n] : null; var b = n < added.Count ? added[n] : null;
                rows.Add(new(a?.LeftLine, b?.RightLine, a == null ? "+" : b == null ? "−" : "~", a?.Left ?? "", b?.Right ?? ""));
            }
        }
        if (!context || rows.All(r => r.Kind == "=")) return rows;
        var visible = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Kind != "=")
                for (var n = Math.Max(0, i - 3); n <= Math.Min(rows.Count - 1, i + 3); n++) visible[n] = true;
        var result = new List<DiffViewRow>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (visible[i]) { result.Add(rows[i]); continue; }
            var start = i;
            while (i + 1 < rows.Count && !visible[i + 1]) i++;
            var label = $"⋯ {i - start + 1} dòng không đổi · bỏ chọn Thu gọn để xem";
            result.Add(new(null, null, "…", label, label));
        }
        return result;
    }

    public static (int Start, int Length) ChangedSpan(string value, string other)
    {
        var start = 0;
        while (start < Math.Min(value.Length, other.Length) && value[start] == other[start]) start++;
        var end = value.Length; var otherEnd = other.Length;
        while (end > start && otherEnd > start && value[end - 1] == other[otherEnd - 1]) { end--; otherEnd--; }
        return (start, end - start);
    }
}
