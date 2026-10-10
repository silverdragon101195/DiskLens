namespace SysLens.Lighting;

/// <summary>Software effects, rendered by SysLens frame by frame. The order is the order shown in the UI.</summary>
public enum EffectKind
{
    Off,
    Static,
    Breathing,
    Flash,
    ColorCycle,
    RainbowWave,
    Gradient,
    ColorShift,
    Comet,
    Twinkle,
}

/// <summary>Display name of an effect and the parameters it reads.</summary>
public sealed record EffectInfo(
    EffectKind Kind,
    string Name,
    string Description,
    bool UsesColor,
    bool UsesSecondColor,
    bool UsesSpeed,
    bool UsesDirection)
{
    /// <summary>Indexed by <see cref="EffectKind"/>.</summary>
    public static IReadOnlyList<EffectInfo> All { get; } =
    [
        new(EffectKind.Off, "Off", "All LEDs off.", false, false, false, false),
        new(EffectKind.Static, "Static", "One steady colour.", true, false, false, false),
        new(EffectKind.Breathing, "Breathing", "Fades the colour in and out.", true, false, true, false),
        new(EffectKind.Flash, "Flash", "Blinks the colour on and off.", true, false, true, false),
        new(EffectKind.ColorCycle, "Colour cycle", "All LEDs move through the spectrum together.", false, false, true, false),
        new(EffectKind.RainbowWave, "Rainbow wave", "A rainbow that scrolls along the LEDs.", false, false, true, true),
        new(EffectKind.Gradient, "Gradient", "A fixed blend from the first colour to the second along the LEDs.", true, true, false, true),
        new(EffectKind.ColorShift, "Colour shift", "Fades back and forth between the two colours.", true, true, true, false),
        new(EffectKind.Comet, "Comet", "A bright dot with a fading tail runs along the LEDs.", true, false, true, true),
        new(EffectKind.Twinkle, "Twinkle", "LEDs light up at random in either colour, like stars.", true, true, true, false),
    ];

    public static EffectInfo Of(EffectKind kind) => All[(int)kind];
}

/// <summary>
/// What one device shows. Immutable, so the render thread can read it while the UI swaps in a new one.
/// </summary>
/// <param name="Speed">1 (slowest) to 10.</param>
/// <param name="Brightness">0 to 100 percent.</param>
/// <param name="Reverse">Runs spatial effects from the last LED to the first.</param>
public sealed record EffectSettings(EffectKind Kind, Rgb Color, Rgb SecondColor, int Speed, int Brightness, bool Reverse)
{
    public const int MinSpeed = 1;
    public const int MaxSpeed = 10;

    public static EffectSettings Default { get; } =
        new(EffectKind.RainbowWave, new Rgb(0x00, 0x6E, 0xFF), new Rgb(0xFF, 0x00, 0x80), 5, 100, false);
}
