using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using SysLens.Native;

namespace SysLens;

public partial class App : Application
{
    private const int ErrorCancelled = 1223;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        MessageBox.Show(e.Exception.Message, "SysLens", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
