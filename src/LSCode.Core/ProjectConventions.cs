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
        var raw = link.Split('?', '#')[0];
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.IsFile is false) raw = uri.AbsolutePath;
        var relative = Uri.UnescapeDataString(raw).Replace('/', Path.DirectorySeparatorChar)
            .TrimStart('~', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
            relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(part => part == "..")) return null;
        foreach (var candidate in new[] { Path.Combine(root, relative), Path.Combine(root, "Main", relative) })
        {
            var path = Path.GetFullPath(candidate);
            if (path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return path;
        }
        // Một số phiên bản ERP lưu link kèm thư mục ảo, trong khi file ASPX thật nằm dưới Main.
        // Chỉ fallback theo đúng tên file và vẫn giới hạn kết quả trong Main.
        var main = Path.Combine(root, "Main");
        var fileName = Path.GetFileName(relative);
        if (Directory.Exists(main) && !string.IsNullOrWhiteSpace(fileName))
        {
            var matches = Directory.EnumerateFiles(main, fileName, new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            }).Take(1001).ToArray();
            if (matches.Length > 1000) throw new IOException("Thư mục Main có quá nhiều file trùng tên; cần thu hẹp đường dẫn menu.");
            var suffix = Path.DirectorySeparatorChar + relative;
            return matches.FirstOrDefault(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                ?? matches.OrderBy(path => path.Length).FirstOrDefault();
        }
        return null;
    }
    public static string? ControllerFromPageMetadata(string root, string sysId, string link = "")
    {
        if (Directory.Exists(Path.Combine(root, "Main"))) return null;
        var pageId = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(link.Split('?', '#')[0]));
        if (string.IsNullOrWhiteSpace(pageId) && string.IsNullOrWhiteSpace(sysId)) return null;
        var path = new[] { "AppData", "App_Data" }
            .Select(folder => Path.Combine(root, folder, "PageMetadata.xml"))
            .FirstOrDefault(File.Exists);
        if (path is null) return null;
        var xml = XmlTools.ParseEditor(File.ReadAllText(path), path, allowedRoot: root);
        string? Controller(string id) => xml.Descendants().FirstOrDefault(element =>
                element.Name.LocalName.Equals("page", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(element.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))?.Value,
                    id, StringComparison.OrdinalIgnoreCase))
            ?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals("controller", StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim();
        return !string.IsNullOrWhiteSpace(pageId) ? Controller(pageId) ?? Controller(sysId.Trim()) : Controller(sysId.Trim());
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

