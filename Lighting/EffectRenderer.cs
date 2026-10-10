namespace SysLens.Lighting;

/// <summary>
/// Computes LED colours for an effect at a point in time. Pure: the same settings, positions and time always
/// give the same frame, so every device and the previews stay in step on one clock.
/// </summary>
public static class EffectRenderer
{
    private const double CometTail = 0.3;

    /// <param name="positions">Where each LED sits along the device, 0 to 1.</param>
    /// <param name="seconds">Time on the shared effect clock.</param>
    public static void Render(EffectSettings settings, ReadOnlySpan<float> positions, double seconds, Span<Rgb> frame)
    {
        // 0 at the slowest speed, 1 at the fastest.
        var speed = (double)(settings.Speed - EffectSettings.MinSpeed) / (EffectSettings.MaxSpeed - EffectSettings.MinSpeed);
        var brightness = Math.Clamp(settings.Brightness, 0, 100) / 100.0;

        for (var i = 0; i < positions.Length; i++)
        {
            var position = settings.Reverse ? 1 - positions[i] : positions[i];
            frame[i] = Pixel(settings, i, position, seconds, speed).Scale(brightness);
        }
    }

    private static Rgb Pixel(EffectSettings s, int index, double position, double seconds, double speed)
    {
        switch (s.Kind)
        {
            case EffectKind.Static:
                return s.Color;

            case EffectKind.Breathing:
                return s.Color.Scale(Wave(seconds / Lerp(6, 1, speed)));

            case EffectKind.Flash:
                return Fraction(seconds / Lerp(2, 0.2, speed)) < 0.3 ? s.Color : default;

            case EffectKind.ColorCycle:
                return Rgb.FromHsv(seconds * Lerp(0.02, 0.5, speed), 1, 1);

            case EffectKind.RainbowWave:
                return Rgb.FromHsv(position - seconds * Lerp(0.05, 1, speed), 1, 1);

            case EffectKind.Gradient:
                return Rgb.Lerp(s.Color, s.SecondColor, position);

            case EffectKind.ColorShift:
                return Rgb.Lerp(s.Color, s.SecondColor, Wave(seconds / Lerp(8, 1.2, speed)));

            case EffectKind.Comet:
            {
                var head = Fraction(seconds * Lerp(0.1, 1.5, speed));
                var behind = Fraction(head - position);
                return behind < CometTail ? s.Color.Scale(Math.Pow(1 - behind / CometTail, 2)) : default;
            }

            case EffectKind.Twinkle:
            {
                // Each LED gets its own phase; each cycle it lights or stays dark, in one of the two colours, at random.
                var phase = seconds / Lerp(4, 0.8, speed) + Hash(index, 0);
                var cycle = (int)Math.Floor(phase);
                if (Hash(index, cycle) > 0.55)
                    return default;
                var level = Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * Fraction(phase))), 3);
                return (Hash(cycle, index) < 0.5 ? s.Color : s.SecondColor).Scale(level);
            }

            default:
                return default;
        }
    }

    /// <summary>0 at whole cycles, 1 half way between, easing in and out.</summary>
    private static double Wave(double cycles) => 0.5 - 0.5 * Math.Cos(2 * Math.PI * cycles);

    private static double Fraction(double value) => value - Math.Floor(value);

    private static double Lerp(double slowest, double fastest, double speed) => slowest + (fastest - slowest) * speed;

    /// <summary>A stable pseudo-random number in [0, 1) for a pair of integers.</summary>
    private static double Hash(int a, int b)
    {
        var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        return (h & 0xFFFFFF) / (double)0x1000000;
    }
}
