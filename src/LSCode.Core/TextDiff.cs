namespace LSCode.Core;

public sealed record DiffRow(int? LeftLine, int? RightLine, string Kind, string Left, string Right);
public static class TextDiff
{
    public static IReadOnlyList<DiffRow> Compare(string left, string right)
    {
        var a = left.Replace("\r\n", "\n").Split('\n');
        var b = right.Replace("\r\n", "\n").Split('\n');
        if ((long)a.Length * b.Length > 4_000_000) throw new ArgumentException("Compare giới hạn tích số dòng hai file ở 4 triệu. Chọn file nhỏ hơn.");
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? 1 + lcs[i + 1, j + 1] : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        var rows = new List<DiffRow>(); var x = 0; var y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y]) { rows.Add(new(x + 1, y + 1, "=", a[x++], b[y++])); }
            else if (y < b.Length && (x == a.Length || lcs[x, y + 1] > lcs[x + 1, y])) rows.Add(new(null, y + 1, "+", "", b[y++]));
            else rows.Add(new(x + 1, null, "−", a[x++], ""));
        }
        return rows;
    }
}
