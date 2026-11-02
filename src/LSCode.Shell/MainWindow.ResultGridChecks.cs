using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LSCode.Shell;

public partial class MainWindow
{
    private async Task CheckResultGridScrollingAsync(List<string> report)
    {
        var table = new DataTable();
        for (var column = 0; column < 100; column++) table.Columns.Add("column_" + column);
        for (var row = 0; row < 5000; row++)
            table.Rows.Add(Enumerable.Range(0, 100).Select(column => (object)$"{row}:{column}").ToArray());
        table.Rows[4999][1] = new string('x', 12000);
        table.Rows[4999][2] = DBNull.Value;

        static int CountCells(DependencyObject parent)
        {
            var count = parent is DataGridCell ? 1 : 0;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) count += CountCells(VisualTreeHelper.GetChild(parent, i));
            return count;
        }
        async Task<(int Cells, double Milliseconds)> Measure(bool optimized)
        {
            var grid = new DataGrid { ItemsSource = table.DefaultView, IsReadOnly = true, CanUserAddRows = false };
            ResultGrids.Prepare(grid);
            var window = Dialogs.Create(this, "SQL scrolling check", grid, 1000, 500);
            try
            {
                window.Show(); window.UpdateLayout();
                if (!optimized)
                {
                    grid.EnableColumnVirtualization = false;
                    grid.RowHeight = double.NaN;
                    foreach (var column in grid.Columns) column.Width = DataGridLength.Auto;
                    grid.UpdateLayout();
                }
                var widths = grid.Columns.Select(c => c.ActualWidth).ToArray();
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 8; i++)
                {
                    grid.ScrollIntoView(table.DefaultView[i % 2 == 0 ? 4999 : 0], grid.Columns[i % 2 == 0 ? 99 : 0]);
                    grid.UpdateLayout();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                }
                watch.Stop();
                var cells = CountCells(grid);
                if (optimized)
                {
                    if (cells >= 1000) throw new Exception($"Result column virtualization failed: {cells} live cells");
                    if (!grid.Columns.Select(c => c.ActualWidth).SequenceEqual(widths)) throw new Exception("Result widths changed while scrolling");
                    grid.Items.SortDescriptions.Add(new("column_0", System.ComponentModel.ListSortDirection.Descending));
                    grid.UpdateLayout();
                    if (grid.Items.Count != 5000 || Convert.ToString(table.Rows[4999][1])?.Length != 12000) throw new Exception("Result data changed");
                }
                return (cells, watch.Elapsed.TotalMilliseconds);
            }
            finally { window.Close(); }
        }
        var before = await Measure(false);
        var after = await Measure(true);
        report.Add($"PASS SQL 5000 x 100 scroll/layout fixture: live cells {before.Cells} → {after.Cells}; 8 vertical/horizontal jumps {before.Milliseconds:N0} → {after.Milliseconds:N0} ms; stable widths, sorting and full values retained");
    }
}
