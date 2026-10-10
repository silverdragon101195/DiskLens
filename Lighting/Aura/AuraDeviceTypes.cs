namespace SysLens.Lighting.Aura;

/// <summary>
/// Aura SDK device type codes. The high 16 bits are the family; the next bits a variant, such as a motherboard's
/// addressable header. Unlisted variants fall back to their family.
/// </summary>
internal static class AuraDeviceTypes
{
    private const string ChipGlyph = "\uE950";

    private static readonly Dictionary<uint, (string Kind, string Glyph)> Types = new()
    {
        [0x00010000] = ("Motherboard", ChipGlyph),
        [0x00011000] = ("Motherboard LED header", ChipGlyph),
        [0x00012000] = ("All-in-one PC", "\uE7F4"),
        [0x00015000] = ("Wallpaper", "\uE8B9"),
        [0x00020000] = ("Graphics card", ChipGlyph),
        [0x00030000] = ("Display", "\uE7F4"),
        [0x00040000] = ("Headset", "\uE7F6"),
        [0x00050000] = ("Microphone", "\uE720"),
        [0x00060000] = ("External drive", "\uEDA2"),
        [0x00061000] = ("External optical drive", "\uEDA2"),
        [0x00070000] = ("Memory", ChipGlyph),
        [0x00080000] = ("Keyboard", "\uE765"),
        [0x00081000] = ("Laptop keyboard", "\uE765"),
        [0x00090000] = ("Mouse", "\uE962"),
        [0x000A0000] = ("Chassis", ChipGlyph),
        [0x000B0000] = ("Projector", "\uE7F4"),
    };

    public static string Describe(uint type) => Lookup(type)?.Kind ?? $"Aura device 0x{type:X8}";

    public static string Glyph(uint type) => Lookup(type)?.Glyph ?? ChipGlyph;

    private static (string Kind, string Glyph)? Lookup(uint type) =>
        Types.TryGetValue(type, out var exact) ? exact
        : Types.TryGetValue(type & 0xFFFF0000, out var family) ? family
        : null;
}
