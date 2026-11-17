using System.Data;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;

internal sealed partial class CatalogDesigner
{
    // Explicit opt-in uses only localhost and a fresh database owned by this run.
    internal static async Task CheckLocalSqlAsync(List<string> report)
    {
        string database = "LSCode_Catalog_Test_" + Guid.NewGuid().ToString("N");
        const string master = "Server=localhost;Database=master;Integrated Security=True;Encrypt=False;Connect Timeout=5;Application Name=LSCode Catalog Tests";
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        bool created = false;
        CatalogDesigner? fixtureDesigner = null;
        async Task Execute(string sql)
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
            await command.ExecuteNonQueryAsync();
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        try
        {
            await Execute("CREATE DATABASE " + Quote(database)); created = true;
            var builder = new SqlConnectionStringBuilder(master) { InitialCatalog = database };
            var target = ProjectDatabase.Target(builder.ConnectionString);
            var designer = new CatalogDesigner(target, () => { }, false);
            fixtureDesigner = designer;
            var source = new TableName("dbo", "catalog_source");
            designer.draft.Add(new Field { Name = "code", Type = "nvarchar", Size = "16", Key = true });
            designer.draft.Add(new Field { Name = "label", Type = "nvarchar", Size = "128", Default = "Tiếng Việt" });
            designer.draft.Add(new Field { Name = "serial", Type = "int", Identity = true, IdentitySeed = "7", IdentityIncrement = "3" });
            designer.draft.Add(new Field { Name = "active", Type = "bit", Default = "1" });
            designer.draft.Add(new Field { Name = "parent_code", Type = "nvarchar", Size = "16", Reference = ["dbo", source.Name, "code", "nvarchar"] });
            designer.draft.Add(new Field { Name = "ansi", Type = "varchar", Size = "4", Collation = "SQL_Latin1_General_CP1_CI_AS", Default = "AB" });
            await designer.WriteAsync(designer.DefinitionCommands(source, true));
            designer.draft.Clear(); designer.selected = source;
            designer.metadata = await designer.MetadataAsync(source, CancellationToken.None);
            designer.uniqueKeyColumns = await designer.UniqueKeysAsync(source, CancellationToken.None);
            string?[] Record(string code, string label)
            {
                var record = new string?[designer.metadata.Rows.Count];
                for (int i = 0; i < record.Length; i++) record[i] = (string)designer.metadata.Rows[i][0] switch
                { "code" => code, "label" => label, "parent_code" => ":null", _ => null };
                return record;
            }
            designer.records.Add(Record("A", "Nguồn"));
            await designer.WriteAsync(designer.InsertCommands(source)); designer.records.Clear();
            var copied = await designer.ReadTemplateAsync(source);
            Check(copied.Count == 6 && copied.Single(f => f.Name == "serial").IdentitySeed == "7", "SQL template identity/default extraction failed");
            foreach (var field in copied) designer.draft.Add(field);
            designer.duplicateSource = source;
            var clone = new TableName("dbo", "catalog_copy");
            await designer.WriteAsync(designer.DefinitionCommands(clone, true));
            designer.draft.Clear(); designer.selected = clone;
            designer.metadata = await designer.MetadataAsync(clone, CancellationToken.None);
            designer.uniqueKeyColumns = await designer.UniqueKeysAsync(clone, CancellationToken.None);
            async Task<DataTable> Read(string sql) => await ProjectDatabase.ReadAsync(target, sql, CancellationToken.None);
            var counts = await Read($"SELECT (SELECT COUNT(*) FROM {source.Sql}), (SELECT COUNT(*) FROM {clone.Sql})");
            Check((int)counts.Rows[0][0] == 1 && (int)counts.Rows[0][1] == 0, "Duplicate copied data or changed source");
            var audit = designer.metadata.Rows.Cast<DataRow>().Where(r => Audit.Contains((string)r[0])).ToArray();
            Check(audit.Length == 4 && audit.All(r => (string)r[1] == (((string)r[0]).StartsWith("user_", StringComparison.Ordinal) ? "int" : "datetime")), "Cloned audit contract failed");
            var foreign = await Read("SELECT OBJECT_NAME(referenced_object_id) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.catalog_copy')");
            Check((string)foreign.Rows[0][0] == clone.Name, "Cloned self-reference still targets source");
            designer.records.Add(Record("A", ":text::null"));
            designer.records.Add(Record("B", ":empty"));
            await designer.WriteAsync(designer.InsertCommands(clone)); designer.records.Clear();
            var values = await Read($"SELECT code,label,serial,active,datetime0,datetime2 FROM {clone.Sql} ORDER BY code");
            Check((string)values.Rows[0][1] == ":null" && (string)values.Rows[1][1] == "" &&
                (int)values.Rows[0][2] == 7 && (int)values.Rows[1][2] == 10 && (bool)values.Rows[0][3] &&
                Equals(values.Rows[0][4], values.Rows[0][5]), "SQL paste literal/empty/identity/default/audit round trip failed");

            designer.records.Add(Record("ROLLBACK", "Must roll back"));
            designer.records.Add(Record("A", "Duplicate key"));
            bool failed = false;
            try { await designer.WriteAsync(designer.InsertCommands(clone)); }
            catch (SqlException) { failed = true; }
            Check(failed, "Expected database UNIQUE failure");
            counts = await Read($"SELECT COUNT(*) FROM {clone.Sql} WHERE code=N'ROLLBACK'");
            Check((int)counts.Rows[0][0] == 0 && designer.records.Count == 2, "Failed insert did not roll back or draft was discarded");
            designer.records.Clear();
            designer.records.Add(Record("DUP", "One")); designer.records.Add(Record("DUP", "Two"));
            failed = false;
            try { designer.InsertCommands(clone); } catch (CellException) { failed = true; }
            Check(failed, "In-batch duplicate keys were not rejected"); designer.records.Clear();

            var lossy = Record("LOSS", "Check encoding");
            int ansiIndex = designer.metadata.Rows.Cast<DataRow>().ToList().FindIndex(r => (string)r[0] == "ansi");
            lossy[ansiIndex] = "ữ"; designer.records.Add(lossy); failed = false;
            try { await designer.WriteAsync(designer.InsertCommands(clone)); }
            catch (SqlException ex) when (ex.Number == 50001) { failed = true; }
            Check(failed, "Lossy ANSI conversion was not rejected"); designer.records.Clear();

            await Execute($"USE {Quote(database)}; ALTER TABLE {clone.Sql} ADD CONSTRAINT CK_test CHECK (active IN (0,1)); USE master;");
            failed = false;
            try { await designer.ReadTemplateAsync(clone); } catch (InvalidOperationException ex) when (ex.Message.Contains("CHECK", StringComparison.Ordinal)) { failed = true; }
            Check(failed, "Unsupported CHECK was silently dropped");
            report.Add("PASS local SQL Server: create source, duplicate structure without rows, self-FK remapping, identity(7,3), DEFAULT/audit/Unicode/NULL-literal/empty round trip, batch duplicate rejection, transaction rollback, ANSI loss prevention, unsupported CHECK guard");
        }
        finally
        {
            if (fixtureDesigner != null) { fixtureDesigner.dirty = false; fixtureDesigner.Close(); }
            if (created)
            {
                SqlConnection.ClearAllPools();
                await Execute("USE master; ALTER DATABASE " + Quote(database) + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + Quote(database));
                report.Add("PASS isolated local SQL test database removed");
            }
        }
    }
}
