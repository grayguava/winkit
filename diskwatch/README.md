# diskwatch — disk health monitor with change detection

- **Tool:** `diskwatch/bin/diskwatch.exe`
- **Source:** `diskwatch/src/`
- **Language:** C#, compiled via `csc.exe`
- **Role:** Read-only disk health monitor. Runs `fsutil`, `chkdsk`, `smartctl`, and Event Log checks; compares against previous state and shows a popup when something changes. Silent when healthy.

---

## Usage

```cmd
diskwatch
```

Runs all checks, prints a verdict to the console, and shows a popup with the summary.

```cmd
diskwatch --remind
```

Shows the same popup from the last run without running checks again. Useful for Task Scheduler.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Healthy — no changes since last check |
| `1` | Something changed |

---

## Configuration

`bin/config.ini`:

```ini
Drives=C,D
SmartCtlPath=smartctl
SmartDevices=/dev/sda
SmartAttrs=5,196,197,198
```

Only configured `SmartAttrs` IDs are tracked.

---

## State

Stored in `logs/result.json`. Raw command output preserved in `logs/<timestamp>/` per run. Only the 5 most recent run directories are kept.

---

## Design

```
src/
├── program.cs         — Main(), --remind, orchestrates checks, popup
├── config.cs          — INI reader
├── commandrunner.cs   — Runs processes
├── resultfile.cs      — Raw output save/load
├── masterstate.cs     — State model, parsing, diff, pretty JSON
└── remind.cs          — On-screen popup
```

Build with `build.bat`. Requires `System.Runtime.Serialization.dll`, `System.Web.Extensions.dll`, and `System.Windows.Forms.dll` (ship with .NET Framework).

---

## Known limitations

- **Admin required** — run as Administrator for `fsutil`, `chkdsk`, and full `smartctl` data.
- **Windows-only** — uses `fsutil`, `chkdsk`, and Windows Event Log.
- **smartctl needed for SMART** — optional, but required if you configure SmartDevices.
- **No drive discovery** — configure drives and devices in `config.ini`.
- **No daemon mode** — use Task Scheduler for periodic runs.
