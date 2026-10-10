using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SysLens.ViewModels;

namespace SysLens.Views;

/// <summary>The Hardware Monitor tab, bound to a <see cref="MonitorViewModel"/>. Graphs are moved by dragging them.</summary>
public partial class HardwareMonitorView : UserControl
{
    private SensorItem? _pressed;
    private Point _pressedAt;

    public HardwareMonitorView() => InitializeComponent();

    private MonitorViewModel? ViewModel => DataContext as MonitorViewModel;

    private async void InstallPawnIo_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
            await viewModel.InstallPawnIoAsync();
    }

    private void ResetToDefaults_Click(object sender, RoutedEventArgs e) => ViewModel?.ResetToDefaults();

    private void ClearHistory_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearHistory();

    private void HideSensor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SensorItem item })
            item.IsShown = false;
    }

    private void Graph_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A press on the remove button is a click, not the start of a drag.
        _pressed = IsInButton(e.OriginalSource as DependencyObject) ? null : (sender as FrameworkElement)?.DataContext as SensorItem;
        _pressedAt = e.GetPosition(this);
    }

    private void Graph_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is not { } graph || e.LeftButton != MouseButtonState.Pressed)
            return;
        var moved = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pressed = null;
        graph.IsDragged = true;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(SensorItem), graph), DragDropEffects.Move);
        }
        finally
        {
            graph.IsDragged = false;
            // A drag cancelled over a graph may leave its bar showing.
            foreach (var shown in ViewModel?.Shown ?? [])
                shown.DropSide = DropSide.None;
        }
    }

    private void Graph_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (Dragged(e) is not { } dragged || sender is not FrameworkElement { DataContext: SensorItem target } card || target == dragged)
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        target.DropSide = e.GetPosition(card).X < card.ActualWidth / 2 ? DropSide.Before : DropSide.After;
    }

    private void Graph_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SensorItem target })
            target.DropSide = DropSide.None;
    }

    private void Graph_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: SensorItem target } || Dragged(e) is not { } dragged)
            return;
        var after = target.DropSide == DropSide.After;
        target.DropSide = DropSide.None;
        ViewModel?.MoveGraph(dragged, target, after);
    }

    private void Graphs_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = Dragged(e) is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void Graphs_Drop(object sender, DragEventArgs e)
    {
        if (Dragged(e) is { } dragged)
            ViewModel?.MoveGraph(dragged, null, after: true);
    }

    private static SensorItem? Dragged(DragEventArgs e) => e.Data.GetData(typeof(SensorItem)) as SensorItem;

    private static bool IsInButton(DependencyObject? element)
    {
        for (; element is not null; element = element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is ButtonBase)
                return true;
        }
        return false;
    }
}
