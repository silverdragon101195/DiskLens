using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DiskLens.Native;

internal static partial class Privileges
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const int ErrorNotAllAssigned = 1300;

    public const string Backup = "SeBackupPrivilege";

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Enables a privilege the process token holds but has disabled.
    /// With <see cref="Backup"/> enabled, directory handles opened with backup semantics
    /// bypass ACL checks, so folders denied even to administrators can be listed.
    /// </summary>
    public static bool TryEnable(string privilege)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            return false;

        try
        {
            if (!LookupPrivilegeValueW(null, privilege, out var luid))
                return false;

            var state = new TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = SePrivilegeEnabled };
            if (!AdjustTokenPrivileges(token, false, ref state, 0, 0, 0))
                return false;

            // AdjustTokenPrivileges succeeds even when the token lacks the privilege.
            return Marshal.GetLastPInvokeError() != ErrorNotAllAssigned;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint desiredAccess, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValueW(string? systemName, string name, out Luid luid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustTokenPrivileges(
        nint token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll,
        ref TokenPrivileges newState,
        uint bufferLength,
        nint previousState,
        nint returnLength);
}
