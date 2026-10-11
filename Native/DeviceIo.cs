using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SysLens.Native;

/// <summary>Opens devices such as \\.\PhysicalDrive0 and sends them control codes.</summary>
internal static unsafe partial class DeviceIo
{
    /// <summary>GENERIC_READ | GENERIC_WRITE: what a SCSI miniport command needs; only an administrator gets it.</summary>
    public const uint ReadWrite = 0xC0000000;

    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    /// <summary>
    /// Opens a device without reading or writing it. Access 0 is enough for the control codes any user may send, and
    /// neither spins up a sleeping drive nor keeps another app from opening it.
    /// </summary>
    public static SafeFileHandle Open(string path, uint access = 0) =>
        CreateFileW(path, access, FileShareReadWrite, 0, OpenExisting, 0, 0);

    /// <summary>Sends a control code that takes no input and returns a <typeparamref name="T"/>.</summary>
    public static bool Query<T>(SafeFileHandle device, uint code, out T result) where T : unmanaged
    {
        T value = default;
        var done = DeviceIoControl(device, code, null, 0, &value, (uint)sizeof(T), out _, 0);
        result = value;
        return done;
    }

    /// <summary>
    /// Sends the first <paramref name="inputLength"/> bytes of <paramref name="buffer"/> and fills the buffer with what
    /// the device returns.
    /// </summary>
    public static bool Control(SafeFileHandle device, uint code, Span<byte> buffer, int inputLength = 0)
    {
        fixed (byte* data = buffer)
        {
            return DeviceIoControl(device, code, inputLength > 0 ? data : null, (uint)inputLength, data, (uint)buffer.Length,
                out _, 0);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition,
        uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, void* input, uint inputSize, void* output,
        uint outputSize, out uint returned, nint overlapped);
}
