# etsu — ExifTool Simple Use

- **Language:** PowerShell
- **Role:** Interactive tools built around `exiftool` — **read**, **clean**, and **date** subcommands.

---

## Tools

### date — date setter

Sets DateTimeOriginal, CreateDate, ModifyDate, FileModifyDate, and FileCreateDate on one or more files to a user-specified value using `exiftool -AllDates -FileModifyDate -FileCreateDate`.

```
etsu date                          C# binary (recommended)
powershell -ExecutionPolicy Bypass -File date.ps1    PS1 original
```

1. Opens a multi-file picker.
2. Prompts for a date in `YYYY:MM:DD HH:MM:SS` format.
3. Copies files to a temp workspace, runs `ExifTool` on each, verifies, then swaps originals with full rollback on failure.
4. Logs to `logs/date_*.log` with 10-log rotation.

### read — metadata viewer

Displays all metadata from a single file in a styled layout.

```
etsu read                          C# binary (recommended)
powershell -ExecutionPolicy Bypass -File read.ps1    PS1 original
```

The tool:
1. Detects `exiftool.exe` on PATH (falls back to local `exiftool.exe`).
2. Shows a styled header with the detected version.
3. Prompts to open a file picker — single file only.
4. Displays every tag returned by `exiftool`.

No logging, no multi-file support.

### clean — metadata stripper

Strips EXIF/IPTC/XMP/metadata from images, videos, and PDFs with full safety guarantees.

```
etsu clean                         C# binary (recommended)
powershell -ExecutionPolicy Bypass -File clean.ps1   PS1 original
```

The tool:
1. Detects `exiftool.exe` on PATH (falls back to local `exiftool.exe`).
2. Shows a styled header with the detected version.
3. Prompts to open a file picker — multi-file selection.
4. **Stage 1** — Copies selected files to a temp workspace and verifies byte sizes match.
5. **Stage 2** — Runs `exiftool -all= -overwrite_original -P -v` on each temp copy to strip metadata.
6. **Stage 3** — Verifies cleaned files exist, are non-empty, and are readable.
7. **Stage 4** — Renames originals to `.bak`, moves cleaned copies into place, confirms integrity, then deletes `.bak` files. On any failure, all originals are restored before exit.
8. **Stage 5** — Reports success, writes a timestamped log, and waits for Enter/Spacebar.

#### Cancellation

Pressing `n` at the file picker prompt, or closing the file dialog without selecting files, exits immediately without creating a log.

---

## Common features

All tools share the same UI style:

```
 ┌─ 🐾 ETSU   |   Read   |   Exiftool: vX.X.X
 ──────────────────────────────────────────────

  Open file picker? (Y/N): y
  ...
```

- Box-drawing header with tool name, mode, and exiftool version
- Enter / Spacebar to exit (other keys ignored)
- exiftool resolved from PATH first, then local `exiftool.exe`

---

## Requirements

| Dependency | Why |
|---|---|
| **PowerShell 5.1+** | Ships with Windows 10/11. |
| **exiftool.exe** | The actual metadata engine. Place on PATH *or* copy into the `etsu/` directory. |
| **System.Windows.Forms** | Built into .NET Framework (used for the native file dialog via `Add-Type`). |

### Supported file types

`jpg`, `jpeg`, `png`, `webp`, `heic`, `tif`, `tiff`, `mp4`, `mov`, `pdf`

---

## Logging

Both `clean.ps1` and `date.ps1` produce timestamped logs in `logs/`:

```
logs/
  clean_20260713_212950.log
  clean_20260713_213100.log
  date_20260723_153000.log
  ...
```

Only successful processing runs (files were selected and work began) produce a log. Cancelled runs do not. Logs are rotated — the 10 most recent per prefix are kept.

### Log format (clean.ps1)

```
ExifTool Metadata Clean Log
Timestamp : 2026-07-13 21:29:50
Outcome   : SUCCESS
----------------------------------------

ExifTool path: D:\exiftool\exiftool.exe
ExifTool version: 12.92
Files selected (3):
  D:\pics\photo1.jpg
  D:\pics\photo2.png
  D:\pics\photo3.pdf

[1/5] Copying files to temp workspace
  ...
[2/5] Cleaning metadata
  D:\pics\photo1.jpg
    - EXIF
    - IPTC
  ...
All done. 3 file(s) cleaned in place.
```

### Log format (date.ps1)

```
ExifTool Date Set Log
Timestamp : 2026-07-23 15:30:00
Outcome   : SUCCESS
----------------------------------------

ExifTool path: D:\exiftool\exiftool.exe
ExifTool version: 12.92
Files selected (2):
  D:\pics\photo1.jpg
  D:\pics\photo2.jpg
Target date: 2026:01:15 14:30:00

[1/4] Copying files to temp workspace
  ...
[2/4] Setting date: 2026:01:15 14:30:00
  Set OK: photo1.jpg (EXIF + FS timestamps)
  Set OK: photo2.jpg (EXIF + FS timestamps)
[3/4] Verifying files
  ...
[4/4] Replacing originals
  ...
All done. 2 file(s) date set in place.
```

---

## Design decisions

### Why temp workspace first instead of in-place stripping?

Running exiftool directly on originals risks corrupting files if the process is interrupted or a disk error occurs. By copying to a temp directory first, the originals remain untouched until the cleaned copies are fully verified (exists, non-empty, readable). Only then are originals swapped out one-by-one via `.bak` rename, with full rollback on any single failure.

### Why byte-size check during copy?

A silent partial copy (disk full, permission issue mid-write) can produce a file that appears to exist but is truncated. Comparing byte sizes immediately after copy catches this before any processing begins.

### Why a file dialog instead of drag-and-drop or CLI args?

Metadata inspection and cleaning are inherently interactive — you need to see the files being selected. The native Explorer dialog provides search, thumbnails, multi-select, and quick-access navigation that a CLI argument can't match.

### Why no log on cancellation?

Writing a log for every cancelled or mis-typed prompt would clutter the log directory with noise. Logs are only created when actual work happens.

---

## File structure

```
etsu/
├── clean.ps1         ← metadata stripper (PS1 original, kept for reference)
├── date.ps1          ← date setter (PS1 original, kept for reference)
├── read.ps1          ← metadata viewer (PS1 original, kept for reference)
├── logs/             ← auto-created, holds last 10 logs per tool
├── README.md
└── (shared/bin/etsu.exe)  ← C# port, all three tools in one binary
```

---

## Compatibility

| Aspect | Status |
|---|---|
| OS | Windows 7+ (requires PowerShell 5.1+) |
| exiftool | Required on PATH or in `etsu/` directory |
| File dialog | Native Windows Explorer (via WinForms) |
| Log format | Plain text UTF-8, append-only |
| Log retention | Last 10 logs per prefix, auto-rotated (clean.ps1, date.ps1) |

## Known limitations

- **Windows-only** — the WinForms file dialog via `Add-Type` won't work on non-Windows systems.
- **clean.ps1: Sequential processing** — files are processed one at a time. No parallel metadata stripping.
- **clean.ps1: No dry-run mode** — there is no preview of what metadata will be deleted before committing.
- **date.ps1: No offset support** — all files get the same timestamp. No per-file timezone or offset adjustment.
- **date.ps1: FileAccessDate unchanged** — exiftool sets FileCreateDate and FileModifyDate but not FileAccessDate by default. Access time is a NTFS property, not typically meaningful for media files.
- **read.ps1: Single file only** — one file per invocation.
