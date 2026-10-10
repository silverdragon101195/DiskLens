using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using SysLens.Lighting.Aura;
using SysLens.Native;

namespace SysLens;

public partial class App : Application
{
    private const int ErrorCancelled = 1223;

    private bool _isAuraHost;
    private bool _isShowingError;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The child process SysLens starts for the ASUS Aura SDK; it inherits SysLens's elevation.
        if (e.Args is [AuraProtocol.HostArgument, var input, var output])
        {
            _isAuraHost = true;
            AuraHost.Run(this, input, output);
            return;
        }

        if (!Privileges.IsAdministrator())
        {
            RelaunchElevated(e.Args);
            Shutdown();
            return;
        }

        try
        {
            SigningRoot.EnsureTrusted();
        }
        catch (CryptographicException ex)
        {
            MessageBox.Show($"SysLens could not add its signing root CA to the Trusted Root Certification Authorities store: {ex.Message}",
                "SysLens", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        new MainWindow().Show();
    }

    /// <summary>
    /// Starts a new elevated instance through ShellExecute, the only launch path that shows
    /// the UAC prompt; this unelevated instance exits afterwards.
    /// </summary>
    private static void RelaunchElevated(string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        try
        {
            Process.Start(start);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            MessageBox.Show("SysLens needs administrator rights to read every folder on the selected drives.",
                "SysLens", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        // The Aura host has no window to show a message in; ending it hands the devices back to Armoury Crate.
        if (_isAuraHost)
        {
            Shutdown(1);
            return;
        }

        // The message box pumps messages, so an error that repeats, say on every timer tick, would
        // otherwise open box inside box until the stack overflows.
        if (_isShowingError)
            return;
        _isShowingError = true;
        try
        {
            MessageBox.Show(e.Exception.Message, "SysLens", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isShowingError = false;
        }
    }
}
