using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace LSCode.Infrastructure;

internal static class DatabaseObjectScript
{
    internal static async Task<string> ReadAsync(SqlTarget target, string name, CancellationToken ct)
    {
        var definition = await ProjectDatabase.ReadAsync(target, """
            SELECT OBJECT_DEFINITION(o.object_id), o.type, s.name, o.name
            FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.object_id = OBJECT_ID(@object)
            """, ct, new SqlParameter("@object", name));
        if (definition.Rows.Count > 0 && definition.Rows[0][0] is string script)
        {
            var row = definition.Rows[0];
            if (((string)row[1]).TrimEnd() == "P")
                script = ForProcedure(script, (string)row[2], (string)row[3]);
            return script.TrimEnd() + "\nGO\n\n";
        }

        // TableDefinition already terminates its final batch with GO.
        return (await LSCode.Shell.Services.TableDefinition.ReadAsync(target, name, ct)).TrimEnd() + "\n\n";
    }

    private static string ForProcedure(string script, string schema, string name)
    {
        var tokens = new TSql150Parser(true).GetTokenStream(new StringReader(script), out _)
            .Where(token => token.TokenType is not (TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment))
            .Take(3).ToArray();
        if (tokens.Length > 0 && tokens[0].TokenType == TSqlTokenType.Create)
        {
            var last = tokens.Length == 3 && tokens[1].TokenType == TSqlTokenType.Or &&
                tokens[2].TokenType == TSqlTokenType.Alter ? tokens[2] : tokens[0];
            script = script[..tokens[0].Offset] + "ALTER" + script[(last.Offset + last.Text.Length)..];
        }

        var qualified = ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(name);
        var objectLiteral = qualified.Replace("'", "''", StringComparison.Ordinal);
        return $"""
            IF OBJECT_ID(N'{objectLiteral}', N'P') IS NULL
                EXEC(N'CREATE PROCEDURE {objectLiteral} AS RETURN 0;');
            GO

            {script}
            """;
    }
}
