## Log battery capacity

- **Source:** `src/program.cs`
- **Dependencies:** `System.Management.dll` (WMI, ships with Windows)
- **Description:** Logs battery stats via WMI to `logs/batcap.log`. Runs silently - built as a Windows application (`/target:winexe`), no console window. Designed for Task Scheduler.

To know why I built this tool, read `STORY.md`.

---

### Usage

Run via Task Scheduler (daily or weekly trigger) or double-click to log silently. No console window is shown.

Appends one line to `logs/batcap.log`:

```
[2026-07-23 15:01:15] Design=44021mWh Full=44494mWh Remaining=39555mWh Voltage=11794mV ChargeRate=0mW DischargeRate=12949mW Cycles=0 Charging=False
```

#### Fields

| Field | Source | Unit |
|---|---|---|
| Design | `bin/.conf` (44021 default) | mWh |
| Full | BatteryFullChargedCapacity WMI | mWh |
| Remaining | BatteryStatus WMI | mWh |
| Voltage | BatteryStatus WMI | mV |
| ChargeRate | BatteryStatus WMI | mW |
| DischargeRate | BatteryStatus WMI | mW |
| Cycles | BatteryCycleCount WMI | count |
| Charging | BatteryStatus WMI | bool |

---

### How it works

#### Design capacity

Reads `bin/.conf` - a one-line file with the battery's design capacity in mWh. This value never changes, so it is read from config instead of queried each run (see the story for why WMI can't supply it on this hardware).

#### WMI polling

Queries `BatteryFullChargedCapacity`, `BatteryStatus`, and `BatteryCycleCount` via WMI each run. All three return valid data reliably.

#### Logging

Appends one line to `logs/batcap.log` with a timestamp and all fields. Append-only - never rewrites or prunes history.

#### Configuration

`bin/.conf`:

```ini
# Design capacity in mWh
44021
```

Edit if your battery has a different design capacity. Lines starting with `#` are comments.

---

### Design decisions

- **Hardcoded design capacity over WMI:** `BatteryStaticData.DesignedCapacity` intermittently fails on this hardware. Since design capacity never changes, the config file removes the dependency on the broken class entirely.
- **WMI over `powercfg`:** `powercfg /batteryreport` returns blanks for every capacity field on this machine, so the report is useless. WMI counters that do work are polled directly.
- **Append-only log:** a single running history file for manual trend tracking, exactly the view `powercfg` was supposed to provide.

---

### References

No external references - self-contained.

---

### Source tree

```
batcap/
├── src/
│   └── program.cs            ← source
├── bin/
│   ├── batcap.exe           ← compiled binary
│   └── .conf                 ← design capacity (edit this)
├── logs/
│   └── batcap.log           ← append-only log file
├── STORY.md                  ← why batcap exists
├── build.bat
└── README.md                ← this document
```

---

### Known limitations

- Silent by design - no console output, no notification on failure.
- Design capacity is config-derived, not auto-detected - must be set correctly for accurate health percentages.
- No retention policy - the log grows until you delete it.
