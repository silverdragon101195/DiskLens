using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SysLens.Native;
using SysLens.Usb;

namespace SysLens.ViewModels;

/// <summary>
/// The USB tree, rooted at the root hubs, and the history of devices no longer connected. The tree holds
/// connected devices, plus disconnected ones under their last parent when <see cref="ShowDisconnected"/> is set.
/// </summary>
public sealed class UsbViewModel : ObservableObject
{
    // Windows sends a burst of device-change messages for one plug or unplug.
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(750);

    // Guards the parent walk against a corrupt PnP database whose parent links form a loop.
    private const int MaxChainLength = 32;

    private readonly DispatcherTimer _debounce;
    private string _status = "";
    private string _summary = "";
    private bool _isRefreshing;
    private bool _refreshAgain;
    private bool _showDisconnected;
    private List<UsbDeviceItem> _items = [];

    public UsbViewModel()
    {
        _debounce = new DispatcherTimer { Interval = RefreshDelay };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RefreshAsync();
        };
    }

    public ObservableCollection<UsbDeviceItem> Roots { get; } = [];

    /// <summary>Absent devices, most recent arrival or removal first.</summary>
    public ObservableCollection<UsbDeviceItem> History { get; } = [];

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (Set(ref _isRefreshing, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsRefreshing;

    public bool ShowDisconnected
    {
        get => _showDisconnected;
        set
        {
            if (Set(ref _showDisconnected, value))
                BuildTree();
        }
    }

    /// <summary>Refreshes once the device-change messages have gone quiet for <see cref="RefreshDelay"/>.</summary>
    public void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            _refreshAgain = true;
            return;
        }

        IsRefreshing = true;
        Status = "Reading USB devices…";
        try
        {
            do
            {
                _refreshAgain = false;
                var devices = await Task.Run(UsbDevices.Enumerate);
                Apply(devices);
            } while (_refreshAgain);

            Status = $"Updated {DateTime.Now:HH:mm:ss}. Refreshes automatically when a device is plugged in or removed.";
        }
        catch (ExternalException ex)
        {
            Status = $"Could not read USB devices: {ex.Message} (error {ex.ErrorCode})";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void Apply(List<UsbDevice> devices)
    {
        var byId = devices.ToDictionary(d => d.InstanceId, StringComparer.OrdinalIgnoreCase);
        var items = new Dictionary<string, UsbDeviceItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
            Build(device, byId, items, 0);

        _items = [.. items.Values];
        BuildTree();

        History.Clear();
        foreach (var ghost in _items.Where(i => !i.IsPresent).OrderByDescending(i => i.LastSeen ?? DateTime.MinValue))
            History.Add(ghost);

        var present = _items.Where(i => i.IsPresent).ToList();

        var hubs = present.Count(i => i.IsHub && !i.IsRootHub);
        var deepest = present.Count > 0 ? present.Max(i => i.HubDepth) : 0;
        var problems = present.Count(i => i.HasProblem);
        var quickDrops = History.Count(i => i.IsQuickDrop);
        Summary = $"{present.Count(i => !i.IsRootHub)} devices · {hubs} external hubs · deepest chain {deepest} of "
                  + $"{UsbDeviceItem.MaxExternalHubs} hubs · {problems} with errors · {quickDrops} quick drops in history";
    }

    private void BuildTree()
    {
        var collapsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectCollapsed(Roots, collapsed);

        var shown = _items.Where(i => i.IsPresent || ShowDisconnected).ToList();
        var shownIds = shown.Select(i => i.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool IsShown(UsbDeviceItem? item) => item is not null && shownIds.Contains(item.InstanceId);

        var childrenOf = shown.Where(i => IsShown(i.Parent))
            .ToLookup(i => i.Parent!.InstanceId, StringComparer.OrdinalIgnoreCase);

        // Hidden items are reset too: a hub's children are what Ask AI reports as connected to it.
        foreach (var item in _items)
        {
            item.Children = new ObservableCollection<UsbDeviceItem>(Ordered(childrenOf[item.InstanceId]));
            item.IsExpanded = !collapsed.Contains(item.InstanceId);
        }

        Roots.Clear();
        // Connected root hubs first, then disconnected root hubs, then devices whose last parent is gone
        // from the Plug and Play database.
        var roots = Ordered(shown.Where(i => !IsShown(i.Parent)))
            .OrderByDescending(i => i.IsPresent)
            .ThenByDescending(i => i.IsRootHub);
        foreach (var root in roots)
            Roots.Add(root);
    }

    private static UsbDeviceItem Build(UsbDevice device, Dictionary<string, UsbDevice> byId,
        Dictionary<string, UsbDeviceItem> items, int chainLength)
    {
        if (items.TryGetValue(device.InstanceId, out var existing))
            return existing;

        var parent = chainLength < MaxChainLength && device.ParentId is { } parentId
                     && byId.TryGetValue(parentId, out var parentDevice)
            ? Build(parentDevice, byId, items, chainLength + 1)
            : null;

        var item = new UsbDeviceItem(device, parent);
        items[device.InstanceId] = item;
        return item;
    }

    private static IEnumerable<UsbDeviceItem> Ordered(IEnumerable<UsbDeviceItem> items) =>
        items.OrderBy(i => i.Port)
            .ThenByDescending(i => i.IsPresent)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase);

    private static void CollectCollapsed(IEnumerable<UsbDeviceItem> items, HashSet<string> collapsed)
    {
        foreach (var item in items)
        {
            if (!item.IsExpanded && item.Children.Count > 0)
                collapsed.Add(item.InstanceId);
            CollectCollapsed(item.Children, collapsed);
        }
    }
}
