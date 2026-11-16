using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace LSCode.Shell.Services;

public static class SqlLanguageService
{
    // Keep the legacy SQLBeautyFormat profile used by the existing SQL scripts.
    public static string Format(string text)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: false);
        var fragment = parser.Parse(new StringReader(text), out var errors);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(8).Select(e => $"SQL ({e.Line},{e.Column}): {e.Message}")));
        var options = new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IndentationMode = IndentationMode.Tabs,
            IndentationSize = 4,
            PreserveComments = true,
            AlignClauseBodies = false,
            AlignSetClauseItem = false,
            MultilineSelectElementsList = false,
            MultilineSetClauseItems = false,
            MultilineWherePredicatesList = false,
            MultilineHavingPredicatesList = false,
            MultilineInsertSourcesList = false,
            MultilineProcedureParametersList = true,
            NewLineAfterJoinKeyword = false,
            NewLineBeforeJoinClause = false,
            NewLineBeforeOnClause = false,
            SpaceBetweenDataTypeAndParameters = false,
            SpaceBetweenParametersInDataType = false,
            NewLineBeforeFromClause = true,
            NewLineBeforeWhereClause = true,
            NewLineBeforeOrderByClause = true,
            PersistTrailingGo = true,
        };
        new Sql150ScriptGenerator(options).GenerateScript(fragment, out var formatted);
        return formatted;
    }
}
