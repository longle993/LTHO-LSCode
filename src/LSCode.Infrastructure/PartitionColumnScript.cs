using System.Text;
using System.Text.RegularExpressions;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal sealed class PartitionColumnScript(string schema, string baseTable, string stem)
{
    internal static async Task<PartitionColumnScript?> DetectAsync(SqlTarget target, string schema, string table,
        CancellationToken ct)
    {
        var stem = Regex.Replace(table, @"\$(?:[0-9]{6}|log)$", "", RegexOptions.IgnoreCase);
        var metadata = await ProjectDatabase.ReadAsync(target, """
            IF COL_LENGTH(N'dbo.sysgendata', N'basetable') IS NOT NULL
                EXEC sys.sp_executesql N'
                    SELECT TOP (1) 1
                    FROM dbo.sysgendata
                    WHERE LTRIM(RTRIM(basetable)) IN
                        (@stem, @prefix, @template, @qualifiedStem, @qualifiedPrefix, @qualifiedTemplate);',
                    N'@stem nvarchar(128), @prefix nvarchar(129), @template nvarchar(135),
                      @qualifiedStem nvarchar(257), @qualifiedPrefix nvarchar(258), @qualifiedTemplate nvarchar(264)',
                    @stem, @prefix, @template, @qualifiedStem, @qualifiedPrefix, @qualifiedTemplate;
            ELSE
                SELECT 1 WHERE 1 = 0;
            """, ct,
            new SqlParameter("@stem", stem), new SqlParameter("@prefix", stem + "$"),
            new SqlParameter("@template", stem + "$000000"), new SqlParameter("@qualifiedStem", schema + "." + stem),
            new SqlParameter("@qualifiedPrefix", schema + "." + stem + "$"),
            new SqlParameter("@qualifiedTemplate", schema + "." + stem + "$000000"));
        return metadata.Rows.Count == 0 ? null : new(schema, stem == table ? table : stem + "$000000", stem);
    }

    internal string Build(string database, IEnumerable<(string Name, string Definition, bool Backfill)> columns, bool alter)
    {
        string Qualified(string table) => ProjectDatabase.Quote(schema) + "." + ProjectDatabase.Quote(table);
        var template = Qualified(baseTable);
        var log = Qualified(stem + "$log");
        var partition = Qualified(stem + "$%Partition");
        var selected = columns.ToArray();
        var script = new StringBuilder().AppendLine($"USE {ProjectDatabase.Quote(database)};").AppendLine("GO")
            .AppendLine($"IF DB_NAME() <> {Literal(database)} THROW 50000, N'Hãy chạy script trên đúng database App.', 1;")
            .AppendLine("""
            DECLARE @strsql nvarchar(4000), @dFrom smalldatetime, @dTo smalldatetime;
            IF COL_LENGTH(N'dbo.dmstt', N'ngay_gh1') IS NULL OR COL_LENGTH(N'dbo.dmstt', N'ngay_gh2') IS NULL
                THROW 50000, N'Thiếu ngay_gh1 hoặc ngay_gh2 trong dbo.dmstt.', 1;
            IF OBJECT_ID(N'dbo.FastBusiness$Partition$Execute', N'P') IS NULL
                THROW 50000, N'Thiếu thủ tục FastBusiness$Partition$Execute.', 1;
            EXEC sys.sp_executesql
                N'SELECT @dFrom = MIN(ngay_gh1), @dTo = MAX(ngay_gh2) FROM dbo.dmstt;',
                N'@dFrom smalldatetime OUTPUT, @dTo smalldatetime OUTPUT', @dFrom OUTPUT, @dTo OUTPUT;
            IF @dFrom IS NULL OR @dTo IS NULL OR @dFrom > @dTo
                THROW 50000, N'Khoảng ngày phân kỳ trong dbo.dmstt không hợp lệ.', 1;
            """);
        foreach (var table in new[] { template, log })
        {
            script.AppendLine().AppendLine($"IF OBJECT_ID({Literal(table)}, N'U') IS NOT NULL").AppendLine("BEGIN");
            foreach (var (name, definition, backfill) in selected)
            {
                script.AppendLine("    " + ColumnDdl(table, name, definition, alter));
                if (backfill)
                {
                    var column = ProjectDatabase.Quote(name);
                    var update = $"UPDATE {table} SET {column} = N'' WHERE {column} IS NULL;";
                    script.AppendLine($"    EXEC sys.sp_executesql {Literal(update)};");
                }
            }
            script.AppendLine("END;");
        }
        foreach (var (name, definition, backfill) in selected)
        {
            AppendPartition(ColumnDdl(partition, name, definition, alter));
            if (backfill)
            {
                var column = ProjectDatabase.Quote(name);
                AppendPartition($"UPDATE {partition} SET {column} = N'' WHERE %[ {column} IS NULL ]%;");
            }
        }
        return script.AppendLine("GO").ToString();

        void AppendPartition(string command)
        {
            if (command.Length > 4000)
                throw new NotSupportedException("Lệnh cho một phân kỳ vượt quá 4.000 ký tự.");
            script.AppendLine().AppendLine($"SET @strsql = {Literal(command)};")
                .AppendLine("EXEC dbo.[FastBusiness$Partition$Execute] @strsql, '', 'ngay_ct', @dFrom, @dTo, 1, 1;");
        }
    }

    private static string ColumnDdl(string table, string name, string definition, bool alter) =>
        $"IF COL_LENGTH({Literal(table)}, {Literal(name)}) IS {(alter ? "NOT " : "")}NULL " +
        $"ALTER TABLE {table} {(alter ? "ALTER COLUMN" : "ADD")} {definition};";

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
}
