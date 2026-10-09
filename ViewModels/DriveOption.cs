using System.IO;

namespace SysLens.ViewModels;

public sealed class DriveOption : ObservableObject
{
    private bool _isSelected;

    public DriveOption(DriveInfo drive)
    {
        Name = drive.Name;
        Label = drive.VolumeLabel;
        FileSystem = drive.DriveFormat;
        Type = drive.DriveType;
        TotalSize = drive.TotalSize;
        FreeSize = drive.TotalFreeSpace;
    }

    public string Name { get; }
    public string Label { get; }
    public string FileSystem { get; }
    public DriveType Type { get; }
    public long TotalSize { get; }
    public long FreeSize { get; }
    public long UsedSize => TotalSize - FreeSize;

    public string Title => string.IsNullOrEmpty(Label) ? Name : $"{Name}  {Label}";
    public string Summary => $"{Bytes.Format(FreeSize)} free of {Bytes.Format(TotalSize)} · {FileSystem} · {Type}";
    public double UsedPercent => TotalSize == 0 ? 0 : 100.0 * UsedSize / TotalSize;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}
