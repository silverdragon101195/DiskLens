using System.Windows;
using System.Windows.Interop;

namespace SysLens.Native;

internal static class DeviceChanges
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDevNodesChanged = 0x0007;

    /// <summary>
    /// Calls <paramref name="onChanged"/> whenever a device node is added or removed anywhere in the
    /// system. Windows broadcasts DBT_DEVNODES_CHANGED to every top-level window without registration,
    /// often several times for one plug or unplug, so callers should debounce. Call once the HWND exists.
    /// </summary>
    public static void Watch(Window window, Action onChanged)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook((nint _, int message, nint wParam, nint _, ref bool _) =>
        {
            if (message == WmDeviceChange && wParam == DbtDevNodesChanged)
                onChanged();
            return 0;
        });
    }
}
