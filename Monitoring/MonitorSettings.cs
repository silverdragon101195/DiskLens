namespace SysLens.Monitoring;

/// <summary>
/// The graphs the user arranged, kept under "HardwareMonitor" in <see cref="AppSettings"/>. A sensor in neither list
/// follows <see cref="MonitorDefaults"/>.
/// </summary>
internal sealed class MonitorSettings
{
    /// <summary>The sensors with a graph, by <see cref="SensorInfo.Key"/>, in the order they are shown.</summary>
    public List<string> Graphs { get; set; } = [];

    /// <summary>Default sensors the user removed.</summary>
    public List<string> Hidden { get; set; } = [];
}
