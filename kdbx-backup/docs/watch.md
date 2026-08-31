# kdbxWatch - deep-dive documentation

**Tool:** `bin\kdbxWatch.exe`
**Source:** `src\watcher.cs`
**Language:** C#, compiled via `csc.exe /target:winexe`
**Role:** Always-running daemon. Watches source directory for `.kdbx`
changes, creates local timestamped snapshots in `databaseCopies\`.

---

## Configuration reference

File: `bin\.watch.conf`

| Key | Required | Default | Description |
|---|---|---|---|
| `sourceDir` | yes | - | Directory to watch for `.kdbx` files. Spaces and special characters (e.g. `&`) work fine - no quoting needed, no trailing backslash needed. |
| `DestDir` | no | `snapshots` | Snapshot destination. Relative paths resolve against the `.exe`'s own folder. In production: `D:\Tools\kdbx-backup\databaseCopies` (absolute) or `..\..\databaseCopies` (relative). |
| `DebounceSeconds` | no | `5` | Seconds to wait after the last filesystem event on a file before processing. Absorbs multi-event saves. |
| `MaxSnapshotsPerHour` | no | `10` | Warn when snapshot rate exceeds this in any rolling hour. 0 disables the check. |
| `LastKnownGoodFile` | no | `%APPDATA%\kdbxWatch\hash-history.txt` | Offline append-only hash history. Set to a path on a different drive for tamper-detection value. |

Hashing is always SHA256 - it is hardcoded, not configurable. The snapshot
manifest is always named `SHA256SUMS.txt`.

---

## Core logic walkthrough

### Startup sequence

1. Acquire named mutex `Local\kdbxWatchSingleInstance`. If already
   held → log a squat warning and exit immediately.
2. Load `.watch.conf` from the `.exe`'s own directory. Parse errors
   or missing required keys cause a FATAL log entry and exit with
   code 1 (Task Scheduler records the failure).
3. Create `DestDir` and log directory if missing.
4. Log `Started. Watching: <SourceDir>`.
5. Call `TakeBaselineSnapshot()`.
6. Start `FileSystemWatcher` on `SourceDir`, filter `*.kdbx`.
7. Block forever on `ManualResetEvent` - Task Scheduler ends the process
   on logoff.

### Baseline snapshot

On every startup, the watcher checks whether anything has changed since
the last run *before* deciding to copy:

1. Hash all `.kdbx` files currently in `SourceDir`.
2. Read the most recent snapshot folder's `SHA256SUMS.txt` manifest. The
   newest snapshot is found by walking the `DestDir\MonthName\dd\HHmm`
   hierarchy - the month is the full name (alphabetical order), and `dd`/
   `HHmm` are zero-padded, so the lexicographically last entry at each
   level is the newest. No date parsing needed.
3. Compare current hashes against manifest hashes.
4. **If identical:** load hashes into memory, log "Baseline unchanged,
   skipping snapshot", do not copy. This prevents redundant duplicate
   snapshots when the watcher is restarted without any database changes
   having occurred.
5. **If different (or no prior snapshot):** copy all files, write
   manifest, log "Baseline snapshot created".

This design choice - using the manifest written by the *previous* run as
the cross-restart state mechanism - came from an observed bug: before
this check existed, every restart created a new snapshot regardless of
whether anything had changed, because all state was in-memory only and
lost on exit.

### Change detection

`FileSystemWatcher` fires `Changed`, `Created`, and `Renamed` events for
`*.kdbx` files. All three route to `ScheduleDebounce(fileName)`.

**Why `Renamed`?** KeePassXC saves by writing to a temp file then
renaming it into place. Without the `Renamed` handler, saves would be
missed entirely.

### Debounce

Each filename gets its own `System.Threading.Timer`. On each event:
- If a timer already exists for that file → reset it to fire
  `DebounceSeconds` from now (`timer.Change(DebounceMs, Timeout.Infinite)`).
- If no timer exists → create one.

This means rapid saves (multiple filesystem events for one logical save
operation) collapse into a single `OnDebounceElapsed` call, firing once
after the last event settles. Example: save at t=0 and t=3 with a 5s
debounce → timer fires at t=8, not twice.

### Hash comparison and snapshot decision

When `OnDebounceElapsed` fires for a file:

1. Acquire `StateLock`.
2. Remove the timer entry for this file (so future events create a fresh
   timer).
3. Verify the file still exists (could have been deleted in the debounce
   window).
4. Hash the file. If it's still locked (IOException) → reschedule debounce
   rather than failing.
5. Compare against in-memory `LastHashes[fileName]`.
6. **If identical:** log "Hash unchanged, skipping". Do nothing. This
   handles the rare case where a write occurs but content is unchanged
   (e.g. a backup or AV tool touching the file without modifying it).
   KeePassXC itself won't trigger this - it doesn't write the file at all
   unless content changed.
7. **If different:** call `TakeSnapshot()`.

### Snapshot creation

`TakeSnapshot` (called while holding `StateLock`):

1. Create a new folder at `DestDir\MonthName\dd\HHmm` (current local
   date and time - e.g. `databaseCopies\August\02\1226`).
2. Copy **all** `.kdbx` files from `SourceDir` into it - not just the
   triggering file. Every snapshot is a complete point-in-time backup of
   the whole set.
3. For each file, hash **the source** before copy, then hash **the copy**
   after. If the two hashes differ, the copy is retried once and a loud
   error is logged if the mismatch persists. This catches disk errors or
   AV interference during the copy. The manifest records the copy's hash
   (what actually landed).
4. Write `SHA256SUMS.txt` with `filename: hash` per line, sorted by
   filename for stable diffs.
5. Update `LastHashes` for every file in the snapshot (not just the
   triggering file) - since the snapshot just captured all of them, the
   baseline for all should reflect the snapshot's state.
6. Append the snapshot's hashes to the offline hash history file
   (`LastKnownGoodFile`, default `%APPDATA%\kdbxWatch\hash-history.txt`).
   This provides a tamper-detection baseline outside the synced tree.

Before creating the snapshot, the rate limiter checks whether more than
`MaxSnapshotsPerHour` snapshots have occurred in the last rolling hour.
If so, a warning is logged (the snapshot is still created - data safety
takes priority over rate concerns).

### No automatic pruning

Snapshots are never deleted automatically. Local `DestDir` and the cloud
archive both grow unbounded - prune `DestDir\MonthName\` month folders manually
when they get large. There is deliberately no retention policy, so the
cloud keeps every snapshot that ever existed locally.

---

## Concurrency model

All mutable state (`LastHashes`, `DebounceTimers`, `SnapshotTimes`) is guarded by a single
`StateLock` object. `FileSystemWatcher` events fire on background threads;
`Timer` callbacks also fire on threadpool threads. The lock ensures:

- Two filesystem events can't both see "no timer exists" and create
  duplicate timers.
- Two debounce callbacks can't both decide "hash changed" and race to
  create overlapping snapshots before `LastHashes` updates.

`ScheduleDebounce` is called from inside `OnDebounceElapsed` (in the
file-locked reschedule path) while `StateLock` is already held. This
doesn't deadlock because .NET's `Monitor` (used by `lock`) is
re-entrant on the same thread.

Logging uses a separate `LogLock` so timer callbacks logging concurrently
don't interleave partial lines in the log file.

---

## Single-instance enforcement

Two layers:

1. **Named mutex** (`Local\kdbxWatchSingleInstance`) - checked as the
   very first act in `Main()`, before config load. A squat attempt
   logs a warning to the default log path before exiting. Session-local
   (`Local\`) so a different user or session isn't blocked.
2. **Task Scheduler setting** - "If task is already running → Do not start
   a new instance." Prevents Task Scheduler itself from spawning a second
   process (e.g. on logoff/logon cycles or RDP reconnects).

Both layers are needed: the mutex handles double-clicks and manual
launches; the Task Scheduler setting handles automated re-triggers.

---

## Path resolution

All relative paths in `.watch.conf` resolve against
`AppDomain.CurrentDomain.BaseDirectory` - the directory containing the
`.exe` file, not the process's current working directory.

This matters because Task Scheduler's working directory is not guaranteed
to match the `.exe` location. Using CWD (`Directory.GetCurrentDirectory()`)
would break silently if "Start in" isn't set in the task definition.
`AppDomain.CurrentDomain.BaseDirectory` is always the `.exe`'s own folder,
regardless of launch context.

---

## Verified behavior

Tested 2026-06-29 / 2026-07-02:

- ✅ Baseline snapshot fires on first run, all `.kdbx` files copied.
- ✅ Restart without changes → "Baseline unchanged, skipping snapshot".
- ✅ Real edit in KeePassXC → debounce → snapshot of all files.
- ✅ Two separate real edits → two separate snapshots.
- ✅ No-op open/close in KeePassXC → no filesystem write, no log entry.
- ✅ Source path with spaces and `&` works unquoted in `.watch.conf`.
- ✅ Double-clicking `.exe` while already running → second instance logs
  squat warning and exits, first instance unaffected.
- ✅ Config parse error (e.g. `DebounceSeconds=1O`) → FATAL log, exit 1.
- ✅ Copy mismatch detected → retry logged, error logged if still mismatched.
- ✅ `%APPDATA%\kdbxWatch\hash-history.txt` grows with each snapshot.
- ✅ Restart across midnight → next log entry starts a new `[dd-MM-yyyy]`
  header block.
- ⬜ Hash-unchanged skip - not naturally triggered by KeePassXC; only
  fires if another tool writes to the source folder without changing content.

---

## Log format

Append-only text file at `logs\watch.log` (beside `bin\`, hardcoded). A `[dd-MM-yyyy]` header is written the first time
something is logged on a given day, and each entry is time-only
(`hh:mm:ss tt`, no repeated date) followed by two spaces and the message:

```
[01-08-2026]
20:57:09  Started. Watching: D:\...\KeePassXC\DB
20:57:09  Change detected: db-jretd.kdbx
20:57:09  Snapshot created: 08\01\205709 (5 files)

[02-08-2026]
12:01:42  Baseline unchanged since last run, skipping snapshot (5 files).
```

---

## Known edge cases

- **File deleted from source mid-debounce:** `OnDebounceElapsed` checks
  `File.Exists` before hashing and skips with a log entry if missing.
- **File still locked after debounce:** `IOException` on hash → reschedule
  debounce. The file will be retried after another `DebounceSeconds`.
- **Multiple files change near-simultaneously:** Each file has an
  independent debounce timer. Two files' timers firing close together
  produce two separate snapshots (both containing all files), not one
  merged snapshot. This keeps the logic simple at the cost of occasional
  near-duplicate snapshots during multi-file edit sessions.
- **`databaseCopies\` contains non-snapshot subdirectories:** the newest-snapshot
  walk sorts all subdirectories at each level and picks the last one. Don't
  manually create subfolders inside `databaseCopies\` (a stray folder could
  be picked as the "newest" snapshot when searching for the baseline manifest).
