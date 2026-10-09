using System.IO;
using System.IO.Enumeration;

namespace SysLens.Scanning;

/// <summary>
/// Walks a directory tree and totals the logical size of the files beneath each folder.
/// Folder reparse points (junctions, symlinks, mounted volumes) are not followed, so nothing is
/// counted twice and link cycles cannot occur. One instance may scan several roots concurrently.
/// </summary>
public sealed class DiskScanner
{
    // Folders shallower than this fan their subfolders out to the thread pool.
    private const int ParallelDepth = 3;

    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    private long _folderCount;
    private long _fileCount;

    public long FolderCount => Interlocked.Read(ref _folderCount);
    public long FileCount => Interlocked.Read(ref _fileCount);

    public DirNode Scan(string rootPath, CancellationToken ct) => ScanFolder(rootPath, rootPath, 0, ct);

    /// <summary>
    /// Lists the files directly inside a folder. Scans keep only per-folder totals, so file rows
    /// are read on demand; an unreadable folder yields an empty list.
    /// </summary>
    public static List<FileEntry> ListFiles(string path)
    {
        var files = new List<FileEntry>();
        try
        {
            files.AddRange(new FileSystemEnumerable<FileEntry>(
                path,
                static (ref FileSystemEntry e) => new FileEntry(e.FileName.ToString(), e.Length),
                Options)
            {
                ShouldIncludePredicate = static (ref FileSystemEntry e) => !e.IsDirectory,
            });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Show whatever was listed before the failure.
        }

        return files;
    }

    private DirNode ScanFolder(string path, string name, int depth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var node = new DirNode(name, path);
        var subfolders = new List<string>();
        long ownSize = 0;
        long ownFiles = 0;

        try
        {
            var entries = new FileSystemEnumerable<Entry>(
                path,
                static (ref FileSystemEntry e) => new Entry(e.IsDirectory ? e.FileName.ToString() : null, e.Length),
                Options)
            {
                ShouldIncludePredicate = static (ref FileSystemEntry e) =>
                    !e.IsDirectory || (e.Attributes & FileAttributes.ReparsePoint) == 0,
            };

            foreach (var entry in entries)
            {
                if (entry.FolderName is not null)
                {
                    subfolders.Add(entry.FolderName);
                }
                else
                {
                    ownSize += entry.Length;
                    ownFiles++;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            node.Unreadable = true;
        }

        var children = new DirNode[subfolders.Count];
        if (depth < ParallelDepth && children.Length > 1)
        {
            Parallel.For(0, children.Length, new ParallelOptions { CancellationToken = ct },
                i => children[i] = ScanFolder(Path.Join(path, subfolders[i]), subfolders[i], depth + 1, ct));
        }
        else
        {
            for (var i = 0; i < children.Length; i++)
                children[i] = ScanFolder(Path.Join(path, subfolders[i]), subfolders[i], depth + 1, ct);
        }

        Array.Sort(children, static (a, b) => b.Size.CompareTo(a.Size));

        long size = ownSize;
        long files = ownFiles;
        foreach (var child in children)
        {
            size += child.Size;
            files += child.TotalFileCount;
        }

        node.OwnFilesSize = ownSize;
        node.OwnFileCount = ownFiles;
        node.Size = size;
        node.TotalFileCount = files;
        node.Children = children;

        Interlocked.Increment(ref _folderCount);
        Interlocked.Add(ref _fileCount, ownFiles);
        return node;
    }

    private readonly record struct Entry(string? FolderName, long Length);
}

public readonly record struct FileEntry(string Name, long Length);
