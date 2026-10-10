using SysLens.Monitoring;

namespace SysLens.ViewModels;

/// <summary>How a graph sets its vertical range.</summary>
public enum GraphScale
{
    /// <summary>From 0 to a round number at or above the highest reading.</summary>
    FromZero,

    /// <summary>From 0 to 100, or higher when a reading exceeds it.</summary>
    Percent,

    /// <summary>Round numbers just around the lowest and highest readings.</summary>
    Fit,
}

/// <summary>How the monitor groups, names, writes and graphs each <see cref="SensorKind"/>.</summary>
public static class SensorFormat
{
    // Group order within a device, and so the order of its graphs: most watched first.
    private static readonly SensorKind[] Order =
    [
        SensorKind.Temperature, SensorKind.Power, SensorKind.Clock, SensorKind.Load, SensorKind.Fan, SensorKind.Control,
        SensorKind.Voltage, SensorKind.Current, SensorKind.Data, SensorKind.Throughput, SensorKind.Frequency,
        SensorKind.Flow, SensorKind.Level, SensorKind.Factor, SensorKind.TimeSpan, SensorKind.Timing, SensorKind.Energy,
        SensorKind.Noise, SensorKind.Conductivity, SensorKind.Humidity, SensorKind.Framerate, SensorKind.Frametime,
    ];

    public static int SortOrder(SensorKind kind) => Array.IndexOf(Order, kind);

    /// <summary>The heading of the kind's group in the sensor tree.</summary>
    public static string GroupName(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => "Temperatures",
        SensorKind.Clock => "Clocks",
        SensorKind.Fan => "Fans",
        SensorKind.Control => "Controls",
        SensorKind.Voltage => "Voltages",
        SensorKind.Current => "Currents",
        SensorKind.Frequency => "Frequencies",
        SensorKind.Flow => "Flows",
        SensorKind.Level => "Levels",
        SensorKind.Factor => "Factors",
        SensorKind.TimeSpan => "Times",
        SensorKind.Timing => "Timings",
        _ => Name(kind),
    };

    /// <summary>What one sensor of the kind measures.</summary>
    public static string Name(SensorKind kind) => kind switch
    {
        SensorKind.TimeSpan => "Time",
        SensorKind.Framerate => "Frame rate",
        SensorKind.Frametime => "Frame time",
        _ => kind.ToString(),
    };

    /// <summary>
    /// A graph's title, as the sensor tree shows it: the sensor's name and its group ("CPU Package - Temperatures"),
    /// or the name alone when the group only repeats it ("Framerate" in "Frame rate").
    /// </summary>
    public static string Title(SensorKind kind, string name)
    {
        var group = GroupName(kind);
        return string.Equals(group.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name} - {group}";
    }

    /// <summary>A reading with its unit, or a dash when the sensor has none.</summary>
    public static string Format(SensorKind kind, double? value) => value is not { } v || !double.IsFinite(v) ? "—" : kind switch
    {
        SensorKind.Voltage => $"{v:0.000} V",
        SensorKind.Current => $"{v:0.00} A",
        SensorKind.Power => $"{v:0.0} W",
        SensorKind.Clock => $"{v:N0} MHz",
        SensorKind.Temperature => $"{v:0} °C",
        SensorKind.Load or SensorKind.Control or SensorKind.Level or SensorKind.Humidity => $"{v:0} %",
        SensorKind.Frequency => $"{v:0.0} Hz",
        SensorKind.Fan => $"{v:N0} RPM",
        SensorKind.Flow => $"{v:0.0} L/h",
        SensorKind.Data => Bytes.Format((long)v),
        SensorKind.Throughput => $"{Bytes.Format((long)v)}/s",
        SensorKind.TimeSpan => FormatDuration(v),
        SensorKind.Timing => $"{v:0.00} ns",
        SensorKind.Energy => $"{v:N0} mWh",
        SensorKind.Noise => $"{v:0.0} dBA",
        SensorKind.Conductivity => $"{v:0.0} µS/cm",
        SensorKind.Framerate => $"{v:0} FPS",
        SensorKind.Frametime => $"{v:0.0} ms",
        _ => $"{v:0.###}",
    };

    public static GraphScale Scale(SensorKind kind) => kind switch
    {
        SensorKind.Load or SensorKind.Control or SensorKind.Level or SensorKind.Humidity => GraphScale.Percent,
        SensorKind.Voltage or SensorKind.Frequency or SensorKind.Factor or SensorKind.Timing or SensorKind.Noise
            or SensorKind.Conductivity => GraphScale.Fit,
        _ => GraphScale.FromZero,
    };

    /// <summary>
    /// The unit a graph's axis is labelled in and what a reading is divided by to get it: byte kinds step up to the
    /// unit that suits <paramref name="highest"/>, other kinds keep their own.
    /// </summary>
    public static (string Unit, double Divisor) AxisUnit(SensorKind kind, double highest)
    {
        if (kind is SensorKind.Data or SensorKind.Throughput)
        {
            var (unit, size) = Bytes.UnitFor(highest);
            return (kind == SensorKind.Throughput ? $"{unit}/s" : unit, size);
        }

        return (kind switch
        {
            SensorKind.Voltage => "V",
            SensorKind.Current => "A",
            SensorKind.Power => "W",
            SensorKind.Clock => "MHz",
            SensorKind.Temperature => "°C",
            SensorKind.Load or SensorKind.Control or SensorKind.Level or SensorKind.Humidity => "%",
            SensorKind.Frequency => "Hz",
            SensorKind.Fan => "RPM",
            SensorKind.Flow => "L/h",
            SensorKind.TimeSpan => "s",
            SensorKind.Timing => "ns",
            SensorKind.Energy => "mWh",
            SensorKind.Noise => "dBA",
            SensorKind.Conductivity => "µS/cm",
            SensorKind.Framerate => "FPS",
            SensorKind.Frametime => "ms",
            _ => "",
        }, 1);
    }

    private static string FormatDuration(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalDays >= 1 ? $"{(int)time.TotalDays}d {time:hh\\:mm\\:ss}" : $"{time:hh\\:mm\\:ss}";
    }
}
