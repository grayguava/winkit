# archive - retired / abandoned tools

| Tool | Lang | Portable | Description |
|---|---|---|---|
| wallswitch/ | C# | Yes | Predecessors of the current wallswitch/, two versions: `v1/` is two separate binaries (nature.cs + tech.cs), one per wallpaper pool, simple random picker with no shuffle queue; `v2/` is the merged single-pool daemon (one hotkey, shuffle queue, `.conf`). Both superseded by the multi-pool multi-target redesign in `wallswitch/`. |
| delpyc/ | Python | No | Superseded by delcache. Recursively deletes `__pycache__` directories. Requires Python + `click`. |
| torui/ | Python | No | Abandoned live terminal dashboard for a local Tor daemon. Uses `rich` and `stem`. |
| PCHealth/ | PowerShell | Yes | Collection of scripts for system health telemetry (temps, storage, network, software, events, drivers, file integrity). |
| dirdiff/ | Python | No | Superseded by `shared/bin/dirdiff.exe` (C# rewrite). Compares two directories by filename, size, and SHA256. Stdlib-only. |
| etsu/ | Python/PowerShell/C# | No | Predecessors of the shared `etsu` tool (ExifTool metadata stripper/viewer/date-setter), three versions: `v1/` Python stripe, `v2/` C# GUI wrapper (etgui_wrapper), `v3/` PowerShell read/clean/date scripts. All require exiftool CLI on PATH; superseded by `shared/bin/etsu.exe`. |
| screentime/ | C# | Yes | Abandoned attempt at a simple screentime reporter (boots/day + active-time/day from the Windows event log, no per-app tracking, no daemon, winexe + log file). `v1/` is the last working state with the full reasoning: standard mode hand-verified exact, elevated mode (Security-log lock/unlock) corrected but never validated long-term. Given up on because every accuracy source lies differently (locked-awake overcount vs. audit dependency vs. retention eviction) and the effort/reward didn't justify continuing. |