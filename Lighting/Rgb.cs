using System.Globalization;

namespace SysLens.Lighting;

/// <summary>One LED colour, 8 bits per channel.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <param name="hue">0 to 1 around the colour wheel, starting and ending at red.</param>
    /// <param name="saturation">0 to 1.</param>
    /// <param name="value">0 to 1.</param>
    public static Rgb FromHsv(double hue, double saturation, double value)
    {
        hue = (hue - Math.Floor(hue)) * 6;
        var sector = (int)hue % 6;
        var f = hue - Math.Floor(hue);
        var p = value * (1 - saturation);
        var q = value * (1 - saturation * f);
        var t = value * (1 - saturation * (1 - f));
        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };
        return new Rgb(ToByte(r), ToByte(g), ToByte(b));
    }

    /// <summary>Hue, saturation and value, each 0 to 1. Hue is 0 for greys, which have none.</summary>
    public (double Hue, double Saturation, double Value) ToHsv()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        var hue = delta == 0 ? 0
            : max == r ? (g - b) / delta / 6
            : max == g ? ((b - r) / delta + 2) / 6
            : ((r - g) / delta + 4) / 6;
        return (hue < 0 ? hue + 1 : hue, max == 0 ? 0 : delta / max, max);
    }

    public Rgb Scale(double factor) => new(ToByte(R * factor / 255), ToByte(G * factor / 255), ToByte(B * factor / 255));

    public static Rgb Lerp(Rgb from, Rgb to, double t) => new(
        ToByte((from.R + (to.R - from.R) * t) / 255),
        ToByte((from.G + (to.G - from.G) * t) / 255),
        ToByte((from.B + (to.B - from.B) * t) / 255));

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Accepts <c>#RRGGBB</c> or <c>RRGGBB</c>.</summary>
    public static bool TryParseHex(string text, out Rgb rgb)
    {
        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            rgb = new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
            return true;
        }

        rgb = default;
        return false;
    }

    private static byte ToByte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255);
}
