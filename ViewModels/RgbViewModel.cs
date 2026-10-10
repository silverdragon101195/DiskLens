using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using SysLens.Lighting;
using SysLens.Lighting.Aura;
using SysLens.Lighting.LampArrays;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;

namespace SysLens.ViewModels;

/// <summary>
/// Every RGB device from ASUS Aura and Windows Dynamic Lighting, with an effect each. Devices that follow
/// All devices show its settings. Changing anything an Aura device shows takes the Aura devices from Armoury
/// Crate; they go back on Release, Refresh or when SysLens closes.
/// </summary>
public sealed class RgbViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly LightingEngine _engine = new();
    private readonly DispatcherTimer _previewTimer;

    // Settings of devices no longer listed, restored when a device with the same key is found again.
    private readonly Dictionary<string, (EffectSettings Settings, bool FollowsAll)> _saved = [];

    private AuraHostClient? _aura;
    private DeviceWatcher? _lampWatcher;
    private RgbTargetItem _selected;
    private string _status = "";
    private string _summary = "";
    private string _busyText = "";
    private string _busyDetail = "";
    private bool _isBusy;
    private bool _isActive;
    private bool _disposed;

    public RgbViewModel()
    {
        All = RgbTargetItem.ForAll(EffectSettings.Default);
        All.Editor.Changed += () => OnEditorChanged(All);
        Items.Add(All);
        _selected = All;
        _engine.Add(All.Target);

        _previewTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / LightingEngine.FramesPerSecond),
        };
        _previewTimer.Tick += (_, _) =>
        {
            foreach (var item in Items)
                item.UpdatePreview();
        };
        RefreshTexts();
    }

    /// <summary>All devices first, then Aura devices in SDK order, then Dynamic Lighting devices.</summary>
    public ObservableCollection<RgbTargetItem> Items { get; } = [];

    public RgbTargetItem All { get; }

    public RgbTargetItem? Selected
    {
        get => _selected;
        // The list clears its selection while items are replaced; the editor falls back to All devices.
        set => Set(ref _selected, value ?? All);
    }

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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsBusy;

    /// <summary>What the running Aura command is doing; shown with a spinner below the devices while busy.</summary>
    public string BusyText
    {
        get => _busyText;
        private set => Set(ref _busyText, value);
    }

    public string BusyDetail
    {
        get => _busyDetail;
        private set => Set(ref _busyDetail, value);
    }

    /// <summary>Set while the RGB tab is shown; previews only redraw then.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!Set(ref _isActive, value))
                return;
            if (value)
                _previewTimer.Start();
            else
                _previewTimer.Stop();
        }
    }

    public bool IsAuraInControl => _aura?.InControl == true;

    public bool CanTakeControl => _aura is { IsAlive: true, InControl: false } && AuraItems.Any();

    private IEnumerable<RgbTargetItem> DeviceItems => Items.Where(i => i.IsDevice);

    private IEnumerable<RgbTargetItem> AuraItems => Items.Where(i => i.Device is AuraDevice);

    public async Task StartAsync()
    {
        StartLampArrays();
        await RestartAuraAsync(takeControl: false);
    }

    /// <summary>Starts a fresh Aura host, which reads the devices again; control is taken again if SysLens had it.</summary>
    public Task RefreshAsync() => RestartAuraAsync(IsAuraInControl);

    public async Task TakeControlAsync()
    {
        if (IsBusy || _aura is not { IsAlive: true, InControl: false } aura)
            return;

        BeginBusy("Taking control of the ASUS Aura devices…");
        Status = "Taking control of the ASUS Aura devices…";
        try
        {
            ApplyAuraDevices(aura, await aura.TakeControlAsync());
            Status = "SysLens controls the ASUS Aura devices. Release, Refresh or close SysLens to hand them back to Armoury Crate.";
        }
        catch (AuraException ex)
        {
            Status = $"Could not take control of the ASUS Aura devices: {ex.Message}.";
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    }

    public async Task ReleaseAsync()
    {
        if (IsBusy || _aura is not { InControl: true } aura)
            return;

        BeginBusy("Handing the ASUS Aura devices back to Armoury Crate…");
        try
        {
            await aura.ReleaseAsync();
            Status = "Armoury Crate controls the ASUS Aura devices again. Changing an effect takes them back.";
        }
        catch (AuraException ex)
        {
            Status = $"Could not release the ASUS Aura devices: {ex.Message}.";
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    }

    /// <summary>Makes every device follow All devices.</summary>
    public void SyncAll()
    {
        foreach (var item in DeviceItems)
            item.FollowsAll = true;
        TakeControlIfNeeded(All);
    }

    /// <summary>Copies a device's settings to All devices and makes every device follow them.</summary>
    public void UseForAll(RgbTargetItem item)
    {
        All.Editor.Load(item.Editor.Settings);
        OnEditorChanged(All);
        SyncAll();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _previewTimer.Stop();
        _engine.Dispose();
        if (_lampWatcher?.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _lampWatcher.Stop();
        _aura?.Dispose();
    }

    private async Task RestartAuraAsync(bool takeControl)
    {
        if (IsBusy)
            return;

        BeginBusy("Reading ASUS Aura devices…", "The Aura SDK can take up to a minute to load its vendor plugins.");
        Status = "Reading ASUS Aura devices… The Aura SDK can take up to a minute to load its vendor plugins.";
        RefreshTexts();
        try
        {
            if (_aura is { } old)
            {
                _aura = null;
                RemoveItems(AuraItems.ToList());
                await Task.Run(old.Dispose);
            }

            var aura = AuraHostClient.Start();
            aura.Stopped += () => _dispatcher.BeginInvoke(() => OnAuraStopped(aura));
            _aura = aura;
            ApplyAuraDevices(aura, takeControl ? await aura.TakeControlAsync() : await aura.EnumerateAsync());
            Status = $"Updated {DateTime.Now:HH:mm:ss}. "
                     + (aura.InControl
                         ? "SysLens controls the ASUS Aura devices."
                         : "Armoury Crate controls the ASUS Aura devices until you change an effect.");
        }
        catch (AuraException ex)
        {
            Status = $"ASUS Aura: {ex.Message}.";
        }
        catch (Win32Exception ex)
        {
            Status = $"Could not start the ASUS Aura host: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    }

    private void BeginBusy(string text, string detail = "")
    {
        BusyText = text;
        BusyDetail = detail;
        IsBusy = true;
    }

    /// <summary>Replaces the Aura rows, unless the SDK reported the same devices again.</summary>
    private void ApplyAuraDevices(AuraHostClient aura, List<AuraDeviceInfo> infos)
    {
        var occurrences = new Dictionary<(uint, string), int>();
        var devices = infos.Select((info, index) =>
        {
            var occurrence = occurrences.GetValueOrDefault((info.Type, info.Name));
            occurrences[(info.Type, info.Name)] = occurrence + 1;
            return new AuraDevice(aura, index, info, occurrence);
        }).ToList();

        var current = AuraItems.ToList();
        if (current.Count > 0 && current.Select(i => i.Key).SequenceEqual(devices.Select(d => d.Key)))
            return;

        var selectedKey = Selected?.Key;
        RemoveItems(current);
        for (var i = 0; i < devices.Count; i++)
            AddItem(devices[i], 1 + i);
        Selected = Items.FirstOrDefault(i => i.Key == selectedKey);
    }

    private void OnAuraStopped(AuraHostClient aura)
    {
        if (aura != _aura)
            return;
        Status = $"The ASUS Aura host stopped ({aura.StopReason}); its devices went back to Armoury Crate. Press Refresh to start it again.";
        RefreshTexts();
    }

    private void StartLampArrays()
    {
        _lampWatcher = DeviceInformation.CreateWatcher(LampArray.GetDeviceSelector());
        _lampWatcher.Added += async (_, info) =>
        {
            if (await LampArrayDevice.OpenAsync(info) is { } device)
                await _dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed && Items.All(i => i.Key != device.Key))
                        AddItem(device, Items.Count);
                    RefreshTexts();
                });
        };
        _lampWatcher.Removed += (_, update) => _dispatcher.BeginInvoke(() =>
        {
            RemoveItems(Items.Where(i => i.Key == LampArrayDevice.KeyOf(update.Id)).ToList());
            RefreshTexts();
        });
        // Removed is only raised while an Updated handler is attached.
        _lampWatcher.Updated += (_, _) => { };
        _lampWatcher.Start();
    }

    private void AddItem(LightDevice device, int index)
    {
        var (settings, followsAll) = _saved.Remove(device.Key, out var saved) ? saved : (All.Editor.Settings, true);
        var item = RgbTargetItem.ForDevice(device, settings, followsAll);
        item.Editor.Changed += () => OnEditorChanged(item);
        item.FollowsAllChanged += OnFollowsAllChanged;
        device.ControlChanged += (_, _) => _dispatcher.BeginInvoke(RefreshTexts);

        UpdateTarget(item);
        Items.Insert(index, item);
        _engine.Add(item.Target);
    }

    private void RemoveItems(List<RgbTargetItem> items)
    {
        foreach (var item in items)
        {
            _saved[item.Key] = (item.Editor.Settings, item.FollowsAll);
            _engine.Remove(item.Target);
            Items.Remove(item);
        }
    }

    private void OnEditorChanged(RgbTargetItem item)
    {
        if (item.IsAll)
        {
            foreach (var device in DeviceItems.Where(d => d.FollowsAll))
                UpdateTarget(device);
        }
        UpdateTarget(item);
        RefreshTexts();
        TakeControlIfNeeded(item);
    }

    private void OnFollowsAllChanged(RgbTargetItem item)
    {
        // A device that stops following keeps looking the same until its own settings are changed.
        if (!item.FollowsAll)
            item.Editor.Load(All.Editor.Settings);
        UpdateTarget(item);
        RefreshTexts();
    }

    private void UpdateTarget(RgbTargetItem item) =>
        item.Target.Settings = item.ShowsOwnEditor ? item.Editor.Settings : All.Editor.Settings;

    private void TakeControlIfNeeded(RgbTargetItem item)
    {
        var affectsAura = item.Device is AuraDevice
                          || (item.IsAll && AuraItems.Any(i => i.FollowsAll));
        if (affectsAura && CanTakeControl)
            _ = TakeControlAsync();
    }

    private void RefreshTexts()
    {
        var devices = DeviceItems.ToList();
        foreach (var item in devices)
        {
            var device = item.Device!;
            item.Subtitle = $"{device.Source} · {device.Kind} · {Count(device.LedCount, "LED")}";
            item.ControlText = device.ControlText;
            item.IsControlled = device.CanPush;
            item.EffectText = item.FollowsAll ? $"Follows All devices · {All.Editor.Effect.Name}" : item.Editor.Effect.Name;
        }

        var followers = devices.Count(d => d.FollowsAll);
        var leds = devices.Sum(d => d.Device!.LedCount);
        All.Subtitle = $"{Count(devices.Count, "device")} · {Count(leds, "LED")}";
        All.ControlText = $"{followers} of {devices.Count} follow";
        All.IsControlled = true;
        All.EffectText = All.Editor.Effect.Name;

        var auraState = _aura is null ? "not started"
            : IsBusy && !AuraItems.Any() ? "reading…"
            : !_aura.IsAlive ? "host stopped"
            : _aura.InControl ? "controlled by SysLens"
            : "controlled by Armoury Crate";
        Summary = $"{Count(AuraItems.Count(), "ASUS Aura device")} ({auraState}) · "
                  + $"{Count(devices.Count(d => d.Device is LampArrayDevice), "Dynamic Lighting device")} · {Count(leds, "LED")}";

        OnPropertyChanged(nameof(IsAuraInControl));
        OnPropertyChanged(nameof(CanTakeControl));
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
}
