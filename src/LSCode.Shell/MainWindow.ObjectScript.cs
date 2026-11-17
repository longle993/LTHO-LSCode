using System.Data;
using System.Windows;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;
public partial class MainWindow
{
    private sealed record DbObjectReference(SqlTarget Target, string Schema, string Name, string Type);
    private CancellationTokenSource? objectScriptCancellation;
    private async Task OpenDbObjectAsync(DbObjectReference item)
    {
        objectScriptCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        objectScriptCancellation = cancellation;
        var destination = Active is { Language: ".sql" } sql ? sql : null;
        var version = destination?.Version;
        using var busy = BeginBusy("Đang đọc script " + item.Schema + "." + item.Name + "…");
        try
        {
            var qualified = ProjectDatabase.Quote(item.Schema) + "." + ProjectDatabase.Quote(item.Name);
            var data = await ProjectDatabase.ReadAsync(item.Target,
                "SELECT o.type, OBJECT_DEFINITION(o.object_id) AS definition FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE s.name=@schema AND o.name=@name",
                cancellation.Token, new SqlParameter("@schema", item.Schema), new SqlParameter("@name", item.Name));
            if (data.Rows.Count == 0) throw new InvalidOperationException("Đối tượng đã bị xóa hoặc tài khoản không có quyền xem metadata.");
            string script;
            if (Convert.ToString(data.Rows[0][0])!.Trim() == "U")
            {
                script = await TableDefinition.ReadAsync(item.Target, qualified, cancellation.Token);
            }
            else
            {
                script = Convert.ToString(data.Rows[0][1]) ?? "";
                if (string.IsNullOrWhiteSpace(script)) throw new InvalidOperationException("Không đọc được định nghĩa: object có thể được mã hóa, là CLR hoặc tài khoản thiếu quyền VIEW DEFINITION.");
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (closing || objectScriptCancellation != cancellation) return;
            // Do not overwrite a buffer edited or closed while metadata was in flight.
            if (destination != null && (!tabs.Contains(destination) || destination.Version != version)) destination = null;
            if (destination == null) { destination = AddDocument(item.Schema + "." + item.Name + ".sql", "", sqlTarget: item.Target); version = null; }
            await destination.TargetReady;
            cancellation.Token.ThrowIfCancellationRequested();
            if (closing || closeRequested || objectScriptCancellation != cancellation || !tabs.Contains(destination)) return;
            if (version != null && destination.Version != version) destination = AddDocument(item.Schema + "." + item.Name + ".sql", "", sqlTarget: item.Target);
            ApplyObjectScript(destination, script, item.Target);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing && objectScriptCancellation == cancellation) { DbObjectStatus.Text = ex.Message; Log(ex.Message); } }
        finally { if (objectScriptCancellation == cancellation) objectScriptCancellation = null; }
    }
    private void ApplyObjectScript(EditorTab destination, string script, SqlTarget target)
    {
        // Insert/replace as one undoable edit; an existing file remains associated with its path.
        destination.Buffer.Replace(0, destination.Buffer.TextLength, script);
        destination.SqlTarget = target;
        Documents.SelectedItem = destination.Tab;
        destination.Primary.CaretOffset = 0; destination.Primary.ScrollToHome(); destination.Primary.Focus();
        UpdateStatus();
    }
}
