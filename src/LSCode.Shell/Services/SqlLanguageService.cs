using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace LSCode.Shell.Services;

public static class SqlLanguageService
{
    // SQL150 matches the legacy SQLBeautyFormat default; do not silently upgrade dialect.
    public static string Format(string text)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: false);
        var fragment = parser.Parse(new StringReader(text), out var errors);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(8).Select(e => $"SQL ({e.Line},{e.Column}): {e.Message}")));
        new Sql150ScriptGenerator(new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase, IndentationSize = 4,
            NewLineBeforeFromClause = true, NewLineBeforeWhereClause = true, NewLineBeforeOrderByClause = true
        }).GenerateScript(fragment, out var formatted);
        return formatted;
    }
}
