using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace LSCode.Infrastructure;
public static class ReadOnlyQuery
{
    public static string Validate(string sql)
    {
        var script = new TSql150Parser(false).Parse(new StringReader(sql), out var errors) as TSqlScript;
        if (errors.Count > 0 || script == null || script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1 ||
            script.Batches[0].Statements[0] is not SelectStatement select || select.Into != null)
            throw new ArgumentException("Cửa sổ xem bảng chỉ hỗ trợ một SELECT, không cho phép SELECT INTO hoặc lệnh ghi.");
        return sql;
    }
}
