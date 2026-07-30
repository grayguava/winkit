# ymdl — YouTube/YT Music audio downloader

Wraps yt-dlp into a simple two-argument command. Downloads the highest quality audio available without re-encoding (Opus or AAC).

```
ymdl <url> "filename"
```

- **url** — YouTube or YouTube Music link
- **filename** — output name without extension (ymdl appends the correct extension)

### Examples

```
ymdl "https://youtu.be/dQw4w9WgXcQ" "Rick Astley - Never Gonna Give You Up"
ymdl "https://music.youtube.com/watch?v=..." "Song Name"
```

### Requirements

- `yt-dlp.exe` — on PATH, placed alongside ymdl.exe, or specified in `.ymdl` config
- `ffmpeg.exe` — alongside ymdl.exe or in `ffmpeg/bin/` (optional but recommended)

## Configuration

### .ymdl

**Location:** `conf/.ymdl`

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

**Location:** `conf/yt-dlp.conf`

Passed via `--config-location` so yt-dlp ignores all other config files. Contains sensible defaults:

- Best audio only (`-f ba`)
- Extract audio, keep original codec (`-x --audio-format best`)
- Embed metadata and thumbnail
- No playlists
- Windows-safe filenames
- Quiet output (ymdl prints its own success message)

Edit freely — no recompilation needed.

## How it works

1. Reads `conf/.ymdl` for yt-dlp path and output directory.
2. Resolves `conf/yt-dlp.conf` relative to the .exe location.
3. Locates `yt-dlp.exe` — explicit path from `.ymdl`, alongside binary, or via PATH.
4. Locates `ffmpeg.exe` (alongside binary or in `ffmpeg/bin/`).
5. Sanitizes the filename (removes invalid Windows filename characters).
6. Runs yt-dlp with `--config-location` pointing at `conf/yt-dlp.conf`.
7. On success, prints the output filename and target directory.

## Design decisions

- **No transcoding:** `--audio-format best` keeps the original codec (Opus or AAC). No quality loss from re-encoding.
- **Config over flags:** Both yt-dlp and ymdl use config files instead of long argument strings. Easy to tweak without recompilation.
- **Self-contained:** The wrapper locates everything relative to its own directory when using alongside-binary placement. Alternatively, use PATH with `ytdlp=default`.
- **Clean output:** yt-dlp's verbose progress is suppressed. ymdl prints only `✔ filename.ext` and the save path.

## Known limitations

- Requires yt-dlp.exe (not bundled — user must download separately).
- Requires ffmpeg.exe for audio extraction (not bundled).
- Single-file downloads only (no playlists).
