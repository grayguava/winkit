## Simple screentime (no per-app tracking) — ABANDONED ATTEMPT

> **Status (2026-09-06): attempted, then given up on for now.** The code below is the last working state, kept for the reasoning as much as the implementation. Standard (non-elevated) mode worked and was hand-verified exact; elevated mode was corrected but never validated long-term. Nothing supersedes it.

### Why it was abandoned

The goal was a tiny accurate screentime reporter: boots/day + active-time/day, no per-app tracking, no GUI, no daemon. Every design converged on the same wall — **there is no source of truth for "was a human here," only proxies, and each proxy lies in a different direction:**

- **System log only (default mode):** locked-but-awake counts as active. Always overcounts, by an amount you can't measure from inside this mode.
- **+ Security log (elevated mode):** true unlocked time, but needs admin *and* the "Other Logon/Logoff Events" audit subcategory, which was found `No Auditing` on the dev machine — i.e. the feature silently had no data until auditing is manually enabled via `auditpol`. Fragile dependency for a personal tool.
- **Event-log retention:** old events get purged, so wide history windows degrade. Mitigated with a per-field-max merge (history heals upward, never zeroes), but that's complexity defending against the platform, not solving it.
- **Missed runs + long-off periods:** days older than the log's coverage horizon can't be distinguished from true-zero off-days without explicitly tracking the horizon (designed, never implemented).
- **Daemon alternative (considered, rejected for now):** a 60s-tick daemon with wall-clock sleep clamping + `GetLastInputInfo` idle detection would be more precise and needs no admin — but input-idle lies the other way (a 2-hour film reads as "idle"), and daemon failures are silent and unrecoverable (killed process = time that never existed; gaps can't tell "laptop off" from "daemon dead"). Even commercial trackers ship these same approximations behind nicer charts. For a personal tool, the effort/reward stopped making sense.

### What was actually built and proven (v1, this dir)

- One-shot winexe reporter: System-log markers (6005/12 boot, Power-Troubleshooter 1 + Kernel-Power 107 resume, Kernel-Power 42 sleep, 6006/6008 shutdown), span pairing with idempotent opens, local-midnight splitting, date-keyed upsert into a single log file (idempotent re-runs verified byte-identical).
- `elevatedMode` flag: awake spans **minus** definitely-locked spans (4800→4801), with a 1-day lookback to seed boundary state; sparse lock data keeps awake time rather than zeroing.
- History protection: per-field-max merge for past days (boots/active guarded independently), today exempt; `ERROR:` lines and out-of-window history preserved verbatim; legacy duplicate blocks collapse.
- Real bugs found by testing and fixed: (1) upsert swallowing trailing `ERROR:` lines into replaced block ranges; (2) intersecting awake∩unlocked treating unknown-as-locked, which zeroed a full week of real history (recovered via the max-rule on the next run); (3) boot-count evidence masking zeroed active time.
- Hand-verified exact on 3 days against raw event data (boots and active minutes matched to the minute, including stray-resume handling).
- Open at abandonment: 2-day-write simplification proposal, coverage-horizon skip for pre-horizon days, daemon-with-backfill hybrid. None implemented.

The notes below are kept as-written (two stale lines corrected to match the code) as documentation of the attempt.

---

## Simple screentime (no per-app tracking)

- **Source:** `screentime/src/` (program.cs, config/, events/, models/)
- **Dependencies:** none (Windows event log API, in-box since .NET 3.5)
- **Description:** One-shot reporter for boots-per-day and active-time-per-day. No daemon, no background process, no per-app tracking - it just tallies events Windows already logs, whenever you run it. Built as a windowless app (winexe): no console flashes, results are appended to `logs/screentime.log`, so right-click Run as administrator just works. No GUI (a small GUI can be added later; the event/timeline layers are kept separate from output for that).

---

### Usage

```
screentime [days]
```

Each run appends one block per day to `logs/screentime.log`:

```
[2026-09-06]
Boot count: 2
Screentime estimate: 2h 12m

[2026-09-05]
Boot count: 3
Screentime estimate: 9h 12m

```

`days` (1-365) overrides `days` in `bin/.conf`. No arguments = configured default. Errors are appended to the same log with a timestamp and `ERROR:` prefix.

Single file, no retention to configure: each run **upserts** its days by `[date]` header - existing blocks for those dates are refreshed in place, new dates are appended, everything else (including `ERROR:` lines) is left untouched. Re-running ten times a day leaves the file unchanged except today's refreshed numbers; past days are stable since their data is complete. Duplicate day blocks (from very old versions) are collapsed on the next run.

Eviction guarantee: if Windows has purged old events, a fresh query shows zero evidence for those days — and history is still safe. For past days the log keeps the per-field maximum of recorded vs. fresh numbers (boot count and active time guarded independently, so evidence in one channel can't mask loss in the other): history can only heal upward, never be zeroed. Today is exempt since its numbers legitimately accumulate. So `screentime.log` keeps your history even after the event log forgets it.

#### Exit codes

| Code | Meaning |
|---|---|
| 0 | Report printed |
| 1 | Runtime error (event log unreadable) |
| 2 | Usage/config error, or `elevatedMode=true` without admin rights |

---

### How it works

#### Modes

`bin/.conf` - behavior flags:

```ini
# elevatedMode=true -> also read lock/unlock events (Security log, needs admin)
# for true unlocked/active time. false -> System log only, no elevation needed.
elevatedMode=false
# days=N -> how many days to report (1-365, default 7). Overridable: screentime [days]
days=7
```

| Mode | Sources | Privilege | Meaning of "active" |
|---|---|---|---|
| `elevatedMode=false` (default) | System log only | Standard user | Machine awake: boot/resume -> sleep/shutdown. Locked-but-awake time counts as active (overcounts slightly). |
| `elevatedMode=true` | System log + Security log | Must run elevated, else exit 2 | Awake spans minus definitely-locked spans (each lock -> next unlock). Periods with no lock data keep awake time - sparse auditing never zeroes history. |

With `elevatedMode=true` but zero lock/unlock events in range (lock auditing off), it appends a warning to the log and falls back to awake time for that run rather than reporting a misleading zero.

#### Events used

| Event | Source | Meaning |
|---|---|---|
| 6005 | EventLog (System) | Boot marker; also the boots-per-day counter (one per boot) |
| 12 | Microsoft-Windows-Kernel-General (System) | OS started (active-start) |
| 1 | Microsoft-Windows-Power-Troubleshooter (System) | Resumed from sleep (active-start) |
| 107 | Microsoft-Windows-Kernel-Power (System) | Resumed from suspend (active-start) |
| 42 | Microsoft-Windows-Kernel-Power (System) | Entering sleep (active-end) |
| 6006 / 6008 | EventLog (System) | Clean / unexpected shutdown (active-end) |
| 4801 / 4800 | Security (elevated mode only) | Workstation unlocked / locked |

#### Timeline logic (`events/timeline.cs`)

Opens pair with closes into spans, clamped to the query window. Duplicate opens collapse, stray closes are ignored, and a span still open at the end closes at "now" (covers crashes and the current session). Spans crossing local midnight are split at the boundary before per-day summing, so a 23:00 -> 01:00 session attributes correctly to both days.

---

### Design decisions

- **One-shot over daemon:** the OS already logs continuously in the background; reading the log on demand needs no always-on process, no service, no elevation (default mode).
- **Event log over polling:** no timers, no missed samples, no battery/CPU cost. The tradeoff is dependence on log retention (below).
- **Modular for a future GUI:** `events/` (reading + timeline math) and `models/` know nothing about the console; a GUI later only replaces `program.cs` output.
- **Fail-fast on missing elevation:** `elevatedMode=true` without admin exits 2 with a clear message instead of silently returning the less-precise number.

---

### Source tree

```
screentime/
├── src/
│   ├── program.cs           ← Main(), args, elevation check, log output
│   ├── config/
│   │   ├── conf.cs          ← shared .conf key=value reader
│   │   └── settings.cs      ← elevatedMode, days
│   ├── events/
│   │   ├── eventReader.cs   ← System + Security log queries
│   │   └── timeline.cs      ← span pairing, locked-span build, subtract, midnight split, aggregate
│   ├── models/
│   │   └── report.cs        ← Marker, ActiveSpan, DayReport
│   └── output/
│       └── logWriter.cs     ← appends report/errors to logs/screentime.log
├── bin/
│   ├── screentime.exe       ← compiled binary (winexe, windowless)
│   └── .conf                ← behavior flags (edit this)
├── logs/
│   └── screentime.log       ← appended report, one [date] block per day per run
├── build.bat
└── README.md                ← this document
```

---

### Known limitations

- **Log retention bounds history:** event logs overwrite old entries when full. Days older than retention simply show no events (boots 0, active 0h 00m). For permanent history, run daily via Task Scheduler and append the output to your own file.
- **Locked-but-awake overcounts in default mode:** without the Security log there is no lock signal; that time counts as active. Use `elevatedMode=true` (elevated) for the precise number.
- **Lock auditing must be on for elevated mode:** 4800/4801 require the "Other Logon/Logoff Events" audit subcategory (check: `auditpol /get /subcategory:"Other Logon/Logoff Events"`; enable: `/set ... /success:enable`). If absent, you get the log fallback warning.
- **Fast Startup / hibernate:** treated uniformly as inactive boundaries; the tool does not classify sleep vs. hibernate vs. hybrid shutdown.
- **Local machine only:** no remote log support.
