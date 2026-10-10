using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SysLens.Lighting;

namespace SysLens.ViewModels;

/// <summary>A row in the RGB list: one device, or All devices, whose settings every following device shows.</summary>
public sealed class RgbTargetItem : ObservableObject
{
    private const int AllPreviewLeds = 24;

    // The preview draws each LED 3 pixels wide with a 1 pixel gap in the window colour.
    private const int LedPixels = 3;
    private const int GapColor = 0x1E1F22;

    private readonly int[] _pixels;
    private bool _followsAll;
    private string _subtitle = "";
    private string _controlText = "";
    private bool _isControlled;
    private string _effectText = "";

    private RgbTargetItem(LightDevice? device, float[] positions, EffectSettings settings, bool followsAll)
    {
        Device = device;
        Editor = new EffectEditor(settings);
        Target = new LightTarget(positions, device, settings);
        _followsAll = followsAll;

        _pixels = new int[positions.Length * (LedPixels + 1) - 1];
        Array.Fill(_pixels, GapColor);
        Preview = new WriteableBitmap(_pixels.Length, 1, 96, 96, PixelFormats.Bgr32, null);
    }

    public static RgbTargetItem ForAll(EffectSettings settings) =>
        new(null, LightDevice.EvenPositions(AllPreviewLeds), settings, false);

    public static RgbTargetItem ForDevice(LightDevice device, EffectSettings settings, bool followsAll) =>
        new(device, device.Positions, settings, followsAll);

    /// <summary>Raised when the user ticks or clears <see cref="FollowsAll"/>.</summary>
    public event Action<RgbTargetItem>? FollowsAllChanged;

    /// <summary>Null for All devices.</summary>
    public LightDevice? Device { get; }

    public EffectEditor Editor { get; }

    public LightTarget Target { get; }

    public WriteableBitmap Preview { get; }

    public bool IsAll => Device is null;

    public bool IsDevice => !IsAll;

    public string Key => Device?.Key ?? "";

    public string Name => Device?.Name ?? "All devices";

    /// <summary>E790: colour palette.</summary>
    public string Glyph => Device?.Glyph ?? "\uE790";

    /// <summary>For a device: shows the All devices settings instead of its own.</summary>
    public bool FollowsAll
    {
        get => _followsAll;
        set
        {
            if (!Set(ref _followsAll, value))
                return;
            OnPropertyChanged(nameof(ShowsOwnEditor));
            FollowsAllChanged?.Invoke(this);
        }
    }

    public bool ShowsOwnEditor => IsAll || !FollowsAll;

    public string Subtitle
    {
        get => _subtitle;
        set => Set(ref _subtitle, value);
    }

    public string ControlText
    {
        get => _controlText;
        set => Set(ref _controlText, value);
    }

    /// <summary>True when the LEDs show what the preview shows.</summary>
    public bool IsControlled
    {
        get => _isControlled;
        set => Set(ref _isControlled, value);
    }

    public string EffectText
    {
        get => _effectText;
        set => Set(ref _effectText, value);
    }

    /// <summary>Draws the latest rendered frame. UI thread only.</summary>
    public void UpdatePreview()
    {
        if (Target.LatestFrame is not { } frame)
            return;

        for (var i = 0; i < frame.Length; i++)
        {
            var pixel = frame[i].R << 16 | frame[i].G << 8 | frame[i].B;
            Array.Fill(_pixels, pixel, i * (LedPixels + 1), LedPixels);
        }
        Preview.WritePixels(new Int32Rect(0, 0, _pixels.Length, 1), _pixels, _pixels.Length * sizeof(int), 0);
    }
}
