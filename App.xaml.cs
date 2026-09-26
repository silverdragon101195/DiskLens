using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DiskLens.Native;

namespace DiskLens;

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
            MessageBox.Show("DiskLens needs administrator rights to read every folder on the selected drives.",
                "DiskLens", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "DiskLens", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
