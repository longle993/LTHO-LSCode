using System.Windows.Automation;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LSCode.Shell;

// The read-only page has one visual instead of a WPF control for each visible cell.
internal sealed class CatalogPreview : FrameworkElement, IScrollInfo
{
    private const double RowHeight = 32, HeaderHeight = 40, InitialWidth = 150;
    private static readonly RectangleGeometry DefaultTextClip = TextClip(InitialWidth);
    private string[] headers = [];
    private object?[][] rows = [];
    private double[] edges = [0];
    private TranslateTransform[] columnPositions = [];
    private TranslateTransform[] rowPositions = [];
    private static readonly TranslateTransform HeaderPosition = Position(0, 10);
    private Size viewport;
    private RectangleGeometry? bodyClip;
    private Pen? rowLine;
    private readonly DrawingVisual contentVisual = new();
    private bool redrawPending;
    private DrawingGroup?[] rowText = [];
    private int cachedFirstColumn = -1, cachedLastColumn = -1;
    private double lastDrawnHorizontalOffset = double.NaN;
    private const int TextCacheLimit = 8192;
    private Dictionary<(string Text, bool Header, double Width), DrawingGroup> textCache = [];
    private readonly Dictionary<(string Text, bool Header, double Width), DrawingGroup> resizedText = [];
    private Dictionary<(string Text, bool Header, double Width), DrawingGroup> retainedText = [];
    private Brush? retainedBrush;
    private double retainedDpi;
    private Brush? cachedTextBrush;
    private Brush? drawingTextBrush;
    private static readonly Typeface BodyTypeface = new("Segoe UI");
    private static readonly Typeface HeaderTypeface = new(BodyTypeface.FontFamily, FontStyles.Normal,
        FontWeights.SemiBold, FontStretches.Normal);
    private double cachedDpi;
    private int row = -1, column = -1, resizing = -1;
    private double resizeOrigin, resizeWidth;
    public double HorizontalOffset { get; private set; }
    public double VerticalOffset { get; private set; }
    public double ExtentWidth => edges[^1];
    public double ExtentHeight => rows.Length * RowHeight;
    public double ViewportWidth => viewport.Width;
    public double ViewportHeight => Math.Max(0, viewport.Height - HeaderHeight);
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public ScrollViewer? ScrollOwner { get; set; }

    private static readonly DependencyProperty SurfaceBrush = RenderBrush("SurfaceBrush");
    private static readonly DependencyProperty AlternateBrush = RenderBrush("AlternateBrush");
    private static readonly DependencyProperty HeaderBrush = RenderBrush("HeaderBrush");
    private static readonly DependencyProperty TextBrush = RenderBrush("TextBrush");
    private static readonly DependencyProperty LineBrush = RenderBrush("LineBrush");

    private static DependencyProperty RenderBrush(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(CatalogPreview), new FrameworkPropertyMetadata(Brushes.Transparent,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public CatalogPreview()
    {
        AddVisualChild(contentVisual);
        Focusable = true;
        ClipToBounds = true;
        SetResourceReference(SurfaceBrush, "Surface");
        SetResourceReference(AlternateBrush, "Panel");
        SetResourceReference(HeaderBrush, "Raised");
        SetResourceReference(TextBrush, "Text");
        SetResourceReference(LineBrush, "GridLine");
        AutomationProperties.SetName(this, "Dữ liệu danh mục chỉ đọc");
    }

    public void SetPage(string[] columns, object?[][] values)
    {
        textCache = [];
        resizedText.Clear();
        headers = columns;
        rows = values;
        rowText = new DrawingGroup?[values.Length];
        lastDrawnHorizontalOffset = double.NaN;
        cachedFirstColumn = cachedLastColumn = -1;
        edges = Enumerable.Range(0, columns.Length + 1).Select(i => i * InitialWidth).ToArray();
        columnPositions = Enumerable.Range(0, columns.Length).Select(i => Position(edges[i] + 6, 0)).ToArray();
        rowPositions = Enumerable.Range(0, values.Length).Select(i => Position(0, HeaderHeight + i * RowHeight + 6)).ToArray();
        row = column = -1;
        SetHorizontalOffset(0);
        SetVerticalOffset(0);
        ScrollOwner?.InvalidateScrollInfo();
        QueueRedraw();
    }

    public async Task PreparePageAsync(string[] columns, object?[][] values, CancellationToken token)
    {
        var brush = (Brush)GetValue(TextBrush);
        var frozenBrush = brush.CloneCurrentValue();
        frozenBrush.Freeze();
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var culture = CultureInfo.CurrentCulture;
        var reusable = ReferenceEquals(brush, retainedBrush) && dpi == retainedDpi ? retainedText : null;
        var prepared = await Task.Run(() =>
        {
            var keys = new HashSet<(string Text, bool Header, double Width)>();
            foreach (string heading in columns)
                if (heading.Length > 0) keys.Add((heading, true, InitialWidth));
            foreach (var valuesInRow in values)
            {
                token.ThrowIfCancellationRequested();
                foreach (var value in valuesInRow)
                {
                    if (keys.Count >= TextCacheLimit) break;
                    var text = Convert.ToString(value, culture);
                    if (!string.IsNullOrEmpty(text)) keys.Add((text, false, InitialWidth));
                }
            }

            var entries = keys.ToArray();
            var drawings = new DrawingGroup[entries.Length];
            Parallel.For(0, entries.Length, new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 1, 7)
            }, index =>
            {
                var (text, header, width) = entries[index];
                drawings[index] = reusable != null && reusable.TryGetValue(entries[index], out var drawing)
                    ? drawing : CreateGlyphs(text, header, width, frozenBrush, dpi, culture);
            });

            var cache = new Dictionary<(string Text, bool Header, double Width), DrawingGroup>(entries.Length);
            for (int index = 0; index < entries.Length; index++) cache.Add(entries[index], drawings[index]);
            return cache;
        }, token);
        token.ThrowIfCancellationRequested();
        SetPage(columns, values);
        if (ReferenceEquals(brush, GetValue(TextBrush)) && dpi == VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            textCache = prepared;
            if (reusable == null || prepared.Count >= reusable.Count)
            {
                retainedText = prepared;
                retainedBrush = brush;
                retainedDpi = dpi;
            }
            cachedTextBrush = brush;
            drawingTextBrush = frozenBrush;
            cachedDpi = dpi;
        }
    }

    private static RectangleGeometry TextClip(double width)
    {
        var clip = new RectangleGeometry(new Rect(0, 0, Math.Max(1, width - 12), RowHeight - 6));
        clip.Freeze();
        return clip;
    }

    private static DrawingGroup CreateGlyphs(string value, bool header, double width, Brush brush, double dpi, CultureInfo culture)
    {
        var font = header ? HeaderTypeface : BodyTypeface;
        var glyphs = new DrawingGroup();
        using (var glyphContext = glyphs.Open())
        {
            var formatted = new FormattedText(value, culture, FlowDirection.LeftToRight,
                font, 14, brush, null, TextFormattingMode.Display, dpi);
            glyphContext.DrawText(formatted, new Point());
        }
        glyphs.ClipGeometry = width == InitialWidth ? DefaultTextClip : TextClip(width);
        glyphs.Freeze();
        return glyphs;
    }

    protected override Size MeasureOverride(Size available) => new(
        double.IsFinite(available.Width) ? available.Width : 0,
        double.IsFinite(available.Height) ? available.Height : 0);

    protected override Size ArrangeOverride(Size size)
    {
        if (viewport != size)
        {
            viewport = size;
            bodyClip = new RectangleGeometry(new Rect(0, HeaderHeight, ViewportWidth, ViewportHeight));
            bodyClip.Freeze();
            SetHorizontalOffset(HorizontalOffset);
            SetVerticalOffset(VerticalOffset);
            ScrollOwner?.InvalidateScrollInfo();
            QueueRedraw();
        }
        return size;
    }

    private static TranslateTransform Position(double x, double y)
    {
        var position = new TranslateTransform(x, y);
        position.Freeze();
        return position;
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0
        ? contentVisual : throw new ArgumentOutOfRangeException(nameof(index));

    protected override void OnRender(DrawingContext drawing) => QueueRedraw();

    private void QueueRedraw()
    {
        if (redrawPending) return;
        redrawPending = true;
        _ = Dispatcher.InvokeAsync(() =>
        {
            redrawPending = false;
            Redraw();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void Redraw()
    {
        var trace = PreviewTrace.Log;
        bool tracing = trace.IsEnabled();
        long start = tracing ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using (var drawing = contentVisual.RenderOpen()) DrawViewport(drawing);
        if (tracing) trace.Redrawn(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    [System.Diagnostics.Tracing.EventSource(Name = "LSCode-CatalogPreview")]
    private sealed class PreviewTrace : System.Diagnostics.Tracing.EventSource
    {
        public static readonly PreviewTrace Log = new();

        [System.Diagnostics.Tracing.Event(1)]
        public void Redrawn(double milliseconds) => WriteEvent(1, milliseconds);
    }

    private void DrawViewport(DrawingContext drawing)
    {
        var surface = (Brush)GetValue(SurfaceBrush);
        var panel = (Brush)GetValue(AlternateBrush);
        var raised = (Brush)GetValue(HeaderBrush);
        var text = (Brush)GetValue(TextBrush);
        var lineBrush = (Brush)GetValue(LineBrush);
        if (rowLine == null || !ReferenceEquals(rowLine.Brush, lineBrush)) rowLine = new Pen(lineBrush, 1);
        drawing.DrawRectangle(surface, null, new Rect(RenderSize));
        if (headers.Length == 0) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (cachedTextBrush != text || cachedDpi != dpi)
        {
            textCache = [];
            resizedText.Clear();
            cachedFirstColumn = cachedLastColumn = -1;
            cachedTextBrush = text;
            drawingTextBrush = text.CloneCurrentValue();
            drawingTextBrush.Freeze();
            cachedDpi = dpi;
        }
        void DrawText(DrawingContext target, string value, int columnIndex, bool header)
        {
            if (value.Length == 0) return;
            double width = edges[columnIndex + 1] - edges[columnIndex];
            var key = (value, header, width);
            if (!textCache.TryGetValue(key, out var glyphs) && !resizedText.TryGetValue(key, out glyphs))
            {
                glyphs = CreateGlyphs(value, header, width, drawingTextBrush!, dpi, CultureInfo.CurrentCulture);
                if (resizedText.Count < 512) resizedText.Add(key, glyphs);
            }
            target.PushTransform(columnPositions[columnIndex]);
            target.DrawDrawing(glyphs);
            target.Pop();
        }

        int first = ColumnAt(HorizontalOffset);
        int last = ColumnAt(Math.Max(0, HorizontalOffset + ViewportWidth - 1));
        drawing.DrawRectangle(raised, null, new Rect(0, 0, ViewportWidth, HeaderHeight));
        drawing.PushTransform(Position(-HorizontalOffset, 0));
        drawing.PushTransform(HeaderPosition);
        for (int c = first; c <= last; c++) DrawText(drawing, headers[c], c, true);
        drawing.Pop();
        drawing.Pop();

        drawing.PushClip(bodyClip);
        int start = (int)(VerticalOffset / RowHeight);
        int end = Math.Min(rows.Length, (int)Math.Ceiling((VerticalOffset + ViewportHeight) / RowHeight));
        for (int r = start; r < end; r++)
        {
            double y = HeaderHeight + r * RowHeight - VerticalOffset;
            if (r % 2 != 0) drawing.DrawRectangle(panel, null, new Rect(0, y, ViewportWidth, RowHeight));
            drawing.DrawLine(rowLine, new Point(0, y + RowHeight - 0.5), new Point(ViewportWidth, y + RowHeight - 0.5));
        }
        if (row >= start && row < end && column >= first && column <= last)
            drawing.DrawRectangle(raised, null, new Rect(edges[column] - HorizontalOffset,
                HeaderHeight + row * RowHeight - VerticalOffset, edges[column + 1] - edges[column], RowHeight));

        if (first != cachedFirstColumn || last != cachedLastColumn)
        {
            Array.Clear(rowText);
            cachedFirstColumn = first;
            cachedLastColumn = last;
        }
        bool reuseRows = lastDrawnHorizontalOffset == HorizontalOffset;
        lastDrawnHorizontalOffset = HorizontalOffset;
        drawing.PushTransform(Position(-HorizontalOffset, -VerticalOffset));
        for (int r = start; r < end; r++)
        {
            drawing.PushTransform(rowPositions[r]);
            if (!reuseRows)
            {
                for (int c = first; c <= last; c++)
                    DrawText(drawing, Convert.ToString(rows[r][c], CultureInfo.CurrentCulture) ?? "", c, false);
            }
            else if (rowText[r] is not { } textRow)
            {
                textRow = new DrawingGroup();
                using (var rowContext = textRow.Open())
                    for (int c = first; c <= last; c++)
                        DrawText(rowContext, Convert.ToString(rows[r][c], CultureInfo.CurrentCulture) ?? "", c, false);
                textRow.Freeze();
                rowText[r] = textRow;
                drawing.DrawDrawing(textRow);
            }
            else drawing.DrawDrawing(textRow);
            drawing.Pop();
        }
        drawing.Pop();
        drawing.Pop();
    }

    private int ColumnAt(double x)
    {
        int index = Array.BinarySearch(edges, x);
        return Math.Clamp(index >= 0 ? index : ~index - 1, 0, Math.Max(0, headers.Length - 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (headers.Length == 0) return;
        var point = e.GetPosition(this);
        if (point.Y < HeaderHeight)
        {
            int candidate = ColumnAt(point.X + HorizontalOffset - 5);
            if (Math.Abs(edges[candidate + 1] - HorizontalOffset - point.X) <= 5)
            {
                resizing = candidate;
                resizeOrigin = point.X;
                resizeWidth = edges[candidate + 1] - edges[candidate];
                CaptureMouse();
            }
            return;
        }
        int selected = (int)((point.Y - HeaderHeight + VerticalOffset) / RowHeight);
        if (selected >= rows.Length || point.X + HorizontalOffset >= ExtentWidth) return;
        Focus();
        row = selected;
        column = ColumnAt(point.X + HorizontalOffset);
        QueueRedraw();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (resizing >= 0)
        {
            double width = Math.Max(40, resizeWidth + point.X - resizeOrigin);
            double delta = width - (edges[resizing + 1] - edges[resizing]);
            if (delta == 0) return;
            cachedFirstColumn = cachedLastColumn = -1;
            for (int i = resizing + 1; i < edges.Length; i++) edges[i] += delta;
            for (int i = resizing + 1; i < columnPositions.Length; i++) columnPositions[i] = Position(edges[i] + 6, 0);
            SetHorizontalOffset(HorizontalOffset);
            ScrollOwner?.InvalidateScrollInfo();
            QueueRedraw();
        }
        int candidate = ColumnAt(point.X + HorizontalOffset - 5);
        Cursor = resizing >= 0 || headers.Length > 0 && point.Y < HeaderHeight &&
            Math.Abs(edges[candidate + 1] - HorizontalOffset - point.X) <= 5 ? Cursors.SizeWE : null;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        resizing = -1;
        base.OnLostMouseCapture(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (resizing >= 0) ReleaseMouseCapture();
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (rows.Length == 0 || headers.Length == 0) return;
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && row >= 0)
        {
            try { Clipboard.SetText(Convert.ToString(rows[row][column], CultureInfo.CurrentCulture) ?? ""); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            row = Math.Clamp(row < 0 ? 0 : row + (e.Key == Key.Up ? -1 : e.Key == Key.Down ? 1 : 0), 0, rows.Length - 1);
            column = Math.Clamp(column < 0 ? 0 : column + (e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0), 0, headers.Length - 1);
            if (edges[column] < HorizontalOffset) SetHorizontalOffset(edges[column]);
            else if (edges[column + 1] > HorizontalOffset + ViewportWidth) SetHorizontalOffset(edges[column + 1] - ViewportWidth);
            if (row * RowHeight < VerticalOffset) SetVerticalOffset(row * RowHeight);
            else if ((row + 1) * RowHeight > VerticalOffset + ViewportHeight) SetVerticalOffset((row + 1) * RowHeight - ViewportHeight);
            QueueRedraw();
        }
        else return;
        e.Handled = true;
    }

    public void SetHorizontalOffset(double offset)
    {
        offset = Math.Clamp(offset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        if (HorizontalOffset == offset) return;
        HorizontalOffset = offset;
        if (!redrawPending) Redraw();
        ScrollOwner?.InvalidateScrollInfo();

    }

    public void SetVerticalOffset(double offset)
    {
        offset = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (VerticalOffset == offset) return;
        VerticalOffset = offset;
        if (!redrawPending) Redraw();
        ScrollOwner?.InvalidateScrollInfo();

    }

    public void LineUp() => SetVerticalOffset(VerticalOffset - RowHeight);
    public void LineDown() => SetVerticalOffset(VerticalOffset + RowHeight);
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - 24);
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + 24);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - RowHeight * 3);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + RowHeight * 3);
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - 72);
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + 72);
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;
}
