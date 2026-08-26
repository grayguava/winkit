# kdbxPushToRemote - deep-dive documentation

**Tool:** `bin\kdbxPushToRemote.exe`
**Source:** `src\push.cs`
**Language:** C#, compiled via `csc.exe /target:winexe`
**Role:** Run-to-completion. Pushes `databaseCopies\` to all configured
rclone remotes sequentially. Started by Task Scheduler on a schedule,
exits when done.

---

## Configuration reference

File: `bin\.push.conf`

| Key | Required | Default | Description |
|---|---|---|---|
| `sourceDir` | no | `..\databaseCopies` | Local folder to push. Relative paths resolve against the `.exe`'s own folder. |
| `Remotes` | no | - | Comma-separated rclone remote names. Must match names in `rclone config` exactly (case-sensitive). |
| `RemotePath` | no | `kdbx-backup` | Folder name to create inside each remote. |
| `RclonePath` | no | `null` (PATH) | Set to `null` to resolve rclone from PATH; or provide a full path to the executable. |

rclone is resolved from PATH when `RclonePath=null` (the default). Set it to a full path in `.push.conf` if rclone isn't on PATH.

---

## Why rclone copy and not rclone sync

This is the most important design decision in this tool and worth being
explicit about.

`rclone sync` mirrors the source to the destination *exactly*, including
**deleting from the remote anything not present locally**. If `rclone sync`
were used, anything pruned or removed from `databaseCopies\` locally would
be deleted from all cloud remotes too - defeating the purpose of cloud
backup, which is to retain history. `rclone copy` only uploads what's
missing on the remote and never deletes anything. The cloud becomes an
append-only archive of every snapshot that ever existed locally.

**Summary:**
- `rclone copy` → cloud grows indefinitely, local never auto-deleted. ✅
- `rclone sync` → cloud mirrors local, any local cleanup deletes remotely too. ❌

---

## Why three providers

Three providers were chosen for genuine redundancy - meaning they fail
independently of each other. The three in use:

| Remote name | Provider | Type | Notes |
|---|---|---|---|
| `Google` | Google Drive | OAuth / drive | Already configured; most mature rclone backend |
| `Dropbox` | Dropbox | OAuth / dropbox | Already configured |
| `Koofr` | Koofr | koofr | Already configured |

All three were pre-existing rclone remotes, so no new OAuth setup was
needed. The `kdbx-backup` folder is created inside each remote on first
push.

Free tiers on all three are sufficient for `.kdbx` files - even with
unlimited cloud retention (no pruning on remotes), the total size of
thousands of snapshots of five small databases remains well within any
free tier.

---

## How the push works

Each remote is a separate `rclone copy` child process, launched via
`System.Diagnostics.Process`. stdout and stderr are captured
asynchronously (to avoid deadlocks if both buffers fill simultaneously)
and logged when non-empty after the process exits.

Sequential, not parallel - one remote at a time. Rationale: simplicity
over speed. A failed remote doesn't block the others; if Google fails,
Dropbox and Koofr still run. Upload time for small `.kdbx` files is
negligible, so parallelism buys nothing meaningful.

rclone is launched without extra flags - keep the command line simple. Any non-empty output (stats, errors) is logged after the process exits.

Exit codes:
- `0` → logged as `<remote>: OK`
- Non-zero → logged as `<remote>: FAILED (exit <code>)`
- Exception launching rclone → logged as `<remote>: ERROR launching rclone - <message>`

A failed remote does **not** stop execution - the loop continues to the
next remote regardless.

---

## Process lifecycle

Unlike `kdbxWatch`, this tool is **not** a daemon. It:

1. Loads config.
2. Validates `SourceDir` exists and `Remotes` is non-empty.
3. Loops over remotes, running one `rclone copy` per remote.
4. Logs completion.
5. Exits.

No `ManualResetEvent`, no `FileSystemWatcher`, no mutex. Task Scheduler
manages the schedule and handles the "don't stack instances" concern via
the "If task is already running → Do not start a new instance" setting.

---

## Task Scheduler setup

- **Trigger:** Schedule (e.g. hourly, or daily)
- **Action:** Start `bin\kdbxPushToRemote.exe`
- **Start in:** `D:\Tools\kdbx-backup\bin\`
- **Settings → If task is already running:** Do not start a new instance

The "Start in" field matters: without it, relative paths in `.push.conf`
(`sourceDir=..\databaseCopies`) would
resolve against Task Scheduler's own working directory rather than the
`.exe`'s folder. Setting "Start in" ensures they resolve correctly.

Alternatively, use absolute paths in `.push.conf` to make this
Task Scheduler dependency disappear entirely.

---

## Path resolution

Same pattern as `kdbxWatch`: relative paths in `.push.conf` are resolved
via `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relPath))`.

`AppDomain.CurrentDomain.BaseDirectory` = the folder containing
`kdbxPushToRemote.exe`, not the process CWD. See `kdbxWatch.md` for the
full reasoning - same principle applies here.

---

## Log format

Append-only text file at `logs\push.log` (beside `bin\`, hardcoded). A `[dd-MM-yyyy]` header is written the first time
something is logged on a given day, and each entry is time-only
(`hh:mm:ss tt`, no repeated date) followed by a colon, a space, and the
message:

```
[12-07-2026]
06:00:45 PM: Pushing to Koofr
06:00:50 PM: Push completed to Koofr
07:00:00 PM: Pushing to Google
07:00:44 PM: Push completed to Google
07:00:44 PM: Google output: Transferred: 12.500 KiB / 12.500 KiB, 100%
07:01:02 PM: Pushing to Dropbox
07:01:08 PM: Push failed to Dropbox (exit 1)
07:01:08 PM: Dropbox output: ERROR : ...temp file ... not found
```

Per-remote messages:

- `Pushing to <Remote>` - starting the rclone copy for that remote.
- `Push completed to <Remote>` - rclone exited 0.
- `Push failed to <Remote> (exit N)` - rclone exited non-zero (e.g. exit 1
  on a network failure). Non-empty stdout/stderr is logged after this line.
- `Push failed to <Remote> (<message>)` - rclone could not be launched at
  all (e.g. not on PATH or configured path invalid).

---

## Adding or removing a remote

Edit `Remotes=` in `.push.conf`. No recompile needed. Example - add a
fourth remote:

```ini
Remotes=Google,Dropbox,Koofr,Backblaze
```

The new remote must already exist in `rclone config`. The tool will
create `kdbx-backup\` inside it on first push.

To temporarily disable a remote without removing it from rclone config,
just remove it from the `Remotes=` line.

---

## Known limitations

- **No retry logic.** If a remote fails, it's logged and skipped. The
  next scheduled run will retry (rclone copy picks up where it left off -
  already-uploaded folders are skipped, only missing ones are uploaded).
- **No network check before starting.** If the machine has no internet,
  all three remotes fail and log errors. Not a problem in practice - the
  next scheduled run will succeed when connectivity is restored, and
  rclone copy is idempotent.
- **rclone on PATH or configured** - by default rclone is resolved from PATH (`RclonePath=null`). Set `RclonePath=` in `.push.conf` to a full path if rclone isn't on PATH.
