using System.IO;
using System.Text;
using SysLens.ViewModels;

namespace SysLens.Ai;

public static class Prompts
{
    private const int ListedChildren = 12;

    private const string System =
        """
        You are a Windows storage expert helping a user free disk space. The user shows you one
        folder or file found by a disk-usage scanner, with its size and largest contents.

        Rules:
        - Always answer in Vietnamese, concise, formatted as Markdown (headings, bullet lists, `code`).
        - Be accurate. If you are not sure which application owns an item, say so and explain how the
          user can check (e.g. file properties, Task Manager, the parent folder, searching the name).
        - Never advise deleting Windows or system-critical data by hand (Windows, System32, WinSxS,
          pagefile.sys, hiberfil.sys, swapfile.sys, System Volume Information, $Recycle.Bin internals,
          installed program folders). Point to the proper tool instead: Disk Cleanup / cleanmgr,
          Storage Sense, DISM /StartComponentCleanup, powercfg /h off, System Protection settings,
          the app's own uninstaller or settings.
        - Prefer the owning app's settings or built-in Windows tools over deleting by hand, and say
          which apps must be closed first. Mention backing up anything that holds user data.
        """;

    private const string InitialQuestionDisplay =
        "What is this, which app does it belong to, can it be deleted, and how to delete it safely?";

    public static AssistantTopic ForItem(FolderItem item) =>
        new(item.FullPath, System, InitialQuestion(item), InitialQuestionDisplay);

    private static string InitialQuestion(FolderItem item)
    {
        var text = new StringBuilder();
        text.AppendLine(Describe(item));
        text.AppendLine();
        text.AppendLine("Answer with exactly these three sections:");
        text.AppendLine("1. What it is and which application or Windows component it belongs to.");
        text.AppendLine("2. Whether it can be deleted (yes / no / partly) and what happens if it is removed.");
        text.AppendLine("3. If it can be deleted: how to remove it safely, step by step.");
        return text.ToString();
    }

    private static string Describe(FolderItem item)
    {
        var text = new StringBuilder();
        var kind = item.IsDrive ? "Drive root" : item.IsFile ? "File" : "Folder";
        text.AppendLine($"{kind}: `{item.FullPath}`");
        text.AppendLine($"Size: {Bytes.Format(item.Size)}");

        if (item.IsFile)
        {
            var info = new FileInfo(item.FullPath);
            if (info.Exists)
                text.AppendLine($"Last modified: {info.LastWriteTime:yyyy-MM-dd}");
            return text.ToString();
        }

        if (item.Node is not { } node)
            return text.ToString();

        text.AppendLine($"Files inside (recursive): {node.TotalFileCount:N0}");
        if (node.Unreadable)
            text.AppendLine("Note: the folder could not be read completely.");

        if (node.Children.Length > 0)
        {
            text.AppendLine("Largest subfolders:");
            foreach (var child in node.Children.Take(ListedChildren))
                text.AppendLine($"- {child.Name} ({Bytes.Format(child.Size)})");
        }

        if (node.OwnFileCount > 0)
            text.AppendLine($"Files directly inside: {node.OwnFileCount:N0}, {Bytes.Format(node.OwnFilesSize)} in total");

        return text.ToString();
    }
}
