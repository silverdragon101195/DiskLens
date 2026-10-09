using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SysLens.ViewModels;

namespace SysLens.Views;

/// <summary>
/// Maps a <see cref="UsbSeverity"/> to a brush. Normal yields no value, so the binding's
/// FallbackValue (or the inherited brush) applies.
/// </summary>
public sealed class SeverityBrushConverter : IValueConverter
{
    public Brush? CautionBrush { get; set; }
    public Brush? CriticalBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            UsbSeverity.Caution => CautionBrush,
            UsbSeverity.Critical => CriticalBrush,
            _ => DependencyProperty.UnsetValue,
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
