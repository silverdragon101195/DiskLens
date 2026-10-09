using System.Collections.ObjectModel;
using System.Globalization;
using SysLens.Usb;

namespace SysLens.ViewModels;

public enum UsbSeverity
{
    Normal,
    Caution,
    Critical,
}

/// <summary>A device or hub in the USB tree, or an absent device in the history list.</summary>
public sealed class UsbDeviceItem : ObservableObject
{
    /// <summary>USB allows at most five hubs between the root hub and a device.</summary>
    public const int MaxExternalHubs = 5;

    /// <summary>A device removed within this time of arriving most likely failed to enumerate.</summary>
    public static readonly TimeSpan QuickDropWindow = TimeSpan.FromSeconds(10);

    private const int CodeDisabled = 22;

    private ObservableCollection<UsbDeviceItem> _children = [];
    private bool _isExpanded;

    public UsbDeviceItem(UsbDevice device, UsbDeviceItem? parent)
    {
        Device = device;
        Parent = parent;
        HubDepth = (parent?.HubDepth ?? 0) + (device.IsHub && !device.IsRootHub ? 1 : 0);
        IsChainKnown = parent?.IsChainKnown ?? device.IsRootHub;
    }

    public UsbDevice Device { get; }

    /// <summary>The hub this device is connected to, present or not; null for a root hub.</summary>
    public UsbDeviceItem? Parent { get; }

    /// <summary>External hubs from the root hub down to this node, counting this node when it is a hub.</summary>
    public int HubDepth { get; }

    /// <summary>
    /// False when the parent walk ends before a root hub, because Windows has purged a disconnected
    /// ancestor from the Plug and Play database; <see cref="HubDepth"/> then counts only the known part.
    /// </summary>
    public bool IsChainKnown { get; }

    public string Name => Device.Name;
    public string VidPid => Device.VidPid;
    public string InstanceId => Device.InstanceId;
    public string Location => Device.LocationInfo ?? "";
    public bool IsHub => Device.IsHub;
    public bool IsRootHub => Device.IsRootHub;
    public bool IsPresent => Device.IsPresent;
    public bool HasProblem => IsPresent && Device.ProblemCode != 0;

    public string StatusText => !IsPresent ? "Unknown · not connected"
        : Device.ProblemCode == 0 ? "OK"
        : Device.ProblemCode == CodeDisabled ? "Disabled · code 22"
        : $"Error · code {Device.ProblemCode}: {ProblemCodes.Describe(Device.ProblemCode)}";

    public string DepthText => !IsChainKnown ? "hub chain unknown"
        : HubDepth == 0 ? ""
        : IsHub ? $"hub tier {HubDepth}"
        : $"behind {HubDepth} hub{(HubDepth == 1 ? "" : "s")}";

    /// <summary>Caution at the fourth hub tier, critical at the fifth and beyond, where devices start to drop.</summary>
    public UsbSeverity DepthSeverity =>
        !IsChainKnown ? UsbSeverity.Normal
        : HubDepth >= MaxExternalHubs ? UsbSeverity.Critical
        : HubDepth == MaxExternalHubs - 1 ? UsbSeverity.Caution
        : UsbSeverity.Normal;

    public UsbSeverity StatusSeverity =>
        IsQuickDrop || (HasProblem && Device.ProblemCode != CodeDisabled) ? UsbSeverity.Critical
        : HasProblem ? UsbSeverity.Caution
        : UsbSeverity.Normal;

    public UsbSeverity Severity => (UsbSeverity)Math.Max((int)DepthSeverity, (int)StatusSeverity);

    /// <summary>How long after its last arrival the device was removed, when the removal is the later event.</summary>
    public TimeSpan? DroppedAfter =>
        Device is { LastArrival: { } arrival, LastRemoval: { } removal } && removal >= arrival ? removal - arrival : null;

    public bool IsQuickDrop => !IsPresent && DroppedAfter <= QuickDropWindow;

    /// <summary>The most recent arrival or removal, used to order the history.</summary>
    public DateTime? LastSeen =>
        Device.LastRemoval > Device.LastArrival || Device.LastArrival is null ? Device.LastRemoval : Device.LastArrival;

    public string HistoryText
    {
        get
        {
            var parts = new List<string>();
            if (Device.LastArrival is { } arrival)
                parts.Add($"arrived {Format(arrival)}");
            if (Device.LastRemoval is { } removal)
                parts.Add($"removed {Format(removal)}");
            if (IsQuickDrop)
                parts.Add($"dropped {DroppedAfter!.Value.TotalSeconds:0.#} s after arrival: likely failed to enumerate");
            return parts.Count > 0 ? string.Join(" · ", parts) : "no arrival or removal recorded";
        }
    }

    /// <summary>Hubs from the root hub down to, but not including, this node.</summary>
    public IReadOnlyList<UsbDeviceItem> HubChain
    {
        get
        {
            var chain = new List<UsbDeviceItem>();
            for (var hub = Parent; hub is not null; hub = hub.Parent)
                chain.Add(hub);
            chain.Reverse();
            return chain;
        }
    }

    public string HubChainText => string.Join("  ›  ", HubChain.Select(h => h.Name));

    /// <summary>Port number on the parent hub from <c>Port_#0003.Hub_#0005</c>, for ordering siblings.</summary>
    public int Port
    {
        get
        {
            const string prefix = "Port_#";
            var location = Location;
            return location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(location.AsSpan(prefix.Length, Math.Min(4, location.Length - prefix.Length)),
                       NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                ? port
                : int.MaxValue;
        }
    }

    public ObservableCollection<UsbDeviceItem> Children
    {
        get => _children;
        set => Set(ref _children, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    private static string Format(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
