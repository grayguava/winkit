# mmu — my music util

Single-binary music utility. Today it downloads audio from YouTube/YT Music;
future commands (e.g. cutting ads out of local files) will reuse the same
binary and config.

```
mmu -d
```

Starts an interactive download session:

- **Paste link** — YouTube or YouTube Music link
- **Filename (optional)** — output name without extension (mmu appends the
  correct extension). Press Enter to use the video title.

### Examples

```
C:\> mmu -d
Paste link: https://youtu.be/dQw4w9WgXcQ
Filename (Enter to use the title): Rick Astley - Never Gonna Give You Up
Downloading...

✨ Done. Saved to "D:\Music"
```

### Requirements

- `yt-dlp.exe` — on PATH, placed alongside mmu.exe, or specified in `.conf`
- `ffmpeg.exe` — alongside mmu.exe or in `ffmpeg/bin/` (optional but recommended)

## Configuration

### .conf

**Location:** `conf/mmu/.conf`

```ini
; ytdlp:  "default" for PATH detection, or full path to yt-dlp.exe
; ffmpeg: "default" for PATH detection, or full path to ffmpeg/ffprobe
; outdir: download directory

ytdlp=default
ffmpeg=default
outdir=D:\Music
```

| Key | Values | Description |
|---|---|---|
| `ytdlp` | `default` or full path | How to locate yt-dlp.exe. `default` checks alongside binary, then PATH. |
| `ffmpeg` | `default` or full path | How to locate ffmpeg.exe/ffprobe.exe. `default` checks alongside binary, then PATH. Accepts path to exe or directory. |
| `outdir` | directory path | Where downloaded files are saved. Defaults to current directory. |

### yt-dlp.conf

**Location:** `conf/mmu/yt-dlp.conf`

Passed via `--config-location` so yt-dlp ignores all other config files. Contains sensible defaults:

- Best audio only (`-f ba`)
- Extract audio, keep original codec (`-x --audio-format best`)
- Embed metadata and thumbnail
- No playlists
- Windows-safe filenames
- Quiet output (mmu prints its own success message)

Edit freely — no recompilation needed.

## How it works

1. Reads `conf/mmu/.conf` for yt-dlp path and output directory.
2. Resolves `conf/mmu/yt-dlp.conf` relative to the .exe location.
3. Locates `yt-dlp.exe` — explicit path from `.conf`, alongside binary, or via PATH.
4. Locates `ffmpeg.exe` (alongside binary or in `ffmpeg/bin/`).
5. Sanitizes the filename if one was given (removes invalid Windows filename characters).
6. Runs yt-dlp with `--config-location` pointing at `conf/mmu/yt-dlp.conf`.
7. On success, prints `✨ Done. Saved to "<dir>"`.

## Design decisions

- **No transcoding:** `--audio-format best` keeps the original codec (Opus or AAC). No quality loss from re-encoding.
- **Config over flags:** Both yt-dlp and mmu use config files instead of long argument strings. Easy to tweak without recompilation.
- **Self-contained:** The wrapper locates everything relative to its own directory when using alongside-binary placement. Alternatively, use PATH with `ytdlp=default`.
- **Clean output:** yt-dlp's verbose progress is suppressed. mmu prints a single `✨ Done. Saved to "<dir>"` line.
- **Extensible:** `mmu -d` is one dispatch branch; future subcommands (`-c` for cutting) add new branches without changing the download behavior.

## Known limitations

- Requires yt-dlp.exe (not bundled — user must download separately).
- Requires ffmpeg.exe for audio extraction (not bundled).
- Single-file downloads only (no playlists).
