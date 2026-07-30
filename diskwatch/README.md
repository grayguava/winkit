# diskwatch — disk health monitor with change detection

Read-only disk health monitor that runs system checks, compares results against previous state, and alerts you when something changes. Silent when healthy.

- **Tool:** `diskwatch/bin/diskwatch.exe`
- **Source:** `diskwatch/src/`
- **Language:** C#, compiled via `csc.exe` as winexe (no console window)
- **Role:** Detection only. Never runs DISM, SFC, chkdsk /f, or SMART self-tests.

---

## Usage

```
diskwatch
```

Runs all checks, prints a verdict to the console (still visible if run from a terminal), and shows a dark-themed popup with aligned monospace layout.

```
diskwatch --remind
```

Shows the same popup from the last run without re-running checks. Useful for Task Scheduler reminders.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Healthy — no changes since last check |
| 1 | Something changed (important attrs only) |

### Mutex

A named mutex prevents concurrent instances. If diskwatch is already running, a second launch prints `diskwatch is already running.` and exits.

---

## What it checks

### fsutil dirty query

Reads the volume dirty bit for each configured drive. A set dirty bit means the filesystem detected corruption and will run chkdsk at next boot.

```
fsutil dirty query C:
```

Parsed for: "NOT Dirty" (clean), "is set" (dirty).

### chkdsk /scan

Performs a read-only scan of the filesystem metadata and reports problems without repairing anything.

```
chkdsk C: /scan
```

Parsed for:
- **Access Denied** — tool not elevated, result unknown.
- **found no problems / No further action** — filesystem is clean.
- **found problems / problems found** — issues detected.
- **KB in bad sectors** — exact count of bad sector reallocations.

### smartctl -x

Runs smartctl with full output for each configured device. Parsed for:
- **Device Model, Serial Number, Firmware Version** — drive identity.
- **SMART overall-health self-assessment test result** — PASSED/FAILED.
- **Percentage Used Endurance Indicator** — remaining endurance (NVMe).
- **Watched attributes** — tracked by ID from config; stored by name.

### Windows Event Log

Scans up to 50 recent entries across three logs (Wininit/Operational, System, Application) for disk repair activity. Only flags:
- Wininit-sourced events with InstanceId 262 or 264.
- Any Warning event containing both "disk" and "repair" in the message.

---

## Configuration

### .cmds

`bin/.cmds` lists every command to run, grouped by section. Each section is a command category; each line under it is a full command:

```ini
[fsutil]
fsutil dirty query C:
fsutil dirty query D:

[chkdsk]
chkdsk C: /scan
chkdsk D: /scan

[smartctl]
smartctl -x /dev/sda
```

The first word of each line is the executable, the rest are its arguments. Only `fsutil`, `chkdsk`, and `smartctl` are accepted as executables; argument strings are sanitized against shell metacharacters. Invalid entries are silently skipped. Section names determine how the parser interprets output:
- `[fsutil]` — dirty bit check via fsutil. Drive letter extracted from output.
- `[chkdsk]` — read-only filesystem scan. Drive letter extracted from output.
- `[smartctl]` — SMART data. Device keyed by section+index.

The Event Log reader is built-in (uses .NET EventLog API, not an external command).

### .smart

`bin/.smart` lists SMART attributes to track in `ID=Name` format. The first 5 are **important** (shown in the Critical Health section of the popup and trigger warnings on change). The rest are **extras** (informational, shown in Drive Information, no warning).

```ini
# First 5 = important values shown in popup
# Rest = informational only
5=Reallocated Sectors
197=Current Pending Sectors
198=Offline Uncorrectable
196=Reallocation Events
199=UDMA CRC Errors

169=Endurance Remaining
194=Temperature Celsius
9=Power On Hours
177=Wear Leveling Count
241=Total LBAs Written
242=Total LBAs Read
1=Raw Read Error Rate
```

Names are human-readable (spaces allowed) and used as keys in result.json. Lines starting with `#` or `;` are comments. Blank lines are ignored.

---

## State and change detection

### result.json

Pretty-printed JSON stored in `logs/<timestamp>/result.json` after every run. Contains parsed state for all drives, SMART devices, and the most recent repair timestamp. The previous run's `result.json` is loaded as the comparison baseline — no root-level `logs/result.json` duplicate. Loaded via `JavaScriptSerializer` for deserialization; written with a custom pretty-printer.

Structure:

```json
{
  "timestamp": "2026-07-30T11:14:26.0000000",
  "drives": {
    "C": {
      "dirty": false,
      "filesystem": "clean",
      "badSectorsKb": -1
    }
  },
  "smart": {
    "sda": {
      "model": "CONSISTENT S6 SSD 256GB",
      "serial": "AA000000000000001311",
      "firmware": "V0422A0",
      "health": "FAILED",
      "endurance": 98,
      "important": {
        "Reallocated Sectors": 41,
        "Current Pending Sectors": 41,
        "Offline Uncorrectable": 32,
        "Reallocation Events": 32,
        "UDMA CRC Errors": 1
      },
      "extras": {
        "Temperature Celsius": 50,
        "Power On Hours": 1626,
        "Total LBAs Written": 193694,
        "Total LBAs Read": 276902
      }
    }
  },
  "lastRepair": null
}
```

SMART attributes are stored by name (not numeric ID). The `important` and `extras` split is determined by position in `.smart` (first 5 = important).

### Diff comparison

On every run, the current state is compared against the previous state loaded from the newest timestamped run directory's `result.json`. The following differences trigger a change:

- **Dirty bit** toggled.
- **Filesystem status** changed (clean / issues / unknown).
- **Bad sector count** changed.
- **SMART health** changed (PASSED / FAILED).
- **SMART endurance** changed.
- **Any important SMART attribute** changed.
- **Repair event timestamp** changed.

Extra SMART attribute changes are tracked but do NOT trigger a warning popup or exit code 1.

If no previous state exists (first run), no changes are reported.

### Raw output logging

Every run saves the raw command output to `logs/<timestamp>/runs/` directory:

```
logs/
├── 2026-07-30T11-14-26/
│   ├── result.json
│   └── runs/
│       ├── fsutil_C.json
│       ├── chkdsk_C.json
│       ├── smartctl_sda.json
│       └── wininit.json
└── 2026-07-10T09-15-00/
    └── ...
```

Each file contains:

```json
{"ExitCode":0,"Output":"..."}
```

Raw output files use inline JSON (compact, no pretty printing).

### Log retention

Only the 5 most recent timestamped run directories are kept. Older runs are pruned automatically on each execution.

---

## Popup

At the end of every normal run (and via `--remind`), a custom dark-themed dialog with Consolas monospace font shows the summary. Two tiers:

| Condition | Icon | Title text |
|---|---|---|
| No important changes | Information | "Critical values are stable since last run." |
| Important changes detected | Warning | "Some critical values have changed since the last run." |

Layout:

```
<status line>

<date>

Drive C: Clean.
Drive D: Clean | 41 KB in bad sectors.

SMART overall: FAILED

Critical Health
──────────────────────────────
Reallocated Sectors         41
Current Pending Sectors     41
Offline Uncorrectable       32
Reallocation Events         32
UDMA CRC Errors              1

Drive Information
──────────────────────────────
Endurance                 98%
Temperature              50 °C
Power-On Hours        1626 h
LBAs Written         193,694
LBAs Read            276,902
```

Both sections share the same label/value column alignment. Values are right-aligned.

---

## Building

### Prerequisites

- .NET Framework 4.0+ (ships with Windows 8+; available for Windows 7).
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` — part of the .NET Framework SDK.
- `System.Web.Extensions.dll`, `System.Windows.Forms.dll`, `System.Drawing.dll` — reference assemblies that ship with .NET Framework.

### Build

```
build.bat
```

Compiles all source files in `src/` to `bin/diskwatch.exe` as a winexe (no console window). No Visual Studio, no NuGet, no dotnet CLI, no install step.

### Build output

```
diskwatch/
├── src/
│   ├── program.cs           ← Main(), mutex, --remind, runs commands, Event Log reader
│   ├── commandrunner.cs     ← Process launcher (no timeout)
│   ├── parser.cs            ← State model, SmartAttrDef, parsing, diff, pretty JSON
│   └── popup.cs             ← Custom dark monospace dialog with aligned tables
├── bin/
│   ├── diskwatch.exe        ← compiled binary (build output)
│   ├── .cmds                ← commands to run (edit this)
│   └── .smart               ← SMART attr IDs and names (edit this)
├── logs/                    ← auto-created, holds per-run dirs with result.json + runs/
├── build.bat
└── README.md
```

---

## Design decisions

### Why read-only

diskwatch never repairs, cleans, or modifies the system. It only reads diagnostic data and flags changes. The reasoning: automated repair tools (DISM, SFC, chkdsk /f) can cause more damage than they fix when triggered without human judgment. The tool's job is to tell you something changed — you decide what to do about it.

### Why change detection instead of logging

Logging every run creates noise — most runs are identical. Change detection suppresses the common case (healthy, no changes) and only surfaces deltas. The exit code (0 = clean, 1 = change) makes it scriptable: a Task Scheduler trigger on non-zero exit can send an alert.

### Why raw output is preserved

If the parser misinterprets a tool's output (new Windows version, locale differences), the raw output is still available in `logs/<timestamp>/` for manual inspection without re-running.

### Why smartctl device paths are manual

Auto-detecting drives via WMI or device enumeration adds complexity and can miss devices. A static config list is simpler and more predictable — you know exactly what the tool checks.

### Why no daemon mode

Disk health checks are IO-intensive (fsutil, chkdsk, smartctl all read from disk) and don't need sub-minute granularity. Task Scheduler with a weekly trigger is the right tool for periodic monitoring.

### Why a custom dialog instead of MessageBox

The monospace dialog with Consolas font enables aligned tables (Critical Health + Drive Information columns) that would be misaligned in MessageBox's proportional font.

---

## Compatibility

| Aspect | Status |
|---|---|
| OS | Windows 7+ (requires .NET Framework 4.0+) |
| Architecture | x64 (recompile for x86 if needed) |
| File system checks | NTFS, ReFS (via fsutil + chkdsk) |
| SMART | Any drive supported by smartctl |
| Dependencies | None beyond Windows built-ins + optional smartctl |
| Admin required | Yes — for fsutil, chkdsk, and full smartctl data |

---

## Known limitations

- **Admin required** — run as Administrator. Without elevation, fsutil reports "Access Denied", chkdsk cannot scan, and smartctl may show limited data.
- **Windows-only** — uses fsutil, chkdsk, and Windows Event Log.
- **smartctl optional but manual** — must be installed and configured in .cmds if you want SMART checks. Not bundled.
- **No drive discovery** — configure every drive in .cmds and SMART attrs in .smart.
- **No daemon mode** — use Task Scheduler for periodic runs.
- **Event log filtering is heuristic** — the wininit/repair event detection is based on keyword matching and may miss or falsely flag events depending on Windows version and language.
