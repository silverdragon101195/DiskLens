namespace SysLens.ViewModels;

public static class Bytes
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        var (unit, size) = UnitFor(bytes);
        return size == 1 ? $"{bytes} B" : $"{bytes / size:0.0} {unit}";
    }

    /// <summary>The largest unit <paramref name="bytes"/> holds at least one of, and that unit's size in bytes.</summary>
    public static (string Unit, double Size) UnitFor(double bytes)
    {
        var unit = 0;
        for (var value = Math.Abs(bytes); value >= 1024 && unit < Units.Length - 1; value /= 1024)
            unit++;
        return (Units[unit], Math.Pow(1024, unit));
    }
}
