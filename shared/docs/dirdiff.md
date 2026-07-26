# dirdiff — directory comparison

Compares two directories by filename, size, and SHA256. Opens native Explorer-style folder pickers and prints a detailed report.

```
dirdiff [<source> <destination>]
```

## Modes

| Args | Behavior |
|---|---|
| `dirdiff "D:\src" "D:\dst"` | Compare the two paths directly — works on any OS |
| `dirdiff` | Opens two Explorer-style folder pickers (Windows only) |

Config file: `conf/.thr` — contains a single number (default 8, max 32) for parallel hash threads. Cap prevents accidental CPU thrashing from unreasonably high values.

## How it works

### Source and destination

If two arguments are provided, they're used directly as source and destination paths — this works on any OS. If no arguments are given (Windows only), two Explorer-style folder pickers pop up sequentially using `OpenFileDialog` with `ValidateNames = false` and `CheckFileExists = false`.

### Directory scanning

`Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)` recursively walks each selected directory. Each file's relative path is computed by stripping the root prefix. Files that can't be stat'd (permission, locked) are silently skipped.

### Three comparisons

| Check | Method | What's reported |
|---|---|---|
| **Presence** | `HashSet` difference on relative paths | Missing (in source only) and extra (in dest only) files |
| **Size** | `FileInfo.Length` comparison | Path + both byte counts when they differ |
| **SHA256** | `Parallel.ForEach` (up to 32 threads), 1 MB chunks | Count of mismatched or unreadable files |

### Example output

```
  ================================================
  Directory Comparison Report
  ================================================

  Source:      D:\source
  Dest:        D:\dest

  Files present:      957 / 959        ( 99.8%)

  Missing files (2):

    - file_a.txt
    - file_b.txt

  Sizes matched:      957 / 957        (100.0%)

  Computing SHA256 hashes (957/957)

  Hashes matched:     957 / 957        (100.0%)

  All 959 files verified OK.
```

## Design decisions

- **Why C# over Python:** The original Python dirdiff launched a PowerShell subprocess to show a folder picker. That meant two runtimes (Python + PowerShell) and a fragile command-line construction. C# calls `System.Windows.Forms.OpenFileDialog` directly — no subprocess, no runtime dependencies.
- **Why a folder picker instead of CLI arguments:** Directory comparison is inherently interactive. A folder dialog is faster, eliminates typos, and shows the actual filesystem tree.
- **Why parallel hashing:** SHA256 of large files is CPU-bound. Hashing sequentially can take minutes for many large files. `Parallel.ForEach` with configurable threads saturates modern CPUs.
- **Why OpenFileDialog repurposed as a folder picker:** The classic `FolderBrowserDialog` is an XP-era tree widget with no address bar, search, or quick access. The OpenFileDialog trick gives the full modern Explorer dialog.

## Known limitations

- No single-file diff — only presence/size/hash comparison, no line-by-line diff.
- No filtering — all files are included. Use `dirdiff | grep` at the shell level.
- In-memory file map — directories with millions of files will use significant memory.
- Hash progress counter is approximate — files complete in non-deterministic order.
