using System.Windows;
using System.Windows.Controls;
using LSCode.Core;
using LSCode.Shell.Services;

namespace LSCode.Shell;
public partial class MainWindow
{
    private static readonly string[] DefaultActivity = ["workspace.files", "workspace.objects", "workspace.search", "sql.new", "tools.profiler", "snippet.open", "project.select", "theme.switch"];
    private void InitializeActivityBar()
    {
        commands.AddRange([
            new("project.profile", "Project: Profile mới", "", CreateProjectAsync),
            new("tools.profiler", "SQL: Profiler", "", OpenProfilerAsync),
            new("workspace.files", "Workspace: File dự án", "", () => { ShowSide(ExplorerPanel); return Task.CompletedTask; }),
            new("workspace.objects", "Workspace: DB Object", "", () => { ExpandProjectPanel(); DbObjectTab.IsSelected = true; return Task.CompletedTask; })
        ]);
        RenderActivityBar();
    }
    private void RenderActivityBar()
    {
        ActivityButtons.Children.Clear();
        foreach (var id in settings.ActivityCommands ?? DefaultActivity.ToList())
        {
            var command = commands.FirstOrDefault(c => c.Id == id); if (command == null) continue;
            var glyph = id switch { "workspace.search" => "\uE721", "sql.new" => "\uE710", "snippet.open" => "\uE943", "project.profile" => "\uE8F4", "tools.profiler" => "\uE9D9", "project.select" => "\uE8B7", "theme.switch" => "\uE793", "workspace.files" => "\uE8A5", "workspace.objects" => "\uE968", "file.save" => "\uE74E", "file.open" => "\uE8E5", _ => "\uE713" };
            var button = new Button { Content = glyph, Style = (Style)FindResource("IconButton"), FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), ToolTip = command.Name, Margin = new Thickness(0, 0, 0, 8), FontSize = 18 };
            ToolTipService.SetInitialShowDelay(button, 100);
            ToolTipService.SetBetweenShowDelay(button, 0);
            ToolTipService.SetShowDuration(button, 60000);
            ToolTipService.SetPlacement(button, System.Windows.Controls.Primitives.PlacementMode.Right);
            System.Windows.Automation.AutomationProperties.SetName(button, command.Name);
            button.Click += async (_, _) => await Guard(command.Execute); ActivityButtons.Children.Add(button);
        }
    }
    private void CustomizeActivity_Click(object sender, RoutedEventArgs e)
    {
        var panel = new DockPanel { Margin = new Thickness(16) };
        var save = new Button { Content = "Lưu", HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(save, Dock.Bottom); panel.Children.Add(save);
        var list = new StackPanel(); panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var ordered = (settings.ActivityCommands ?? DefaultActivity.ToList()).Distinct().ToList();
        var selected = ordered.ToHashSet();
        foreach (var id in ordered.Concat(commands.Select(c => c.Id).Where(id => !selected.Contains(id))))
        {
            var c = commands.FirstOrDefault(c => c.Id == id); if (c == null) continue;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var arrows = new StackPanel { Orientation = Orientation.Horizontal };
            var up = new Button { Content = "↑", Width = 28, Padding = new Thickness(0), ToolTip = "Đưa lên" };
            var down = new Button { Content = "↓", Width = 28, Padding = new Thickness(0), ToolTip = "Đưa xuống" };
            up.Click += (_, _) => MoveActivityRow(list, row, -1);
            down.Click += (_, _) => MoveActivityRow(list, row, 1);
            arrows.Children.Add(up); arrows.Children.Add(down); DockPanel.SetDock(arrows, Dock.Right); row.Children.Add(arrows);
            row.Children.Add(new CheckBox { Content = c.Name, Tag = c.Id, IsChecked = selected.Contains(c.Id) }); list.Children.Add(row);
        }
        var dialog = Dialogs.Create(this, "Lối tắt — chọn chức năng và dùng ↑ ↓ để sắp xếp", panel, 560, 580);
        save.Click += async (_, _) => await Guard(() =>
        {
            var previous = settings.ActivityCommands;
            settings.ActivityCommands = list.Children.OfType<DockPanel>().SelectMany(r => r.Children.OfType<CheckBox>()).Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            try { SettingsStore.Save(settings); } catch { settings.ActivityCommands = previous; throw; }
            RenderActivityBar(); dialog.Close(); return Task.CompletedTask;
        });
        dialog.ShowDialog();
    }
    internal static void MoveActivityRow(Panel list, UIElement row, int delta)
    {
        var index = list.Children.IndexOf(row); var next = index + delta;
        if (index < 0 || next < 0 || next >= list.Children.Count) return;
        list.Children.RemoveAt(index); list.Children.Insert(next, row);
        if (row is FrameworkElement element) element.BringIntoView();
    }
}

