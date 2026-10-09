namespace SysLens.Usb;

/// <summary>
/// One USB device node as Plug and Play records it. Interface and software children
/// (<c>&amp;MI_</c>, <c>&amp;IG_</c>, <c>&amp;LAMPARRAY</c>) are not devices of their own and never appear here.
/// </summary>
public sealed record UsbDevice(
    string InstanceId,
    string? ParentId,
    string Name,
    string? LocationInfo,
    bool IsPresent,
    int ProblemCode,
    bool IsHub,
    DateTime? LastArrival,
    DateTime? LastRemoval)
{
    public bool IsRootHub => InstanceId.StartsWith(@"USB\ROOT_HUB", StringComparison.OrdinalIgnoreCase);

    /// <summary><c>VVVV:PPPP</c> from the instance id, or empty for a root hub.</summary>
    public string VidPid
    {
        get
        {
            var vid = Field("VID_");
            var pid = Field("PID_");
            return vid.Length == 4 && pid.Length == 4 ? $"{vid}:{pid}".ToUpperInvariant() : "";
        }
    }

    private string Field(string prefix)
    {
        var start = InstanceId.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        return start < 0 || start + prefix.Length + 4 > InstanceId.Length
            ? ""
            : InstanceId.Substring(start + prefix.Length, 4);
    }
}
