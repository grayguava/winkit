## Randomize the desktop wallpaper

- **Source:** `src/program.cs` (entry), `src/daemon.cs` (queue + apply), `src/hotkey.cs` (hotkey)
- **Dependencies:** `System.Windows.Forms` (hidden window for hotkey messages)
- **Description:** Background daemon that registers a global hotkey and applies the next image from `assets/` as the desktop wallpaper on each press. Tracks a shuffle queue so images are cycled without repeats, persisted across reboots via the registry.

---

### Usage

Start it once - launch `bin/wallswitch.exe` (double-click, or at logon via a Startup shortcut / Task Scheduler). It runs in the background, registers the configured hotkey, and does nothing else until you press it:

| Action | Result |
|---|---|
| Launch | Registers the `Hotkey` from `.conf`, sits in the background |
| Press hotkey | Instantly applies the next wallpaper |
| Second launch | Detects the running daemon (mutex) and exits |

The built-in hotkey listener replaces any third-party hotkey tool, so switching is instant - no command-chain hop.

To change the wallpaper without the hotkey, delete `bin/state` to recreate the cycle.

---

### How it works

#### Startup sequence

1. A named mutex ensures only one daemon runs; a second launch exits.
2. `bin/.conf` is read for `AssetsDir` and `Hotkey`.
3. A hidden WinForms window (`HotkeyForm`) registers the global hotkey via `RegisterHotKey`.
4. The daemon enters the message loop and idles until the hotkey is pressed.
5. On `WM_HOTKEY`, it picks the next image and applies it.

#### Hotkey listener

The hidden `HotkeyForm` overrides `WndProc` to catch `WM_HOTKEY` (0x0312), then calls `Advance()`. The combo is parsed from the `Hotkey=` config value (e.g. `Ctrl+Alt+W`); modifiers are `Ctrl`/`Alt`/`Shift`/`Win` (aliases `Super`/`Meta`/`Cmd`), and the key is a letter, digit, `space`, or `F1`-`F24`. A bare key (no modifier) is only accepted for `F1`-`F12`, e.g. `Hotkey=F7` - Windows won't register other bare keys. If the combo is already in use, `RegisterHotKey` fails and a warning dialog is shown.

#### Shuffle queue

A flat section-based `bin/state` file holds two lists:

- **`queue`** - images scheduled to be shown, in order. The front is the next wallpaper; when shown, it moves to `shown`.
- **`shown`** - images already shown this cycle.

When the queue empties, `shown` and `queue` are merged, Fisher-Yates shuffled, and the cycle starts fresh - every image shows exactly once before any repeats. Seeding uses `new Random(Guid.NewGuid().GetHashCode())` so each reshuffle is non-deterministic.

#### Image sync

Each run compares the files in `assets/` against the union of `queue` and `shown`:

- **Removed** images are silently dropped from both lists.
- **Added** images are shuffled and appended to the queue.

So images can be added or removed at any time without corrupting the cycle. New images appear after the current queue drains; deleted ones stop being scheduled without error.

#### Wallpaper application

Two independent mechanisms:

1. **Registry** - writes `HKCU\Control Panel\Desktop\Wallpaper`, sets `WallpaperStyle=10` (Fill) and `TileWallpaper=0`. Windows reads this on login, so the wallpaper survives reboots without a startup helper.
2. **Win32 API** - calls `SystemParametersInfo(SPI_SETDESKWALLPAPER=20, 0, path, 3)` to apply immediately.

Both are needed: registry alone requires logoff/logon to take effect; `SystemParametersInfo` alone doesn't persist across reboots.

#### Configuration

`bin/.conf` - keys case-insensitive, `#` lines are comments:

| Key | Required | Default | Description |
|---|---|---|---|
| `AssetsDir` | no | `assets` | Image directory. Relative paths resolve against the `.exe` folder. |
| `Hotkey` | yes | - | Global hotkey combo, e.g. `Ctrl+Alt+W` or bare `F7` |

Other settings are hardcoded in source:

| Setting | How to change | Default |
|---|---|---|
| Image pool | Add/remove files in `AssetsDir` | Empty |
| Supported formats | Edit `exts` array in source | `jpg`, `jpeg`, `png`, `bmp` |
| Wallpaper style | `WallpaperStyle` in `daemon.cs` | Fill (10) |
| Tiling | `TileWallpaper` in `daemon.cs` | Off (0) |

`bin/state` is auto-managed; delete it to reset the cycle.

---

### Design decisions

- **C# over PowerShell/Python:** wallpaper setting needs P/Invoke (`SystemParametersInfo`) and registry access. C# provides both in the standard library with no runtime dependency, launches in under 100 ms, and shows up by name in Task Manager.
- **Relative paths in state:** storing paths relative to the `.exe` makes the whole folder portable - the state file stays valid as long as `assets/` is alongside `bin/`.
- **Shuffle queue over simple random:** simple random can repeat an image before others show. The queue guarantees every image once before any repeats.
- **Two wallpaper mechanisms:** registry alone needs logoff/logon; `SystemParametersInfo` alone doesn't survive reboots. Together they cover immediate change + persistence.
- **Silent operation:** a hotkey-triggered tool shouldn't print or pop boxes. Failures are ignored or surfaced as a single startup dialog.

---

### References

No external references - self-contained. (For WebP support, the [WebP Codec for Windows](https://apps.microsoft.com/detail/9pg2dk419drg) is an optional add-on.)

---

### Source tree

```
wallswitch/
├── src/
│   ├── program.cs         ← entry: mutex, config, daemon startup
│   ├── daemon.cs          ← wallpaper queue + apply logic
│   └── hotkey.cs          ← global hotkey registration + parse
├── bin/
│   ├── wallswitch.exe    ← compiled binary
│   ├── .conf             ← configuration (AssetsDir, Hotkey)
│   └── state             ← shuffle queue / shown list (auto-managed)
├── assets/               ← your image collection
├── STORY.md              ← why wallswitch exists
├── build.bat
└── README.md             ← this document
```

---

### Known limitations

- **No logging** - no output on success or failure; diagnosing requires a debugger or checking `state` manually.
- **Single-monitor wallpaper** - the image spans the virtual desktop. Per-monitor wallpapers need a different tool.
- **No format conversion** - Windows must natively support the format (`jpg`, `jpeg`, `png`, `bmp`; `webp` needs the codec pack).
- **No exclusions** - all images in `assets/` are included.
- **Manual auto-start** - no startup entry is installed; add a Startup shortcut or Task Scheduler task yourself.
- **One hotkey, one action** - a single hotkey per instance.
- **State can go stale** - on a read-only `.exe` mount, `state` can't be updated and the cycle may repeat or skip.
