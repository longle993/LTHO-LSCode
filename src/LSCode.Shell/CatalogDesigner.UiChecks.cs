using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace LSCode.Shell;

internal sealed partial class CatalogDesigner
{
    internal static async Task CheckSingleClickEditingAsync(Window owner, List<string> report)
    {
        var field = new Field { Name = "ma_bo_phan" };
        var grid = Grid(); grid.IsReadOnly = false;
        grid.Columns.Add(TextColumn("Tên trường", nameof(Field.Name)));
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Bắt buộc", Binding = new Binding(nameof(Field.Required)) });
        grid.Columns.Add(new DataGridComboBoxColumn { Header = "Kiểu", ItemsSource = Types,
            SelectedItemBinding = new Binding(nameof(Field.Type)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Hệ thống", Binding = new Binding(nameof(Field.ForeignKey)), IsReadOnly = true });
        grid.ItemsSource = new[] { field };
        var window = new Window { Title = "Kiểm tra nhập danh mục", Width = 850, Height = 260, Content = grid, Owner = owner };
        try
        {
            window.Show(); window.UpdateLayout();
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
            var presenter = Descendants<DataGridCellsPresenter>(row).Single();
            DataGridCell Cell(int index) => (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(index);
            void Click(int index)
            {
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = Cell(index) };
                Cell(index).RaiseEvent(args);
            }
            Click(0);
            if (Cell(0).Content is not TextBox text || !Cell(0).IsEditing) throw new InvalidOperationException($"Single click: cellReadOnly={Cell(0).IsReadOnly}, editing={Cell(0).IsEditing}, selected={Cell(0).IsSelected}, content={Cell(0).Content?.GetType().Name}, current={grid.CurrentCell.Column?.DisplayIndex}, enabled={grid.IsEnabled}");
            text.Text = "ma_bo_phan_moi";
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (field.Name != "ma_bo_phan_moi") throw new InvalidOperationException("Edited text was not committed");
            Click(1);
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!field.Required) throw new InvalidOperationException("First checkbox click did not toggle");
            Click(1);
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (field.Required) throw new InvalidOperationException("Second checkbox click did not toggle back");
            Click(2);
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (Cell(2).Content is not ComboBox { IsDropDownOpen: true } combo) throw new InvalidOperationException($"Type chooser: editing={Cell(2).IsEditing}, content={Cell(2).Content?.GetType().Name}, open={(Cell(2).Content as ComboBox)?.IsDropDownOpen}");
            combo.SelectedItem = "int"; combo.IsDropDownOpen = false;
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (field.Type != "int") throw new InvalidOperationException("Chosen type was not committed");
            Click(3);
            if (Cell(3).IsEditing) throw new InvalidOperationException("Read-only cell became editable");
            grid.IsReadOnly = true; Click(0);
            if (Cell(0).IsEditing) throw new InvalidOperationException("Read-only grid became editable");
            MainWindow.SaveDesignImage(window, "compact-catalog-" + Application.Current.Resources["ThemeName"]);
            report.Add("PASS single-click text edit, checkbox on/off, type picker, read-only guard; no SQL connection");
        }
        finally { window.Close(); }
    }

    private static IEnumerable<T> Descendants<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
