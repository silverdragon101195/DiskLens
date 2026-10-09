# Freeing space on C: without reinstalling Windows

Measure first, then clean the biggest items. Steps marked **(admin)** need an elevated terminal.

On this machine the page file is on F:, hibernation is off, `%LOCALAPPDATA%\Temp` links to `Z:\Temp`,
and [mklink.bat](mklink.bat) links profile caches and app data to `H:\UserData`.

## 1. Measure

Prints the size of each usual hotspot, or its target when the folder links to another drive.
For anything not listed, scan C: with SysLens.

```powershell
function Get-FolderBytes([string]$Path) {
    # robocopy /L lists without copying and reads the long paths and container layers that Get-ChildItem
    # fails on. Parses the English summary line.
    $summary = robocopy $Path "$env:TEMP\size-probe" /L /S /XJ /BYTES /NJH /NFL /NDL /NC /NP /R:0 /W:0 | Select-String '^\s*Bytes\s*:\s*(\d+)'
    if ($summary) { [int64]$summary.Matches[0].Groups[1].Value } else { -1 }
}
$L = $env:LOCALAPPDATA
$paths = @(
    'C:\ProgramData\Microsoft\Windows\Containers'
    'C:\ProgramData\Microsoft\VisualStudio\Packages'
    'C:\ProgramData\NVIDIA Corporation\NVIDIA App\UpdateFramework'
    'C:\ProgramData\Corsair\CUE5\GameSDKEffects'
    'C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis'
    'C:\Windows\System32\DriverStore\FileRepository'
    "$L\NVIDIA\DXCache"
    "$L\Google\Chrome\User Data\OptGuideOnDeviceModel"
    "$L\Packages\MSTeams_8wekyb3d8bbwe"
    "$L\npm-cache"
    "$L\uv"
    "$L\NuGet"
    "$L\nvm"
    "$L\puccinialin"
    "$L\ReclaiMe"
    "$L\AzureFunctionsTools"
)
foreach ($p in $paths) {
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $item = Get-Item -LiteralPath $p -Force
    if ($item.LinkType) { '{0,-70} -> {1}' -f $p, ($item.Target -join ', '); continue }
    $bytes = Get-FolderBytes $p
    if ($bytes -lt 0) { '{0,-70} {1,11}' -f $p, 'no access' } else { '{0,-70} {1,8:N2} GB' -f $p, ($bytes / 1GB) }
}
```

## 2. Old drivers

Every driver install copies the whole package into `C:\Windows\System32\DriverStore\FileRepository`.
Installing a newer version keeps the old package, so Device Manager can roll back and a reconnected
device installs without a download. Windows never removes these packages on its own.

This lists the packages that are older than the newest package of the same driver family and that no
device uses. `pnputil /enum-drivers` shows every package.

```powershell
$delete = $false   # $true also deletes the listed packages; needs an elevated PowerShell
$repo = "$env:windir\System32\DriverStore\FileRepository"
# Parses the English output of pnputil.
$pkgs = New-Object System.Collections.Generic.List[object]
$cur = $null
foreach ($line in (pnputil /enum-drivers /ids /devices)) {
    if ($line -match '^Published Name:\s+(.+)$') { $cur = @{ Name = $Matches[1].Trim(); Devices = 0 }; $pkgs.Add($cur); continue }
    if ($null -eq $cur) { continue }
    if ($line -match '^\s+Instance ID:') { $cur.Devices++; continue }
    if ($line -match '^(Original Name|Driver Version|Driver Package ID|Family ID):\s+(.+)$') { $cur[$Matches[1]] = $Matches[2].Trim() }
}
$rows = foreach ($p in $pkgs) {
    $version = [version]'0.0'
    try { $version = [version](($p['Driver Version'] -split '\s+')[1]) } catch {}
    $family = $p['Family ID']
    if (-not $family) { $family = $p['Original Name'] }
    [pscustomobject]@{ Name = $p.Name; Inf = $p['Original Name']; Family = $family; Version = $version; Devices = $p.Devices; Id = $p['Driver Package ID'] }
}
$old = @(foreach ($group in ($rows | Group-Object Family | Where-Object Count -gt 1)) {
    $newest = ($group.Group | Sort-Object Version -Descending | Select-Object -First 1).Version
    $group.Group | Where-Object { $_.Version -lt $newest -and $_.Devices -eq 0 }
})
$total = 0
foreach ($p in $old) {
    $bytes = (Get-ChildItem -LiteralPath (Join-Path $repo $p.Id) -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    $total += $bytes
    '{0,-11} {1,-40} {2,-18} {3,7:N1} MB' -f $p.Name, $p.Inf, $p.Version, ($bytes / 1MB)
    if ($delete) { pnputil /delete-driver $p.Name }
}
'{0} old, unused packages, {1:N0} MB' -f $old.Count, ($total / 1MB)
```

Ways to delete them **(admin)**, pick one:

1. **Disk Cleanup:** `cleanmgr` → C: → _Clean up system files_ → tick **Device driver packages**.
   It keeps the newest version of each driver.
2. **DriverStore Explorer (RAPR)**, open source and portable
   (<https://github.com/lostindark/DriverStoreExplorer>): _Select Old Driver(s)_ → _Delete Package_.
   Leave _Force Deletion_ unticked.
3. **pnputil:** run the script above with `$delete = $true`. Without `/force`, pnputil refuses to
   delete a package that a device uses.

Rules:

- Create a restore point first. A deleted version is no longer available to _Roll Back Driver_.
- Never pass `/force` to pnputil, and never delete folders in `FileRepository` by hand.
- After switching GPU vendor (AMD ↔ NVIDIA), remove the old vendor's packages with DriverStore Explorer.

GPU driver installers also leave copies outside the driver store:

- `C:\ProgramData\NVIDIA Corporation\NVIDIA App\UpdateFramework\ota-artifacts`: the NVIDIA App keeps the
  downloaded installer and its extracted files after installing the driver. Delete the contents **(admin)**.
- `C:\NVIDIA`, `C:\AMD`, `C:\Intel`: extracted files from standalone driver setups. Delete them if present.

## 3. Hotspots on this machine

| What                                    | Path                                                                         | How to clean                                                                                                                                                                                                    |
| --------------------------------------- | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Windows Sandbox base image              | `C:\ProgramData\Microsoft\Windows\Containers`                                | If Sandbox is unused: _Turn Windows features on or off_ → untick **Windows Sandbox** → restart. Hard links inflate its measured size.                                                                           |
| Visual Studio download cache            | `C:\ProgramData\Microsoft\VisualStudio\Packages`                             | Visual Studio Installer → _Modify_ → _Installation locations_ → untick _Keep download cache after the installation_.                                                                                            |
| NVIDIA shader cache                     | `%LOCALAPPDATA%\NVIDIA\DXCache`                                              | Delete the contents; it refills as games recompile shaders. Cap it with _Shader Cache Size_ in the NVIDIA App.                                                                                                  |
| Chrome on-device AI model (Gemini Nano) | `%LOCALAPPDATA%\Google\Chrome\User Data\OptGuideOnDeviceModel`               | Disable `chrome://flags/#optimization-guide-on-device-model`, or set DWORD `GenAILocalFoundationalModelSettings` = 1 under `HKLM\SOFTWARE\Policies\Google\Chrome`. Chrome's on-device AI features stop working. |
| NVIDIA App driver installers            | `C:\ProgramData\NVIDIA Corporation\NVIDIA App\UpdateFramework\ota-artifacts` | Delete the contents **(admin)**.                                                                                                                                                                                |
| Corsair iCUE game lighting effects      | `C:\ProgramData\Corsair\CUE5\GameSDKEffects`                                 | Delete if iCUE game integration is unused.                                                                                                                                                                      |
| Teams (new) cache                       | `%LOCALAPPDATA%\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams` | Quit Teams, then delete the contents.                                                                                                                                                                           |
| ReclaiMe saved scans                    | `%LOCALAPPDATA%\ReclaiMe\*.savestate`                                        | Delete once the recovery is finished.                                                                                                                                                                           |
| Rust toolchain for Python builds        | `%LOCALAPPDATA%\puccinialin`                                                 | Delete; it is downloaded again when needed.                                                                                                                                                                     |
| Armoury Crate diagnostics               | `C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis`                                | Delete the contents.                                                                                                                                                                                            |
| Azure Functions Core Tools              | `%LOCALAPPDATA%\AzureFunctionsTools`                                         | Delete old releases; Visual Studio downloads the one it needs.                                                                                                                                                  |
| Large apps                              | `C:\Program Files`, `C:\Program Files (x86)`                                 | Uninstall what is unused: Docker Desktop, SQL Server and SSMS, Office, old Windows SDKs under `Windows Kits`.                                                                                                   |

## 4. Developer caches

They grow wherever they live, including the folders that `mklink.bat` keeps on H:.

| Cache                        | How to clear                                                                                                                                                                                                                             |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| npm                          | `npm cache clean --force`                                                                                                                                                                                                                |
| uv                           | `uv cache prune` drops unused entries, `uv cache clean` drops everything                                                                                                                                                                 |
| pip                          | `pip cache purge`                                                                                                                                                                                                                        |
| NuGet                        | `dotnet nuget locals all --clear` (packages download again on the next restore)                                                                                                                                                          |
| Node versions (nvm)          | `nvm list`, then `nvm uninstall <version>`                                                                                                                                                                                               |
| Docker                       | `docker system prune -a`; add `--volumes` to drop unused volumes too                                                                                                                                                                     |
| WSL and Docker `.vhdx` disks | They never shrink on their own. Run `wsl --shutdown`, then `Optimize-VHD -Path <file> -Mode Full` **(admin, Hyper-V module)**, or in `diskpart`: `select vdisk file="<file>"`, `attach vdisk readonly`, `compact vdisk`, `detach vdisk`. |

## 5. Moving data off C:

To move one more folder with [mklink.bat](mklink.bat):

1. Quit the apps that use the folder.
2. Move the folder to its target on H:. `mklink` fails while the link path still exists.
3. Run the matching `mklink /d` line **(admin, or Developer Mode on)**.

Other ways:

- Tools that take a cache location: `UV_CACHE_DIR`, `PIP_CACHE_DIR`, `NUGET_PACKAGES`, `npm config set cache <path>`.
- Settings → System → Storage → _Advanced storage settings_ → _Where new content is saved_.
- Store apps: Settings → Apps → _Installed apps_ → _Move_.
- Known folders (Documents, Pictures…): folder _Properties_ → _Location_.

## 6. Windows built-in cleanup

- **Storage Sense:** Settings → System → Storage. Turn on Storage Sense, then check _Temporary files_ and
  _Cleanup recommendations_.
- **Disk Cleanup (admin):** `cleanmgr` → _Clean up system files_: Windows Update Cleanup, Delivery
  Optimization Files, Device driver packages, Previous Windows installation(s), Temporary Windows
  installation files, system error memory dumps and minidumps, Windows error reports, Microsoft Defender
  Antivirus, DirectX Shader Cache, Language resource files.
- **Component store (admin):** `DISM /Online /Cleanup-Image /AnalyzeComponentStore`, then
  `DISM /Online /Cleanup-Image /StartComponentCleanup`. Adding `/ResetBase` frees more, but installed
  updates can no longer be uninstalled.
- **Restore points (admin):** `vssadmin list shadowstorage`. Cap them with
  `vssadmin resize shadowstorage /for=C: /on=C: /maxsize=5%` or in System Properties → _System Protection_
  → _Configure_.
- **Reserved storage (admin):** `DISM /Online /Get-ReservedStorageState`.
  `DISM /Online /Set-ReservedStorageState /State:Disabled` works only while no update is pending;
  Microsoft advises keeping it on.
- **CompactOS (admin):** `compact /compactos:query`. `compact /compactos:always` compresses the Windows
  binaries and `compact /compactos:never` reverts it. Windows enables it only where it decides it helps;
  a fast CPU on NVMe barely notices it.
- **Hibernation (admin):** `powercfg /h off` deletes `hiberfil.sys` and disables Fast Startup;
  `powercfg /h /type reduced` keeps Fast Startup with a smaller file.
- **Page file:** System Properties → _Advanced_ → _Performance_ → _Virtual memory_: move or resize it.
- **Optional features and languages:** Settings → System → _Optional features_; Settings → Time & language
  → _Language & region_.

## 7. Do not

- Delete by hand: `C:\Windows\WinSxS`, `C:\Windows\System32\DriverStore`, `C:\Windows\Installer` (the MSI
  and MSP files Windows needs to repair and uninstall apps), `C:\ProgramData\Package Cache`,
  `C:\ProgramData\LGHUB\depots`.
- Run `pnputil /delete-driver ... /force`, "driver updater" tools or registry cleaners.
