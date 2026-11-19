using System.Text.RegularExpressions;

namespace LSCode.Core;
public sealed record SqlSymbol(string Schema, string Table, string? Column, string Kind);
public sealed record SqlSuggestion(string Text, string InsertText, string Description, bool IsSnippet = false);
public static class SqlSuggestions
{
    public static readonly string[] Keywords = ["SELECT", "FROM", "WHERE", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "GROUP BY", "ORDER BY", "HAVING", "INSERT INTO", "UPDATE", "DELETE FROM", "CREATE PROCEDURE", "ALTER PROCEDURE", "DECLARE", "BEGIN", "END", "EXEC", "AS", "CASE", "WHEN", "THEN", "ELSE", "AND", "OR", "NOT", "IN", "NOT IN", "LIKE", "BETWEEN", "NULL", "IS NULL", "IS NOT NULL", "ASC", "DESC", "ISNULL", "COALESCE", "COUNT", "SUM", "AVG", "MIN", "MAX", "TOP", "DISTINCT", "GETDATE", "DATEADD", "DATEDIFF", "CAST", "CONVERT", "EXISTS", "NOT EXISTS", "UNION ALL", "OVER", "PARTITION BY", "ROW_NUMBER", "WITH", "SET", "VALUES"];
    public static IReadOnlyList<SqlSuggestion> Get(string beforeCaret, IEnumerable<SqlSymbol> symbols, string? fullSql = null)
    {
        const string identifier = @"(?:\[(?:[^\]]|\]\])+\]|[\w$]+)";
        string Unquote(string value) => value.StartsWith('[') && value.EndsWith(']') ? value[1..^1].Replace("]]", "]") : value;
        var qualifier = Unquote(Regex.Match(beforeCaret, "(?<q>" + identifier + @")\.[\w$]*$", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Groups["q"].Value);
        var list = symbols.ToList();
        var documentSql = fullSql ?? beforeCaret;
        var locals = LocalSymbols(documentSql);
        var prefix = Regex.Match(beforeCaret, @"(?<![\w@$#])(?:@{1,2}|#{1,2})[\w$]*\z",
            RegexOptions.None, TimeSpan.FromMilliseconds(100)).Value;
        if (prefix.Length > 0)
            return locals.Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (qualifier.Length > 0)
        {
            var aliases = Regex.Matches(fullSql ?? beforeCaret, @"\b(?:FROM|JOIN)\s+(?:(?<schema>" + identifier + @")\.)?(?<table>" + identifier + @")\s+(?:AS\s+)?(?<alias>" + identifier + ")", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            var alias = aliases.Cast<Match>().LastOrDefault(m => Unquote(m.Groups["alias"].Value).Equals(qualifier, StringComparison.OrdinalIgnoreCase));
            var table = alias == null ? qualifier : Unquote(alias.Groups["table"].Value);
            var columns = list.Where(s => s.Column != null && s.Table.Equals(table, StringComparison.OrdinalIgnoreCase) && (alias == null || alias.Groups["schema"].Value.Length == 0 || s.Schema.Equals(Unquote(alias.Groups["schema"].Value), StringComparison.OrdinalIgnoreCase))).ToList();
            if (columns.Count > 0) return columns.Select(s => new SqlSuggestion(s.Column!, FormatIdentifier(s.Column!), s.Schema + "." + s.Table + " — " + s.Kind)).DistinctBy(s => s.Text).ToList();
            return list.Where(s => s.Column == null && s.Schema.Equals(qualifier, StringComparison.OrdinalIgnoreCase)).Select(s => new SqlSuggestion(s.Table, FormatIdentifier(s.Table), s.Kind)).ToList();
        }
        var tableContext = Regex.IsMatch(beforeCaret, @"\b(?:FROM|JOIN|UPDATE|INTO|TABLE)\s+(?:\[[^\]]+\]|[\w$]|\.)*$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        var tableSuggestions = list.Where(s => s.Column == null)
            .Select(s => new SqlSuggestion(s.Table, FormatIdentifier(s.Schema) + "." + FormatIdentifier(s.Table), s.Kind + " — " + s.Schema));
        if (tableContext) return locals.Where(s => s.Text.StartsWith('#') || s.Description.Contains("bảng", StringComparison.OrdinalIgnoreCase))
            .Concat(tableSuggestions).DistinctBy(s => s.InsertText, StringComparer.OrdinalIgnoreCase).ToList();
        return Keywords.Select(k => new SqlSuggestion(k, k, "T-SQL syntax / function"))
            .Concat(locals)
            .Concat(list.Where(s => s.Column != null).Select(s => new SqlSuggestion(s.Column!, FormatIdentifier(s.Column!), s.Schema + "." + s.Table + " — " + s.Kind)))
            .Concat(tableSuggestions)
            .DistinctBy(s => s.InsertText).ToList();
    }
    private static IReadOnlyList<SqlSuggestion> LocalSymbols(string sql)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var timeout = TimeSpan.FromMilliseconds(100);
        sql = Regex.Replace(sql, @"\[(?:[^\]]|\]\])*\]|""(?:[^""]|"""")*""|'(?:[^']|'')*'|--[^\r\n]*|/\*[\s\S]*?(?:\*/|\z)",
            match => match.Value[0] is '[' or '"' ? match.Value : " ", options, timeout);
        var parameters = Regex.Matches(sql,
                @"\b(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+PROC(?:EDURE)?\s+(?:\[[^\]]+\]|[\w$]+)(?:\s*\.\s*(?:\[[^\]]+\]|[\w$]+))?(?<header>[\s\S]*?)(?=\bAS\b|\bBEGIN\b|\z)",
                options, timeout).Cast<Match>()
            .SelectMany(procedure => Regex.Matches(procedure.Groups["header"].Value,
                @"(?:^|,)\s*(?<name>@[\p{L}_][\w@$#]*)\s+", options, timeout).Cast<Match>())
            .Select(match => new SqlSuggestion(match.Groups["name"].Value, match.Groups["name"].Value, "Tham số stored procedure"));
        var variables = Regex.Matches(sql, @"\bDECLARE\s+(?<body>[^;\r\n]+)", options, timeout).Cast<Match>()
            .SelectMany(declaration => Regex.Matches(declaration.Groups["body"].Value, @"@[A-Za-z_][\w@$#]*", RegexOptions.None, timeout).Cast<Match>())
            .Select(match => new SqlSuggestion(match.Value, match.Value, "Biến trong tài liệu hiện tại"));
        var tableVariables = Regex.Matches(sql, @"\bDECLARE\s+(?<name>@[A-Za-z_][\w@$#]*)\s+TABLE\b", options, timeout)
            .Cast<Match>().Select(match => new SqlSuggestion(match.Groups["name"].Value, match.Groups["name"].Value, "Biến bảng trong tài liệu hiện tại"));
        var tempTables = Regex.Matches(sql, @"(?:\bCREATE\s+TABLE|\bINTO)\s+(?<name>#{1,2}[A-Za-z_][\w@$#]*)", options, timeout)
            .Cast<Match>().Select(match => new SqlSuggestion(match.Groups["name"].Value, match.Groups["name"].Value, "Bảng tạm trong tài liệu hiện tại"));
        return parameters.Concat(tableVariables).Concat(variables).Concat(tempTables)
            .DistinctBy(item => item.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static string FormatIdentifier(string text) =>
        Regex.IsMatch(text, @"\A[\p{L}_][\w$]*\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            ? text
            : "[" + text.Replace("]", "]]") + "]";
}
