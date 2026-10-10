namespace SysLens.Lighting;

/// <summary>A set of LEDs SysLens can drive, from one lighting backend.</summary>
public abstract class LightDevice(string key, string name, string source, string kind, string glyph, float[] positions)
{
    /// <summary>Raised, on any thread, when <see cref="CanPush"/> or <see cref="ControlText"/> may have changed.</summary>
    public event EventHandler? ControlChanged;

    /// <summary>Stays the same when the device is found again, so its settings can be restored.</summary>
    public string Key { get; } = key;

    public string Name { get; } = name;

    /// <summary>The backend, such as "ASUS Aura".</summary>
    public string Source { get; } = source;

    /// <summary>What the device is, such as "Graphics card".</summary>
    public string Kind { get; } = kind;

    /// <summary>A Segoe Fluent Icons code point.</summary>
    public string Glyph { get; } = glyph;

    /// <summary>Where each LED sits along the device, 0 to 1, in LED order.</summary>
    public float[] Positions { get; } = positions;

    public int LedCount => Positions.Length;

    /// <summary>True while frames passed to <see cref="Push"/> reach the LEDs.</summary>
    public abstract bool CanPush { get; }

    /// <summary>Who drives the LEDs now, for display.</summary>
    public abstract string ControlText { get; }

    /// <summary>Sends one frame, <see cref="LedCount"/> colours long. Called on the render thread only; never throws.</summary>
    public abstract void Push(Rgb[] frame);

    protected void OnControlChanged() => ControlChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>LEDs spread evenly from 0 to 1, or a single LED at 0.</summary>
    public static float[] EvenPositions(int count) =>
        [.. Enumerable.Range(0, count).Select(i => count > 1 ? (float)i / (count - 1) : 0f)];
}
