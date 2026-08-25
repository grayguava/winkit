## Cycle wallpapers across desktop and terminal

- **Source:** `src/program.cs` (entry), `src/core/` (pools, state, targets), `src/platform/` (config, hotkey)
- **Dependencies:** `System.Windows.Forms` (hidden window for hotkey messages), Windows Terminal (`settings.json`) for the `Terminal` target
- **Description:** Background daemon that registers one global hotkey per pool and, on each press, applies an image to the enabled targets: the live desktop, Windows Terminal's background, and/or the registry persistence. Pools are configurable folders with `random` (distinct image per target) or `sync` (same image everywhere) modes, and each tracks its own shuffle queue so images never repeat until the cycle drains.

To know why I built this tool, read `STORY.md`.

---

### Usage

Launch `bin/wallswitch.exe` once (double-click, or at logon via a Startup shortcut / Task Scheduler). It runs hidden in the background, registers a hotkey for every valid pool, and does nothing else until you press one:

| Action | Result |
|---|---|
| Launch | Reads `.pools` + `.targets`, registers each pool's hotkey, hides |
| Press a pool's hotkey | Applies the next image to every enabled target |
| Edit `.pools` while running | Mode/Dir changes apply on the next press (no restart) |
| Second launch | Detects the running daemon (mutex) and exits |

Config is live-editable: flip a pool's `Mode` or `PoolDir` and the very next press uses it. To reset a pool's rotation, delete its state file (see below).

---

### How it works

#### Startup sequence

1. A named mutex ensures only one daemon runs; a second launch exits.
2. `bin/.targets` is read for which targets are enabled (and target-specific keys like `SettingsPath`).
3. `bin/.pools` is read; each pool is validated (key parses, dir exists, has images) and registered with a distinct hotkey id.
4. Invalid pools are reported in a startup dialog but don't block valid ones; with none valid, the daemon exits.
5. The hidden WinForms window enters the message loop and idles until a hotkey fires.

#### Hotkey dispatch

`HotkeyForm` registers one `RegisterHotKey` per pool with a unique id, so `WM_HOTKEY`'s `wParam` identifies the owning pool. A bare key (no modifier) is only accepted for `F1`-`F12` - Windows won't register other bare keys; otherwise modifiers are `Ctrl`/`Alt`/`Shift`/`Win` and the key is a letter, digit, `space`, or `F1`-`F24`. If a combo is already in use, that pool stays registered but never fires.

On each press the pool's section is re-read from `.pools` (live Mode/Dir changes), then the pool activates.

#### Targets

A target is a place an image can be written to. Three exist:

| Target | `.targets` section | What it does |
|---|---|---|
| `Desktop` | `[Desktop]` | `SystemParametersInfo` - applies live, instantly |
| `Terminal` | `[Terminal]` | Rewrites `backgroundImage` in Windows Terminal `settings.json` (hot-reloads, no restart) |
| `Registry` | `[Registry]` | Writes `HKCU\Control Panel\Desktop\Wallpaper` so the wallpaper survives reboots |

Apply order is Desktop → Terminal → Registry: the visible change lands first, persistence last.

The Terminal target edits the existing `backgroundImage` value only - if that key isn't already in `settings.json`, nothing is written and the miss is recorded in `bin\logs\fail.log`. Each successful edit saves the previous file to `settings.json.bak` (next to it), and the swap itself is atomic, so a crash mid-write can't corrupt your Terminal config.

#### Pool modes

- **`random`** - each enabled target gets a *different* image from the pool.
- **`sync`** - all enabled targets get the *same* image.

Both pull from the pool's shuffle queue, so a pool never shows a repeat until every image has been used once.

#### Shuffle state

Each pool has its own flat state file at `bin/state/<poolName>` holding two lists:

- **`queue`** - images scheduled, front is next; when shown it moves to `shown`.
- **`shown`** - images used this cycle.

When the queue empties, it's rebuilt by scanning the pool folder (dropping removed files, picking up new ones) and reshuffling, then `shown` resets. Entries are stored relative to the pool directory, so moving the pool folder doesn't corrupt the cycle. The fast path pops from the in-memory queue without a directory scan, so presses are instant until the queue drains.

Delete `bin/state/<poolName>` to force a fresh cycle for that pool.

---

### Configuration

`bin/.targets` - which targets are enabled:

```ini
[Desktop]
Enable=true

[Terminal]
Enable=true
SettingsPath=C:\Users\...\LocalState\settings.json

[Registry]
Enable=true
```

`bin/.pools` - one section per pool:

```ini
[Space]
PoolDir=D:\Pictures\Backgrounds
PoolKey=F7
Mode=random
```

| Key | Required | Description |
|---|---|---|
| `PoolDir` | yes | Pool folder. Relative paths resolve against the `.exe` folder. |
| `PoolKey` | yes | Global hotkey, e.g. `F7` or `Ctrl+Alt+W`. Empty/invalid → pool disabled. |
| `Mode` | no (default `random`) | `random` (distinct per target) or `sync` (same everywhere) |

Other settings are hardcoded in source:

| Setting | How to change | Default |
|---|---|---|
| Supported image formats | `Extensions` in `src/core/pool.cs` | `jpg`, `jpeg`, `png`, `bmp`, `webp` |
| Wallpaper style | `WallpaperStyle` in `src/core/targets/regKey.cs` | Fill (10) |
| Tiling | `TileWallpaper` in `src/core/targets/regKey.cs` | Off (0) |

Extensions are hardcoded - `pool.cs` only picks up files matching that list, so anything outside it (e.g. a new format) requires a source edit and rebuild. `WebP` is scanned by default and works out of the box on Windows 11; on Windows 10 install the codec pack from the References section.

---

### Image formats

The daemon only ever schedules files with a known extension (`jpg`, `jpeg`, `png`, `bmp`, `webp`); everything else in a pool directory is ignored. Matching is by extension only - no file-content sniffing, and no conversion. Windows must render the format natively (`webp` needs the codec pack below on Windows 10).

---

### Design decisions

- **C# over PowerShell/Python:** wallpaper setting needs P/Invoke (`SystemParametersInfo`) and registry access. C# provides both in the standard library with no runtime dependency and shows up by name in Task Manager.
- **Pools + targets split:** pools answer "which images, which hotkey, random or sync"; targets answer "where to put it". Adding a new place to write (a pool, or a future target) stays isolated.
- **Shuffle queue over simple random:** simple random can repeat an image before others show. The queue guarantees every image once before any repeats.
- **Distinct-per-target random:** `random` mode advances the queue once per enabled target, so desktop, terminal, and registry get three different images instead of one repeated.
- **Live config reload:** re-reading a pool's section on each press means editing `.pools` takes effect immediately - no daemon restart for a mode change.
- **Desktop first:** applying the desktop before the terminal/registry keeps the visible change instant; the slower writes finish right after.
- **Silent operation:** a hotkey-triggered tool shouldn't print or pop boxes. Config problems surface once at startup as a dialog; runtime failures append to `bin\logs\fail.log` and the daemon keeps running.

---

### References

No external references - self-contained. (For WebP support, the [WebP Codec for Windows](https://apps.microsoft.com/detail/9pg2dk419drg) is an optional add-on.)

---

### Source tree

```
wallswitch/
├── src/
│   ├── program.cs              ← entry: mutex, load configs, message loop
│   ├── core/
│   │   ├── daemon.cs           ← orchestration: validate pools, dispatch hotkeys
│   │   ├── pool.cs             ← one pool: validation + random/sync activate
│   │   ├── state.cs            ← per-pool shuffle queue (bin/state/<pool>)
│   │   ├── targetRegistry.cs   ← enabled targets + dispatch by name
│   │   └── targets/
│   │       ├── desktop.cs      ← live apply via SystemParametersInfo
│   │       ├── winTerminal.cs  ← backgroundImage rewrite in settings.json
│   │       └── regKey.cs       ← registry persistence for reboots
│   └── platform/
│       ├── config.cs           ← INI parse + .targets/.pools readers
│       ├── hotkey.cs           ← hidden form + RegisterHotKey per pool
│       └── faillog.cs          ← append-only failure log (bin/logs/fail.log)
├── bin/
│   ├── wallswitch.exe         ← compiled binary
│   ├── .pools                 ← pool definitions (dir, hotkey, mode)
│   ├── .targets               ← enabled targets (+ SettingsPath)
│   └── state/                 ← <poolName> shuffle queues (auto-managed)
├── STORY.md                   ← why wallswitch exists
├── build.bat
└── README.md                  ← this document
```

---

### Known limitations

- **Minimal logging** - successes print nothing and aren't recorded; runtime failures append to `bin\logs\fail.log` (created on first failure).
- **Single-monitor wallpaper** - the image spans the virtual desktop. Per-monitor wallpapers need a different tool.
- **No format conversion** - Windows must natively render a scanned format (`jpg`, `jpeg`, `png`, `bmp`, `webp`). `webp` renders natively on Windows 11; Windows 10 needs the codec pack.
- **No exclusions** - all images in a pool folder are included.
- **Manual auto-start** - no startup entry is installed; add a Startup shortcut or Task Scheduler task yourself.
- **Hotkeys fixed at startup** - changing a `PoolKey` needs a restart; only `Mode`/`Dir` reload live.
- **State can go stale** - on a read-only mount, `bin/state` can't be written and the cycle may repeat or skip.
