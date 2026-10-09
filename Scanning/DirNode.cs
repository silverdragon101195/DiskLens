namespace SysLens.Scanning;

public sealed class DirNode(string name, string fullPath)
{
    public string Name { get; } = name;
    public string FullPath { get; } = fullPath;

    /// <summary>Total size of every file beneath this folder.</summary>
    public long Size { get; internal set; }

    /// <summary>Size of the files directly inside this folder.</summary>
    public long OwnFilesSize { get; internal set; }

    public long OwnFileCount { get; internal set; }
    public long TotalFileCount { get; internal set; }

    /// <summary>The folder could not be listed completely; its totals are a lower bound.</summary>
    public bool Unreadable { get; internal set; }

    /// <summary>Subfolders, largest first.</summary>
    public DirNode[] Children { get; internal set; } = [];
}
