## Rename files sequentially

- **Source:** `src/reindex.cs`
- **Dependencies:** none
- **Description:** Reindexes all files in a directory to sequential numbers (01.jpg, 02.png, 03.pdf, etc.) preserving extensions, with rollback support. Handles collisions by renaming through temp GUIDs.

---

### Usage

```
reindex [directory] [--dry-run] [--rollback]
```

| Arg | Default | Description |
|---|---|---|
| `directory` | current dir | Directory whose files to rename |
| `--dry-run` / `-n` | off | Preview only - no actual renames |
| `--rollback` / `-r` | off | Revert the most recent reindex in the target directory |

Padding adjusts automatically: 1-9 files -> `01.ext`, 10-99 -> `001.ext`, etc.

---

### How it works

#### Ignore list

Loads `conf/.indexignore` - filenames to skip, one per line (case-insensitive), `#`/`;` comments allowed. Built-in defaults: `desktop.ini`, `thumbs.db`, `.ds_store`, `folder.jpg`.

#### Two-phase rename

Scans the directory for files (non-recursive), sorted alphabetically. Renames each file to a random GUID temp name (avoids collisions with final names), then renames each temp file to the sequential name.

#### Rollback

Every successful reindex writes a log to `logs/reindex/<timestamp>.txt` with the original directory and each original-to-final mapping. `--rollback` reads the most recent log and reverses the rename using the same two-phase GUID approach.

If any step fails mid-run, temp files are cleaned up and originals are preserved.

---

### Design decisions

- **Two-phase rename (not rename-in-place):** if file `3.jpg` already exists and we want to rename `zzz.jpg` to `3.jpg`, a direct rename would overwrite. The two-phase approach (original -> guid -> final) avoids all collisions.
- **Alphabetical order:** provides a deterministic, reproducible sequence. Sorting by date or size would make the order depend on filesystem metadata.
- **Rollback via logs:** renaming is destructive - `--rollback` gives a safety net without needing a VCS or file history.
- **indexignore for system files:** files like `desktop.ini` and `thumbs.db` shouldn't be touched. A config file keeps the list editable without recompilation.

---

### Source tree

```
shared/
├── src/
│   └── reindex.cs         ← single-file source
├── bin/
│   └── reindex.exe        ← compiled binary
├── conf/
│   └── .indexignore       ← skip list
├── logs/
│   └── reindex/           ← rollback logs (25 kept)
└── docs/
    └── reindex.md
```

---

### Known limitations

- No recursion - only files in the specified directory.
- Order is alphabetical by filename - not by any other property.
- Rollback only works if the log file still exists (25-log rotation).