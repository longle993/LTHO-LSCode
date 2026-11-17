using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;
using LSCode.Core;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell.Services;
public static class SqlSchemaCompletion
{
    private sealed record ObjectInfo(int Id, SqlSymbol Symbol);
    private sealed class Catalog
    {
        public readonly Lazy<Task<IReadOnlyList<ObjectInfo>>> Objects;
        public readonly ConcurrentDictionary<int, Lazy<Task<IReadOnlyList<SqlSymbol>>>> Columns = new();
        public Catalog(SqlTarget target) => Objects = new(() => LoadObjectsAsync(target));
    }
    private static readonly ConcurrentDictionary<string, Catalog> cache = new();
    public static void Clear() => cache.Clear();
    public static async Task<IReadOnlyList<SqlSymbol>> LoadAsync(SqlTarget target, string sql = "")
    {
        if (cache.Count >= 16 && !cache.ContainsKey(target.ConnectionString)) cache.Clear();
        var catalog = cache.GetOrAdd(target.ConnectionString, _ => new Catalog(target));
        IReadOnlyList<ObjectInfo> objects;
        try { objects = await catalog.Objects.Value; }
        catch { if (cache.TryGetValue(target.ConnectionString, out var failed) && failed == catalog) cache.TryRemove(target.ConnectionString, out _); throw; }
        var result = objects.Select(o => o.Symbol).ToList();
        var references = ReferencedNames(sql);
        foreach (var obj in objects.Where(o => references.Any(r => r.Name.Equals(o.Symbol.Table, StringComparison.OrdinalIgnoreCase) && (r.Schema.Length == 0 || r.Schema.Equals(o.Symbol.Schema, StringComparison.OrdinalIgnoreCase)))))
        {
            var pending = catalog.Columns.GetOrAdd(obj.Id, _ => new(() => LoadColumnsAsync(target, obj)));
            try { result.AddRange(await pending.Value); }
            catch (Exception ex) when (ex is SqlException or OperationCanceledException) { catalog.Columns.TryRemove(obj.Id, out _); }
        }
        return result;
    }
    internal static IReadOnlyList<(string Schema, string Name)> ReferencedNames(string sql)
    {
        const string identifier = @"(?:\[(?:[^\]]|\]\])+\]|[\p{L}_#][\w$#]*)";
        static string Unquote(string value) => value.StartsWith('[') ? value[1..^1].Replace("]]", "]") : value;
        sql = Regex.Replace(sql, @"'([^']|'')*'|--[^\r\n]*|/\*[\s\S]*?\*/", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        return Regex.Matches(sql, @"\b(?:FROM|JOIN|UPDATE|INTO)\s+(?:(?<schema>" + identifier + @")\s*\.\s*)?(?<name>" + identifier + ")", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))
            .Cast<Match>().Select(m => (Unquote(m.Groups["schema"].Value), Unquote(m.Groups["name"].Value))).Distinct().ToList();
    }
    private static async Task<IReadOnlyList<ObjectInfo>> LoadObjectsAsync(SqlTarget target)
    {
        var data = await ProjectDatabase.ReadAllAsync(target,
            "SELECT o.object_id,s.name,o.name,o.type_desc FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.type IN ('U','V','P','PC','FN','IF','TF','FS','FT') AND o.is_ms_shipped=0 ORDER BY s.name,o.name", CancellationToken.None);
        return data.Rows.Cast<DataRow>().Select(r => new ObjectInfo((int)r[0], new SqlSymbol((string)r[1], (string)r[2], null, (string)r[3]))).ToList();
    }
    private static async Task<IReadOnlyList<SqlSymbol>> LoadColumnsAsync(SqlTarget target, ObjectInfo obj)
    {
        var data = await ProjectDatabase.ReadAsync(target,
            "SELECT c.name,t.name FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=@id ORDER BY c.column_id", CancellationToken.None, new SqlParameter("@id", obj.Id));
        return data.Rows.Cast<DataRow>().Select(r => new SqlSymbol(obj.Symbol.Schema, obj.Symbol.Table, (string)r[0], (string)r[1])).ToList();
    }
}
