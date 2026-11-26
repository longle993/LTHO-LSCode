using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicTemplateStudio.Core.Interfaces;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;
using DynamicTemplateStudio.Core.Validation;

namespace DynamicTemplateStudio.Infrastructure.ProjectPersistence;

public sealed class ProjectRepository(ITemplateSerializer serializer)
{
    public Action<string>? AfterCommitFile { get; set; }
    private readonly SemaphoreSlim writes = new(1);
    private readonly Dictionary<string, string> fingerprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> skippedTemplates = [];
    private readonly HashSet<string> discoveredTemplateCodes = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SkippedTemplates => skippedTemplates;
    public IReadOnlySet<string> DiscoveredTemplateCodes => discoveredTemplateCodes;
    public static string SafePath(string root, string relative)
    {
        root = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path is outside the project folder.");
        for (var p = Path.GetDirectoryName(path); p is not null && p.Length >= root.Length; p = Path.GetDirectoryName(p))
            if (Directory.Exists(p) && File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked project directories are not supported for writing.");
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked project files are not supported.");
        return path;
    }
    public async Task<ProjectDefinition> OpenAsync(string folder, CancellationToken cancellation = default)
    {
        if (File.Exists(SafePath(folder, ".studio/transaction.json"))) throw new IOException("An interrupted save was detected. Use Recover interrupted save before opening this folder.");
        var manifestPath = SafePath(folder, "project.json");
        var manifestText = await File.ReadAllTextAsync(manifestPath, cancellation);
        var manifest = JsonNode.Parse(manifestText)!.AsObject();
        if (manifest["schemaVersion"]?.GetValue<int>() != 1) throw new IOException("Unsupported project schema version.");
        var project = new ProjectDefinition { ProjectName = manifest["projectName"]!.GetValue<string>(), Description = manifest["description"]?.GetValue<string>() ?? "", Extra = manifest.Where(p => !new[] { "projectName", "description", "schemaVersion", "templates" }.Contains(p.Key)).ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)) };
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        skippedTemplates.Clear();
        discoveredTemplateCodes.Clear();
        foreach (var codeNode in manifest["templates"]!.AsArray())
        {
            var code = codeNode!.GetValue<string>();
            if (!TemplateValidator.SafeCode(code) || !codes.Add(code)) throw new IOException("Invalid or duplicate template code in manifest.");
            await LoadTemplateAsync(code);
        }

        var templateDirectory = SafePath(folder, "templates");
        if (Directory.Exists(templateDirectory))
        {
            var paths = Directory.EnumerateFiles(templateDirectory, "*.json")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                cancellation.ThrowIfCancellationRequested();
                var code = Path.GetFileNameWithoutExtension(path);
                if (codes.Contains(code)) continue;
                try
                {
                    if (!TemplateValidator.SafeCode(code)) throw new IOException("Invalid template filename.");
                    await LoadTemplateAsync(code);
                    codes.Add(code);
                    discoveredTemplateCodes.Add(code);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    skippedTemplates.Add(Path.GetFileName(path));
                }
            }
        }

        async Task LoadTemplateAsync(string code)
        {
            var path = SafePath(folder, "templates/" + code + ".json");
            try
            {
                var text = await File.ReadAllTextAsync(path, cancellation);
                var template = serializer.Deserialize(text);
                if (template.TemplateCode != code) throw new IOException("Manifest code and template code differ.");
                var sample = SafePath(folder, "samples/" + code + ".sample.json");
                if (File.Exists(sample)) { template.SampleJson = await File.ReadAllTextAsync(sample, cancellation); fingerprints[sample] = Hash(template.SampleJson); }
                project.Templates.Add(template); fingerprints[path] = Hash(text);
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { throw new IOException($"Cannot open {path}: {e.Message}. Original files were not changed.", e); }
        }
        var metadata = SafePath(folder, ".studio/metadata.json");
        if (File.Exists(metadata))
        {
            var metadataText = await File.ReadAllTextAsync(metadata, cancellation); fingerprints[metadata] = Hash(metadataText);
            var saved = DocumentState.Restore(metadataText);
            foreach (var t in project.Templates)
            {
                var m = saved.Templates.FirstOrDefault(x => x.TemplateCode == t.TemplateCode);
                if (m is null) continue;
                t.Id = m.Id; t.DisplayName = m.DisplayName; t.Description = m.Description; t.TransactionEnabled = m.TransactionEnabled; t.CreatedAt = m.CreatedAt; t.ModifiedAt = m.ModifiedAt;
                foreach (var table in t.Tables)
                    foreach (var c in table.Columns)
                        c.Description = m.Tables.FirstOrDefault(x => x.Name == table.Name)?.Columns.FirstOrDefault(x => x.Name == c.Name)?.Description ?? "";
            }
        }
        fingerprints[manifestPath] = Hash(manifestText);
        return project;
    }
    public async Task<bool> SaveAsync(ProjectDefinition project, string folder, CancellationToken cancellation = default)
        => (await SaveWithDiagnosticsAsync(project, folder, cancellation)).Count == 0;

    public async Task<IReadOnlyList<(int TemplateIndex, Diagnostic Issue)>> SaveWithDiagnosticsAsync(
        ProjectDefinition project, string folder, CancellationToken cancellation = default)
    {
        await writes.WaitAsync(cancellation);
        try
        {
            Directory.CreateDirectory(folder);
            ValidateProject(project);
            if (File.Exists(SafePath(folder, ".studio/transaction.json"))) throw new IOException("Recover the interrupted save first.");
            var errors = project.Templates.SelectMany((template, index) => TemplateValidator.Validate(template, true)
                .Select(issue => (TemplateIndex: index, Issue: issue))).ToList();
            if (errors.Count > 0)
            {
                await AtomicWriteAsync(SafePath(folder, ".studio/draft.json"), DocumentState.Capture(project), cancellation);
                var initialManifest = SafePath(folder, "project.json");
                if (!File.Exists(initialManifest))
                {
                    var text = JsonSerializer.Serialize(new { projectName = project.ProjectName, description = project.Description, schemaVersion = 1, templates = Array.Empty<string>() });
                    await AtomicWriteAsync(initialManifest, text, cancellation); fingerprints[initialManifest] = Hash(text);
                }
                return errors;
            }
            var files = new Dictionary<string, string?>();
            foreach (var t in project.Templates)
            {
                files["templates/" + t.TemplateCode + ".json"] = serializer.Serialize(t);
                files["samples/" + t.TemplateCode + ".sample.json"] = t.SampleJson;
            }
            files[".studio/metadata.json"] = DocumentState.Capture(project);
            var manifest = JsonNode.Parse(JsonSerializer.Serialize(project.Extra))!.AsObject();
            manifest["projectName"] = project.ProjectName; manifest["description"] = project.Description; manifest["schemaVersion"] = 1;
            manifest["templates"] = new JsonArray(project.Templates.Select(t => (JsonNode?)JsonValue.Create(t.TemplateCode)).ToArray());
            var manifestText = manifest.ToJsonString(new() { WriteIndented = true });
            var manifestPath = SafePath(folder, "project.json");
            if (File.Exists(manifestPath))
            {
                var previous = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, cancellation))!.AsObject();
                var templateDirectory = SafePath(folder, "templates");
                var loadedCodes = fingerprints.Keys.Where(path => Path.GetDirectoryName(path)?.Equals(templateDirectory, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(Path.GetFileNameWithoutExtension);
                var retainedCodes = project.Templates.Select(t => t.TemplateCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var previousCodes = previous["templates"]!.AsArray().Select(x => x!.GetValue<string>());
                foreach (var old in previousCodes.Concat(loadedCodes).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (old is null || retainedCodes.Contains(old)) continue;
                    if (!TemplateValidator.SafeCode(old)) throw new IOException("Unsafe code in original manifest.");
                    files["templates/" + old + ".json"] = null;
                    files["samples/" + old + ".sample.json"] = null;
                }
            }
            files["project.json"] = manifestText; // Manifest is committed last.
            foreach (var (relative, _) in files)
            {
                var path = SafePath(folder, relative);
                if (fingerprints.TryGetValue(path, out var expected) && (!File.Exists(path) || Hash(await File.ReadAllTextAsync(path, cancellation)) != expected)) throw new IOException($"File changed outside Studio: {path}. Reopen or export a copy.");
                if (relative.StartsWith("templates/", StringComparison.Ordinal) && File.Exists(path) && !fingerprints.ContainsKey(path)) throw new IOException($"An untracked template file already exists: {path}. It will not be overwritten.");
            }
            var journal = SafePath(folder, ".studio/transaction.json");
            var backups = new Dictionary<string, string?>();
            foreach (var relative in files.Keys)
            {
                var path = SafePath(folder, relative);
                backups[relative] = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellation) : null;
            }
            await AtomicWriteAsync(journal, JsonSerializer.Serialize(backups), cancellation);
            foreach (var (relative, text) in files)
            {
                var path = SafePath(folder, relative);
                if (text is null) { if (File.Exists(path)) File.Delete(path); fingerprints.Remove(path); }
                else { await AtomicWriteAsync(path, text, cancellation); fingerprints[path] = Hash(text); }
                AfterCommitFile?.Invoke(relative);
            }
            File.Delete(journal);
            var draft = SafePath(folder, ".studio/draft.json"); if (File.Exists(draft)) File.Delete(draft);
            return [];
        }
        finally { writes.Release(); }
    }
    public async Task RecoverAsync(string folder)
    {
        await writes.WaitAsync();
        try
        {
            var path = SafePath(folder, ".studio/transaction.json");
            var backups = JsonSerializer.Deserialize<Dictionary<string, string?>>(await File.ReadAllTextAsync(path))!;
            foreach (var (relative, text) in backups)
            {
                var target = SafePath(folder, relative);
                if (text is null) { if (File.Exists(target)) File.Delete(target); }
                else await AtomicWriteAsync(target, text);
            }
            File.Delete(path); fingerprints.Clear();
        }
        finally { writes.Release(); }
    }
    public async Task ExportAsync(ProjectDefinition project, string destination)
    {
        ValidateProject(project);
        var errors = project.Templates.SelectMany(t => TemplateValidator.Validate(t, true)).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(errors[0].ToString());
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException("Choose a new or empty export folder; existing files will not be overwritten.");
        await SaveAsync(project, destination);
    }
    private static void ValidateProject(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectName)) throw new InvalidOperationException("Project name is required.");
        if (project.Templates.Any(t => !TemplateValidator.SafeCode(t.TemplateCode)) || project.Templates.Select(t => t.TemplateCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != project.Templates.Count) throw new InvalidOperationException("Template codes must be unique safe filenames.");
    }
    public static async Task AtomicWriteAsync(string path, string text, CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(text); await stream.WriteAsync(bytes, cancellation); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
