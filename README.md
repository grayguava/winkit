## Windows utility collection

A collection of small Windows utilities, each built to solve one specific recurring problem - file management, backups, metadata handling, desktop customization, and system monitoring. Designed with safety in mind: destructive operations verify before they commit.

Each tool is standalone and portable - a single `.exe` compiled with Windows' built-in `csc.exe`, no runtime, no install step. CLI tools are colocated in `shared/bin/` so a single PATH entry covers all of them.

---

## Tools

#### [wallswitch](wallswitch/README.md)

- **Source:** `wallswitch/src/` (core/, platform/)
- **Dependencies:** `System.Windows.Forms`
- **Description:** Wallpaper daemon with multiple configurable pools (`.pools`) and targets (`.targets`) - one hotkey per pool cycles a shuffle queue with no repeats until exhausted, applied to the desktop, Windows Terminal background, and registry persistence.

#### [kdbx-backup](kdbx-backup/README.md)

- **Source:** `kdbx-backup/src/` (watcher.cs, push.cs)
- **Dependencies:** rclone (for cloud push)
- **Description:** Two-tool backup pipeline that snapshots KeePass `.kdbx` files on change and pushes the snapshots to three cloud providers.

#### [diskwatch](diskwatch/README.md)

- **Source:** `diskwatch/src/` (multi-file: config/, models/, parsers/, popup/)
- **Dependencies:** Windows built-ins + optional smartctl
- **Description:** Read-only disk health monitor with change detection and popup alerts. Silent when healthy; flags changes with exit code + popup.

#### [batcap](batcap/README.md)

- **Source:** `batcap/src/` (Config.cs, BatteryReader.cs, program.cs)
- **Dependencies:** `System.Management.dll` (WMI)
- **Description:** Battery capacity logger via WMI, appends to `logs/batcap.log`. Silent, designed for Task Scheduler.

#### [shared](shared/README.md)

- **Source:** `shared/src/`
- **Dependencies:** none (plus `System.Windows.Forms` for dirdiff/etsu)
- **Description:** Portable CLI tool collection (delcache, dirdiff, catsort, reindex, etsu, mmu), one PATH entry for all.

#### [archive](archive/README.md)

- **Source:** - (retired)
- **Dependencies:** - (retired)
- **Description:** Retired/abandoned tools, kept for reference only.

---

### Stories

Why some of the tools exist:

- [Why batcap exists](batcap/STORY.md) - `powercfg` shows blanks on an EliteBook; WMI workaround.
- [Why kdbx-backup exists](kdbx-backup/STORY.md) - the bootstrap problem and the trust chain behind it.
- [Why wallswitch exists](wallswitch/STORY.md) - merging two wallpaper tools into one daemon, then growing pools and targets.

---

### Building

Each tool has its own `build.bat` at the tool root (or one combined `build.bat` for everything in `shared/`). All use Windows' built-in C# compiler (`csc.exe`). No Visual Studio, no NuGet, no dotnet CLI, no install step.

- Installer: run `<tool>\build.bat` from that tool's folder.
- For `shared/`: run `shared\build.bat` - compiles every CLI tool into `shared/bin/`.

#### Prerequisites

- .NET Framework 4.0+ (ships with Windows 8+; available for Windows 7).
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` - part of the .NET Framework SDK component of Windows.
- `System.Windows.Forms.dll` / `System.Management.dll` / `System.Web.Extensions.dll` - referenced by specific tools, shipped with .NET Framework.

---

### Compatibility

All tools are compiled for **x64 Windows** (Windows 7+) and target .NET Framework 4.0.

Everything here runs on 64-bit Windows. On 32-bit Windows, change `build.bat` (in each tool or `shared/`) to use the non-64 compiler path `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`.

CLI tools in `shared/bin/` are portable in logic; native folder dialogs (dirdiff, etsu) are Windows-only.

**Safety-first design:** destructive tools verify before committing (copy + hash-check + delete) and support `--dry-run` where relevant. `reindex` includes rollback logs for its last 25 runs.