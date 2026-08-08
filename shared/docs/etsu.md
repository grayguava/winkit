## Read, clean, and set file dates

- **Source:** `src/etsu/etsu.cs` (entry + helpers), `read.cs`, `clean.cs`, `date.cs`
- **Dependencies:** `System.Windows.Forms` (native file pickers) + external `exiftool`
- **Description:** Interactive CLI frontend for ExifTool. Reads all metadata from one file, strips EXIF/IPTC/XMP from several (rollback-safe), or sets EXIF dates and filesystem timestamps on several.

---

### Usage

```
etsu read       Open a single file and display all metadata
etsu clean      Strip EXIF/IPTC/XMP from files (rollback-safe)
etsu date       Set EXIF dates + filesystem timestamps on files
```

No subcommand prints usage and exits.

Every subcommand shares the same flow: header with the detected ExifTool version (`-ver`), native `OpenFileDialog` pickers, `[n/5]` step progress, and `Press enter or spacebar to exit.` so the report stays readable before the window closes.

---

### How it works

#### read

Picks one file and runs `exiftool "<file>"`, printing every tag with no modification. The file is opened read-only - nothing is written.

#### clean

The rollback-safe strip pipeline:

1. Copy - each file is copied into a temp workspace next to the binary (`_exiftool_tmp_<guid>`), preserving extensions. Each copy is verified by byte size.
2. Clean - runs `exiftool -all= -overwrite_original -P` on the temp copies, recording `Deleting ...` lines from `-v` output per file.
3. Verify - each temp file must exist, be non-empty, and open for reading.
4. Swap - renames each original to `<file>.bak`, moves the cleaned copy into the original path, checks the result is non-empty.
5. Done - deletes `.bak` files, removes the temp workspace, writes a log.

The original is never touched until the cleaned copy passes verification. Any failure restores made `.bak` files and removes the temp workspace - originals are left exactly as they were. `-P` preserves filesystem modification times.

#### dates

Prompts for a date (`YYYY:MM:DD HH:MM:SS`), then uses the same copy/verify/swap pipeline:

```
exiftool -AllDates=<date> -FileModifyDate=<date> -FileCreateDate=<date> \
         -overwrite_original -P
```

`-AllDates=` sets DateTimeOriginal, CreateDate, ModifyDate plus the filesystem mtime; `-FileCreateDate=` sets the filesystem creation timestamp. Empty date input cancels.

---

### Design decisions

- **Copy-verify-swap pipeline:** the original is never touched until a verified cleaned copy exists. Any failure restores `.bak` files, so a bad wipe can't destroy metadata permanently.
- **Interactive pickers over arguments:** these operations are inherently interactive - a native dialog avoids typos and shows the real filesystem tree.
- **Raw ExifTool dump for read:** `read` delegates wholesale instead of re-deriving a categorized summary, so all tags (including unknown ones) are shown.
- **Per-file `-v` parsing in clean:** the `Deleting ...` lines are captured into logs so you can see exactly which tags were stripped from each file.

---

### References

- [ExifTool](https://exiftool.org/) - the metadata engine etsu wraps

---

### Source tree

```
shared/
├── src/
│   └── etsu/
│       ├── etsu.cs         ← main entry + shared helpers
│       ├── read.cs         ← metadata viewer
│       ├── clean.cs        ← metadata stripper
│       └── date.cs         ← date setter
├── bin/
│   └── etsu.exe            ← compiled binary
├── logs/                   ← clean_*.log, date_*.log (10 kept each)
└── docs/
    └── etsu.md
```

---

### Known limitations

- Interactive only - no mode takes a file list from arguments or stdin.
- `read` shows the raw ExifTool dump, not a categorized summary.
- Date input is a plain string - no validation beyond ExifTool's own parsing.