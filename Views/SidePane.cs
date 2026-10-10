using System.Windows;
using System.Windows.Controls;

namespace SysLens.Views;

/// <summary>
/// For a Grid whose first column is a resizable side pane and whose other columns are a GridSplitter and the
/// main pane: caps the side column so every other column keeps its fixed width or MinWidth, whether the
/// splitter is dragged or the window shrinks. Set <c>SidePane.IsEnabled="True"</c> on the Grid.
/// </summary>
public static class SidePane
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SidePane), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(Grid grid) => (bool)grid.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Grid grid, bool value) => grid.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid)
            return;
        grid.SizeChanged -= OnSizeChanged;
        if ((bool)e.NewValue)
            grid.SizeChanged += OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var columns = ((Grid)sender).ColumnDefinitions;
        if (columns.Count < 2)
            return;

        var reserved = columns.Skip(1).Sum(c => c.Width.IsAbsolute ? c.Width.Value : c.MinWidth);
        var side = columns[0];
        side.MaxWidth = Math.Max(side.MinWidth, e.NewSize.Width - reserved);
    }
}
