using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using DiskLens.Native;
using DiskLens.ViewModels;

namespace DiskLens;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(DescribePrivileges());
        DataContext = _viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    // App.OnStartup only opens this window once the process is elevated.
    private static string DescribePrivileges() =>
        Privileges.TryEnable(Privileges.Backup)
            ? "Administrator · backup privilege enabled (protected folders are readable)"
            : "Administrator · backup privilege unavailable: some protected folders may be skipped";

    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshDrives();

    private async void Scan_Click(object sender, RoutedEventArgs e) => await _viewModel.ScanAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => _viewModel.Cancel();

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item)
            return;

        // A drive root ends with a backslash, which would escape a closing quote.
        // A file opens its containing folder with the file selected.
        var argument = item.IsFile ? $"/select,\"{item.FullPath}\""
            : item.FullPath.EndsWith('\\') ? item.FullPath
            : $"\"{item.FullPath}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
    }

    private async void AskAi_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            await _viewModel.Assistant.StartAsync(item);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item)
            return;

        try
        {
            Clipboard.SetText(item.FullPath);
        }
        catch (ExternalException)
        {
            // Another process holds the clipboard open; the user can simply retry.
        }
    }

    private static FolderItem? ItemOf(object sender) =>
        sender is FrameworkElement { DataContext: FolderItem { HasPath: true } item } ? item : null;
}
