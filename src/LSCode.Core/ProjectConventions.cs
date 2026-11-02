using System.Net;
using System.Text.RegularExpressions;

namespace LSCode.Core;
public static class ProjectConventions
{
    public static string? FindAppDatabase(string sysDatabase, IEnumerable<string> available)
    {
        var stem = Regex.Replace(sysDatabase, @"_(?:Sys|S)$", "", RegexOptions.IgnoreCase);
        if (stem == sysDatabase) return null;
        var names = available.ToList();
        return names.FirstOrDefault(n => n.Equals(stem + "_A", StringComparison.OrdinalIgnoreCase))
            ?? names.FirstOrDefault(n => n.Equals(stem + "_App", StringComparison.OrdinalIgnoreCase));
    }
    public static IReadOnlyList<string> Controllers(string aspx)
    {
        var timeout = TimeSpan.FromMilliseconds(300);
        aspx = Regex.Replace(aspx, @"<%--[\s\S]*?--%>|<!--[\s\S]*?-->", "", RegexOptions.None, timeout);
        var reports = Extract("ReportExtender");
        return reports.Count > 0 ? reports : Extract("QueryExtender");
        IReadOnlyList<string> Extract(string kind) => Regex.Matches(aspx, @"<FastBusiness:" + kind + @"\b(?:[^>""']|""[^""]*""|'[^']*')*>", RegexOptions.IgnoreCase, timeout)
            .Cast<Match>().Select(tag => Regex.Match(tag.Value, @"\bController\s*=\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)')", RegexOptions.IgnoreCase, timeout))
            .Where(m => m.Success).Select(m => WebUtility.HtmlDecode(m.Groups["value"].Value).Trim())
            .Where(s => s.Length > 0 && !s.Contains('<') && !s.Contains('%')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static string? ResolveMenuFile(string root, string link)
    {
        root = Path.GetFullPath(root);
        var relative = Uri.UnescapeDataString(link.Split('?', '#')[0]).Replace('/', Path.DirectorySeparatorChar).TrimStart('~', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return null;
        foreach (var candidate in new[] { Path.Combine(root, relative), Path.Combine(root, "Main", relative) })
        {
            var path = Path.GetFullPath(candidate);
            if (path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return path;
        }
        return null;
    }
    public static IReadOnlyList<string> GridControllers(string text)
    {
        var timeout = TimeSpan.FromMilliseconds(300);
        return Regex.Matches(text, @"<items\b(?:[^>""']|""[^""]*""|'[^']*')*>", RegexOptions.IgnoreCase, timeout).Cast<Match>()
            .Where(m => Regex.IsMatch(m.Value, @"\bstyle\s*=\s*['""]Grid['""]", RegexOptions.IgnoreCase, timeout))
            .Select(m => Regex.Match(m.Value, @"\bcontroller\s*=\s*['""](?<name>[\w.-]+)['""]", RegexOptions.IgnoreCase, timeout))
            .Where(m => m.Success).Select(m => m.Groups["name"].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

