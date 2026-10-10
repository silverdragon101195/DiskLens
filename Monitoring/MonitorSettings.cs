using System.IO;
using System.Text.Json;

namespace SysLens.Monitoring;

/// <summary>
/// The graphs the user arranged, kept under "HardwareMonitor" in SysLens.settings.json next to SysLens.exe and
/// written on every change. A sensor in neither list follows <see cref="MonitorDefaults"/>.
/// </summary>
internal sealed class MonitorSettings
{
    public const string FileName = "SysLens.settings.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The sensors with a graph, by <see cref="SensorInfo.Key"/>, in the order they are shown.</summary>
    public List<string> Graphs { get; set; } = [];

    /// <summary>Default sensors the user removed.</summary>
    public List<string> Hidden { get; set; } = [];

    public static MonitorSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(FilePath))?.HardwareMonitor ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or unreadable: every sensor follows the defaults.
            return new();
        }
    }

    /// <summary>Returns false when the file cannot be written; the layout then lasts until SysLens closes.</summary>
    public bool Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SettingsFile(this), Json));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record SettingsFile(MonitorSettings? HardwareMonitor);
}
