# DiskLens

A fast disk-usage analyzer for Windows, built with WPF on .NET 10, with an AI assistant that explains
what a folder or file is and whether it is safe to delete.

![DiskLens icon](Assets/DiskLens.png)

## Features

- **Multi-drive scanning** – tick any number of drives and scan them in parallel.
- **Size tree** – every folder shows its total size, share of the parent and file count, sorted largest
  first. Files inside a folder are listed on demand.
- **Complete results** – runs elevated and enables the backup privilege, so protected folders are read
  too. Junctions, symlinks and mounted volumes are not followed, so nothing is counted twice.
- **Ask AI** – right-click any row and ask what it is, which app owns it, whether it can be deleted and
  how to remove it safely. Answers stream in as Markdown and you can keep the conversation going.
- **Quick actions** – open in Explorer (files open with the file selected) or copy the full path.
- Dark theme with a dark title bar.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build) or .NET 10 Desktop Runtime (to run)
- Administrator rights – the app relaunches itself through UAC when started unelevated
- For the AI assistant: an OpenAI-compatible Chat Completions endpoint and API key

## Build and run

```powershell
git clone https://github.com/silverdragon101195/DiskLens.git
cd DiskLens
dotnet run -c Release
```

To produce a standalone folder:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## AI assistant setup

The assistant reads its settings from `Gemini_BYOK.json` next to `DiskLens.exe`. This file holds your
API key and is git-ignored.

1. Copy `Gemini_BYOK.example.json` to `Gemini_BYOK.json` in the project folder (the build copies it to
   the output folder) or directly next to `DiskLens.exe`.
2. Fill in:
   - `apiKey` – your API key, sent as a `Bearer` token
   - `models[0].url` – base URL of an OpenAI-compatible API; `/chat/completions` is appended
   - `models[0].id` – the model name to request

Only the first entry of `models` is used. Scanning works without this file; only **Ask AI** needs it.

## Project layout

| Folder        | Contents                                                        |
| ------------- | --------------------------------------------------------------- |
| `Scanning/`   | Parallel directory walker and the size tree it builds           |
| `ViewModels/` | MVVM view models for drives, results and the AI chat            |
| `Views/`      | AI assistant panel and the Markdown renderer                    |
| `Ai/`         | Config loader, streaming chat client and prompts                |
| `Native/`     | Win32 interop: elevation, backup privilege, dark title bar      |
| `Themes/`     | Dark theme resources                                            |

## Notes

- Sizes are logical file sizes, not size on disk.
- The AI sees only the path, size and largest contents of the selected item – never file contents.
- AI answers are advice. Review them before deleting anything, and prefer the owning app's settings or
  built-in Windows tools (Disk Cleanup, Storage Sense) for system data.
