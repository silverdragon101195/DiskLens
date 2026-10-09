namespace SysLens.Usb;

/// <summary>Short meanings of the Device Manager problem codes (CM_PROB_*) a USB device can show.</summary>
public static class ProblemCodes
{
    private static readonly Dictionary<int, string> Meanings = new()
    {
        [1] = "not configured correctly",
        [3] = "driver corrupted or system low on memory",
        [10] = "device cannot start",
        [12] = "not enough free resources",
        [14] = "restart required",
        [18] = "drivers need reinstalling",
        [19] = "configuration in the registry is damaged",
        [21] = "being removed",
        [22] = "disabled",
        [24] = "not present, not working or missing drivers",
        [28] = "drivers not installed",
        [29] = "disabled by firmware",
        [31] = "not working properly, driver could not load",
        [32] = "driver service disabled",
        [37] = "driver failed to initialise",
        [39] = "driver missing or corrupted",
        [43] = "stopped after the device reported a problem",
        [45] = "not connected",
        [47] = "prepared for safe removal",
        [48] = "driver blocked",
        [52] = "driver signature could not be verified",
    };

    public static string Describe(int code) => Meanings.GetValueOrDefault(code, "see Device Manager");
}
