## Log battery capacity

- **Source:** `src/config.cs` (config parsing), `src/batteryReader.cs` (WMI), `src/program.cs` (orchestrator + line builder)
- **Dependencies:** `System.Management.dll` (WMI, ships with Windows)
- **Description:** Logs battery stats via WMI to `logs/batcap.log`. Runs silently - built as a Windows application (`/target:winexe`), no console window. Designed for Task Scheduler.

To know why I built this tool, read `STORY.md`. For the config format, read `config.md`.

---

### Usage

Run via Task Scheduler (daily or weekly trigger) or double-click to log silently. No console window is shown.

Appends one line to `logs/batcap.log`:

```
[2026-07-23 15:01:15] Design=44021mWh Full=44494mWh Remaining=39555mWh Voltage=11794mV ChargeRate=0mW DischargeRate=12949mW Charging=False
```

The line is built dynamically from the flags enabled in `bin/.conf` - disabled fields are omitted entirely, and new lines can have a different field set than older ones. Fields always appear in the same order when present.

#### Fields

| Field | Source | Unit |
|---|---|---|
| Design | `bin/.conf` (44021 default) | mWh |
| Full | BatteryFullChargedCapacity WMI | mWh |
| Remaining | BatteryStatus WMI | mWh |
| Voltage | BatteryStatus WMI | mV |
| ChargeRate | BatteryStatus WMI | mW |
| DischargeRate | BatteryStatus WMI | mW |
| Charging | BatteryStatus WMI | bool |
| PowerOnline | BatteryStatus WMI | bool |
| Critical | BatteryStatus WMI | bool |
| Chemistry | Win32_Battery WMI | name |
| EstimatedChargeRemaining | Win32_Battery WMI | % |
| WearPercent | computed | % |
| EquivCycles | computed | count |

Every field is independently toggleable; defaults enable the first seven, matching the original output. See `config.md` for each flag's source and reliability caveats.

---

### How it works

#### Configuration

`bin/.conf` holds the design capacity and a boolean flag per field. Flags are grouped in the file by source class, but the parser is flat - keys are case-insensitive, `#`/`;` comments and blank lines are skipped. `config.md` documents every key.

#### WMI polling

`BatteryReader` queries three sources, each in its own isolated `try/catch`:

1. `BatteryFullChargedCapacity` (`root\WMI`) - Full.
2. `BatteryStatus` (`root\WMI`) - Remaining, Voltage, ChargeRate, DischargeRate, Charging, PowerOnline, Critical. All read from the same object; `PowerOnline`/`Critical` add no extra query.
3. `Win32_Battery` (`root\cimv2`) - Chemistry, EstimatedChargeRemaining. Separate namespace, separate query.

If every flag belonging to a class is disabled, that class is not queried at all - the WMI call is skipped, not made and discarded. A computed field (WearPercent, EquivCycles) enables the query that feeds it.

One class failing (e.g. a flaky provider) doesn't block the others - each failure just leaves that field unset, and the field is omitted from the line.

#### Logging

Appends one line to `logs/batcap.log` with a timestamp and the enabled fields. Append-only - never rewrites or prunes history. Old lines are left untouched.

---

### Design decisions

- **Hardcoded design capacity over WMI:** `BatteryStaticData.DesignedCapacity` intermittently fails on this hardware. Since design capacity never changes, the config file removes the dependency on the broken class entirely.
- **WMI over `powercfg`:** `powercfg /batteryreport` returns blanks for every capacity field on this machine, so the report is useless. WMI counters that do work are polled directly.
- **Isolated queries:** one class failing shouldn't blank every other field, so each query has its own `try/catch` and the line only includes values that were actually read.
- **Config-gated queries:** disabling a field skips its WMI class entirely, so an unused flaky class is never touched.
- **Append-only log:** a single running history file for manual trend tracking, exactly the view `powercfg` was supposed to provide.

---

### References

No external references - self-contained.

---

### Source tree

```
batcap/
├── src/
│   ├── config.cs             ← ini-style .conf parser + per-field toggles
│   ├── batteryReader.cs      ← WMI queries (one try/catch per class)
│   └── program.cs            ← orchestrator + dynamic line builder + state
├── bin/
│   ├── batcap.exe           ← compiled binary
│   ├── .conf                 ← design capacity + field toggles (edit this)
│   └── .cyclestate          ← EquivCycles discharge total (auto-managed)
├── logs/
│   └── batcap.log           ← append-only log file
├── config.md                 ← config reference
├── STORY.md                  ← why batcap exists
├── build.bat
└── README.md                ← this document
```

---

### Known limitations

- Silent by design - no console output, no notification on failure.
- Design capacity is config-derived, not auto-detected - must be set correctly for accurate health percentages.
- No retention policy - the log grows until you delete it.
- Cycle count and battery temperature are not supported - this battery's firmware doesn't expose them (see `config.md`).
