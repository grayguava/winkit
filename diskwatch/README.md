## Monitor disk health

- **Source:** `diskwatch/src/` (program.cs, commandrunner.cs, config/, models/, parsers/, popup/)
- **Dependencies:** Windows built-ins (`fsutil`, `chkdsk`) + optional `smartctl` (smartmontools)
- **Description:** Read-only disk health monitor that runs system checks, compares results against previous state, and alerts you when something changes. Silent when healthy. Detection only - never repairs.

---

### Usage

```
diskwatch
```

Runs all checks, prints a verdict to the console (still visible if run from a terminal), and shows a dark-themed popup with aligned monospace layout. Intended to be driven by Task Scheduler; the popup is the core output.

Admin rights are required - `fsutil`, `chkdsk`, and full `smartctl` data all need elevation.

#### Exit codes

| Code | Meaning |
|---|---|
| 0 | Healthy - no changes since last check |
| 1 | Something changed (important attrs only) |

A named mutex prevents concurrent instances - a second launch prints `diskwatch is already running.` and exits.

---

### How it works

#### Checks

| Check | Command | What it reports |
|---|---|---|
| Dirty bit | `fsutil dirty query C:` | Whether the volume dirty bit is set (means chkdsk runs at next boot) |
| Filesystem scan | `chkdsk C: /scan` | Read-only scan of filesystem metadata; access denied, clean, problems, or bad sector count |
| SMART | `smartctl -x /dev/sda` | Drive identity, overall health, endurance, watched attributes |

The exact commands come from `bin/.cmds`, grouped by section. Only `fsutil`, `chkdsk`, and `smartctl` are accepted as executables; argument strings are sanitized against shell metacharacters. Invalid entries are silently skipped.

SMART attributes to track come from `bin/.smart` in `ID=Name` format. The first 5 are **important** (shown in the popup's Critical Health section, trigger warnings on change); the rest are **extras** (informational only).

#### State and comparison

Every run saves parsed state to `logs/<timestamp>/result.json`. The previous run's `result.json` is loaded as the comparison baseline. The following differences trigger a change (exit 1 + warning popup):

- Dirty bit toggled
- Filesystem status changed
- Bad sector count changed
- SMART health changed
- SMART endurance changed
- Any important SMART attribute changed

Extra attribute changes are tracked but never trigger a warning. If no previous state exists (first run), no changes are reported.

#### Raw output logging

Every run also saves the raw command output to `logs/<timestamp>/runs/` (compact JSON per command). If the parser ever misinterprets a tool's output, the raw output is still there for manual inspection.

Only the 5 most recent timestamped run directories are kept; older runs are pruned on each execution.

#### Popup

At the end of every run, a custom dark-themed dialog with Consolas monospace font shows the summary (unless `.warnc` sets `warnOnly=true` and nothing important changed):

| Condition | Title text |
|---|---|
| No important changes | "Critical values are stable since last run." |
| Important changes detected | "Some critical values have changed since the last run." |

A dim Extra button opens the full categorized SMART breakdown in the same window - device identity, health, endurance, the Critical Health table, and the extras table, all right-aligned and scrollable when overflowing.

#### Configuration

`bin/.cmds` - commands to run, grouped by section:

```ini
[fsutil]
fsutil dirty query C:
fsutil dirty query D:

[chkdsk]
chkdsk C: /scan

[smartctl]
smartctl -x /dev/sda
```

`bin/.smart` - SMART attributes to track (first 5 = important, rest = extras):

```ini
5=Reallocated Sectors
197=Current Pending Sectors
198=Offline Uncorrectable
169=Endurance Remaining
194=Temperature Celsius
```

`bin/.warnc` - behavior flags:

```ini
# warnOnly=true -> popup only when something changed, false -> popup every scan
warnOnly=false
```

---

### Design decisions

- **Read-only:** diskwatch never repairs, cleans, or modifies the system. Automated repair (DISM, SFC, chkdsk /f) can cause more damage than it fixes when triggered without human judgment. The tool's job is to tell you something changed - you decide what to do.
- **Change detection over logging:** logging every run creates noise; most runs are identical. Change detection suppresses the common case and surfaces deltas. The exit code makes it scriptable.
- **Raw output preserved:** if the parser misinterprets a tool's output (new Windows version, locale differences), the raw output is still available for manual inspection.
- **Static device list over auto-detection:** auto-detecting drives via WMI adds complexity and can miss devices. A static config list is simpler and more predictable.
- **No daemon mode:** disk health checks are IO-intensive and don't need sub-minute granularity. Task Scheduler with a periodic trigger is the right tool.
- **Custom dialog over MessageBox:** the monospace Consolas dialog enables aligned tables that would be misaligned in MessageBox's proportional font.

---

### References

- [smartmontools](https://www.smartmontools.org/) - provides `smartctl` (optional but required for SMART checks)

---

### Source tree

```
diskwatch/
├── src/
│   ├── program.cs           ← Main(), mutex, command running
│   ├── commandrunner.cs     ← process launcher
│   ├── config/
│   │   ├── commands.cs      ← .cmds loading/validation
│   │   ├── smartattrs.cs    ← .smart loading
│   │   └── settings.cs      ← .warnc loading
│   ├── models/
│   │   ├── state.cs         ← DriveState, SmartState, MasterState
│   │   └── persistence.cs   ← load/save/diff, JSON mapping
│   ├── parsers/
│   │   ├── conf.cs          ← shared config reader
│   │   ├── build.cs         ← run directory → MasterState
│   │   ├── chkdsk.cs        ← fsutil + chkdsk parsing
│   │   └── smartctl.cs      ← smartctl parsing
│   └── popup/
│       ├── window.cs        ← popup dialog (main/extra views)
│       └── scrollpanel.cs   ← custom slim scrollbar
├── bin/
│   ├── diskwatch.exe       ← compiled binary
│   ├── .cmds                ← commands to run (edit this)
│   ├── .smart               ← SMART attr IDs and names (edit this)
│   └── .warnc               ← behavior flags
├── logs/                    ← per-run dirs: result.json + runs/
├── popup.png                ← popup screenshot for docs
├── build.bat
└── README.md                ← this document
```

---

### Known limitations

- **Admin required** - without elevation, `fsutil` reports Access Denied, `chkdsk` cannot scan, and `smartctl` may show limited data.
- **Windows-only** - uses `fsutil`, `chkdsk`, and `smartctl`.
- **smartctl optional but manual** - install and configure it in `.cmds` if you want SMART checks; not bundled.
- **No drive discovery** - every drive must be configured in `.cmds` and SMART attrs in `.smart`.
- **No daemon mode** - use Task Scheduler for periodic runs.