using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LSCode.Shell;

internal static class ResultGrids
{
    public static void Prepare(DataGrid grid, bool compact = false)
    {
        grid.EnableColumnVirtualization = true;
        grid.EnableRowVirtualization = true;
        // Auto widths remeasure newly visible cells and can relayout every column while scrolling.
        // Use a bounded sample once per generated column, then leave resizing to the user.
        grid.ColumnWidth = new DataGridLength(150);
        grid.RowHeight = 32; grid.CanUserResizeRows = false;
        grid.SetValue(ScrollViewer.CanContentScrollProperty, true);
        grid.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        grid.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        grid.SetValue(VirtualizingPanel.ScrollUnitProperty, ScrollUnit.Pixel);
        grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        grid.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.CanUserResizeColumns = true;
        grid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "GridLine");
        grid.SetResourceReference(DataGrid.VerticalGridLinesBrushProperty, "GridLine");
        grid.SetResourceReference(DataGrid.BorderBrushProperty, "GridLine");
        var element = FullText(true);
        grid.AutoGeneratingColumn += (_, e) =>
        {
            // SQL bit generates a CheckBox column. Render read-only values consistently as text;
            // a TextBlock style on DataGridCheckBoxColumn crashes during WPF layout/recycling.
            if (e.Column is DataGridCheckBoxColumn booleanColumn)
                e.Column = new DataGridTextColumn { Header = booleanColumn.Header, Binding = booleanColumn.Binding, SortMemberPath = booleanColumn.SortMemberPath };
            if (grid.ItemsSource is System.Data.DataView view && view.Table?.Columns[e.PropertyName] is { } source && source.ExtendedProperties["OriginalName"] is string original) e.Column.Header = original;
            var headerWidth = Math.Clamp((Convert.ToString(e.Column.Header)?.Length ?? 0) * 8.4 + 28, 64, 360);
            e.Column.MinWidth = 64;
            var width = compact ? 150 : Math.Max(100, headerWidth);
            if (!compact && grid.ItemsSource is System.Data.DataView rows && rows.Table?.Columns.Contains(e.PropertyName) == true)
                for (var i = 0; i < Math.Min(32, rows.Count); i++)
                    width = Math.Max(width, Math.Min(360, (Convert.ToString(rows[i][e.PropertyName])?.Length ?? 0) * 7.2 + 24));
            e.Column.Width = new DataGridLength(width);
            if (e.Column is DataGridTextColumn column)
            {
                column.ElementStyle = element;
                if (column.Binding is Binding binding) binding.Converter = NullValueConverter.Instance;
            }
        };
    }
    public static DataGridTextColumn TextColumn(string header, string path, double minWidth) => new()
    {
        Header = header, Binding = new Binding(path) { Converter = NullValueConverter.Instance }, MinWidth = minWidth, Width = DataGridLength.Auto, ElementStyle = FullText()
    };
    private static Style FullText(bool compact = false)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, compact ? TextTrimming.CharacterEllipsis : TextTrimming.None));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 0, 6, 0)));
        if (compact) style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding("Text") { RelativeSource = RelativeSource.Self }));
        var nullTrigger = new Trigger { Property = TextBlock.TextProperty, Value = "null" };
        nullTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
        nullTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("Muted")));
        nullTrigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, new DynamicResourceExtension("Panel")));
        style.Triggers.Add(nullTrigger);
        return style;
    }
    private sealed class NullValueConverter : IValueConverter
    {
        public static readonly NullValueConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value == null || value == DBNull.Value ? "null" : value;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }
}

