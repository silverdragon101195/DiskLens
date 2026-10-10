namespace SysLens.Monitoring;

/// <summary>
/// What a sensor measures: LibreHardwareMonitor's sensor types, matched by name, plus the RTSS frame rate
/// and frame time. Data is in bytes, Throughput in bytes per second.
/// </summary>
public enum SensorKind
{
    Voltage,
    Current,
    Power,
    Clock,
    Temperature,
    Load,
    Frequency,
    Fan,
    Flow,
    Control,
    Level,
    Factor,
    Data,
    Throughput,
    TimeSpan,
    Timing,
    Energy,
    Noise,
    Conductivity,
    Humidity,
    Framerate,
    Frametime,
}

/// <summary>What a device is: LibreHardwareMonitor's hardware types, matched by name, plus RTSS.</summary>
public enum HardwareKind
{
    Motherboard,
    SuperIO,
    Cpu,
    Memory,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Storage,
    Network,
    Cooler,
    EmbeddedController,
    Psu,
    Battery,
    PowerMonitor,
    FrameRate,
}
