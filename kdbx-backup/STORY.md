# Why kdbx-backup exists

## The bootstrap problem

The cloud accounts holding the backups originally had their login credentials stored inside the `.kdbx` databases being backed up. This creates a circular dependency: losing local access to KeePass locks you out of the cloud accounts holding the KeePass backups.

## The resolution

The database set has a specific structure that breaks the cycle:

- **5 databases total.** One master database, four subordinate databases.
- **Master database** is protected by a memorised password. It contains the long unmemorable keys for the four subordinate databases.
- **Subordinate databases** contain working credentials including cloud account logins.

The master database password being memorised - not stored anywhere - is the actual root of trust. It has no physical form to lose, no device dependency, and isn't discoverable by someone accessing a safe or USB.

Two independent recovery paths exist after total local loss:

1. **Physical copies** - the master database on USB drives kept separately. Staleness doesn't matter: the master database never changes (password and sub-db keys are fixed), so any old copy is as good as the latest one.
2. **Identity-based recovery** - one of the cloud providers is recoverable via account recovery, without needing the stored password at all.

The four subordinate databases are **not** kept on USB because they _do_ change and a stale copy could be misleading.

## The irreducible single point of failure

Every backup strategy has a stopping point. The residual risk here is the simultaneous failure of: both USB copies, all cloud providers, and all recovery paths tied to account recovery. This compound scenario is accepted as the stopping point.

## Tamper resistance

The cloud archive is append-only by design, which means a poisoned snapshot (corrupted `.kdbx` file, ransomware-encrypted source) gets archived permanently alongside the good copies. Detection measures added to the watcher:

- **Offline hash history** (`%APPDATA%\kdbxWatch\hash-history.txt`) — an append-only manifest written outside the synced tree, providing an independent record of what hashes were seen at each snapshot. A discontinuity in this file signals tampering even if the cloud archive is uniformly poisoned.
- **Snapshot rate warning** — if the watcher fires more than `MaxSnapshotsPerHour` snapshots in any rolling hour (default 10), a warning is logged. Mass-encryption events trigger hundreds of saves in minutes, unlike normal usage patterns.
- **Source-vs-copy verification** — the source file is hashed before copy and compared against the copy hash after. A mismatch (disk error, AV interference) triggers an automatic retry and a loud error log.

These are detection measures, not prevention. The fundamental limitation remains: any code running in the user session can modify the source files before the watcher sees them.
