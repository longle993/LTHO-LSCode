using System.Data;
using System.Windows;
using System.Windows.Controls;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    private SqlTarget? appSqlTarget;
    private Task<SqlTarget>? appTargetReady;
    private CancellationTokenSource? objectCancellation;

    private async Task<SqlTarget> DefaultAppTargetAsync()
    {
        if (configuredSqlTarget != null) return configuredSqlTarget;
        var project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        if (project == null || string.IsNullOrWhiteSpace(project.Field("SysConnectionString"))) return CurrentSqlTarget();
        if (appSqlTarget != null) return appSqlTarget;
        if (appTargetReady != null) return await appTargetReady;
        async Task<SqlTarget> Resolve()
        {
            var context = new EditorTab("connection.sql", "") { Project = project };
            try
            {
                var target = await ResolveAppTargetAsync(context, false) ?? throw new InvalidOperationException("Chưa chọn App database.");
                if (settings.ActiveProjectId == project.Id)
                {
                    appSqlTarget = target; settings.Server = target.Server; settings.Database = target.Database;
                    SettingsStore.Save(settings); UpdateStatus();
                }
                return target;
            }
            finally { context.Dispose(); }
        }
        var pending = Resolve(); appTargetReady = pending;
        try { return await pending; }
        finally { if (appTargetReady == pending) appTargetReady = null; }
    }

    private async void ProjectViews_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == ProjectViews && DbObjectTab.IsSelected && DbObjects.Items.Count == 0) await LoadDbObjectsAsync();
    }
    private async void RefreshDbObjects_Click(object sender, RoutedEventArgs e) { SqlSchemaCompletion.Clear(); contentSearchCache.Clear(); DbObjectSearch.Text = ""; dbSearchTimer.Stop(); await LoadDbObjectsAsync(); }
    private sealed class DbGroupState(string types)
    {
        public string Types { get; } = types;
        public IReadOnlyList<LSCode.Core.SqlSymbol>? LocalResults;
        public string Search = "";
        public bool SearchContent;
        public int AfterId;
        public bool Busy, Loaded;
        public SqlTarget? Target;
        public TreeViewItem? More;
    }
    private Task LoadDbObjectsAsync()
    {
        objectCancellation?.Cancel(); objectCancellation?.Dispose(); objectCancellation = new CancellationTokenSource();
        var generation = objectCancellation.Token;
        DbObjects.Items.Clear(); DbObjectStatus.Text = "";
        foreach (var (label, types) in new[] { ("Tables", "'U'"), ("Views", "'V'"), ("Stored procedures", "'P','PC'"), ("Functions", "'FN','IF','TF','FS','FT'") })
        {
            var state = new DbGroupState(types);
            var folder = new TreeViewItem { Header = label, Tag = state };
            folder.Items.Add("Mở để tải…");
            folder.Expanded += async (_, e) => { if (e.OriginalSource == folder && !state.Loaded) { e.Handled = true; await LoadDbPageAsync(folder, state, generation); } };
            DbObjects.Items.Add(folder);
        }
        return Task.CompletedTask;
    }
    private async Task LoadDbPageAsync(TreeViewItem folder, DbGroupState state, CancellationToken generation)
    {
        if (state.Busy || generation.IsCancellationRequested) return;
        state.Busy = true;
        using var busy = BeginBusy("Đang tải " + folder.Header + "…");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(generation);
        try
        {
            state.Target ??= await DefaultAppTargetAsync(); timeout.Token.ThrowIfCancellationRequested();
            var parameters = new List<Microsoft.Data.SqlClient.SqlParameter> { new("@take", 201), new("@after", state.AfterId) };
            var searchPredicate = DbSearchPredicate(state.Search, state.SearchContent, parameters);
            DataTable data;
            if (state.LocalResults != null)
            {
                data = new DataTable(); data.Columns.Add("id", typeof(int)); data.Columns.Add("schema"); data.Columns.Add("name"); data.Columns.Add("type");
                foreach (var pair in state.LocalResults.Skip(state.AfterId).Take(201).Select((item, i) => (item, i))) data.Rows.Add(state.AfterId + pair.i + 1, pair.item.Schema, pair.item.Table, pair.item.Kind);
            }
            else data = await ProjectDatabase.ReadAsync(state.Target,
                "SELECT TOP (@take) o.object_id,s.name AS schema_name,o.name AS object_name,o.type FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0 AND o.type IN (" + state.Types + ") AND o.object_id>@after" + searchPredicate + " ORDER BY o.object_id",
                timeout.Token, parameters.ToArray());
            timeout.Token.ThrowIfCancellationRequested();
            if (!state.Loaded) folder.Items.Clear();
            if (state.More != null) { folder.Items.Remove(state.More); state.More = null; }
            foreach (DataRow row in data.Rows.Cast<DataRow>().Take(200))
            {
                var item = new TreeViewItem { Header = new TextBlock { Text = row[1] + "." + row[2] }, ToolTip = state.Target.Database + " · " + row[3],
                    Tag = new DbObjectReference(state.Target, Convert.ToString(row[1])!, Convert.ToString(row[2])!, Convert.ToString(row[3])!.Trim()) };
                item.PreviewMouseLeftButtonUp += async (_, e) => { e.Handled = true; await OpenDbObjectAsync((DbObjectReference)item.Tag); };
                item.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await OpenDbObjectAsync((DbObjectReference)item.Tag); } };
                folder.Items.Add(item);
                state.AfterId = (int)row[0];
            }
            state.Loaded = true;
            if (data.Rows.Count > 200)
            {
                var more = new TreeViewItem { Header = "Tải thêm 200 đối tượng…" }; more.Items.Add("…"); state.More = more;
                more.Expanded += async (_, e) => { if (e.OriginalSource == more) { e.Handled = true; await LoadDbPageAsync(folder, state, generation); } };
                folder.Items.Add(more);
            }
            else if (folder.Items.Count == 0) folder.Items.Add("Không có đối tượng.");
            DbObjectStatus.Text = state.Target.Server + " / " + state.Target.Database + " • " + folder.Header + " • đã tải " + (folder.Items.Count - (state.More == null ? 0 : 1));
        }
        catch (OperationCanceledException) { if (!generation.IsCancellationRequested) DbObjectStatus.Text = "Tải quá hạn. Thu gọn/mở lại nhóm hoặc mục Tải thêm để thử lại."; }
        catch (Exception ex) { if (!generation.IsCancellationRequested) DbObjectStatus.Text = "Không tải được: " + ex.Message; }
        finally { state.Busy = false; if (state.More != null) state.More.IsExpanded = false; }
    }
    private void ProjectFileTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ProjectFileTabs || PinnedController == null) return;
        var tab = ProjectFileTabs.SelectedItem as TabItem;
        PinnedController.Text = tab?.Header is Panel panel ? panel.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "" : Convert.ToString(tab?.Header) ?? "";
        PinnedController.ToolTip = tab?.ToolTip; RefreshControllerHistory();
        ClosePinnedController.Visibility = tab?.Tag != null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ClosePinnedController_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectFileTabs.SelectedItem is TabItem { Tag: not null } tab) ProjectFileTabs.Items.Remove(tab);
    }
}









