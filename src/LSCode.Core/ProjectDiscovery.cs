using System.Xml.Linq;

namespace LSCode.Core;

public sealed record DiscoveredProject(string Name, string Folder, string ConfigPath)
{
    public override string ToString() => $"{Name} — {Folder}";
}
public static class ProjectDiscovery
{
    public static IReadOnlyList<DiscoveredProject> Find(string root, string name, CancellationToken token)
    {
        name = name.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException("Nhập tên dự án, không nhập đường dẫn.");
        var matches = new List<DiscoveredProject>();
        // CustomerPro / category / project / application / web.config; do not scan the entire share recursively.
        foreach (var category in Directory.EnumerateDirectories(root).Prepend(root))
        {
            token.ThrowIfCancellationRequested();
            if ((File.GetAttributes(category) & FileAttributes.ReparsePoint) != 0) continue;
            var project = Path.Combine(category, name);
            if (!Directory.Exists(project)) continue;
            foreach (var application in Directory.EnumerateDirectories(project))
            {
                token.ThrowIfCancellationRequested();
                if ((File.GetAttributes(application) & FileAttributes.ReparsePoint) != 0) continue;
                var config = Path.Combine(application, "web.config");
                if (File.Exists(config)) matches.Add(new(name, application, config));
            }
        }
        return matches.OrderBy(p => p.Folder, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static (string App, string Sys) ReadConnections(string path)
    {
        var entries = ReadAllConnections(path);
        string Read(string name) => entries.FirstOrDefault(e => e.Name == name)?.ConnectionString
            ?? throw new InvalidDataException($"Thiếu {name} trong connectionStrings.");
        return (Read("appConnectionString"), Read("sysConnectionString"));
    }

    public sealed record ConfiguredConnection(string Name, string ConnectionString, string ProviderName);
    public static IReadOnlyList<ConfiguredConnection> ReadAllConnections(string path)
    {
        var xml = XmlTools.Parse(File.ReadAllText(path));
        var section = xml.Root?.Element("connectionStrings") ?? throw new InvalidDataException("web.config không có connectionStrings.");
        if (section.Attribute("configSource") is { } source)
        {
            var folder = Path.GetFullPath(Path.GetDirectoryName(path)!);
            var external = Path.GetFullPath(Path.Combine(folder, source.Value));
            if (!external.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("configSource phải nằm trong thư mục ứng dụng.");
            section = XmlTools.Parse(File.ReadAllText(external)).Root ?? throw new InvalidDataException("configSource rỗng.");
        }
        var entries = new Dictionary<string, ConfiguredConnection>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in section.Elements())
        {
            var name = (string?)entry.Attribute("name") ?? "";
            if (entry.Name == "clear") entries.Clear();
            else if (entry.Name == "remove") entries.Remove(name);
            else if (entry.Name == "add") entries[name] = new(name, (string?)entry.Attribute("connectionString") ?? "", (string?)entry.Attribute("providerName") ?? "");
        }
        return entries.Values.ToList();
    }
}

public sealed record CommandEntry(string Id, string ParentId, string Caption, string Link, string CommandId, string SysId = "");
public sealed class CommandNode(CommandEntry entry)
{
    public CommandEntry Entry { get; } = entry;
    public List<CommandNode> Children { get; } = [];
}
public static class CommandHierarchy
{
    public static List<CommandNode> Filter(IEnumerable<CommandNode> nodes, string search)
    {
        search = search.Trim();
        var result = new List<CommandNode>();
        foreach (var node in nodes)
        {
            var match = new[] { node.Entry.Caption, node.Entry.SysId, node.Entry.Id, node.Entry.CommandId }.Any(v => v.Contains(search, StringComparison.OrdinalIgnoreCase));
            var children = Filter(node.Children, search);
            if (match || children.Count > 0) { var copy = new CommandNode(node.Entry); copy.Children.AddRange(match ? node.Children : children); result.Add(copy); }
        }
        return result;
    }
    public static List<CommandNode> Build(IEnumerable<CommandEntry> entries)
    {
        var nodes = entries.Where(e => e.Id.Length > 0).GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new CommandNode(g.First()), StringComparer.OrdinalIgnoreCase);
        var roots = new List<CommandNode>();
        foreach (var node in nodes.Values.OrderBy(n => n.Entry.Id, StringComparer.OrdinalIgnoreCase))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { node.Entry.Id };
            var parent = node.Entry.ParentId;
            bool cycle = false;
            while (nodes.TryGetValue(parent, out var ancestor))
            {
                if (!seen.Add(parent)) { cycle = true; break; }
                parent = ancestor.Entry.ParentId;
            }
            if (!cycle && nodes.TryGetValue(node.Entry.ParentId, out var parentNode)) parentNode.Children.Add(node);
            else roots.Add(node);
        }
        return roots;
    }
}
