using System.Collections.ObjectModel;
using System.IO;
using SysLens.Scanning;

namespace SysLens.ViewModels;

/// <summary>
/// A row in the result tree. Child rows are built on first expand so huge trees stay cheap;
/// until then a non-empty folder holds a single placeholder so the expander shows.
/// </summary>
public sealed class FolderItem : ObservableObject
{
    public const double BarMaxWidth = 80;

    private static readonly FolderItem Placeholder = new("Loading…", "", 0, 0, "");

    private readonly DirNode? _node;
    private ObservableCollection<FolderItem> _children = [];
    private bool _isExpanded;

    private FolderItem(string name, string fullPath, long size, long referenceSize, string details)
    {
        Name = name;
        FullPath = fullPath;
        Size = size;
        Details = details;
        Fraction = referenceSize > 0 ? Math.Clamp((double)size / referenceSize, 0, 1) : 0;
    }

    private FolderItem(DirNode node, long referenceSize, string name, string details)
        : this(name, node.FullPath, node.Size, referenceSize, details)
    {
        _node = node;
        IsUnreadable = node.Unreadable;
        if (node.Children.Length > 0 || node.OwnFileCount > 0)
            _children = [Placeholder];
    }

    /// <summary>Scan data behind a drive or folder row; null for file and placeholder rows.</summary>
    public DirNode? Node => _node;

    public string Name { get; }
    public string FullPath { get; }
    public long Size { get; }
    public string Details { get; }

    /// <summary>Share of the parent folder, or of the drive capacity for a drive row.</summary>
    public double Fraction { get; }

    public bool IsDrive { get; private init; }
    public bool IsFile { get; private init; }
    public bool IsUnreadable { get; }
    public bool HasPath => FullPath.Length > 0;

    public string SizeText => HasPath ? Bytes.Format(Size) : "";
    public string PercentText => HasPath ? $"{Fraction * 100:0.0}%" : "";
    public double BarWidth => Fraction * BarMaxWidth;

    public ObservableCollection<FolderItem> Children
    {
        get => _children;
        private set => Set(ref _children, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value) && value)
                LoadChildren();
        }
    }

    public static FolderItem ForDrive(DirNode root, DriveOption drive, TimeSpan elapsed)
    {
        var details = $"{root.TotalFileCount:N0} files · {Bytes.Format(drive.UsedSize)} used, "
                      + $"{Bytes.Format(drive.FreeSize)} free of {Bytes.Format(drive.TotalSize)} · scanned in {elapsed:mm\\:ss}";
        return new FolderItem(root, drive.TotalSize, drive.Title, details) { IsDrive = true, IsExpanded = true };
    }

    private static string FolderDetails(DirNode node) =>
        node.Unreadable
            ? $"{node.TotalFileCount:N0} files · could not be read completely"
            : $"{node.TotalFileCount:N0} files";

    private void LoadChildren()
    {
        if (_node is null || !ReferenceEquals(_children.FirstOrDefault(), Placeholder))
            return;

        // Subfolders first, then files; each group largest first. DirNode.Children is already sorted.
        var files = _node.OwnFileCount > 0 ? DiskScanner.ListFiles(_node.FullPath) : [];
        files.Sort(static (a, b) => b.Length.CompareTo(a.Length));

        var items = new List<FolderItem>(_node.Children.Length + files.Count);
        foreach (var child in _node.Children)
            items.Add(new FolderItem(child, _node.Size, child.Name, FolderDetails(child)));

        foreach (var file in files)
        {
            items.Add(new FolderItem(file.Name, Path.Join(_node.FullPath, file.Name), file.Length, _node.Size, "")
            {
                IsFile = true,
            });
        }

        Children = new ObservableCollection<FolderItem>(items);
    }
}
