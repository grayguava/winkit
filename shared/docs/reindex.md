# reindex — sequential file renaming

Reindexes all files in a directory to sequential numbers (01.jpg, 02.png, 03.pdf, etc.) while preserving extensions. Handles collisions by renaming through temp GUIDs — safe to run on directories with existing numbered files.

```
reindex [directory] [--dry-run] [--rollback]
```

| Arg | Default | Description |
|---|---|---|
| `directory` | current dir | Directory whose files to rename |
| `--dry-run` / `-n` | off | Preview only — no actual renames |
| `--rollback` / `-r` | off | Revert the most recent reindex in the target directory |

Padding adjusts automatically: 1-9 files -> `01.ext`, 10-99 -> `001.ext`, etc.

## indexignore

**Location:** `conf/.indexignore`

Filenames to skip during reindex, one per line (case-insensitive). Built-in defaults:

```ini
desktop.ini
thumbs.db
.ds_store
folder.jpg
```

Lines starting with `#` or `;` are comments.

## Rollback

Every successful reindex saves a log to `logs/reindex/<timestamp>.txt` with the original directory and each original-to-final mapping. `--rollback` reads the most recent log and reverses the rename using the same two-phase temp-GUID approach.

Only the 25 most recent logs are kept. Older logs are pruned automatically.

## How it works

1. Loads ignore list from `conf/.indexignore`.
2. Scans the directory for files (non-recursive), sorted alphabetically, filtered by ignore list.
3. Renames each file to a random GUID temp name (avoids collisions with final names).
4. Renames each temp file to the sequential name.
5. Writes a rollback log to `logs/reindex/`.
6. If any step fails, temp files are cleaned up and originals are preserved.

## Design decisions

- **Two-phase rename (not rename-in-place):** If file `3.jpg` already exists and we want to rename `zzz.jpg` to `3.jpg`, a direct rename would overwrite. The two-phase approach (original -> guid -> final) avoids all collisions.
- **Alphabetical order:** Provides a deterministic, reproducible sequence. Sorting by date or size would make the order depend on filesystem metadata.
- **Rollback via logs:** Renaming is destructive — `--rollback` gives a safety net without needing a VCS or file history.
- **indexignore for system files:** Files like `desktop.ini` and `thumbs.db` shouldn't be touched. A config file keeps the list editable without recompilation.

## Known limitations

- No recursion — only files in the specified directory.
- Order is alphabetical by filename — not by any other property.
- Rollback only works if the log file still exists (25-log rotation).
