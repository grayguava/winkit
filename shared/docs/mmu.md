## Download audio

- **Source:** `src/mmu/mmu.cs` (main + helpers), `src/mmu/download.cs` (download flow)
- **Dependencies:** external `yt-dlp` (required), external `ffmpeg` (recommended for extraction)
- **Description:** Downloads audio from YouTube/YT Music. Interactive, single-binary wrapper around yt-dlp with a quiet, clean progress report.

---

### Usage

```
mmu -d
```

Starts an interactive download session:

1. Paste a YouTube or YouTube Music link.
2. mmu fetches the title and artist up front and prints `Downloading <title> by <artist>...` (missing artist is shown as "Unknown Artist").
3. Filename defaults to yt-dlp's `%(title)s` template - no manual filename prompt.
4. On success, prints `✨ Done. Saved audio to - <outdir>\`.

#### Example

```
C:\> mmu -d

 Paste link: https://youtu.be/dQw4w9WgXcQ

 Downloading Rick Astley - Never Gonna Give You Up by Rick Astley...

✨ Done. Saved audio to - D:\Music\
```

---

### How it works

#### Dependency resolution

Locates `yt-dlp.exe` via `conf/mmu/.conf` (explicit path), alongside the binary, or PATH. Locates `ffmpeg.exe` alongside the binary or in `ffmpeg/bin/`. Both accept `default` for auto-detection.

#### Pre-flight fetch

Runs `yt-dlp --print "%(title)s" --print "%(artist)s"` on the link to get the display name for the progress line.

#### Download

Runs yt-dlp with `--config-location` pointing at `conf/mmu/yt-dlp.conf` so no other yt-dlp config applies. Filename uses `%(title)s.%(ext)s`; Windows-safe names come from the config's flags. Verbose output is suppressed - the config sets quiet output so mmu prints its own progress/success lines.

#### Configuration

`conf/mmu/.conf`:

```
ytdlp=default
ffmpeg=default
outdir=D:\Music
```

| Key | Values | Description |
|---|---|---|
| `ytdlp` | `default` or full path | How to locate yt-dlp.exe. `default` tries alongside binary, then PATH. |
| `ffmpeg` | `default` or full path | How to locate ffmpeg/ffprobe. `default` tries alongside binary, then PATH. Accepts an exe or a directory. |
| `outdir` | directory path | Where downloads are saved. Defaults to current directory. |

`conf/mmu/yt-dlp.conf` holds download flags, passed via `--config-location` so yt-dlp ignores all other config files. Good defaults: best audio only (`-f ba`), extract audio keeping original codec (`-x --audio-format best`), no playlists, Windows-safe filenames, quiet output. Both files are plain text - edit freely, no recompilation.

---

### Design decisions

- **No transcoding:** `--audio-format best` keeps the original codec (Opus or AAC). No quality loss from re-encoding.
- **Config over flags:** both yt-dlp and mmu use config files instead of hardcoded arguments. Tweakable without recompilation.
- **Self-contained wrapper:** everything is located relative to the binary, or on PATH via `default`.
- **Clean output:** yt-dlp's verbose progress is suppressed. One title line, one confirmation line.
- **Extensible:** `mmu -d` is one dispatch branch; future subcommands reuse the same binary and config.

---

### References

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - the download engine mmu wraps
- [ffmpeg](https://ffmpeg.org/) - used for audio extraction/codecs

---

### Source tree

```
shared/
├── src/
│   └── mmu/
│       ├── mmu.cs           ← main entry + shared helpers
│       └── download.cs      ← interactive download flow
├── bin/
│   └── mmu.exe             ← compiled binary
├── conf/
│   └── mmu/
│       ├── .conf           ← yt-dlp/ffmpeg paths + download dir
│       └── yt-dlp.conf     ← download flags
└── docs/
    └── mmu.md
```

---

### Known limitations

- Requires yt-dlp.exe (not bundled - download separately).
- Requires ffmpeg.exe for audio extraction (not bundled).
- Single-file downloads only (no playlists).