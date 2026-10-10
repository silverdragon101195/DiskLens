using System.Windows.Media;
using SysLens.Lighting;

namespace SysLens.ViewModels;

/// <summary>The effect controls for one device, or for All devices.</summary>
public sealed class EffectEditor : ObservableObject
{
    private EffectInfo _effect = EffectInfo.Of(EffectKind.Static);
    private Color _color;
    private Color _secondColor;
    private int _speed;
    private int _brightness;
    private bool _reverse;
    private bool _isEditingSecondColor;

    public EffectEditor(EffectSettings settings) => Load(settings);

    /// <summary>Raised when the user changes any setting, but not by <see cref="Load"/>.</summary>
    public event Action? Changed;

    public EffectInfo Effect
    {
        get => _effect;
        set
        {
            if (!Set(ref _effect, value))
                return;
            if (!value.UsesSecondColor)
                IsEditingSecondColor = false;
            OnPropertyChanged(nameof(ShowColors));
            Changed?.Invoke();
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (!Set(ref _color, value))
                return;
            OnPropertyChanged(nameof(ColorBrush));
            OnPropertyChanged(nameof(EditingColor));
            Changed?.Invoke();
        }
    }

    public Color SecondColor
    {
        get => _secondColor;
        set
        {
            if (!Set(ref _secondColor, value))
                return;
            OnPropertyChanged(nameof(SecondColorBrush));
            OnPropertyChanged(nameof(EditingColor));
            Changed?.Invoke();
        }
    }

    /// <summary>The colour the picker edits: the second one while its slot is selected.</summary>
    public Color EditingColor
    {
        get => IsEditingSecondColor ? SecondColor : Color;
        set
        {
            if (IsEditingSecondColor)
                SecondColor = value;
            else
                Color = value;
        }
    }

    public bool IsEditingSecondColor
    {
        get => _isEditingSecondColor;
        set
        {
            if (!Set(ref _isEditingSecondColor, value))
                return;
            OnPropertyChanged(nameof(IsEditingFirstColor));
            OnPropertyChanged(nameof(EditingColor));
        }
    }

    public bool IsEditingFirstColor
    {
        get => !IsEditingSecondColor;
        set
        {
            if (value)
                IsEditingSecondColor = false;
        }
    }

    public int Speed
    {
        get => _speed;
        set
        {
            if (Set(ref _speed, Math.Clamp(value, EffectSettings.MinSpeed, EffectSettings.MaxSpeed)))
                Changed?.Invoke();
        }
    }

    public int Brightness
    {
        get => _brightness;
        set
        {
            if (Set(ref _brightness, Math.Clamp(value, 0, 100)))
                Changed?.Invoke();
        }
    }

    public bool Reverse
    {
        get => _reverse;
        set
        {
            if (Set(ref _reverse, value))
                Changed?.Invoke();
        }
    }

    public bool ShowColors => Effect.UsesColor || Effect.UsesSecondColor;

    public Brush ColorBrush => Frozen(Color);

    public Brush SecondColorBrush => Frozen(SecondColor);

    public EffectSettings Settings =>
        new(Effect.Kind, ToRgb(Color), ToRgb(SecondColor), Speed, Brightness, Reverse);

    public void Load(EffectSettings settings)
    {
        _effect = EffectInfo.Of(settings.Kind);
        _color = ToColor(settings.Color);
        _secondColor = ToColor(settings.SecondColor);
        _speed = settings.Speed;
        _brightness = settings.Brightness;
        _reverse = settings.Reverse;
        if (!_effect.UsesSecondColor)
            _isEditingSecondColor = false;
        OnPropertyChanged(string.Empty);
    }

    public static Rgb ToRgb(Color color) => new(color.R, color.G, color.B);

    public static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
