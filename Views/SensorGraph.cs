using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SysLens.ViewModels;

namespace SysLens.Views;

/// <summary>
/// A <see cref="SensorItem"/>'s history as a 2px line over a faint wash, the newest reading at the right edge, with
/// hairline gridlines at round values. Line and wash are coloured by height with the theme's GraphGradientStops,
/// from the bottom of the axis to the top. Under the pointer a crosshair snaps to the nearest reading and labels
/// it with its value and time.
/// </summary>
public sealed class SensorGraph : FrameworkElement
{
    public static readonly DependencyProperty SensorProperty = DependencyProperty.Register(
        nameof(Sensor), typeof(SensorItem), typeof(SensorGraph),
        new PropertyMetadata(null, (d, _) => ((SensorGraph)d).OnSensorChanged()));

    // Room left of the plot for the gridline labels; the top one carries the unit.
    private const double AxisWidth = 60;
    private const double AxisGap = 8;

    // Half a label's height above the top gridline and below the bottom one.
    private const double VerticalMargin = 7;
    private const double LabelSize = 11;
    private const double TipSize = 12;
    private const double LineThickness = 2;
    private const double DotRadius = 4;
    private const double RingThickness = 2;

    private static readonly Typeface Font = new("Segoe UI");
    private static readonly double[] NiceSteps = [1, 2, 2.5, 5, 10];

    private SensorItem? _attached;
    private Point? _pointer;

    public SensorGraph()
    {
        Loaded += (_, _) => Attach(Sensor);
        Unloaded += (_, _) => Attach(null);
    }

    public SensorItem? Sensor
    {
        get => (SensorItem?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _pointer = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _pointer = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        // A transparent fill makes the whole graph take the pointer, not only its painted pixels.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var plot = new Rect(AxisWidth, VerticalMargin, ActualWidth - AxisWidth - DotRadius - RingThickness,
            ActualHeight - 2 * VerticalMargin);
        if (Sensor is not { } sensor || plot.Width < 20 || plot.Height < 20)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var stops = (GradientStopCollection)FindResource("GraphGradientStops");
        var gradient = new LinearGradientBrush(stops, new Point(0, plot.Bottom), new Point(0, plot.Top))
        {
            MappingMode = BrushMappingMode.Absolute,
        };
        Brush ColorAtHeight(double y) => new SolidColorBrush(ColorAt(stops, (plot.Bottom - y) / plot.Height));
        var surface = Resource("PanelBrush");
        var muted = Resource("TertiaryTextBrush");

        if (Extent(sensor) is not { } extent)
        {
            var empty = Text("No reading", TipSize, muted, dpi);
            dc.DrawText(empty, new Point(plot.Left + (plot.Width - empty.Width) / 2, plot.Top + (plot.Height - empty.Height) / 2));
            return;
        }

        var (min, max) = extent;
        var (unit, divisor) = SensorFormat.AxisUnit(sensor.Kind, Math.Max(Math.Abs(min), Math.Abs(max)));
        var (low, high, step) = Range(SensorFormat.Scale(sensor.Kind), min / divisor, max / divisor);
        double Y(double reading) => plot.Bottom - (Math.Clamp(reading / divisor, low, high) - low) / (high - low) * plot.Height;
        // Hairlines centred on a device pixel stay one pixel wide.
        static double Snap(double coordinate, double scale) => (Math.Round(coordinate * scale) + 0.5) / scale;

        var gridPen = new Pen(Resource("PanelBorderBrush"), 1 / dpi.DpiScaleY);
        var lines = (int)Math.Round((high - low) / step);
        for (var i = 0; i <= lines; i++)
        {
            var gridValue = low + i * step;
            var y = Snap(Y(gridValue * divisor), dpi.DpiScaleY);
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Text(TickText(gridValue) + (i == lines && unit.Length > 0 ? $" {unit}" : ""), LabelSize, muted, dpi);
            dc.DrawText(label, new Point(plot.Left - AxisGap - label.Width, y - label.Height / 2));
        }

        // The newest reading sits at the right edge; a full history spans the plot.
        var count = sensor.Count;
        var stepX = plot.Width / (SensorItem.HistoryLength - 1);
        double X(int index) => plot.Right - (count - 1 - index) * stepX;

        var line = new StreamGeometry();
        var wash = new StreamGeometry();
        using (var lineContext = line.Open())
        using (var washContext = wash.Open())
        {
            // A missing reading breaks the line.
            Point? previous = null;
            for (var i = 0; i <= count; i++)
            {
                if (i < count && sensor.ValueAt(i) is { } reading)
                {
                    var point = new Point(X(i), Y(reading));
                    if (previous is null)
                    {
                        lineContext.BeginFigure(point, false, false);
                        washContext.BeginFigure(new Point(point.X, plot.Bottom), true, true);
                    }
                    else
                    {
                        lineContext.LineTo(point, true, true);
                    }
                    washContext.LineTo(point, false, false);
                    previous = point;
                }
                else if (previous is { } end)
                {
                    washContext.LineTo(new Point(end.X, plot.Bottom), false, false);
                    previous = null;
                }
            }
        }

        var washBrush = gradient.Clone();
        washBrush.Opacity = 0.12;
        dc.DrawGeometry(washBrush, null, wash);
        dc.DrawGeometry(null, new Pen(gradient, LineThickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);

        if (sensor.ValueAt(count - 1) is { } latest)
            DrawDot(dc, new Point(X(count - 1), Y(latest)), ColorAtHeight(Y(latest)), surface);

        if (_pointer is not { } pointer || pointer.X < plot.Left - stepX || pointer.X > plot.Right + stepX)
            return;

        var index = Math.Clamp((int)Math.Round(count - 1 - (plot.Right - pointer.X) / stepX), 0, count - 1);
        var x = Snap(X(index), dpi.DpiScaleX);
        dc.DrawLine(new Pen(muted, 1 / dpi.DpiScaleX), new Point(x, plot.Top), new Point(x, plot.Bottom));
        var hovered = sensor.ValueAt(index);
        if (hovered is { } hoveredValue)
            DrawDot(dc, new Point(x, Y(hoveredValue)), ColorAtHeight(Y(hoveredValue)), surface);

        // The value leads; the time follows in secondary ink.
        var valueText = SensorFormat.Format(sensor.Kind, hovered);
        var tip = Text($"{valueText}   {sensor.TimeAt(index):HH:mm:ss}", TipSize, Resource("TextBrush"), dpi);
        tip.SetFontWeight(FontWeights.SemiBold, 0, valueText.Length);
        tip.SetForegroundBrush(Resource("SecondaryTextBrush"), valueText.Length, tip.Text.Length - valueText.Length);
        var box = new Rect(0, plot.Top, tip.Width + 12, tip.Height + 6);
        box.X = x + 8 + box.Width <= plot.Right ? x + 8 : x - 8 - box.Width;
        dc.DrawRoundedRectangle(Resource("ControlBrush"), null, box, 4, 4);
        dc.DrawText(tip, new Point(box.X + 6, box.Y + 3));
    }

    /// <summary>The gradient's colour at <paramref name="offset"/>, 0 at the bottom of the axis and 1 at the top.</summary>
    private static Color ColorAt(GradientStopCollection stops, double offset)
    {
        var sorted = stops.OrderBy(stop => stop.Offset).ToList();
        offset = Math.Clamp(offset, sorted[0].Offset, sorted[^1].Offset);
        for (var i = 1; i < sorted.Count; i++)
        {
            if (offset > sorted[i].Offset)
                continue;
            var (from, to) = (sorted[i - 1], sorted[i]);
            var t = to.Offset > from.Offset ? (offset - from.Offset) / (to.Offset - from.Offset) : 0;
            return Color.FromRgb(Mix(from.Color.R, to.Color.R, t), Mix(from.Color.G, to.Color.G, t), Mix(from.Color.B, to.Color.B, t));
        }
        return sorted[^1].Color;
    }

    private static byte Mix(byte from, byte to, double t) => (byte)Math.Round(from + (to - from) * t);

    private static void DrawDot(DrawingContext dc, Point center, Brush fill, Brush surface)
    {
        // A ring in the surface colour keeps the dot clear of the line it sits on.
        dc.DrawEllipse(surface, null, center, DotRadius + RingThickness, DotRadius + RingThickness);
        dc.DrawEllipse(fill, null, center, DotRadius, DotRadius);
    }

    /// <summary>The lowest and highest held readings, or null when none is held.</summary>
    private static (double Min, double Max)? Extent(SensorItem sensor)
    {
        (double Min, double Max)? extent = null;
        for (var i = 0; i < sensor.Count; i++)
        {
            if (sensor.ValueAt(i) is { } value)
                extent = extent is { } held ? (Math.Min(held.Min, value), Math.Max(held.Max, value)) : (value, value);
        }
        return extent;
    }

    /// <summary>The axis range and gridline step, in the axis unit.</summary>
    private static (double Low, double High, double Step) Range(GraphScale scale, double lowest, double highest)
    {
        switch (scale)
        {
            case GraphScale.Percent:
                return highest > 100 ? Nice(0, highest) : (0, 100, 50);
            case GraphScale.FromZero:
                // Readings that are all zero, such as a fan stopped at idle, still get an axis.
                return lowest == 0 && highest == 0 ? (0, 1, 1) : Nice(Math.Min(0, lowest), Math.Max(0, highest));
            default:
                var span = highest - lowest;
                var padding = span > 0 ? span * 0.1 : Math.Max(Math.Abs(highest) * 0.05, 0.001);
                return Nice(lowest - padding, highest + padding);
        }
    }

    /// <summary>Round gridline values covering <paramref name="low"/> to <paramref name="high"/> with the least slack.</summary>
    private static (double Low, double High, double Step) Nice(double low, double high)
    {
        if (high <= low)
            high = low + 1;

        (double Low, double High, double Step) best = (low, high, high - low);
        var bestSlack = double.MaxValue;
        for (var intervals = 2; intervals <= 4; intervals++)
        {
            var step = NiceStep((high - low) / intervals);
            var bottom = Math.Floor(low / step + 1e-9) * step;
            var top = Math.Ceiling(high / step - 1e-9) * step;
            var slack = (top - bottom) / (high - low);
            if (slack < bestSlack - 1e-9)
            {
                best = (bottom, top, step);
                bestSlack = slack;
            }
        }
        return best;
    }

    private static double NiceStep(double raw)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        return NiceSteps.Select(nice => nice * magnitude).First(step => step >= raw * (1 - 1e-9));
    }

    /// <summary>A gridline value without trailing zeros, compact from 1,000 up: 0, 2.5K, 5K.</summary>
    private static string TickText(double value) =>
        Math.Abs(value) >= 1000
            ? (value / 1000).ToString("0.##", CultureInfo.CurrentCulture) + "K"
            : value.ToString("0.###", CultureInfo.CurrentCulture);

    private Brush Resource(string key) => (Brush)FindResource(key);

    private static FormattedText Text(string text, double size, Brush brush, DpiScale dpi) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dpi.PixelsPerDip);

    private void OnSensorChanged()
    {
        if (IsLoaded)
            Attach(Sensor);
        InvalidateVisual();
    }

    private void Attach(SensorItem? sensor)
    {
        if (_attached is not null)
            _attached.Sampled -= InvalidateVisual;
        _attached = sensor;
        if (sensor is not null)
            sensor.Sampled += InvalidateVisual;
        InvalidateVisual();
    }
}
