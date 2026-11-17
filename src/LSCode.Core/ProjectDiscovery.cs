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
        var ordered = entries
            .Where(entry => entry.Id.Length > 0)
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var childrenByParent = ordered
            .GroupBy(entry => entry.ParentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        // FSD Studio presents wcommand as exactly three levels: root, child and grandchild.
        // Orphans, cycles and deeper rows are left out because they have no visible path in that tree.
        var roots = new List<CommandNode>();
        if (!childrenByParent.TryGetValue(string.Empty, out var rootEntries)) return roots;
        foreach (var rootEntry in rootEntries)
        {
            var root = new CommandNode(rootEntry);
            roots.Add(root);
            AddChildren(root, rootEntry.Id, childrenByParent, remainingLevels: 2);
        }
        return roots;
    }

    private static void AddChildren(
        CommandNode parent,
        string parentId,
        IReadOnlyDictionary<string, CommandEntry[]> childrenByParent,
        int remainingLevels)
    {
        if (remainingLevels == 0 || !childrenByParent.TryGetValue(parentId, out var children)) return;
        foreach (var childEntry in children)
        {
            var child = new CommandNode(childEntry);
            parent.Children.Add(child);
            AddChildren(child, childEntry.Id, childrenByParent, remainingLevels - 1);
        }
    }
}
