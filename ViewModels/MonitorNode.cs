using SysLens.Monitoring;

namespace SysLens.ViewModels;

/// <summary>A row of the Hardware Monitor's sensor tree.</summary>
public abstract class MonitorNode : ObservableObject
{
    private bool _isExpanded = true;
    private bool _isVisible = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    /// <summary>False while the search hides the row.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }
}

/// <summary>A device: the devices it contains, then its sensors grouped by kind.</summary>
public sealed class HardwareItem(HardwareInfo info, string glyph, IReadOnlyList<MonitorNode> children) : MonitorNode
{
    public string Key => info.Key;

    public string Name => info.Name;

    /// <summary>A Segoe Fluent Icons code point.</summary>
    public string Glyph { get; } = glyph;

    public IReadOnlyList<MonitorNode> Children { get; } = children;
}

/// <summary>The sensors of one kind on one device.</summary>
public sealed class SensorGroupItem(string key, string name, IReadOnlyList<SensorItem> sensors) : MonitorNode
{
    /// <summary>The device's key and the kind; keeps the group's expanded state when the tree is rebuilt.</summary>
    public string Key { get; } = key;

    public string Name { get; } = name;

    public IReadOnlyList<SensorItem> Sensors { get; } = sensors;
}
