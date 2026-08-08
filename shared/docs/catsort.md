## Sort files into category folders

- **Source:** `src/catsort.cs`
- **Dependencies:** none
- **Description:** Sorts files into category folders by extension. Copies each matched file, verifies the copy via SHA256, then deletes the original. Unmatched files are left untouched.

---

### Usage

```
catsort [directory] [--dry-run]
```

| Arg | Default | Description |
|---|---|---|
| `directory` | current dir | Directory to scan and sort |
| `--dry-run` / `-n` | off | Preview only - no copies or deletes |

---

### How it works

#### Matching

Scans the target directory for files (non-recursive). For each file, matches its extension against every category in `conf/.cats`.

#### Sorting

Creates the category subfolder if it doesn't exist, copies the file into it, computes SHA256 of both original and copy - if they match, deletes the original.

#### Report

Prints moved count, verified count, and any failures.

---

### Design decisions

- **Copy-then-delete (not move):** Moving preserves the file but doesn't verify the destination is readable. Copy-verify-delete ensures the file landed intact before removing the source.
- **SHA256 verification:** Catches silent corruption from disk errors or copy failures. The hash is computed on both sides and compared byte-by-byte.
- **Non-recursive by design:** Sorting is typically a one-time cleanup for a flat download folder. Recursive sorting would also move files within already-sorted subfolders, creating confusion.

---

### Source tree

```
shared/
├── src/
│   └── catsort.cs         ← single-file source
├── bin/
│   └── catsort.exe        ← compiled binary
├── conf/
│   └── .cats              ← category-to-extension map
└── docs/
    └── catsort.md
```

---

### Known limitations

- No recursion - subdirectories are not scanned.
- Overwrite prevention - if a file with the same name already exists in the target category folder, it's skipped with a notice (original left in place).