using System.Runtime.InteropServices;

namespace SysLens.Native;

/// <summary>Physical memory and commit charge, in bytes.</summary>
internal readonly record struct MemoryUsage(ulong PhysicalTotal, ulong PhysicalAvailable, ulong CommitLimit, ulong CommitAvailable);

internal static partial class MemoryStatus
{
    /// <summary>The current memory use, or null when Windows does not report it.</summary>
    public static MemoryUsage? Read()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryUsage(status.TotalPhys, status.AvailPhys, status.TotalPageFile, status.AvailPageFile)
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
