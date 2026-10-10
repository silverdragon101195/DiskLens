namespace SysLens.Lighting.Aura;

/// <summary>An Aura SDK device, driven through the host process.</summary>
internal sealed class AuraDevice : LightDevice
{
    public const string SourceName = "ASUS Aura";

    private readonly AuraHostClient _host;
    private readonly int _index;

    /// <param name="occurrence">Counts earlier devices with the same type and name, which the SDK does report.</param>
    public AuraDevice(AuraHostClient host, int index, AuraDeviceInfo info, int occurrence)
        : base(KeyOf(info, occurrence), info.Name, SourceName, AuraDeviceTypes.Describe(info.Type),
            AuraDeviceTypes.Glyph(info.Type), PositionsOf(info))
    {
        _host = host;
        _index = index;
    }

    public override bool CanPush => _host.InControl;

    public override string ControlText =>
        !_host.IsAlive ? "Aura host stopped"
        : _host.InControl ? "SysLens"
        : "Armoury Crate";

    public override void Push(Rgb[] frame) => _host.SendFrame(_index, frame);

    public static string KeyOf(AuraDeviceInfo info, int occurrence) => $"aura:{info.Type:X8}:{info.Name}:{occurrence}";

    /// <summary>A grid, such as a keyboard, is laid out row by row: position runs across the columns.</summary>
    private static float[] PositionsOf(AuraDeviceInfo info)
    {
        if (info.Width > 1 && info.Height > 1 && info.Width * info.Height == info.LightCount)
            return [.. Enumerable.Range(0, info.LightCount).Select(i => (float)(i % info.Width) / (info.Width - 1))];
        return EvenPositions(info.LightCount);
    }
}
