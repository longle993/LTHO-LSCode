using System.Data;
using LSCode.Core;
using LSCode.Shell.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace LSCode.Shell;
public partial class MainWindow
{
    private readonly DispatcherTimer dbSearchTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private void InitializeDbSearch()
    {
        dbSearchTimer.Tick += async (_, _) => { dbSearchTimer.Stop(); await SearchDbObjectsAsync(); };
        Closed += (_, _) => { dbSearchTimer.Stop(); objectCancellation?.Cancel(); };
    }
    private void DbObjectSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || closing || closeRequested) return;
        dbSearchTimer.Stop(); objectCancellation?.Cancel();
        if (DbSearchMode.SelectedIndex == 0) dbSearchTimer.Start();
        else DbObjectStatus.Text = "Nhấn Enter để tìm nội dung trong procedure/function.";
    }
    private void DbSearchMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || closing || closeRequested || DbObjectSearch == null) return;
        dbSearchTimer.Stop(); objectCancellation?.Cancel();
        if (DbSearchMode.SelectedIndex == 0) dbSearchTimer.Start();
        else DbObjectStatus.Text = "Nội dung SQL: chỉ procedure/function, nhấn Enter để tìm.";
    }
    private async void DbObjectSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return; e.Handled = true; dbSearchTimer.Stop(); await SearchDbObjectsAsync();
    }
    internal static string DbSearchPredicate(string term, bool content, List<SqlParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(term)) return "";
        if (!content) { parameters.Add(new SqlParameter("@search", term)); return " AND CHARINDEX(@search,s.name+'.'+o.name)>0"; }
        var terms = term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var clauses = new List<string>();
        for (var i = 0; i < terms.Length; i++) { var name = "@word" + i; parameters.Add(new SqlParameter(name, terms[i])); clauses.Add("CHARINDEX(" + name + ",m.definition)>0"); }
        return " AND o.type IN ('P','PC','FN','IF','TF','FS','FT') AND EXISTS (SELECT 1 FROM sys.sql_modules m WHERE m.object_id=o.object_id AND " + string.Join(" AND ", clauses) + ")";
    }
    private static Task<DataTable> ReadContentMatchesAsync(SqlTarget target, string predicate, List<SqlParameter> parameters, CancellationToken token) => ProjectDatabase.ReadAllAsync(target,
        "SELECT s.name,o.name,o.type_desc FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0" + predicate + " ORDER BY s.name,o.name", token, parameters.ToArray());
    private readonly Dictionary<string, (DateTime Time, IReadOnlyList<SqlSymbol> Results)> contentSearchCache = [];
    internal static IReadOnlyList<SqlSymbol> FilterObjectNames(IEnumerable<SqlSymbol> objects, string term) => objects.Where(o => (o.Schema + "." + o.Table).Contains(term, StringComparison.OrdinalIgnoreCase)).OrderByDescending(o => o.Table.Equals(term, StringComparison.OrdinalIgnoreCase)).ThenBy(o => o.Table, StringComparer.OrdinalIgnoreCase).ToList();
    private async Task SearchDbObjectsAsync()
    {
        if (!IsLoaded || closing || closeRequested) return;
        var term = DbObjectSearch.Text.Trim();
        if (term.Length == 0) { await LoadDbObjectsAsync(); return; }
        objectCancellation?.Cancel(); objectCancellation?.Dispose(); objectCancellation = new CancellationTokenSource();
        var state = new DbGroupState("'U','V','P','PC','FN','IF','TF','FS','FT'") { Search = term, SearchContent = DbSearchMode.SelectedIndex == 1 };
        var folder = new TreeViewItem { Header = state.SearchContent ? "Nội dung: " + term : "Tên: " + term, IsExpanded = true, Tag = state };
        DbObjects.Items.Clear(); DbObjects.Items.Add(folder);
        DbObjectStatus.Text = "Đang tìm…";
        var generation = objectCancellation.Token;
        using var busy = BeginBusy("Đang tìm DB Object…");
        try
        {
            var target = await DefaultAppTargetAsync(); generation.ThrowIfCancellationRequested(); state.Target = target;
            if (!state.SearchContent)
            {
                var objects = await SqlSchemaCompletion.LoadAsync(target).WaitAsync(generation);
                state.LocalResults = await Task.Run(() => FilterObjectNames(objects, term), generation);
            }
            else
            {
                var key = target.ConnectionString + "\n" + term;
                if (contentSearchCache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Time < TimeSpan.FromMinutes(2)) state.LocalResults = cached.Results;
                else
                {
                    var parameters = new List<SqlParameter>(); var predicate = DbSearchPredicate(term, true, parameters);
                    // Fetch matching names once; subsequent result pages do not rescan module definitions.
                    var data = await ReadContentMatchesAsync(target, predicate, parameters, generation);
                    state.LocalResults = data.Rows.Cast<DataRow>().Select(r => new SqlSymbol((string)r[0], (string)r[1], null, (string)r[2])).ToList();
                    if (contentSearchCache.Count >= 8) contentSearchCache.Clear();
                    contentSearchCache[key] = (DateTime.UtcNow, state.LocalResults);
                }
            }
            generation.ThrowIfCancellationRequested();
            await LoadDbPageAsync(folder, state, generation);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!generation.IsCancellationRequested) DbObjectStatus.Text = "Không tìm được: " + ex.Message; }
    }
}


