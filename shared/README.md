# shared — unified CLI tools

Single directory housing all portable CLI tools. One `bin/` for PATH, one `conf/` for config files, one `build.bat` to compile everything.

Each tool is fully independent — removing one .exe and its config file won't affect any other tool in the directory. The entire shared/ folder is portable: copy it anywhere, add its `bin/` to PATH, and all tools work.

## Tools

| Tool | Description | Docs |
|---|---|---|
| **delcache** | Recursive cache directory cleanup | [docs/delcache.md](docs/delcache.md) |
| **dirdiff** | Directory comparison (presence, size, SHA256) | [docs/dirdiff.md](docs/dirdiff.md) |
| **catsort** | Category-based file sorting by extension | [docs/catsort.md](docs/catsort.md) |
| **reindex** | Sequential file renaming with rollback | [docs/reindex.md](docs/reindex.md) |
| **etsu** | ExifTool frontend (read, clean, date) | [docs/etsu.md](docs/etsu.md) |
| **mmu** | My music util (audio download, more later) | [docs/mmu.md](docs/mmu.md) |

## PATH setup

```
setx PATH "%PATH%;D:\DevEnv\custom_utils\shared\bin"
```

One entry covers all tools. Restart your terminal after setting.

## Building

```
build.bat
```

Uses Windows' built-in C# compiler (`csc.exe`). No Visual Studio, no NuGet, no dotnet CLI, no install step.

### Prerequisites

- .NET Framework 4.0+ (ships with Windows 8+; available for Windows 7).
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` — part of the .NET Framework SDK component of Windows.
- `System.Windows.Forms.dll` — referenced only by `dirdiff.cs`, ships with .NET Framework.

### Build output

```
shared/
├── src/
│   ├── dirdiff.cs           ← source (edit this)
│   ├── delcache.cs          ← source (edit this)
│   ├── catsort.cs           ← source (edit this)
│   ├── reindex.cs           ← source (edit this)
│   └── etsu/
│       ├── etsu.cs          ← main entry + shared helpers
│       ├── read.cs           ← metadata viewer
│       ├── clean.cs          ← metadata stripper
│       └── date.cs           ← date setter
│   └── mmu/
│       ├── mmu.cs           ← main entry + shared helpers
│       └── download.cs       ← audio downloader
├── bin/
│   ├── dirdiff.exe          ← compiled binary (build output)
│   ├── delcache.exe         ← compiled binary (build output)
│   ├── catsort.exe          ← compiled binary (build output)
│   ├── reindex.exe          ← compiled binary (build output)
│   ├── etsu.exe             ← compiled binary (build output)
│   └── mmu.exe              ← compiled binary (build output)
├── conf/
│   ├── .thr                 ← dirdiff parallel hash threads (default 8)
│   ├── .cdirs               ← delcache configuration
│   ├── .cats                ← catsort configuration
│   ├── .indexignore         ← reindex skip list
│   ├── .mmuconfig           ← mmu dependency paths + download dir
│   ├── yt-dlp.conf          ← yt-dlp flags (mmu)
│   └── logs/reindex/        ← rollback history (auto, 25 newest kept)
├── docs/
│   ├── delcache.md
│   ├── dirdiff.md
│   ├── catsort.md
│   ├── reindex.md
│   ├── etsu.md
│   └── mmu.md
├── build.bat
└── README.md
```

### Adding a new tool

1. Write your .cs file in `src/`.
2. Add a compile line to `build.bat` (same pattern as the existing tools).
3. If the tool needs a config file, add it to `conf/` and reference it from code as `../conf/<filename>`.
4. Run `build.bat` — the .exe lands in `bin/` automatically.

### 32-bit systems

For 32-bit Windows, edit `build.bat` to use `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` (no `64`).

## Compatibility

| Aspect | delcache | dirdiff | catsort | reindex | etsu |
|---|---|---|---|---|---|
| OS | Windows 7+ | Windows 7+ | Windows 7+ | Windows 7+ | Windows 7+ |
| .NET version | .NET 4.0 | .NET 4.0 | .NET 4.0 | .NET 4.0 | .NET 4.0 |
| Dependencies | None | `System.Windows.Forms` | None | None | `System.Windows.Forms` + exiftool |
| Architecture | x64 | x64 | x64 | x64 | x64 |
