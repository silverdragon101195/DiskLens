using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace SysLens.Monitoring;

/// <summary>
/// The PawnIO kernel driver, through which LibreHardwareMonitor reads CPU temperature, power and clocks and
/// motherboard sensors. Without it those sensors have no value.
/// </summary>
internal static class PawnIoDriver
{
    public const string Homepage = "https://pawnio.eu/";

    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";

    /// <summary>Read afresh on every call; LibreHardwareMonitor's own check is fixed at first use.</summary>
    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKey);
            return key?.GetValue("DisplayVersion") is string;
        }
    }

    /// <summary>
    /// Installs PawnIO from the winget community repository. SysLens is elevated, so the installer runs without
    /// a UAC prompt. Returns null once installed, otherwise why it is not.
    /// </summary>
    public static async Task<string?> InstallAsync()
    {
        var start = new ProcessStartInfo("winget")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var argument in (string[])["install", "--id", "namazso.PawnIO", "--exact", "--source", "winget", "--silent",
                     "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"])
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (IsInstalled)
                return null;

            // winget redraws progress bars with carriage returns; its last line says what went wrong.
            var last = (await output + "\n" + await error)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(line => line.Any(char.IsLetter));
            // winget's exit codes are HRESULTs, documented in hex.
            return $"winget exited with code 0x{process.ExitCode:X8}{(last is null ? "" : $": {last}")}";
        }
        catch (Win32Exception ex)
        {
            return $"winget could not be started ({ex.Message}). Install PawnIO from {Homepage}";
        }
    }
}
