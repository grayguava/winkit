## Back up KeePass databases

- **Source:** `src/watcher.cs` (kdbxWatch), `src/push.cs` (kdbxPushToRemote)
- **Dependencies:** `rclone` on PATH (for the push tool only)
- **Description:** Two-tool pipeline that snapshots KeePass databases on file change and pushes the snapshots to three cloud providers.

To know why I built this tool, read `STORY.md`.

---

### Usage

Two independent binaries that form a deliberate pipeline:

| Tool | Type | Job |
|---|---|---|
| `kdbxWatch.exe` | Always-on daemon | Snapshot the source `.kdbx` files on change |
| `kdbxPushToRemote.exe` | Scheduled runner | Push `databaseCopies/` to cloud, then exit |

#### kdbxWatch (always-on daemon)

- **Trigger:** At log on (Task Scheduler)
- **Action:** Start `bin\kdbxWatch.exe`, Start in `<kdbx-backup>\bin\`
- **Settings:** If task is already running → Do not start a new instance

The Task Scheduler single-instance setting is a second layer of defence - the `.exe` also holds a named mutex (`Local\kdbxWatchSingleInstance`) that causes any duplicate launch to log a squat warning and exit immediately.

#### kdbxPushToRemote (scheduled, run-to-completion)

- **Trigger:** On a schedule (e.g. hourly, or at logon + repeat)
- **Action:** Start `bin\kdbxPushToRemote.exe`, Start in `<kdbx-backup>\bin\`
- **Settings:** If task is already running → Do not start a new instance

Unlike the watcher, this tool exits on its own when done. The "do not start a new instance" setting prevents a slow upload from stacking with the next scheduled trigger.

#### Config files

Both tools use flat `key=value` INI files in their `bin/` directories - `.watch.conf` for the watcher, `.push.conf` for the pusher. Paths are relative to the `.exe` location (`AppDomain.CurrentDomain.BaseDirectory`), not CWD, so they work regardless of how the process is launched. Logs are written to `logs/` beside `bin/` (hardcoded, not configurable).

Deep-dive references for both tools: [docs/watch.md](docs/watch.md) and [docs/pushToRemote.md](docs/pushToRemote.md).

---

### How it works

#### Pipeline

```
KeePassXC saves a .kdbx file
        ↓
kdbxWatch.exe (always running, event-driven)
  detects the change via FileSystemWatcher
  verifies it's a real change via SHA256 hash
  copies ALL .kdbx files into a new timestamped snapshot folder
        ↓
databaseCopies/
  MonthName/dd/HHmm/         ← e.g. August/02/1226
    *.kdbx + SHA256SUMS.txt
        ↓
kdbxPushToRemote.exe (scheduled, run-to-completion)
  runs rclone copy to Google, Dropbox, Koofr sequentially
  exits when done
        ↓
3 cloud remotes - append-only (nothing ever deleted)
```

The two tools are independent - neither depends on the other - but they form the pipeline.

#### Watcher: change detection

`FileSystemWatcher` fires `Changed`, `Created`, and `Renamed` events for `*.kdbx` (KeePassXC saves by writing a temp file then renaming into place). Each filename gets its own debounce timer so rapid multi-event saves collapse into one snapshot.

The change is verified by SHA256 hash before snapshotting - a write with unchanged content (AV tool touching the file) produces no snapshot. Every snapshot copies **all** `.kdbx` files, then hashes **the copies** to write `SHA256SUMS.txt`, proving what actually landed in the folder.

On startup, the watcher compares current hashes against the newest snapshot's manifest and skips a baseline snapshot if nothing changed since the last run.

#### Pusher: rclone copy

For each remote in `.push.conf`, launches `rclone copy <databaseCopies> <remote>:<path>` as a child process (sequential, one remote at a time), capturing stdout/stderr asynchronously. Output is logged when non-empty. A failed remote is logged and skipped - the loop continues to the next.

#### Configuration

`.watch.conf`:

| Key | Required | Default | Description |
|---|---|---|---|
| `sourceDir` | yes | - | Directory to watch for `.kdbx` files |
| `DestDir` | no | `snapshots` | Snapshot destination, relative to the `.exe` folder |
| `DebounceSeconds` | no | `5` | Seconds to wait after the last event before processing |
| `MaxSnapshotsPerHour` | no | `10` | Warn when snapshot rate exceeds this threshold (0 = disabled) |
| `LastKnownGoodFile` | no | `%APPDATA%\kdbxWatch\hash-history.txt` | Append-only offline hash history file for tamper detection |

Hashing is always SHA256 - hardcoded, not configurable. The snapshot manifest is always named `SHA256SUMS.txt`.

`.push.conf`:

| Key | Required | Default | Description |
|---|---|---|---|
| `sourceDir` | no | `..\databaseCopies` | Local folder to push, relative to the `.exe` folder |
| `Remotes` | no | - | Comma-separated rclone remote names (must match `rclone config`) |
| `RemotePath` | no | `kdbx-backup` | Folder name to create inside each remote |
| `RclonePath` | no | `null` (PATH) | Set to `null` to resolve rclone from PATH; or provide a full path to the executable |

rclone is resolved from PATH when `RclonePath=null` (the default). Set `RclonePath=` to a full path in `.push.conf` if rclone isn't on PATH.

---

### Design decisions

- **Local snapshots first, cloud second:** cloud sync tools operate on their own schedule and can introduce conflicts or partial-write races. The watcher never waits on a network; the cloud push never races with an active save.
- **Three providers:** genuine redundancy requires providers that fail independently. Any single provider can have outages, policy changes, or account suspension; three covers a simultaneous outage of any one.
- **`rclone copy`, not `rclone sync`:** `sync` mirrors source to destination and deletes anything absent locally - local cleanup would cascade to the cloud. `copy` only uploads what's missing and never deletes, making the cloud an append-only archive of every snapshot.
- **Hash the copies, not the originals:** the manifest proves what actually landed in the snapshot folder. The source file is hashed before copy and compared against the copy hash after; a mismatch triggers an automatic retry and a loud error log.
- **Flat INI config:** `key=value` files, no TOML/JSON/XML parser needed. Paths anchored to the `.exe` location, not CWD.
- **Source files never modified:** the watcher only reads and copies; rclone only uploads. Neither touches the original `.kdbx` files.

---

### References

- [rclone](https://rclone.org/) - cloud upload engine (required for the push tool)
- [docs/watch.md](docs/watch.md) - kdbxWatch deep dive
- [docs/pushToRemote.md](docs/pushToRemote.md) - kdbxPushToRemote deep dive

---

### Source tree

```
kdbx-backup/
├── src/
│   ├── watcher.cs            ← kdbxWatch source
│   └── push.cs               ← kdbxPushToRemote source
├── bin/
│   ├── kdbxWatch.exe        ← compiled binary
│   ├── kdbxPushToRemote.exe ← compiled binary
│   ├── .watch.conf           ← watcher config (edit this)
│   └── .push.conf            ← pusher config (edit this)
├── databaseCopies/           ← local snapshot destination (auto-created)
├── logs/                     ← shared log folder (auto-created)
├── docs/
│   ├── watch.md              ← kdbxWatch deep dive
│   └── pushToRemote.md       ← kdbxPushToRemote deep dive
├── STORY.md                   ← why kdbx-backup exists
├── build.bat
└── README.md                 ← this document
```

---

### Known limitations

- **Windows-only** - `FileSystemWatcher`, named mutexes, `winexe`.
- **rclone on PATH or configured** - `kdbxPushToRemote` needs rclone; set `RclonePath=` or ensure it's on PATH.
- **No alerting** - monitoring is via log files only; no notification on push failure.
- **No automatic retention** - snapshots are never pruned automatically, so local `databaseCopies/` and the cloud archive grow unbounded. Purge old local `MonthName/` folders manually; the cloud keeps everything.
- **kdbxWatch can miss rapid saves** - debouncing prevents storming, but very fast successive saves under the debounce window are coalesced into one snapshot.
- **Logs are plain text** - they contain filenames and timestamps but no credentials. Review access controls if stored on shared volumes.
- **Cloud archive is not tamper-resistant** - ransomware or file-corrupting malware running in your session can poison the source `.kdbx` files; the watcher will faithfully snapshot and push the corrupted copies to all three providers (append-only, no pruning). An offline hash history file (`%APPDATA%\kdbxWatch\hash-history.txt`) and snapshot rate warnings provide limited detection.
