using System.Runtime.InteropServices;
using SysLens.Usb;

namespace SysLens.Native;

/// <summary>Reads USB device nodes, present and absent, from the Plug and Play database through SetupAPI.</summary>
internal static unsafe partial class UsbDevices
{
    private const uint DigcfAllClasses = 0x00000004;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorInsufficientBuffer = 122;

    private const uint DevpropTypeUint32 = 0x00000007;
    private const uint DevpropTypeBoolean = 0x00000011;
    private const uint DevpropTypeFiletime = 0x00000010;
    private const uint DevpropTypeString = 0x00000012;
    private const uint DevpropTypeStringList = 0x00002012;

    private static readonly Guid DeviceGuid = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private static readonly Guid BusGuid = new("540b947e-8b40-45bc-a8a2-6a0b894cbda2");
    private static readonly Guid RelationsGuid = new("4340a6c5-93fa-4706-972c-7b648008a5a7");
    private static readonly Guid DatesGuid = new("83da6326-97a6-4088-9453-a1923f573b29");

    private static readonly DevPropKey DeviceDesc = new(DeviceGuid, 2);
    private static readonly DevPropKey CompatibleIds = new(DeviceGuid, 4);
    private static readonly DevPropKey FriendlyName = new(DeviceGuid, 14);
    private static readonly DevPropKey LocationInfo = new(DeviceGuid, 15);
    private static readonly DevPropKey BusReportedDeviceDesc = new(BusGuid, 4);
    private static readonly DevPropKey IsPresent = new(BusGuid, 5);
    private static readonly DevPropKey ProblemCode = new(RelationsGuid, 3);
    private static readonly DevPropKey Parent = new(RelationsGuid, 8);
    private static readonly DevPropKey LastArrivalDate = new(DatesGuid, 102);
    private static readonly DevPropKey LastRemovalDate = new(DatesGuid, 103);

    /// <summary>
    /// Every device and root hub under the USB enumerator, including devices no longer connected.
    /// </summary>
    public static List<UsbDevice> Enumerate()
    {
        // No DIGCF_PRESENT: absent ("ghost") devices keep their properties and are wanted too.
        var set = SetupDiGetClassDevsW(null, "USB", 0, DigcfAllClasses);
        if (set == -1)
            throw new ExternalException("SetupDiGetClassDevs failed.", Marshal.GetLastPInvokeError());

        try
        {
            var devices = new List<UsbDevice>();
            var data = new SpDevInfoData { CbSize = (uint)sizeof(SpDevInfoData) };
            for (uint index = 0; SetupDiEnumDeviceInfo(set, index, ref data); index++)
            {
                if (ReadDevice(set, ref data) is { } device)
                    devices.Add(device);
            }

            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorNoMoreItems)
                throw new ExternalException("SetupDiEnumDeviceInfo failed.", error);

            return devices;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static UsbDevice? ReadDevice(nint set, ref SpDevInfoData data)
    {
        var id = ReadInstanceId(set, ref data);
        if (id is null || !IsDeviceNode(id))
            return null;

        var name = ReadString(set, ref data, BusReportedDeviceDesc)
                   ?? ReadString(set, ref data, FriendlyName)
                   ?? ReadString(set, ref data, DeviceDesc)
                   ?? id;

        return new UsbDevice(
            InstanceId: id,
            ParentId: ReadString(set, ref data, Parent),
            Name: name.Trim(),
            LocationInfo: ReadString(set, ref data, LocationInfo),
            IsPresent: ReadBoolean(set, ref data, IsPresent) ?? false,
            ProblemCode: (int)(ReadUInt32(set, ref data, ProblemCode) ?? 0),
            IsHub: IsHubNode(id, ReadStringList(set, ref data, CompatibleIds)),
            LastArrival: ReadFileTime(set, ref data, LastArrivalDate),
            LastRemoval: ReadFileTime(set, ref data, LastRemovalDate));
    }

    // A physical device has the device id VID_xxxx&PID_xxxx exactly. Anything appended (&MI_ and &IG_
    // interfaces, software children such as &LAMPARRAY) is a function of a device, not a device.
    private static bool IsDeviceNode(string id)
    {
        var parts = id.Split('\\');
        if (parts.Length < 3 || !parts[0].Equals("USB", StringComparison.OrdinalIgnoreCase))
            return false;

        var deviceId = parts[1];
        return deviceId.StartsWith("ROOT_HUB", StringComparison.OrdinalIgnoreCase)
               || (deviceId.Length == 17
                   && deviceId.StartsWith("VID_", StringComparison.OrdinalIgnoreCase)
                   && deviceId.AsSpan(8).StartsWith("&PID_", StringComparison.OrdinalIgnoreCase));
    }

    // USB 2 hubs report class 09 (USB\Class_09, USB\DevClass_09); the USB 3 hub driver adds
    // USB\USB20_HUB or USB\USB30_HUB instead.
    private static bool IsHubNode(string id, string[] compatibleIds) =>
        id.StartsWith(@"USB\ROOT_HUB", StringComparison.OrdinalIgnoreCase)
        || compatibleIds.Any(c => c.Contains("Class_09", StringComparison.OrdinalIgnoreCase)
                                  || c.EndsWith("_HUB", StringComparison.OrdinalIgnoreCase));

    private static string? ReadInstanceId(nint set, ref SpDevInfoData data)
    {
        Span<char> buffer = stackalloc char[512];
        fixed (char* chars = buffer)
        {
            if (!SetupDiGetDeviceInstanceIdW(set, ref data, chars, (uint)buffer.Length, out var length))
                return null;
            return new string(chars, 0, Math.Max(0, (int)length - 1));
        }
    }

    private static byte[]? ReadProperty(nint set, ref SpDevInfoData data, DevPropKey key, uint expectedType)
    {
        var keyCopy = key;
        SetupDiGetDevicePropertyW(set, ref data, ref keyCopy, out _, null, 0, out var size, 0);
        if (Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer || size == 0)
            return null;

        var buffer = new byte[size];
        fixed (byte* bytes = buffer)
        {
            if (!SetupDiGetDevicePropertyW(set, ref data, ref keyCopy, out var type, bytes, size, out _, 0)
                || type != expectedType)
                return null;
        }

        return buffer;
    }

    private static string? ReadString(nint set, ref SpDevInfoData data, DevPropKey key)
    {
        var bytes = ReadProperty(set, ref data, key, DevpropTypeString);
        if (bytes is null)
            return null;

        var text = MemoryMarshal.Cast<byte, char>(bytes).ToString().TrimEnd('\0');
        return text.Length > 0 ? text : null;
    }

    private static string[] ReadStringList(nint set, ref SpDevInfoData data, DevPropKey key)
    {
        var bytes = ReadProperty(set, ref data, key, DevpropTypeStringList);
        return bytes is null
            ? []
            : MemoryMarshal.Cast<byte, char>(bytes).ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static uint? ReadUInt32(nint set, ref SpDevInfoData data, DevPropKey key) =>
        ReadProperty(set, ref data, key, DevpropTypeUint32) is { Length: >= 4 } bytes ? BitConverter.ToUInt32(bytes) : null;

    // DEVPROP_BOOLEAN is one byte: 0 for false, 0xFF for true.
    private static bool? ReadBoolean(nint set, ref SpDevInfoData data, DevPropKey key) =>
        ReadProperty(set, ref data, key, DevpropTypeBoolean) is { Length: >= 1 } bytes ? bytes[0] != 0 : null;

    private static DateTime? ReadFileTime(nint set, ref SpDevInfoData data, DevPropKey key) =>
        ReadProperty(set, ref data, key, DevpropTypeFiletime) is { Length: >= 8 } bytes
            ? DateTime.FromFileTimeUtc(BitConverter.ToInt64(bytes)).ToLocalTime()
            : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DevPropKey(Guid fmtId, uint pid)
    {
        public readonly Guid FmtId = fmtId;
        public readonly uint Pid = pid;
    }

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SetupDiGetClassDevsW(Guid* classGuid, string? enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInfo(nint set, uint index, ref SpDevInfoData data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInstanceIdW(nint set, ref SpDevInfoData data, char* id, uint size, out uint required);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDevicePropertyW(nint set, ref SpDevInfoData data, ref DevPropKey key,
        out uint type, byte* buffer, uint size, out uint required, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint set);
}
