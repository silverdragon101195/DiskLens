using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SysLens.Lighting;

namespace SysLens.Views;

/// <summary>Hue bar, saturation and value square, hex box and preset swatches, editing <see cref="Color"/>.</summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorPicker),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((ColorPicker)d).OnColorChanged((Color)e.NewValue)));

    /// <summary>Saturated colours, which LEDs show best, and two whites.</summary>
    public static IReadOnlyList<SolidColorBrush> Presets { get; } =
    [
        .. new[] { "#FF0000", "#FF5A00", "#FFC800", "#3CFF00", "#00FFB4", "#00C8FF", "#0040FF", "#8C00FF", "#FF00C8", "#FFFFFF", "#FFB46E" }
            .Select(hex =>
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }),
    ];

    // Kept apart from Color so the hue survives greys, black and white, which have none.
    private double _hue;
    private double _saturation;
    private double _value = 1;
    private bool _settingColor;

    public ColorPicker()
    {
        InitializeComponent();
        OnColorChanged(Color);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private void OnColorChanged(Color color)
    {
        if (!_settingColor)
        {
            var (hue, saturation, value) = new Rgb(color.R, color.G, color.B).ToHsv();
            if (saturation > 0 && value > 0)
                _hue = hue;
            _saturation = saturation;
            _value = value;
        }
        UpdateVisuals();
    }

    private void SetFromHsv()
    {
        var rgb = Rgb.FromHsv(_hue, _saturation, _value);
        _settingColor = true;
        try
        {
            Color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        }
        finally
        {
            _settingColor = false;
        }
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var pure = Rgb.FromHsv(_hue, 1, 1);
        HueFill.Fill = new SolidColorBrush(Color.FromRgb(pure.R, pure.G, pure.B));
        Swatch.Background = new SolidColorBrush(Color);
        if (!HexBox.IsKeyboardFocused)
            HexBox.Text = new Rgb(Color.R, Color.G, Color.B).ToHex();

        Canvas.SetLeft(SvMarker, _saturation * SvArea.ActualWidth - SvMarker.Width / 2);
        Canvas.SetTop(SvMarker, (1 - _value) * SvArea.ActualHeight - SvMarker.Height / 2);
        Canvas.SetTop(HueMarker, _hue * HueArea.ActualHeight - HueMarker.Height / 2);
    }

    private void Area_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisuals();

    private void SvArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        PickSaturationValue(e.GetPosition(SvArea));
    }

    private void SvArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (SvArea.IsMouseCaptured)
            PickSaturationValue(e.GetPosition(SvArea));
    }

    private void HueArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        HueArea.CaptureMouse();
        PickHue(e.GetPosition(HueArea));
    }

    private void HueArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueArea.IsMouseCaptured)
            PickHue(e.GetPosition(HueArea));
    }

    private void Area_MouseUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

    private void PickSaturationValue(Point point)
    {
        _saturation = Math.Clamp(point.X / Math.Max(1, SvArea.ActualWidth), 0, 1);
        _value = 1 - Math.Clamp(point.Y / Math.Max(1, SvArea.ActualHeight), 0, 1);
        SetFromHsv();
    }

    private void PickHue(Point point)
    {
        // Just short of 1, which is red again and would jump the marker back to the top.
        _hue = Math.Clamp(point.Y / Math.Max(1, HueArea.ActualHeight), 0, 0.9999);
        SetFromHsv();
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyHex();
    }

    private void HexBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (Rgb.TryParseHex(HexBox.Text, out var rgb))
            Color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        HexBox.Text = new Rgb(Color.R, Color.G, Color.B).ToHex();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SolidColorBrush brush })
            Color = brush.Color;
    }
}
