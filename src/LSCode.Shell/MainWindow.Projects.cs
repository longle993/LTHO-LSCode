using System.IO;
using System.Windows;
using System.Windows.Controls;
using LSCode.Core;
using LSCode.Shell.Services;
using Microsoft.Win32;

namespace LSCode.Shell;
public partial class MainWindow
{
    private bool projectCatalogAvailable = true;
    private void RefreshProjectWebLink()
    {
        var project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        // Catalog names include the application folder (e.g. CNNB_HRM/SP24).
        // The web application is hosted under the outer project folder only.
        var name = project?.Name.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        ProjectWebAddress.Visibility = string.IsNullOrEmpty(name) ? Visibility.Collapsed : Visibility.Visible;
        ProjectWebLink.NavigateUri = string.IsNullOrEmpty(name) ? null : new Uri("http://172.168.5.14/" + Uri.EscapeDataString(name));
        ProjectWebLabel.Text = string.IsNullOrEmpty(name) ? "" : "http://172.168.5.14/" + name;
        ProjectWebAddress.ToolTip = ProjectWebLink.NavigateUri?.AbsoluteUri;
    }
    private async void ProjectWebLink_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await Guard(() =>
        {
            if (ProjectWebLink.NavigateUri is { } uri)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return Task.CompletedTask;
        });
    }
    private async Task ImportProjectAsync()
    {
        if (!projectCatalogAvailable) throw new IOException("Project catalog không đọc được. Khôi phục projects.dat hoặc bản .bak trước khi import để tránh mất catalog.");
        var dialog = new OpenFileDialog { Title = "Import project configuration", Filter = "Legacy XML|*.xml" };
        if (dialog.ShowDialog(this) != true) return;
        var text = await File.ReadAllTextAsync(dialog.FileName);
        var incoming = ProjectImporter.Read(text);
        if (incoming.Count == 0) throw new IOException("Không tìm thấy phần tử project trong XML.");
        var panel = new DockPanel { Margin = new Thickness(16) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        footer.Children.Add(new TextBlock { Text = "Lưu đủ metadata trong catalog mã hóa theo tài khoản Windows. Không tự dùng password cũ hoặc kết nối database.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 12, 4, 8) });
        var overwrite = new CheckBox { Content = "Cập nhật project trùng server / user / tên (mặc định giữ bản hiện có)" }; footer.Children.Add(overwrite);
        var button = new Button { Content = $"Import {incoming.Count} project(s)" }; footer.Children.Add(button);
        panel.Children.Add(new ListBox { ItemsSource = incoming.Select(p => p.ToString()).ToList() });
        var window = Dialogs.Create(this, "Preview project import", panel, 760, 500);
        button.Click += (_, _) => window.DialogResult = true;
        if (window.ShowDialog() != true) return;
        var merge = ProjectImporter.Merge(projects, incoming, overwrite.IsChecked == true);
        ProjectStore.Save(merge.Projects); projects = merge.Projects;
        Log($"Project import: +{merge.Added}, updated {merge.Updated}, kept {merge.Skipped}. Legacy XML unchanged.");
        await SelectProjectAsync();
    }
    private Task SelectProjectAsync()
    {
        ShowProjectPicker(); return Task.CompletedTask;
    }
    private void ActivateProject(LegacyProject project, bool reloadCommands = true)
    {
        commandCancellation?.Cancel();
        controllerNavigation?.Cancel(); controllerFileIndexes.Clear(); menuItems.Clear();
        while (ProjectFileTabs.Items.Count > 1) ProjectFileTabs.Items.RemoveAt(1);
        dbSearchTimer.Stop(); DbObjectSearch.Text = ""; dbSearchTimer.Stop();
        configuredSqlTarget = null; appSqlTarget = null; appTargetReady = null; objectCancellation?.Cancel(); DbObjects.Items.Clear(); DbObjectStatus.Text = "Chọn tab để tải App DB.";
        projectSqlTarget = string.IsNullOrWhiteSpace(project.Field("SysConnectionString")) ? null : ProjectDatabase.Target(project.Field("SysConnectionString"));
        settings.ActiveProjectId = project.Id;
        settings.ProjectLastAccess[project.Id] = DateTimeOffset.Now;
        settings.Server = string.IsNullOrWhiteSpace(project.Server) ? "." : project.Server;
        settings.Database = projectSqlTarget == null ? (string.IsNullOrWhiteSpace(project.Database) ? "master" : project.Database) : AppDatabaseName(project) == "Chưa chọn" ? "master" : AppDatabaseName(project);
        settings.SqlUser = project.User; settings.IntegratedSecurity = string.IsNullOrWhiteSpace(project.User); sqlPassword = "";
        if (!string.IsNullOrWhiteSpace(project.SourcePath) && Directory.Exists(project.SourcePath)) LoadWorkspace(project.SourcePath);
        else { settings.Workspace = null; Explorer.Items.Clear(); Log("Project chưa có SourcePath hợp lệ. Mở thư mục thủ công để duyệt file."); }
        WorkspaceName.Text = project.Name; SettingsStore.Save(settings); UpdateStatus();
        Log($"Project: {project.Name}. SQL tabs đã mở giữ nguyên target riêng; tab mới và DB Object dùng App DB. Menu dùng SYS.");
        if (reloadCommands) _ = RefreshCommandsAsync();
        if (projectSqlTarget != null) _ = Guard(async () => { await DefaultAppTargetAsync(); });
        if (DbObjectTab.IsSelected) _ = LoadDbObjectsAsync();
    }
    private Task CreateProjectAsync()
    {
        if (!projectCatalogAvailable) throw new IOException("Project catalog hiện không đọc được; chưa ghi đè dữ liệu.");
        var name = Dialogs.Text(this, "Tên project"); if (string.IsNullOrWhiteSpace(name)) return Task.CompletedTask;
        var server = Dialogs.Text(this, "SQL Server", settings.Server); if (server == null) return Task.CompletedTask;
        var database = Dialogs.Text(this, "Database", settings.Database); if (database == null) return Task.CompletedTask;
        var folder = new OpenFolderDialog { Title = "SourcePath của project" }; if (folder.ShowDialog(this) != true) return Task.CompletedTask;
        var project = ProjectImporter.Create(name.Trim(), server.Trim(), database.Trim(), folder.FolderName);
        var merged = ProjectImporter.Merge(projects, [project], false);
        ProjectStore.Save(merged.Projects); projects = merged.Projects; ActivateProject(projects.First(p => p.Id == project.Id));
        return Task.CompletedTask;
    }
    private Task ShowProjectDetailsAsync()
    {
        var project = projects.FirstOrDefault(p => p.Id == settings.ActiveProjectId);
        if (project == null) { Log("Chọn project trước khi xem metadata."); return Task.CompletedTask; }
        var fields = XmlTools.Parse(project.RawXml).Root!.Elements().Select(e => new { Field = e.Name.ToString(), Value = ProjectImporter.IsSensitive(e.Name.LocalName) ? "•••• (protected)" : e.HasElements ? "[XML metadata]" : e.Value }).ToList();
        Dialogs.Create(this, "Project metadata — " + project.Name, new DataGrid { ItemsSource = fields, IsReadOnly = true, CanUserAddRows = false, Margin = new Thickness(14) }, 850, 550).ShowDialog();
        return Task.CompletedTask;
    }
    private async Task ExportProjectsAsync()
    {
        if (projects.Count == 0) return;
        var dialog = new SaveFileDialog { FileName = "projects.xml", Filter = "XML|*.xml", Title = "Export catalog (password/token fields được để trống)" };
        if (dialog.ShowDialog(this) != true) return;
        await File.WriteAllTextAsync(dialog.FileName, ProjectImporter.Export(projects));
        Log("Exported project catalog; password/token/secret fields blank. Review custom fields before sharing.");
    }
    private async void SelectProject_Click(object sender, RoutedEventArgs e) => await Guard(SelectProjectAsync);
    private async void CreateProject_Click(object sender, RoutedEventArgs e) => await Guard(CreateProjectAsync);
    private async void ProjectDetails_Click(object sender, RoutedEventArgs e) => await Guard(ShowProjectDetailsAsync);
    private async void ExportProjects_Click(object sender, RoutedEventArgs e) => await Guard(ExportProjectsAsync);
}




