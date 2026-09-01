## Compare two directories

- **Source:** `src/dirdiff/` (multi-file)
- **Dependencies:** `System.Windows.Forms` (native folder picker)
- **Description:** Compares two directories by filename, size, and SHA256 (content-aware), and prints a summary report.

---

### Usage

```
dirdiff [<flags>] [<source> <destination>]
```

| Args | Behavior |
|---|---|
| `dirdiff "D:\src" "D:\dst"` | Compare the two paths directly - works on any OS |
| `dirdiff` | Opens two native folder pickers (Windows only) |
| `dirdiff -l "D:\src" "D:\dst"` | Also follow junctions/symlinks while scanning (see warning below) |

Flags may precede the two paths and also apply in interactive (picker) mode. Supported: `-l`, `-L`, `--links`.

---

### How it works

#### Input

With two arguments, they're used directly as source and destination. Without arguments (Windows only), two `FolderBrowserDialog` pickers pop up sequentially.

#### Scanning

`scanner.cs` recursively walks each directory with `Directory.GetFiles`/`GetDirectories`. Each file stores its relative path, absolute path, and size. Files/dirs that can't be read are counted and reported as `unreadable` instead of silently skewing results.

By default, reparse points (junctions/symlinks) are **skipped** so the scan can't escape the root, double-count files, or hang on a cycle. Pass `-l`/`--links` to follow them instead (see the warning below).

##### Following junctions/symlinks (`-l`) - warning

Enabling `-l` makes the walk descend into every junction/symlink it finds. This is opt-in because it can be genuinely dangerous or wrong:

- **Escapes the root.** A junction commonly points *outside* the two folders being compared (e.g. `C:\Users` junctioned into a tree). Following it pulls unrelated files into the diff, so `missing`/`extra` counts no longer reflect the folders alone.
- **Cycles / infinite recursion.** A junction can point back to an ancestor of itself (a loop). The walk keeps a visited-set of directory paths to break simple self-referencing loops, but a cycle expressed through many distinct path strings can still expand until it errors out, which is reported as `unreadable`. In the worst case this makes a run much slower or effectively hang.
- **Double counting.** Two junction names pointing at the same target appear as two separate directories of files.
- **Slower + more I/O.** Following links can dramatically inflate the tree and the number of files hashed.

If you do use `-l`, compare folders you know are self-contained and free of look-back junctions. The default (no `-l`) is the safe, intended behavior.

#### Comparison (`diff.cs`) - content-aware

Matching is content-aware, so it correctly recognizes *renamed* files as matches:

1. **Names** - count this... actually: which dest files share a name with any source file.
2. **Sizes (cheap pre-filter)** - which dest file sizes exist among source sizes. Only size-matched files are candidates for identical content.
3. **SHA256 (parallel, size-matched only)** - each size-matched source and dest file is hashed (1 MB chunks, `Parallel.ForEach` with thread count from `conf/.thr`, default 8, max 32). This avoids hashing files that can't possibly match.
4. **Hashes** - a dest file's hash matching any source hash proves identical content, regardless of filename.

#### Metrics (base = source)

| Row | Meaning (format: matched / source total) |
|---|---|
| `Files present` | dest file count / source file count |
| `Filenames matched` | dest files whose name also exists in source |
| `Sizes matched` | dest files whose size exists among source sizes |
| `Hashes matched` | dest files whose hash exists among source hashes / number of size-matched files actually hashed |
| `Missing files` | source files with no hash match in dest |
| `Extra files` | dest files with no hash match in source |

Each percentage is computed from that row's own numerator/denominator. Rows are right-aligned with a fixed percentage column, so the `]` of every `[NN.NN%]` lines up regardless of whether the integer part is 1, 2, or 3 digits.

#### Example output

```
  ──────────────────────────────────────────────────

  Comparing directories...

  Files present:           43 / 42   [102.38%]
  Filenames matched:       00 / 42    [00.00%]
  Sizes matched:           41 / 42    [97.62%]
  Hashes matched:          41 / 41   [100.00%]
  Missing files:                 0
  Extra files:                   2

  ──────────────────────────────────────────────────

  Issue(s) found:

    + 2 items extra
```

The console report only shows *counts* of missing/extra files. For the actual filenames, enable run logging (see below).

---

### Run logging (optional)

By default dirdiff prints only how many files are missing/extra. To see *which* files, turn on run logging in `conf/.dirdiff`:

```
[General]
log=true     # true|false - write a per-run log folder
logKeep=10   # keep this many most-recent run folders, delete the oldest
```

When `log=true`, each run creates a timestamped folder under `<exe-dir>\..\logs\dirdiff` (not configurable):

```
logs/dirdiff/
└── 20260901-213014/          ← yyyyMMdd-HHmmss
    ├── summary.log           ← full console report for that run
    ├── missingFiles.txt      ← source files with no hash match in dest (one per line)
    └── extraFiles.txt        ← dest files with no hash match in source (one per line)
```

The listing in `missingFiles.txt`/`extraFiles.txt` is exactly what the summary can't show: the actual file paths behind each missing/extra count. Retention prunes the `logKeep` newest folders, deleting the oldest on each run.

---

### Design decisions

- **Content-aware matching over name-only:** Renamed files share no name between trees, so a name-only diff would report them all as missing. Matching by SHA256 hash (with a size pre-filter to limit hashing) gives the real "is this actually there" answer.
- **Size pre-filter before hashing:** Files of different sizes can't be identical content, so only size-matched candidates are hashed. This keeps large-tree comparisons fast.
- **Separate `report` metrics from the directory picker:** Unlike a name+identical-name diff, presence is defined by content, not path.
- **Multi-file source:** split into `program.cs` (UI/report), `scanner.cs` (walk + hash), `diff.cs` (matching), and `logcfg.cs` (config + run logging) - same pattern as `mmu/` and `etsu/`, compiled together via `src\dirdiff\*.cs`.
- **Reparse points skipped by default:** junctions/symlinks are ignored unless `-l` is passed, because following them can escape the roots, double-count, or loop. The safe default means `missing`/`extra` reflect only the actual folder contents.
- **Run logging on via config:** controlled by `conf/.dirdiff` (`log`, `logKeep`); a normal run stays fast and quiet, and the toggle captures the actual missing/extra filenames for audit.

---

### Source tree

```
shared/
├── src/
│   └── dirdiff/
│       ├── program.cs      ← Main, UI, metric layout
│       ├── scanner.cs      ← tree walk, FileEntry, SHA256
│       ├── diff.cs         ← size pre-filter + hash matching, DiffResult
│       └── logcfg.cs       ← config load + run logging/retention
├── bin/
│   └── dirdiff.exe
├── logs/
│   └── dirdiff/            ← per-run log folders (when enabled)
├── conf/
│   ├── .thr               ← parallel hash thread count
│   └── .dirdiff           ← run logging toggle/retention
└── docs/
    └── dirdiff.md
```

---

### Known limitations

- No single-file diff - presence/size/hash only, no line-by-line diff.
- No filtering - all files are included.
- In-memory file map + hash cache - directories with millions of files will use significant memory.
- Hash matching works at file level; a source file present in dest under a *different* name still counts as present (by design).
- Following junctions (`-l`) can escape the roots or loop; the visited-set breaks simple cycles only.
