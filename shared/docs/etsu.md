# etsu — ExifTool Simple Use

CLI frontend for exiftool with three subcommands.

```
etsu read       Open a single file and display all metadata
etsu clean      Strip EXIF/IPTC/XMP from multiple files with rollback safety
etsu date       Set EXIF dates + filesystem timestamps on multiple files
```

Requires `exiftool.exe` on PATH or in the same directory as the binary.

## etsu read

Opens a single-file picker, runs `exiftool` on the selection, and prints all tags.

## etsu clean

Multi-file picker, copies to a temp workspace, runs `exiftool -all= -overwrite_original -P`, verifies integrity, then swaps originals with `.bak` rollback on any failure. Logs to `logs/clean_*.log`.

## etsu date

Multi-file picker, prompts for a date (`YYYY:MM:DD HH:MM:SS`), runs `exiftool -AllDates= -FileModifyDate= -FileCreateDate=`, verifies, swaps with rollback. Logs to `logs/date_*.log`.
