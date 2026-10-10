## Monitor disk health

- **Source:** `diskwatch/src/` (program.cs, commandRunner.cs, config/, models/, parsers/, popup/)
- **Dependencies:** `smartctl` (smartmontools)
- **Description:** Read-only SMART monitor that runs smartctl checks, compares results against previous state, and alerts you when something changes. Silent when healthy. Detection only - never repairs.

---

### Usage

```
diskwatch
```

Runs SMART checks, prints a verdict to the console (still visible if run from a terminal), and shows a dark-themed popup with aligned monospace layout. Intended to be driven by Task Scheduler; the popup is the core output.

Admin rights are required - full `smartctl` data needs elevation.

#### Exit codes

| Code | Meaning |
|---|---|
| 0 | Healthy - no changes since last check |
| 1 | Something changed (important attrs only) |
| 2 | Configuration/execution error (no commands configured, no output produced) |
| 3 | Another instance is already running |

A named mutex (`Global\diskwatch`) prevents concurrent instances - a second launch prints a message and exits with code 3.

---

### How it works

#### Checks

| Check | Command | What it reports |
|---|---|---|
| SMART | `smartctl -x /dev/sda` | Drive identity, overall health, endurance, watched attributes |

The exact commands come from `bin/.cmds`. Only `smartctl` is accepted as executable; argument strings are sanitized against shell metacharacters. Mutating flags (e.g. `smartctl -t`) are rejected; diskwatch never repairs, cleans, or modifies the system. The executable is resolved to an absolute path at startup (`smartctl` found via `PATH`). A per-command timeout (`commandTimeoutMinutes` in `.conf`, default 60) kills hung processes and reports them as timed out. Invalid entries are silently skipped.

SMART attributes to track come from `bin/.smart` in `ID=Name` format. The first 5 are **important** (shown in the popup's Critical Health section, trigger warnings on change); the rest are **extras** (informational only).

#### State and comparison

Every run saves parsed state to `logs/runs/<timestamp>/result.json`. Previous run directories are scanned backward (newest first) until a parseable baseline is found. This handles corrupted intermediate baselines gracefully. The following differences trigger a change (exit 1 + warning popup):

- SMART health changed
- SMART endurance changed
- Any important SMART attribute changed
- A previously present SMART device disappeared

Extra attribute changes are tracked but never trigger a warning. If a previous baseline exists but none can be read, the baseline-unreadable condition is reported as an important change. If no previous run directories exist at all (first run), no changes are reported.

#### Raw output logging

Every run also saves the raw command output to `logs/runs/<timestamp>/smartctl_*.json` (compact JSON per command, alongside `result.json`). If the parser ever misinterprets a tool's output, the raw output is still there for manual inspection.

Only the `logRetention` most recent timestamped run directories are kept (configurable in `.conf`, default 5, minimum 1); older runs are pruned on each execution. Non-timestamped directories and symlinks under `logs/runs/` are never pruned.

#### Changelog

When `.conf` sets `saveChanges=true`, every run with an important change (the same condition that yields exit 1) appends a timestamped entry to `logs/changelog.txt`:

```
[2026-09-06 12:55:59]
sda: health PASSED → FAILED

```

Normal healthy runs append nothing, and extra-attribute-only diffs never qualify — so the file shows exactly when each important change happened. The stamp matches the run directory name, so each entry maps 1:1 to `logs/runs/<timestamp>/` while normal pruning hasn't removed it. Append-only with no retention; change lines accumulate slowly.

#### Popup

At the end of every run, a custom dark-themed dialog with Consolas monospace font shows the summary (unless `.conf` sets `warnOnly=true` and nothing important changed):

| Condition | Title text |
|---|---|
| No important changes | "Critical values are stable since last run." |
| Important changes detected | "Some critical values have changed since the last run." |

A dim Extra button opens the full categorized SMART breakdown in the same window - device identity, health, endurance, the Critical Health table, and the extras table, all right-aligned and scrollable when overflowing.

#### Configuration

`bin/.cmds` - commands to run:

```ini
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

`bin/.conf` - behavior flags:

```ini
# warnOnly=true -> popup only when something changed, false -> popup every scan
warnOnly=false
# logRetention=N -> keep the N latest scan log folders, delete older ones
logRetention=5
# saveChanges=true -> append important changes to logs/changelog.txt
saveChanges=false
# commandTimeoutMinutes=N -> kill commands that run longer than N minutes (default 60)
commandTimeoutMinutes=60
```

---

### Design decisions

- **Read-only:** diskwatch never repairs, cleans, or modifies the system. The tool's job is to tell you something changed - you decide what to do.
- **Change detection over logging:** logging every run creates noise; most runs are identical. Change detection suppresses the common case and surfaces deltas. The exit code makes it scriptable.
- **Raw output preserved:** if the parser misinterprets a tool's output (new Windows version, locale differences), the raw output is still available for manual inspection.
- **Static device list over auto-detection:** auto-detecting drives via WMI adds complexity and can miss devices. A static config list is simpler and more predictable.
- **No daemon mode:** disk health checks are IO-intensive and don't need sub-minute granularity. Task Scheduler with a periodic trigger is the right tool.
- **Custom dialog over MessageBox:** the monospace Consolas dialog enables aligned tables that would be misaligned in MessageBox's proportional font.

---

### References

- [smartmontools](https://www.smartmontools.org/) - provides `smartctl` (required)

---

### Source tree

```
diskwatch/
├── src/
│   ├── program.cs           ← Main(), mutex, command running
│   ├── commandRunner.cs     ← process launcher
│   ├── config/
│   │   ├── commands.cs      ← .cmds loading/validation
│   │   ├── smartattrs.cs    ← .smart loading
│   │   └── settings.cs      ← .conf loading
│   ├── models/
│   │   ├── state.cs         ← SmartState, MasterState
│   │   └── persistence.cs   ← load/save/diff, JSON mapping
│   ├── parsers/
│   │   ├── conf.cs          ← shared config reader
│   │   ├── build.cs         ← run directory → MasterState
│   │   └── smartctl.cs      ← smartctl parsing
│   └── popup/
│       ├── window.cs        ← popup dialog (main/extra views)
│       └── scrollpanel.cs   ← custom slim scrollbar
├── bin/
│   ├── diskwatch.exe       ← compiled binary
│   ├── .cmds                ← commands to run (edit this)
│   ├── .smart               ← SMART attr IDs and names (edit this)
│   └── .conf               ← behavior flags
├── logs/                    ← runs/ (per-run dirs: result.json + smartctl_*.json) + changelog.txt
├── popup.png                ← popup screenshot for docs
├── build.bat
└── README.md                ← this document
```

---

### Known limitations

- **Admin required** - without elevation, `smartctl` may show limited data.
- **Windows-only** - uses `smartctl`.
- **smartctl manual** - install and configure it in `.cmds`; not bundled.
- **No drive discovery** - every device must be configured in `.cmds` and SMART attrs in `.smart`.
- **No daemon mode** - use Task Scheduler for periodic runs.
- **Locale-sensitive parsing** - `smartctl` output varies by system locale. The parser handles English output; other locales may cause attributes to be skipped (raw output preserved in `runs/` for manual inspection).
- **Read-only is enforced, not guaranteed** - mutating flags in `.cmds` are rejected, but the tool cannot prevent a user from editing `.cmds` to add repair commands. The README documents the intended read-only contract.