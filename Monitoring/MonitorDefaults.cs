namespace SysLens.Monitoring;

/// <summary>
/// The sensors shown until the user ticks or clears them, matching an MSI Afterburner on-screen display: CPU
/// temperature, power, clock and usage; for each GPU its temperature, power, power percent, core and memory clocks,
/// usage and memory used; RAM used; and the RTSS frame rate and frame time.
/// </summary>
internal static class MonitorDefaults
{
    public static bool IsDefault(HardwareInfo hardware, SensorInfo sensor) => (hardware.Kind, sensor.Kind, sensor.Name) switch
    {
        // Intel's names, then AMD's.
        (HardwareKind.Cpu, SensorKind.Temperature, "CPU Package" or "Core (Tctl/Tdie)") => true,
        (HardwareKind.Cpu, SensorKind.Power, "CPU Package" or "Package") => true,
        (HardwareKind.Cpu, SensorKind.Clock, HardwareMonitor.CoreMaxName) => true,
        (HardwareKind.Cpu, SensorKind.Load, "CPU Total") => true,

        (HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel, SensorKind.Temperature, "GPU Core") => true,
        (HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel, SensorKind.Clock, "GPU Core" or "GPU Memory") => true,
        (HardwareKind.GpuNvidia or HardwareKind.GpuAmd, SensorKind.Power, "GPU Package") => true,
        (HardwareKind.GpuNvidia or HardwareKind.GpuAmd, SensorKind.Load, "GPU Core") => true,
        (HardwareKind.GpuNvidia or HardwareKind.GpuAmd, SensorKind.Data, "GPU Memory Used") => true,
        // Power draw as a share of the power limit.
        (HardwareKind.GpuNvidia, SensorKind.Load, "GPU Power") => true,
        // Intel graphics report usage per Direct3D engine, and integrated ones use shared memory only.
        (HardwareKind.GpuIntel, SensorKind.Power, "GPU Power") => true,
        (HardwareKind.GpuIntel, SensorKind.Load, "D3D 3D") => true,
        (HardwareKind.GpuIntel, SensorKind.Data, "D3D Dedicated Memory Used" or "D3D Shared Memory Used") => true,

        // Total Memory, not Virtual Memory, which uses the same sensor names.
        (HardwareKind.Memory, SensorKind.Data, "Memory Used") => hardware.Key == "/ram",
        (HardwareKind.FrameRate, _, _) => true,
        _ => false,
    };
}
