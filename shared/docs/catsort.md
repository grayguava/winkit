# catsort — category-based file sorting

Sorts files into category folders by extension. Copies each matched file into its category subfolder, verifies the copy via SHA256, then deletes the original. Unmatched files are left untouched.

```
catsort [directory] [--dry-run]
```

| Arg | Default | Description |
|---|---|---|
| `directory` | current dir | Directory to scan and sort |
| `--dry-run` / `-n` | off | Preview only — no copies or deletes |

## Configuration

**Location:** `conf/.cats`

```ini
[Images]
ext=.jpg,.jpeg,.png,.gif,.bmp,.webp,.svg,.ico,.avif,.heic

[Videos]
ext=.mp4,.mkv,.avi,.mov,.wmv,.flv,.webm,.m4v,.mpg,.mpeg

[Documents]
ext=.pdf,.doc,.docx,.txt,.md,.rtf,.odt,.odp,.epub,.tex

[Code]
ext=.cs,.rs,.py,.js,.ts,.java,.cpp,.c,.go,.rb,.php,.swift,.kt,.lua,.pl,.zig

[Web]
ext=.html,.htm,.css,.scss,.less,.jsx,.tsx,.vue,.svelte,.astro

[Config]
ext=.json,.xml,.yaml,.yml,.toml,.ini,.cfg,.conf,.env,.gitignore

[Scripts]
ext=.bat,.cmd,.ps1,.psm1,.sh,.bash,.zsh,.vbs

[Data]
ext=.csv,.tsv,.sql,.db,.sqlite,.jsonl,.parquet
```

Full list in [`conf/.cats`](../conf/.cats) — edit freely, no recompilation needed.

## How it works

1. Reads `conf/.cats` relative to the .exe location.
2. Scans the target directory for files (non-recursive).
3. For each file, matches its extension against every category.
4. Creates the category subfolder if it doesn't exist.
5. Copies the file into the category folder.
6. Computes SHA256 of both original and copy — if they match, deletes the original.
7. Reports moved count, verified count, and any failures.

## Design decisions

- **Copy then delete (not move):** Moving preserves the file but doesn't verify the destination is readable. Copy-verify-delete ensures the file landed intact before removing the source.
- **SHA256 verification:** Catches silent corruption from disk errors or copy failures. The hash is computed on both sides and compared byte-by-byte.
- **Non-recursive by design:** Sorting is typically a one-time cleanup for a flat download folder. Recursive sorting would also move files within already-sorted subfolders, creating confusion.

## Known limitations

- No recursion — subdirectories are not scanned.
- Overwrite prevention — if a file with the same name already exists in the target category folder, it's skipped with a notice (original left in place).
