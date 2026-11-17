using System.Text;
using System.Text.RegularExpressions;
using LSCode.Shell.Services;

namespace LSCode.Infrastructure;

public sealed record AdvanceNoteUpdateResult(string OutputPath, int FileCount, string[] ScriptFiles);

public static class AdvanceNoteUpdateGenerator
{
    public static async Task<AdvanceNoteUpdateResult> GenerateAsync(string projectRoot, string updatePath,
        AdvanceNoteItem item, CancellationToken cancellationToken)
        => await GenerateAsync(projectRoot, updatePath, item, new Dictionary<string, SqlTarget>(), cancellationToken);

    public static async Task<AdvanceNoteUpdateResult> GenerateAsync(string projectRoot, string updatePath,
        AdvanceNoteItem item, IReadOnlyDictionary<string, SqlTarget> databaseTargets, CancellationToken cancellationToken)
    {
        projectRoot = RequiredDirectory(projectRoot, "Thư mục dự án");
        if (string.IsNullOrWhiteSpace(updatePath)) throw new InvalidOperationException("Dự án chưa có đường dẫn update.");
        var scripts = new List<(string Database, string Text)>();
        foreach (var database in new[] { "App", "Sys" })
        {
            if (item.Sql?.TryGetValue(database, out var sql) != true || sql is null) continue;
            databaseTargets.TryGetValue(database, out var target);
            var text = await ScriptAsync(sql, target, database, cancellationToken).ConfigureAwait(false);
            if (text.Length > 0) scripts.Add((database, text));
        }
        if (item.SourceFiles.Length == 0 && scripts.Count == 0)
            throw new InvalidOperationException("Hãy thêm file vào File Path hoặc nhập nội dung SQL trước khi tạo update.");

        var sources = new List<(string Path, string Relative)>();
        foreach (var source in item.SourceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullSource = Path.GetFullPath(source);
            if (!File.Exists(fullSource)) throw new FileNotFoundException("Không tìm thấy source trong File Path.", fullSource);
            var relative = Path.GetRelativePath(projectRoot, fullSource);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new InvalidDataException("Source nằm ngoài thư mục dự án: " + fullSource);
            sources.Add((fullSource, relative));
        }

        cancellationToken.ThrowIfCancellationRequested();
        updatePath = Path.GetFullPath(updatePath.Trim());
        var token = Guid.NewGuid().ToString("N")[..12];
        var name = SafeName(item.Title);
        if (name.Length > 60) name = name[..60].TrimEnd('.', ' ');
        var package = Path.Combine(updatePath, $"Update-{DateTime.Now:yyyyMMdd-HHmmss}-{name}-{token}");
        var staging = Path.Combine(updatePath, ".lscode-update-" + token);
        var scriptFiles = new List<string>();
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.GetFullPath(Path.Combine(staging, source.Relative));
                if (!IsWithin(destination, staging)) throw new InvalidDataException("Đường dẫn update không hợp lệ: " + source.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
                await input.CopyToAsync(output, cancellationToken);
            }

            foreach (var script in scripts)
            {
                var directory = Path.Combine(staging, "SQL", script.Database);
                Directory.CreateDirectory(directory);
                var path = UniquePath(directory, "Update-" + name + ".sql");
                await File.WriteAllTextAsync(path, script.Text, Encoding.Unicode, cancellationToken);
                scriptFiles.Add(Path.Combine(package, Path.GetRelativePath(staging, path)));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, package);
            return new(package, sources.Count, scriptFiles.ToArray());
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<string> ScriptAsync(AdvanceNoteSql sql, SqlTarget? target, string database,
        CancellationToken cancellationToken)
    {
        var sections = new List<(string Name, string Text)>();
        if (!string.IsNullOrWhiteSpace(sql.TopScript)) sections.Add(("TOP SCRIPT", sql.TopScript.Trim()));
        if (!string.IsNullOrWhiteSpace(sql.Objects))
        {
            var text = LooksLikeSql(sql.Objects)
                ? sql.Objects.Trim()
                : string.Join("\r\n", await Task.WhenAll(Lines(sql.Objects).Select(async name =>
                {
                    var connection = RequiredTarget(target, database, "SQL Object");
                    return (await DatabaseObjectScript.ReadDropCreateAsync(connection, QualifiedName(name), cancellationToken)
                        .ConfigureAwait(false)).TrimEnd();
                })));
            sections.Add(("OBJECT", text));
        }
        if (!string.IsNullOrWhiteSpace(sql.Data))
        {
            var text = LooksLikeSql(sql.Data) ? sql.Data.Trim()
                : string.Join("\r\n\r\n", await Task.WhenAll(Lines(sql.Data).Select(async line =>
                {
                    var (table, condition) = DataEntry(line);
                    var connection = RequiredTarget(target, database, "SQL Data");
                    var qualified = QualifiedName(table);
                    var query = ReadOnlyQuery.Validate($"SELECT * FROM {qualified} WHERE {condition}");
                    var data = await ProjectDatabase.ReadAllAsync(connection, query, cancellationToken).ConfigureAwait(false);
                    var insert = await TableInspection.InsertScriptAsync(connection, qualified, data, cancellationToken)
                        .ConfigureAwait(false);
                    return $"-- {table}: {condition}\r\n{insert.TrimEnd()}";
                })));
            sections.Add(("DATA", text));
        }
        var rendered = sections.Select(section =>
        {
            var text = section.Text.TrimEnd();
            var terminator = Regex.IsMatch(text, @"(?:^|\r?\n)\s*GO\s*$", RegexOptions.IgnoreCase) ? "" : "\r\nGO";
            return "-- " + section.Name + "\r\n" + text + terminator;
        });
        return string.Join("\r\n\r\n", rendered) is { Length: > 0 } result ? result + "\r\n" : "";
    }

    private static SqlTarget RequiredTarget(SqlTarget? target, string database, string section) => target ??
        throw new InvalidOperationException($"Không có kết nối {database} để sinh {section}.");

    private static string[] Lines(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => line.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static (string Table, string Condition) DataEntry(string value)
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            throw new FormatException("SQL Data phải theo dạng <tên bảng>: <điều kiện WHERE>. Dòng lỗi: " + value);
        var table = value[..separator].Trim();
        var condition = value[(separator + 1)..].Trim();
        _ = QualifiedName(table);
        if (condition.Length == 0) throw new FormatException("Điều kiện WHERE không được để trống: " + value);
        return (table, condition);
    }

    private static string QualifiedName(string value)
    {
        var parts = SplitIdentifier(value.Trim());
        if (parts.Count is < 1 or > 2) throw new FormatException("Tên SQL không hợp lệ: " + value);
        if (parts.Count == 1) parts.Insert(0, "dbo");
        return string.Join(".", parts.Select(ProjectDatabase.Quote));
    }

    private static List<string> SplitIdentifier(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var bracketed = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (bracketed && character == ']' && index + 1 < value.Length && value[index + 1] == ']')
            {
                current.Append(']'); index++; continue;
            }
            if (character == '[' && current.Length == 0 && !bracketed) { bracketed = true; continue; }
            if (character == ']' && bracketed) { bracketed = false; continue; }
            if (character == '.' && !bracketed) { AddIdentifierPart(parts, current, value); continue; }
            current.Append(character);
        }
        if (bracketed) throw new FormatException("Tên SQL thiếu dấu ]: " + value);
        AddIdentifierPart(parts, current, value);
        return parts;
    }

    private static void AddIdentifierPart(List<string> parts, StringBuilder current, string original)
    {
        var part = current.ToString().Trim();
        current.Clear();
        if (part.Length == 0 || part.Length > 128 || part.Contains('\0') ||
            (!original.Contains('[', StringComparison.Ordinal) && !Regex.IsMatch(part, @"^[\p{L}_@#][\p{L}\p{N}_$@#]*$")))
            throw new FormatException("Tên SQL không hợp lệ: " + original);
        parts.Add(part);
    }

    private static bool LooksLikeSql(string value) => Regex.IsMatch(value,
        @"^\s*(?:(?:--[^\r\n]*|/\*[\s\S]*?\*/)\s*)*(?:CREATE|ALTER|DROP|IF|EXEC(?:UTE)?|UPDATE|INSERT|DELETE|MERGE|SELECT|SET|DECLARE|WITH)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = new string(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(name) ? "AdvanceNote" : name;
    }

    private static string UniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 2; ; suffix++)
        {
            path = Path.Combine(directory, $"{stem}_{suffix:00}{extension}");
            if (!File.Exists(path)) return path;
        }
    }

    private static string RequiredDirectory(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
            throw new DirectoryNotFoundException(label + " không tồn tại: " + value);
        return Path.GetFullPath(value);
    }

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
