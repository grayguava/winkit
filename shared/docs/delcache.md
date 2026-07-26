# delcache — recursive cache cleanup

Finds and deletes cache/temp directories (`__pycache__`, `node_modules`, etc.) by recursively scanning a root path and matching directory names against a config file.

```
delcache [path]
```

- **path** — root directory to search (default: current directory)

## Configuration

**Location:** `conf/.cdirs`

One directory name per line, `#` for comments:

```ini
__pycache__
node_modules
.bazel
.cache
.vs
```

If the file is missing or empty, defaults to `__pycache__` and `node_modules`.

> [!WARNING]
> A typo or malicious entry can cause data loss. Always read the found list before typing y.

## How it works

1. Resolves `conf/.cdirs` relative to the .exe location (`shared/bin/delcache.exe` -> `shared/conf/.cdirs`).
2. Reads target directory names from the file — one per line, blank lines and `#` comments ignored.
3. For each target, calls `Directory.EnumerateDirectories(root, target, SearchOption.AllDirectories)` to find every matching subdirectory at any depth.
4. Prints the full path of every match, numbered by count.
5. Prompts [y/N] — only proceeds on explicit y or yes.
6. Iterates the list and deletes each directory with `Directory.Delete(path, true)`.
7. Reports success count and prints failures to stderr (permission errors, locked files).

**Error handling:**
- Directories that can't be read during search (permission denied) are silently skipped.
- Directories that fail to delete are logged to stderr with the reason; remaining deletions continue.
- If the root path doesn't exist, prints an error and exits.

## Design decisions

- **Why C# over Python (delpyc):** The original delpyc required Python 3.8+ and the `click` package. delcache is a standalone .exe with zero runtime dependencies — copy and run.
- **Why always prompt:** Cache directories are safe to delete in theory, but a typo in `.cdirs` or a wrong root path can delete the wrong data. Forcing Y/N confirmation on every run ensures you see exactly what will be deleted.
- **Why a config file:** Adding or removing targets (node_modules, .cache, .vs) doesn't require recompilation. The config file is editable by any text editor.

## Known limitations

- No exclusions — you can't skip specific paths within a single run.
- No parallel deletion — directories are removed sequentially.
- Follows symlinks — `SearchOption.AllDirectories` traverses junctions and symlinks.
