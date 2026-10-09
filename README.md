# SysLens

A Windows system inspector built with WPF on .NET 10. It shows where disk space goes and how USB devices
are wired together, with an AI assistant that explains any folder, file, device or hub you point it at.

![SysLens icon](Assets/SysLens.png)

## Features

### Disk

- **Multi-drive scanning** – tick any number of drives and scan them in parallel.
- **Size tree** – every folder shows its total size, share of the parent and file count, sorted largest
  first. Files inside a folder are listed on demand.
- **Complete results** – runs elevated and enables the backup privilege, so protected folders are read
  too. Junctions, symlinks and mounted volumes are not followed, so nothing is counted twice.
- **Quick actions** – open in Explorer (files open with the file selected) or copy the full path.

### USB

- **Device tree** – every USB device and hub under each root hub, built from the Plug and Play parent
  links. Interface and software children (`&MI_`, `&IG_`, `&LAMPARRAY`) are folded into their device.
  **Show disconnected** adds devices that are not connected now, dimmed, under the hub they were last
  on; when Windows has purged that hub, the device sits at the top level marked "hub chain unknown".
- **Per device** – the name the device reports about itself, VID:PID, status with the Device Manager
  problem code, port location (`Port_#0003.Hub_#0005`) and the instance id, selectable for copying.
- **Hub tiers** – counts the external hubs between the root hub and each node. USB allows five; tier 4
  is shown in yellow and tier 5 and deeper in red, since that is where devices start to drop off.
  Monitors, docks and keyboards often hold internal hubs that count as tiers too.
- **Errors** – devices with a problem code are shown in red with the code and its meaning.
- **History** – devices not connected now, with their last arrival and removal times, latest first.
  A device removed within seconds of arriving is flagged in red: it most likely failed to enumerate.
- **Live** – refreshes by itself shortly after a device is plugged in or removed.

### Ask AI

Right-click any row, or press its **?** button, to ask what it is. For a folder or file: which app owns
it, whether it can be deleted and how to remove it safely. For a USB device or hub: what it is, whether
its place in the hub chain or its status is a problem, and how to fix it. Answers stream in as Markdown
and you can keep the conversation going.

Dark theme with a dark title bar throughout.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build) or .NET 10 Desktop Runtime (to run)
- Administrator rights – the app relaunches itself through UAC when started unelevated
- For the AI assistant: an OpenAI-compatible Chat Completions endpoint and API key

## Build and run

```powershell
git clone https://github.com/silverdragon101195/SysLens.git
cd SysLens
dotnet run -c Release
```

To produce a standalone folder:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## AI assistant setup

The assistant reads its settings from `Gemini_BYOK.json` next to `SysLens.exe`. This file holds your
API key and is git-ignored.

1. Copy `Gemini_BYOK.example.json` to `Gemini_BYOK.json` in the project folder (the build copies it to
   the output folder) or directly next to `SysLens.exe`.
2. Fill in:
   - `apiKey` – your API key, sent as a `Bearer` token
   - `models[0].url` – base URL of an OpenAI-compatible API; `/chat/completions` is appended
   - `models[0].id` – the model name to request

Only the first entry of `models` is used. Everything else works without this file; only **Ask AI**
needs it.

## Project layout

| Folder        | Contents                                                                                                     |
| ------------- | ------------------------------------------------------------------------------------------------------------ |
| `Scanning/`   | Parallel directory walker and the size tree it builds                                                        |
| `Usb/`        | USB device record and Device Manager problem codes                                                           |
| `ViewModels/` | MVVM view models for drives, results, the USB tree and the AI chat                                           |
| `Views/`      | AI assistant panel, the Markdown renderer and value converters                                               |
| `Ai/`         | Config loader, streaming chat client and prompts                                                             |
| `Native/`     | Win32 interop: elevation, backup privilege, dark title bar, signing root CA, USB enumeration, device changes |
| `Themes/`     | Dark theme resources                                                                                         |

## Notes

- Builds signed with the Hoshizora code-signing certificate (made by `publish.bat`) add the Hoshizora
  Root CA, embedded in the app, to the machine's Trusted Root Certification Authorities store when it is
  missing there, so Windows can verify the signature; UAC names the publisher from the next launch on.
  Unsigned builds, such as `dotnet run` or a plain `dotnet publish`, never change the certificate store.
- Sizes are logical file sizes, not size on disk.
- The AI sees only the path, size and largest contents of a disk item, never file contents. For a USB
  device it sees the name, VID:PID, status, port location and hub chain, never the instance id, which
  can carry the device's serial number.
- AI answers are advice. Review them before deleting anything, and prefer the owning app's settings or
  built-in Windows tools (Disk Cleanup, Storage Sense) for system data.
