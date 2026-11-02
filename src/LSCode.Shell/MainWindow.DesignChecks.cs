using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LSCode.Shell.Services;

namespace LSCode.Shell;
public partial class MainWindow
{
    // UI regression coverage for the shared navigation host; all data stays in the smoke directory.
    private void CheckRedesignedWorkspace(List<string> report)
    {
        var theme = settings.Theme;
        var previousState = WindowState;
        var oldWidth = Width; var oldHeight = Height;
        WindowState = WindowState.Normal; Width = 1280; Height = 800;
        var doc = AddDocument("design-preview.sql", "-- Dữ liệu minh họa, không chạy truy vấn.\nSELECT N'Xin chào từ LS Code' AS message,\n       DB_NAME() AS database_name;\nGO\nSELECT 1 AS id, N'Khách hàng mẫu' AS customer_name;",
            sqlTarget: new SqlTarget("localhost", "ERP_Demo_App", ""));
        WorkspaceName.Text = "ERP-Demo";
        var fixture = new DataTable(); fixture.Columns.Add("id", typeof(int)); fixture.Columns.Add("customer_name"); fixture.Rows.Add(1, "Khách hàng mẫu");
        var results = new DataGrid { ItemsSource = fixture.DefaultView, CanUserAddRows = false }; ResultGrids.Prepare(results);
        var resultTab = new TabItem { Header = "Kết quả 1 · 1 dòng", Content = results };
        Results.Items.Add(resultTab); Results.SelectedItem = resultTab; BottomPanel.SelectedIndex = 2;
        var entry = new TreeViewItem { Header = "query.sql", IsSelected = true };
        Explorer.Items.Add(entry);
        try
        {
            ShowSide(ExplorerPanel); ProjectFileTabs.SelectedIndex = 0; UpdateLayout();
            if (ProjectPanel.IsVisible || !SidePanelContent.IsVisible || NavigationColumn.ActualWidth != 264) throw new Exception("Option A must show one 264px navigation column");
            if (Documents.ActualWidth < 900) throw new Exception("Option A editor lost expected horizontal space at 1280px");
            FunctionsNavigation_Click(this, new RoutedEventArgs()); UpdateLayout();
            if (!CommandTree.IsVisible || SidePanelContent.IsVisible || !Equals(FunctionsNavigation.Tag, "Selected")) throw new Exception("Function navigation did not select the shared host");
            DatabaseNavigation_Click(this, new RoutedEventArgs()); UpdateLayout();
            if (!DbObjects.IsVisible || CommandTree.IsVisible || !Equals(DatabaseNavigation.Tag, "Selected")) throw new Exception("Database navigation did not switch views");
            ShowSide(SearchPanel); UpdateLayout();
            if (!SearchPanel.IsVisible || ProjectPanel.IsVisible) throw new Exception("Search failed to replace navigation view");
            ShowSide(SnippetPanel); UpdateLayout();
            if (!SnippetPanel.IsVisible || SearchPanel.IsVisible) throw new Exception("Snippets failed to replace search");
            ShowSide(ExplorerPanel); ToggleNavigation_Click(this, new RoutedEventArgs()); UpdateLayout();
            if (NavigationPane.IsVisible || NavigationColumn.ActualWidth != 26) throw new Exception("Navigation collapse did not return editor space");
            ShowSide(ExplorerPanel); UpdateLayout();
            if (!ExplorerPanel.IsVisible || NavigationColumn.ActualWidth != 264) throw new Exception("File command did not restore navigation");
            foreach (var current in new[] { "Dark", "Light", "High Contrast" })
            {
                settings.Theme = current; ApplyTheme(); UpdateLayout();
                if (Equals(FindResource("Hover"), FindResource("Raised"))) throw new Exception("Hover and selection must differ");
                if (!Equals(FilesNavigation.Foreground, FindResource("Accent"))) throw new Exception("Selected navigation must be visibly identified");
                SaveDesignImage(this, "ui-a-" + current.Replace(" ", "-").ToLowerInvariant());
            }
            settings.Theme = "Dark"; ApplyTheme(); Width = 980; Height = 640; UpdateLayout();
            if (Documents.ActualWidth < 620 || BottomPanel.ActualHeight < 100 || !doc.Primary.IsVisible) throw new Exception("Narrow desktop lost editor or result region");
            SaveDesignImage(this, "ui-a-980");
            var savedProjects = projects;
            try { projects = [LSCode.Core.ProjectImporter.Create("ERP-Demo", "localhost", "ERP_Demo_Sys", "samples/ERP-Demo")]; ShowProjectPicker(smoke: true); }
            finally { projects = savedProjects; }
            CaptureDesignDialog(ConfigureExternalTools, "External tools settings", "ui-a-settings");
            CaptureDesignDialog(ShowShortcuts, "Phím tắt", "ui-a-shortcuts");
            report.Add("PASS option A File/Function/DB/Search/Snippets navigation, collapse/restore, selected state, 1280/980px layout and three-theme renders");
        }
        finally
        {
            Results.Items.Remove(resultTab); Explorer.Items.Remove(entry);
            tabs.Remove(doc); Documents.Items.Remove(doc.Tab); doc.Dispose();
            settings.Theme = theme; ApplyTheme(); Width = oldWidth; Height = oldHeight; WindowState = previousState;
        }
    }
    private void CaptureDesignDialog(Action show, string title, string image)
    {
        Exception? failure = null;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => {
            var window = OwnedWindows.Cast<Window>().FirstOrDefault(w => w.Title == title);
            if (window == null) return;
            timer.Stop();
            try { SaveDesignImage(window, image); } catch (Exception ex) { failure = ex; }
            finally { window.Close(); }
        };
        timer.Start(); try { show(); } finally { timer.Stop(); }
        if (failure != null) throw failure;
    }
    internal static void SaveDesignImage(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(SettingsStore.Root, name + ".png")); encoder.Save(stream);
    }
}
