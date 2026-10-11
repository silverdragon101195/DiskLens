using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SysLens.Native;

/// <summary>A RAID volume as its controller's driver reports it.</summary>
/// <param name="Name">
/// The name Intel RST gives the volume, which it also reports as the volume's serial number; empty when the driver
/// gives none.
/// </param>
/// <param name="Capacity">In bytes.</param>
/// <param name="Members">The serial numbers of the drives the volume is made of.</param>
internal sealed record RaidSet(string Name, ulong Capacity, IReadOnlyList<string> Members);

/// <summary>
/// Reads RAID volumes through CSMI, the Common Storage Management Interface, which Intel Rapid Storage Technology
/// implements. Needs administrator rights.
/// </summary>
internal static class CsmiRaid
{
    private const uint IoctlScsiMiniport = 0x4D008;
    private const uint GetRaidInfo = 10;
    private const uint GetRaidConfig = 11;
    private const uint TimeoutSeconds = 60;

    // SRB_IO_CONTROL, which leads every miniport command: its own size, the signature, the timeout, the control code,
    // the return code and the size of what follows.
    private const int HeaderSize = 28;

    // CSMI_SAS_RAID_INFO, led by the number of RAID sets and the most drives a set holds.
    private const int RaidInfoSize = 100;

    // CSMI_SAS_RAID_CONFIG up to its drives, then a CSMI_SAS_RAID_DRIVES for each drive.
    private const int ConfigSize = 36;
    private const int DriveSize = 136;

    // Bounds on what a driver reports, so a garbled answer cannot ask for huge buffers.
    private const int MaxSets = 32;
    private const int MaxDrivesPerSet = 64;

    private static readonly byte[] RaidSignature = Encoding.ASCII.GetBytes("CSMIARY\0");

    /// <summary>
    /// The RAID volumes on the controller at \\.\Scsi<paramref name="port"/>:, or none when its driver does not say.
    /// </summary>
    public static List<RaidSet> Read(int port)
    {
        using var controller = DeviceIo.Open($@"\\.\Scsi{port}:", DeviceIo.ReadWrite);
        var info = new byte[HeaderSize + RaidInfoSize];
        if (controller.IsInvalid || !Send(controller, GetRaidInfo, info))
            return [];

        var raidInfo = info.AsSpan(HeaderSize);
        var setCount = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(raidInfo), MaxSets);
        var drivesPerSet = Math.Clamp(BinaryPrimitives.ReadInt32LittleEndian(raidInfo[4..]), 1, MaxDrivesPerSet);
        var sets = new List<RaidSet>();
        for (var index = 0; index < setCount; index++)
        {
            var config = new byte[HeaderSize + ConfigSize + drivesPerSet * DriveSize];
            BinaryPrimitives.WriteInt32LittleEndian(config.AsSpan(HeaderSize), index);
            if (!Send(controller, GetRaidConfig, config))
                continue;

            // The capacity in MiB at 4 and the number of drives at 15; each drive's serial number 48 bytes into it.
            var set = config.AsSpan(HeaderSize);
            var members = new List<string>();
            for (var drive = 0; drive < Math.Min(set[15], drivesPerSet); drive++)
                members.Add(Ascii(set.Slice(ConfigSize + drive * DriveSize + 48, 40)));
            sets.Add(new RaidSet(Name(set), (ulong)BinaryPrimitives.ReadUInt32LittleEndian(set[4..]) << 20, members));
        }
        return sets;
    }

    /// <summary>
    /// Intel RST writes the volume's name, up to 16 characters, over the bytes from 16 on, where CSMI puts a data type
    /// of 0 to 2 and reserved bytes.
    /// </summary>
    private static string Name(ReadOnlySpan<byte> set) => set[16] is >= 0x20 and < 0x7F ? Ascii(set.Slice(16, 16)) : "";

    private static string Ascii(ReadOnlySpan<byte> text)
    {
        var end = text.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? text : text[..end]).Trim();
    }

    /// <summary>Sends a RAID command; <paramref name="buffer"/> holds the header, written here, then its data.</summary>
    private static bool Send(SafeFileHandle controller, uint code, byte[] buffer)
    {
        var header = buffer.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(header, HeaderSize);
        RaidSignature.CopyTo(header[4..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], TimeoutSeconds);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], code);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], buffer.Length - HeaderSize);
        // The driver sets the return code to 0, CSMI_SAS_STATUS_SUCCESS, when it did what was asked.
        return DeviceIo.Control(controller, IoctlScsiMiniport, buffer, buffer.Length)
               && BinaryPrimitives.ReadUInt32LittleEndian(header[20..]) == 0;
    }
}
