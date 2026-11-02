using System.Text.RegularExpressions;

namespace LSCode.Core;
public sealed record SqlSymbol(string Schema, string Table, string? Column, string Kind);
public sealed record SqlSuggestion(string Text, string InsertText, string Description, bool IsSnippet = false);
public static class SqlSuggestions
{
    public static readonly string[] Keywords = ["SELECT", "FROM", "WHERE", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "GROUP BY", "ORDER BY", "HAVING", "INSERT INTO", "UPDATE", "DELETE FROM", "CREATE PROCEDURE", "ALTER PROCEDURE", "DECLARE", "BEGIN", "END", "EXEC", "CASE", "WHEN", "THEN", "ELSE", "IS NULL", "ISNULL", "COALESCE", "COUNT", "SUM", "AVG", "MIN", "MAX", "TOP", "DISTINCT", "GETDATE", "DATEADD", "DATEDIFF", "CAST", "CONVERT", "EXISTS", "NOT EXISTS", "UNION ALL", "OVER", "PARTITION BY", "ROW_NUMBER", "WITH", "SET", "VALUES"];
    public static IReadOnlyList<SqlSuggestion> Get(string beforeCaret, IEnumerable<SqlSymbol> symbols, string? fullSql = null)
    {
        const string identifier = @"(?:\[(?:[^\]]|\]\])+\]|\w+)";
        string Unquote(string value) => value.StartsWith('[') && value.EndsWith(']') ? value[1..^1].Replace("]]", "]") : value;
        var qualifier = Unquote(Regex.Match(beforeCaret, "(?<q>" + identifier + @")\.\w*$", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Groups["q"].Value);
        var list = symbols.ToList();
        if (qualifier.Length > 0)
        {
            var aliases = Regex.Matches(fullSql ?? beforeCaret, @"\b(?:FROM|JOIN)\s+(?:(?<schema>" + identifier + @")\.)?(?<table>" + identifier + @")\s+(?:AS\s+)?(?<alias>" + identifier + ")", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            var alias = aliases.Cast<Match>().LastOrDefault(m => Unquote(m.Groups["alias"].Value).Equals(qualifier, StringComparison.OrdinalIgnoreCase));
            var table = alias == null ? qualifier : Unquote(alias.Groups["table"].Value);
            var columns = list.Where(s => s.Column != null && s.Table.Equals(table, StringComparison.OrdinalIgnoreCase) && (alias == null || alias.Groups["schema"].Value.Length == 0 || s.Schema.Equals(Unquote(alias.Groups["schema"].Value), StringComparison.OrdinalIgnoreCase))).ToList();
            if (columns.Count > 0) return columns.Select(s => new SqlSuggestion(s.Column!, Quote(s.Column!), s.Schema + "." + s.Table + " — " + s.Kind)).DistinctBy(s => s.Text).ToList();
            return list.Where(s => s.Column == null && s.Schema.Equals(qualifier, StringComparison.OrdinalIgnoreCase)).Select(s => new SqlSuggestion(s.Table, Quote(s.Table), s.Kind)).ToList();
        }
        var tableContext = Regex.IsMatch(beforeCaret, @"\b(?:FROM|JOIN|UPDATE|INTO|TABLE)\s+(?:\[[^\]]+\]|\w|\.)*$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        var tableSuggestions = list.Where(s => s.Column == null)
            .Select(s => new SqlSuggestion(s.Table, Quote(s.Schema) + "." + Quote(s.Table), s.Kind + " — " + s.Schema));
        if (tableContext) return tableSuggestions.DistinctBy(s => s.InsertText).ToList();
        return Keywords.Select(k => new SqlSuggestion(k, k, "T-SQL syntax / function"))
            .Concat(list.Where(s => s.Column != null).Select(s => new SqlSuggestion(s.Column!, Quote(s.Column!), s.Schema + "." + s.Table + " — " + s.Kind)))
            .Concat(tableSuggestions)
            .DistinctBy(s => s.InsertText).ToList();
    }
    private static string Quote(string text) => "[" + text.Replace("]", "]]") + "]";
}
