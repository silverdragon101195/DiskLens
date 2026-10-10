using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using WinColor = Windows.UI.Color;

namespace SysLens.Lighting.LampArrays;

/// <summary>
/// A Windows Dynamic Lighting (HID LampArray) device. Windows lets an unpackaged app drive it only while one of
/// the app's windows is in the foreground; otherwise Windows' own ambient effect runs.
/// </summary>
internal sealed class LampArrayDevice : LightDevice
{
    public const string SourceName = "Dynamic Lighting";

    private readonly LampArray _lamps;
    private readonly int[] _indices;
    private readonly WinColor[] _colors;

    private LampArrayDevice(DeviceInformation info, LampArray lamps)
        : base(KeyOf(info.Id), info.Name, SourceName, Describe(lamps.LampArrayKind), GlyphOf(lamps.LampArrayKind), PositionsOf(lamps))
    {
        _lamps = lamps;
        _indices = [.. Enumerable.Range(0, lamps.LampCount)];
        _colors = new WinColor[lamps.LampCount];
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            _lamps.AvailabilityChanged += (_, _) => OnControlChanged();
    }

    public override bool CanPush => _lamps.IsConnected && _lamps.IsEnabled && IsAvailable;

    public override string ControlText =>
        !_lamps.IsConnected ? "Disconnected"
        : !_lamps.IsEnabled ? "Off in Dynamic Lighting settings"
        : !IsAvailable ? "Windows · SysLens is not in front"
        : "SysLens";

    /// <summary>False while another app, or Windows' ambient effect, has the device.</summary>
    private bool IsAvailable => !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) || _lamps.IsAvailable;

    public static string KeyOf(string deviceId) => $"lamparray:{deviceId}";

    /// <returns>Null when the device is gone or has no lamps.</returns>
    public static async Task<LampArrayDevice?> OpenAsync(DeviceInformation info)
    {
        try
        {
            var lamps = await LampArray.FromIdAsync(info.Id);
            return lamps is { LampCount: > 0 } ? new LampArrayDevice(info, lamps) : null;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public override void Push(Rgb[] frame)
    {
        for (var i = 0; i < _colors.Length; i++)
            _colors[i] = new WinColor { A = 255, R = frame[i].R, G = frame[i].G, B = frame[i].B };
        try
        {
            _lamps.SetColorsForIndices(_colors, _indices);
        }
        catch (COMException)
        {
            // Unplugged mid-frame; the device watcher removes it.
        }
    }

    /// <summary>Position along the device's width, from each lamp's reported position.</summary>
    private static float[] PositionsOf(LampArray lamps)
    {
        var width = lamps.BoundingBox.X;
        if (width <= 0)
            return EvenPositions(lamps.LampCount);
        return [.. Enumerable.Range(0, lamps.LampCount).Select(i => Math.Clamp(lamps.GetLampInfo(i).Position.X / width, 0f, 1f))];
    }

    private static string Describe(LampArrayKind kind) => kind switch
    {
        LampArrayKind.Undefined => "Lighting device",
        LampArrayKind.GameController => "Game controller",
        _ => kind.ToString(),
    };

    private static string GlyphOf(LampArrayKind kind) => kind switch
    {
        LampArrayKind.Keyboard => "\uE765",
        LampArrayKind.Mouse => "\uE962",
        LampArrayKind.GameController => "\uE7FC",
        LampArrayKind.Headset => "\uE7F6",
        LampArrayKind.Microphone => "\uE720",
        LampArrayKind.Speaker => "\uE7F5",
        _ => "\uE950",
    };
}
