using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    public async Task RunCompactUiSmokeAsync()
    {
        var report = new List<string>();
        int result = 0;
        try
        {
            Directory.CreateDirectory(SettingsStore.Root);
            EmbeddedHighlightingChecks.Run();
            report.Add("PASS XML/SQL/JavaScript token boundaries in three themes");
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            WindowState = WindowState.Normal; Width = 1280; Height = 800;
            var document = AddDocument("readability.xml", EmbeddedHighlightingChecks.Fixture);
            ShowSide(ExplorerPanel);
            ProjectFileTabs.SelectedIndex = 0;
            Explorer.Items.Clear();
            var folder = new TreeViewItem { Header = CreateTreeHeader("Controllers", true), IsExpanded = true };
            var nested = new TreeViewItem { Header = CreateTreeHeader("DanhMuc", true), IsExpanded = true };
            foreach (var file in new[] { "dmbp.f", "dmbp.xml", "bao-cao.rpt", "bang-gia.xlsx" })
                nested.Items.Add(new TreeViewItem { Header = CreateTreeHeader(file, false) });
            folder.Items.Add(nested); Explorer.Items.Add(folder);
            var menu = new TreeViewItem { Header = "Danh mục", IsExpanded = true };
            menu.Items.Add(new TreeViewItem { Header = "Bộ phận" }); CommandTree.Items.Add(menu);
            document.ToggleSplit();
            UpdateLayout();
            if (document.Primary.FontSize != 13 || document.Secondary?.FontSize != 13 ||
                document.Primary.TextArea.TextView.DefaultLineHeight < 20 ||
                document.Secondary.TextArea.TextView.DefaultLineHeight < 20)
                throw new InvalidOperationException("Editor font/line spacing did not reach both split views");
            report.Add($"PASS both editors: 13 DIP, line height {document.Primary.TextArea.TextView.DefaultLineHeight:F2} DIP");
            document.ToggleSplit();
            foreach (var theme in new[] { "Dark", "Light", "High Contrast" })
            {
                settings.Theme = theme; ApplyTheme(); UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                SaveDesignImage(this, "compact-" + theme.Replace(" ", "-").ToLowerInvariant());
                if (FilesNavigation.ActualHeight > 30 || Explorer.ActualHeight < 460)
                    throw new InvalidOperationException($"Compact navigation: button={FilesNavigation.ActualHeight}, files={Explorer.ActualHeight}, window={ActualHeight}");
                FunctionsNavigation_Click(this, new RoutedEventArgs()); UpdateLayout();
                SaveDesignImage(this, "compact-menu-" + theme.Replace(" ", "-").ToLowerInvariant());
                ShowSide(ExplorerPanel);
                await CatalogDesigner.CheckSingleClickEditingAsync(this, report);
            }
            Width = 980; Height = 640; UpdateLayout();
            SaveDesignImage(this, "compact-980");
            report.Add("PASS compact file/menu trees and three-theme/narrow-desktop renders");
        }
        catch (Exception ex) { report.Add("FAIL " + ex); result = 1; }
        Directory.CreateDirectory(SettingsStore.Root);
        await File.WriteAllLinesAsync(Path.Combine(SettingsStore.Root, "compact-ui-result.txt"), report);
        Application.Current.Shutdown(result);
    }
}
