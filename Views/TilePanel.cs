using System.Windows;
using System.Windows.Controls;

namespace SysLens.Views;

/// <summary>
/// Lays children out in as many equal columns as fit at <see cref="MinTileWidth"/> or wider, row by row, each
/// row as tall as its tallest child.
/// </summary>
public sealed class TilePanel : Panel
{
    public static readonly DependencyProperty MinTileWidthProperty = DependencyProperty.Register(
        nameof(MinTileWidth), typeof(double), typeof(TilePanel),
        new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(TilePanel),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinTileWidth
    {
        get => (double)GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    /// <summary>The gap between columns and between rows.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, width) = Columns(availableSize.Width);
        var height = 0.0;
        foreach (var row in Rows(columns))
        {
            var rowHeight = 0.0;
            foreach (UIElement child in row)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }
            height += (height > 0 ? Spacing : 0) + rowHeight;
        }
        return new Size(columns * width + (columns - 1) * Spacing, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, width) = Columns(finalSize.Width);
        var top = 0.0;
        foreach (var row in Rows(columns))
        {
            var rowHeight = row.Max(child => child.DesiredSize.Height);
            for (var i = 0; i < row.Count; i++)
                row[i].Arrange(new Rect(i * (width + Spacing), top, width, rowHeight));
            top += rowHeight + Spacing;
        }
        return finalSize;
    }

    private (int Columns, double Width) Columns(double available)
    {
        if (double.IsInfinity(available) || available <= MinTileWidth)
            return (1, double.IsInfinity(available) ? MinTileWidth : Math.Max(0, available));

        var columns = Math.Max(1, (int)((available + Spacing) / (MinTileWidth + Spacing)));
        return (columns, (available - (columns - 1) * Spacing) / columns);
    }

    private IEnumerable<List<UIElement>> Rows(int columns) =>
        InternalChildren.Cast<UIElement>().Chunk(columns).Select(row => row.ToList());
}
