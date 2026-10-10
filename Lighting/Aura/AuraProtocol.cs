using System.IO;

namespace SysLens.Lighting.Aura;

/// <summary>Messages on the pipes between SysLens and its Aura host process.</summary>
internal static class AuraProtocol
{
    /// <summary>Followed by the host's input and output pipe handles.</summary>
    public const string HostArgument = "--aura-host";

    // SysLens to host. Every command but Frame gets exactly one reply.
    public const byte Enumerate = 1;
    public const byte TakeControl = 2;
    public const byte Release = 3;
    public const byte Frame = 4;

    // Host to SysLens.
    public const byte Devices = 1;
    public const byte Done = 2;
    public const byte Failed = 3;

    public static void WriteDevices(BinaryWriter writer, IReadOnlyList<AuraDeviceInfo> devices)
    {
        writer.Write(Devices);
        writer.Write(devices.Count);
        foreach (var device in devices)
        {
            writer.Write(device.Name);
            writer.Write(device.Type);
            writer.Write(device.LightCount);
            writer.Write(device.Width);
            writer.Write(device.Height);
        }
    }

    /// <summary>Reads the body of a <see cref="Devices"/> reply, after its message byte.</summary>
    public static List<AuraDeviceInfo> ReadDevices(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var devices = new List<AuraDeviceInfo>(count);
        for (var i = 0; i < count; i++)
            devices.Add(new AuraDeviceInfo(reader.ReadString(), reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        return devices;
    }
}

/// <summary>One device as the Aura SDK reports it. Its index in the reply list addresses it in frames.</summary>
/// <param name="Type">The SDK's device type code, see <see cref="AuraDeviceTypes"/>.</param>
/// <param name="Width">LED columns; Width × Height equals LightCount for a grid such as a keyboard.</param>
internal sealed record AuraDeviceInfo(string Name, uint Type, int LightCount, int Width, int Height);

/// <summary>An Aura command failed, or the host process is gone; the message is shown to the user.</summary>
internal sealed class AuraException(string message) : Exception(message);
