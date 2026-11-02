using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Search;
using LSCode.Core;
using LSCode.Shell.Services;

namespace LSCode.Shell;

public partial class MainWindow
{
    private void RegisterExtraShortcuts()
    {
        commands.AddRange([
            new("xml.include", "XML: Xem nhanh include", "F12", PreviewIncludeAsync),
            new("xml.include.open", "XML: Mở file include", "Ctrl+F12", GoToIncludeAsync),
            new("file.saveAs", "File: Lưu thành", "Ctrl+Shift+S", async () => { if (Active is { } d) await SaveAsync(d, true); }),
            new("file.quickOpen", "File: Mở nhanh", "Ctrl+P", QuickOpenAsync),
            new("editor.palette", "Editor: Bảng lệnh", "Ctrl+Shift+P", ShowPaletteAsync),
            new("editor.find", "Editor: Tìm trong file", "Ctrl+F", () => { if (Active is { } d) ApplicationCommands.Find.Execute(null, d.ActiveEditor.TextArea); return Task.CompletedTask; }),
            new("editor.replace", "Editor: Thay thế", "Ctrl+H", ReplaceAsync),
            new("sql.complete", "SQL: Gợi ý", "Ctrl+Space", () => Active is { Language: ".sql" or ".txt" or ".f" } d ? d.CompleteAsync() : Task.CompletedTask)
        ]);
    }

    private string[] ShortcutValues(IdeCommand command) => settings.Shortcuts.TryGetValue(command.Id, out var values)
        ? values.Take(2).Concat(Enumerable.Repeat("", 2)).Take(2).ToArray() : [command.Shortcut, command.Id == "sql.new" ? "Ctrl+N" : ""];

    private static bool TryGesture(string value, out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { gesture = (KeyGesture?)new KeyGestureConverter().ConvertFromInvariantString(value.Replace("Ctrl+\\", "Ctrl+Oem5")); return gesture != null; }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException) { return false; }
    }

    private void ShowShortcuts()
    {
        var panel = new DockPanel { Margin = new Thickness(14) };
        var help = new TextBlock { Text = "Chọn ô rồi nhấn tổ hợp phím. Backspace/Delete để xóa. Mỗi chức năng tối đa 2 phím tắt.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 4, 12) };
        DockPanel.SetDock(help, Dock.Top); panel.Children.Add(help);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reset = new Button { Content = "Khôi phục mặc định" };
        var save = new Button { Content = "Lưu", Style = (Style)FindResource("PrimaryButton") };
        var cancel = new Button { Content = "Hủy", IsCancel = true };
        buttons.Children.Add(reset); buttons.Children.Add(save); buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(180) });
        var inputs = new Dictionary<string, TextBox[]>();
        void Cell(UIElement element, int row, int column) { Grid.SetRow(element, row); Grid.SetColumn(element, column); grid.Children.Add(element); }
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        foreach (var (title, column) in new[] { ("Chức năng", 0), ("Phím tắt 1", 1), ("Phím tắt 2", 2) })
            Cell(new TextBlock { Text = title, FontWeight = FontWeights.Bold, Margin = new Thickness(6) }, 0, column);
        foreach (var command in commands)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            Cell(new TextBlock { Text = command.Name, Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center }, row, 0);
            var values = ShortcutValues(command);
            inputs[command.Id] = Enumerable.Range(0, 2).Select(i =>
            {
                var input = new TextBox { Text = values[i], IsReadOnly = true };
                input.PreviewKeyDown += (_, e) =>
                {
                    var key = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (key == Key.Tab) return;
                    e.Handled = true;
                    if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
                    if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Delete or Key.Back) { input.Text = ""; return; }
                    try { input.Text = new KeyGestureConverter().ConvertToInvariantString(new KeyGesture(key, Keyboard.Modifiers)) ?? ""; }
                    catch (NotSupportedException) { help.Text = "Dùng Ctrl/Alt + phím hoặc F1–F12."; }
                };
                Cell(input, row, i + 1); return input;
            }).ToArray();
        }
        panel.Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var window = Dialogs.Create(this, "Phím tắt", panel, 900, 650);
        reset.Click += (_, _) => { foreach (var c in commands) { inputs[c.Id][0].Text = c.Shortcut; inputs[c.Id][1].Text = c.Id == "sql.new" ? "Ctrl+N" : ""; } };
        save.Click += (_, _) =>
        {
            var seen = new Dictionary<(Key, ModifierKeys), string>();
            foreach (var c in commands)
                foreach (var input in inputs[c.Id].Where(t => t.Text.Length > 0))
                {
                    if (!TryGesture(input.Text, out var gesture)) { help.Text = "Phím không hợp lệ: " + input.Text; input.Focus(); return; }
                    var key = (gesture!.Key, gesture.Modifiers);
                    if (seen.TryGetValue(key, out var other)) { help.Text = $"{input.Text} bị trùng: {other} và {c.Name}. Xóa hoặc đổi một ô trước khi lưu."; input.Focus(); return; }
                    seen[key] = c.Name;
                }
            var previous = settings.Shortcuts;
            settings.Shortcuts = inputs.ToDictionary(p => p.Key, p => p.Value.Select(t => t.Text).ToArray());
            try { SettingsStore.Save(settings); window.DialogResult = true; }
            catch (Exception ex) { settings.Shortcuts = previous; help.Text = "Không lưu được: " + ex.Message; }
        };
        window.ShowDialog();
    }
}


