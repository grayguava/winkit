## Clean cache directories

- **Source:** `src/delcache.cs`
- **Dependencies:** none
- **Description:** Finds and deletes cache/temp directories (`__pycache__`, `node_modules`, etc.) recursively, matching directory names against a config file.

---

### Usage

```
delcache [path]
```

- **path** - root directory to search (default: current directory)

> [!WARNING]
> A typo or malicious entry in the config can cause data loss. Always read the found list before typing `y`.

---

### How it works

#### Discovery

Reads target directory names from `conf/.cdirs` (one per line, `#` comments ignored). For each target it calls `Directory.EnumerateDirectories(root, target, SearchOption.AllDirectories)` to find every matching subdirectory at any depth.

#### Confirmation

Prints the full path of every match, numbered, then prompts [y/N] - only proceeds on explicit `y` or `yes`.

#### Deletion

Iterates the list and deletes each directory with `Directory.Delete(path, true)`. Reports success count and prints failures to stderr.

#### Error handling

- Directories that can't be read during search (permission denied) are silently skipped.
- Directories that fail to delete are logged to stderr with the reason; remaining deletions continue.
- If the root path doesn't exist, prints an error and exits.

---

### Design decisions

- **C# over Python (delpyc):** The original delpyc required Python 3.8+ and the `click` package. delcache is a standalone .exe with zero runtime dependencies - copy and run.
- **Always prompt:** Cache directories are safe to delete in theory, but a typo in `.cdirs` or a wrong root path can delete the wrong data. Forcing Y/N confirmation on every run ensures you see exactly what will be deleted.
- **Config file:** Adding or removing targets (node_modules, .cache, .vs) doesn't require recompilation. The config file is editable by any text editor.

---

### Source tree

```
shared/
├── src/
│   └── delcache.cs        ← single-file source
├── bin/
│   └── delcache.exe       ← compiled binary
├── conf/
│   └── .cdirs             ← target directory names
└── docs/
    └── delcache.md
```

---

### Known limitations

- No exclusions - you can't skip specific paths within a single run.
- No parallel deletion - directories are removed sequentially.
- Follows symlinks - `SearchOption.AllDirectories` traverses junctions and symlinks.