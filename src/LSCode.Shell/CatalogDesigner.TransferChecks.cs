using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    internal async Task RunCatalogTransferSmokeAsync()
    {
        var report = new List<string>(); int exitCode = 0;
        try
        {
            Directory.CreateDirectory(SettingsStore.Root);
            if (Environment.GetEnvironmentVariable("LSCODE_CATALOG_LOCAL_SQL_TEST") == "1")
                await CatalogDesigner.CheckLocalSqlAsync(report);
            foreach (var theme in new[] { "Light", "Dark", "High Contrast" })
            {
                settings.Theme = theme; ApplyTheme();
                await CatalogDesigner.CheckTransferAsync(this, report, theme);
            }
        }
        catch (Exception ex) { report.Add("FAIL " + ex); exitCode = 1; }
        Directory.CreateDirectory(SettingsStore.Root);
        await File.WriteAllLinesAsync(Path.Combine(SettingsStore.Root, "catalog-transfer-result.txt"), report);
        Application.Current.Shutdown(exitCode);
    }
}

internal sealed partial class CatalogDesigner
{
    internal static async Task CheckTransferAsync(Window owner, List<string> report, string theme)
    {
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Reject(Action action, string message)
        {
            try { action(); } catch (Exception ex) when (ex is InvalidOperationException or CellException) { return; }
            throw new InvalidOperationException("Expected rejection: " + message);
        }
        var designer = new CatalogDesigner(ProjectDatabase.Target("Server=unused-fixture;Database=fixture;Integrated Security=true"),
            () => throw new InvalidOperationException("Smoke must not write SQL"), false) { Owner = owner };
        try
        {
            designer.Show(); designer.UpdateLayout();
            await designer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var columns = new DataTable(); for (int i = 0; i < 16; i++) columns.Columns.Add("C" + i, typeof(object));
            columns.Rows.Add("ma", "int", (short)4, (byte)10, (byte)0, false, true, false, DBNull.Value, "7", "3", DBNull.Value, 0, false, false, 0);
            columns.Rows.Add("ten", "nvarchar", (short)256, (byte)0, (byte)0, true, false, false, "(N'Tiếng Việt')", DBNull.Value, DBNull.Value, "Latin1_General_CI_AS", 0, false, false, 0);
            for (int i = 0; i < Audit.Length; i++)
                columns.Rows.Add(Audit[i], i < 2 ? "int" : "datetime", (short)8, (byte)0, (byte)0, true, false, false, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0, false, false, 0);
            var keys = new DataTable(); for (int i = 0; i < 4; i++) keys.Columns.Add("K" + i, typeof(object));
            keys.Rows.Add("ma", true, false, 1);
            var references = new DataTable(); for (int i = 0; i < 5; i++) references.Columns.Add("R" + i, typeof(object));
            var copied = TemplateFields(columns, keys, references);
            Check(copied.Count == 2 && copied[0].IdentitySeed == "7" && copied[0].IdentityIncrement == "3" &&
                copied[0].Key && copied[1].Default == "Tiếng Việt", "Template did not preserve identity/key/literal DEFAULT");
            var deep = CloneField(new Field { Reference = ["dbo", "source", "ma", "int"] });
            var unpacked = UnpackField(PackField(deep)); unpacked.Reference![1] = "other";
            Check(deep.Reference![1] == "source", "Pasted reference shares source array");
            foreach (var field in copied) designer.draft.Add(field);
            var ddl = designer.DefinitionCommands(new TableName("dbo", "new_table"), true)[0].Sql;
            Check(ddl.Contains("IDENTITY(7,3)") && ddl.Contains("COLLATE Latin1_General_CI_AS") && ddl.Contains("[datetime2] datetime NULL") &&
                Audit.All(a => ddl.Split("[" + a + "]", StringSplitOptions.None).Length == 2), "Template DDL/audit invariant failed");
            copied[0].Name = "ma_changed";
            Check((string)columns.Rows[0][0] == "ma", "Source metadata was mutated");
            copied[0].Name = "ma";
            designer.duplicateSource = new TableName("dbo", "source");
            designer.templateOriginalNames[copied[0]] = "ma";
            var selfReference = new Field { Name = "parent", Type = "int", Reference = ["dbo", "source", "ma", "int"] };
            designer.draft.Add(selfReference); copied[0].Name = "ma_renamed";
            var renamedDdl = designer.DefinitionCommands(new TableName("dbo", "new_table"), true)[0].Sql;
            Check(renamedDdl.Contains("REFERENCES [dbo].[new_table]([ma_renamed])"), "Self-FK did not follow a renamed cloned key");
            designer.draft.Remove(selfReference); copied[0].Name = "ma";
            designer.duplicateSource = null; designer.templateOriginalNames.Clear();
            Reject(() => ReadLiteralDefault(new Field { Name = "bad" }, "(GETDATE())"), "expression DEFAULT");
            columns.Rows[0][7] = true;
            Reject(() => TemplateFields(columns, keys, references), "computed column"); columns.Rows[0][7] = false;
            columns.Rows[2][1] = "bigint";
            Reject(() => TemplateFields(columns, keys, references), "audit type conflict"); columns.Rows[2][1] = "int";
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ma_copy" };
            Check(UniqueCopyName("ma", names) == "ma_copy_2" && UniqueCopyName(new string('x', 128), names).Length == 128,
                "Copy naming collision/identifier limit failed");
            Reject(() => designer.ValidateFieldCopies([new Field { Name = "ma", Type = "int" }]), "duplicate field");
            Check(designer.draft.Count == 2, "Rejected paste changed original fields");
            Reject(() => designer.ValidateFieldCopies([new Field { Name = "identity2", Type = "int", Identity = true }]), "multiple identities");
            report.Add("PASS " + theme + ": duplicate identity/key/default/collation/audit preservation, deep copies, unsupported schema guards, atomic field validation");

            designer.draft.Clear(); designer.selected = new TableName("dbo", "destination");
            var metadata = new DataTable(); for (int i = 0; i < 15; i++) metadata.Columns.Add("M" + i, typeof(object));
            metadata.Rows.Add("ma", "int", (short)4, (byte)10, (byte)0, false, false, false, DBNull.Value, 0, 56, 1, 42, false, DBNull.Value);
            metadata.Rows.Add("ten", "nvarchar", (short)256, (byte)0, (byte)0, true, false, false, DBNull.Value, 0, 231, 2, 42, false, DBNull.Value);
            designer.metadata = metadata;
            designer.ValidateRecordCopies([["1", ":text::null"], ["2", ":empty"]]);
            Check(designer.records.Count == 0, "Validation applied records prematurely");
            Reject(() => designer.ValidateRecordCopies([["bad", "test"]]), "invalid integer");
            Check(designer.records.Count == 0, "Failed paste left partial records");
            designer.records.Add(["3", ":text::default"]);
            var insert = designer.InsertCommands(designer.selected)[0];
            Check((string)insert.Parameters.Single(p => p.ParameterName == "@p1").Value == ":default", "Literal sentinel became DEFAULT");
            designer.records.Clear();

            designer.preview.SetPage(["ma", "ten"], [[1, "Một"], [2, "Hai"], [3, "Ba"]]);
            designer.preview.SelectWholeRow(0, ModifierKeys.None); designer.preview.SelectWholeRow(2, ModifierKeys.Control);
            Check(designer.preview.SelectedRowIndexes.SequenceEqual(new[] { 0, 2 }), "Noncontiguous preview row selection failed");
            designer.preview.SelectWholeRow(0, ModifierKeys.Shift);
            Check(designer.preview.SelectedRowIndexes.Count == 3, "Shift row range failed");
            designer.preview.SetPage(["ma", "ten"], [[1, "Một"]]);
            Check(designer.preview.SelectedRowIndexes.Count == 0, "Selection leaked across pages");
            designer.name.Text = "destination"; designer.modes.SelectedIndex = 1;
            ((TabItem)designer.modes.Items[1]).IsEnabled = true;

            Exception? dialogError = null;
            var callback = designer.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = designer.OwnedWindows.Cast<Window>().Single();
                try
                {
                    dialog.UpdateLayout();
                    MainWindow.SaveDesignImage(dialog, "catalog-paste-" + theme.Replace(" ", "-"));
                    var buttons = TransferDescendants<Button>(dialog).ToArray();
                    buttons.Single(b => Equals(b.Content, "Kiểm tra")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    var apply = buttons.Single(b => Equals(b.Content, "Áp dụng"));
                    Check(apply.IsEnabled, "Valid paste preview did not enable Apply: " + string.Join(" | ", TransferDescendants<TextBlock>(dialog).Select(t => t.Text)));
                    apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
                catch (Exception ex) { dialogError = ex; dialog.DialogResult = false; }
            }));
            designer.ShowDataPaste(["ma", "ten"], [["10", "Một"], ["11", ":text::null"]], true);
            await callback;
            if (dialogError != null) throw dialogError;
            Check(designer.records.Count == 2 && designer.records[1][1] == ":text::null", "Accepted paste was not applied atomically to draft");
            Check(designer.InsertCommands(designer.selected).Count == 2, "Pasted record generator failed");
            if (theme == "Light")
            {
                void VisitPasteDialog(bool accept, bool setHeader)
                {
                    var modal = designer.OwnedWindows.Cast<Window>().Single();
                    try
                    {
                        if (!accept) { modal.DialogResult = false; return; }
                        if (setHeader) TransferDescendants<CheckBox>(modal).Single(c => Equals(c.Content, "Dòng đầu là tên cột")).IsChecked = true;
                        var buttons = TransferDescendants<Button>(modal).ToArray();
                        buttons.Single(b => Equals(b.Content, "Kiểm tra")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        var apply = buttons.Single(b => Equals(b.Content, "Áp dụng"));
                        Check(apply.IsEnabled, "Valid external/field paste was rejected: " + string.Join(" | ", TransferDescendants<TextBlock>(modal).Select(t => t.Text)));
                        apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    }
                    catch (Exception ex) { dialogError = ex; modal.DialogResult = false; }
                }
                var external = designer.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => VisitPasteDialog(true, true)));
                designer.ShowExternalDataPaste(LSCode.Core.CatalogClipboard.Read("ma\tten\n12\tTiếng Việt\n13\tNULL\n"));
                await external; if (dialogError != null) throw dialogError;
                Check(designer.records.Count == 4 && designer.records[3][1] == "NULL", "External header mapping/text NULL handling failed");
                var cancel = designer.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => VisitPasteDialog(false, false)));
                designer.ShowExternalDataPaste(LSCode.Core.CatalogClipboard.Read("14\tCanceled"));
                await cancel;
                Check(designer.records.Count == 4, "Cancel paste changed pending records");
                designer.records.Clear(); designer.modes.SelectedIndex = 0;
                var fieldPaste = designer.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => VisitPasteDialog(true, false)));
                designer.PasteFields([new Field { Name = "ma" }]); await fieldPaste;
                if (dialogError != null) throw dialogError;
                Check(designer.draft.Single().Name == "ma_copy", "Pasted field collision did not use independent name");
                designer.fields.SelectedItem = designer.draft[0]; designer.RemoveDraftRows(designer.fields);
                Check(designer.draft.Count == 0, "Remove draft field failed");
                designer.AddRecord(); designer.data.SelectedItem = designer.records[0]; designer.RemoveDraftRows(designer.data);
                Check(designer.records.Count == 0 && designer.previewScroll.Visibility == Visibility.Visible, "Remove draft row failed");
                designer.AddRecord(); designer.records[0] = ["10", "Một"]; designer.records.Add(["11", ":text::null"]);
                designer.modes.SelectedIndex = 1;
                report.Add("PASS external TSV header mapping, literal NULL text, canceled paste, pasted field renaming, remove draft fields/rows");
            }
            designer.UpdateLayout();
            await designer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            MainWindow.SaveDesignImage(designer, "catalog-transfer-" + theme.Replace(" ", "-"));
            designer.Width = 520; designer.Height = 500; designer.UpdateLayout();
            await designer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            MainWindow.SaveDesignImage(designer, "catalog-transfer-narrow-" + theme.Replace(" ", "-"));
            report.Add("PASS " + theme + ": multirow paste preview/Apply, literal NULL tokens, record validation, multi-selection, narrow/desktop renders; no SQL connection");
        }
        finally { designer.dirty = false; designer.Close(); }
    }
}
