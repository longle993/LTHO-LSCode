using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal static class FastEditorCompletion
{
    private static readonly IReadOnlyDictionary<string, (string Legacy, string Portable)> CatalogFiles =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["xml"] = ("htmlsnippet.xml", "htmlsnippet.xml"),
            ["javascript"] = ("viewsrcsnippet.xml", "jssnippet.xml"),
            ["sql"] = ("viewsrcsqlsnippet.xml", "sqlsnippet.xml"),
            ["css"] = ("csssnippet.xml", "csssnippet.xml"),
        };
    private static readonly IReadOnlyDictionary<string, Lazy<IReadOnlyList<FastCompletionEntry>>> Catalogs = CatalogFiles.ToDictionary(
        pair => pair.Key, pair => new Lazy<IReadOnlyList<FastCompletionEntry>>(() => ReadCatalog(pair.Value)));

    public static async Task<object?> CompleteAsync(string text, int caret, string path, IReadOnlyDictionary<string, string> buffers,
        Func<SqlTarget>? target, CancellationToken token)
    {
        if (FastXmlCompletion.Context(text, caret) is not { } context) return null;
        var hints = FastXmlCompletion.Suggest(context, Catalogs[context.Language].Value);
        token.ThrowIfCancellationRequested();
        IEnumerable<SqlSuggestion> suggestions = hints;
        if (context.Language == "sql")
        {
            var sql = context.Code;
            IReadOnlyList<SqlSymbol> symbols = [];
            if (target is not null && !context.Prefix.StartsWith('@') && !context.Prefix.StartsWith('#'))
            {
                try { symbols = await SqlSchemaCompletion.LoadAsync(target(), sql).WaitAsync(token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException) { }
            }
            suggestions = hints.Concat(SqlSuggestions.Get(sql[..context.CodeOffset], symbols, sql)
                .Where(hint => hint.Text.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase)));
        }
        else if (hints.Count == 0)
            suggestions = FastXmlCompletion.Fields(context, text, path, buffers);
        return new { context.Start, context.End, context.Language,
            suggestions = suggestions.DistinctBy(hint => hint.Text, StringComparer.OrdinalIgnoreCase).Take(150).ToArray() };
    }

    private static IReadOnlyList<FastCompletionEntry> ReadCatalog((string Legacy, string Portable) files)
    {
        var custom = Environment.GetEnvironmentVariable("LSCODE_FCODE_SNIPPETS");
        var drive = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
        var legacyFolder = Path.Combine(drive, "FAST", "FCode", "Config", "snippet");
        var systemFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FCode", "Config", "snippet");
        var candidates = new[]
        {
            string.IsNullOrWhiteSpace(custom) || !Path.IsPathFullyQualified(custom) ? null : Path.Combine(custom, files.Legacy),
            Path.Combine(legacyFolder, files.Legacy),
            Path.Combine(systemFolder, files.Legacy),
            Path.Combine(AppContext.BaseDirectory, "Assets", files.Portable),
        };
        IReadOnlyList<FastCompletionEntry> legacy = [];
        var portable = candidates[^1];
        foreach (var path in candidates.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (legacy.Count > 0 && path != portable) continue;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 1_000_000) continue;
                var entries = FastXmlCompletion.ReadCatalog(File.ReadAllText(path));
                if (path == portable) return legacy.Concat(entries).DistinctBy(entry => (entry.Object, entry.Keyword)).ToArray();
                if (entries.Count > 0) legacy = entries;
            }
            catch (Exception exception) when (exception is System.Xml.XmlException or IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException)
            {
                // A missing or invalid local catalog falls back to the portable editor catalog.
            }
        }
        return legacy;
    }
}
