# Why kdbx-backup exists

## The bootstrap problem

The cloud accounts (Google, Dropbox, Koofr) originally had their login credentials stored inside the `.kdbx` databases being backed up. This creates a circular dependency: losing local access to KeePass locks you out of the cloud accounts holding the KeePass backups.

## The resolution

The database set has a specific structure that breaks the cycle:

- **5 databases total.** One master database, four subordinate databases.
- **Master database** is protected by a memorised password (unchanged for years, held by one person). It contains the long unmemorable keys for the four subordinate databases.
- **Subordinate databases** contain working credentials including cloud account logins.

The master database password being memorised - not stored anywhere - is the actual root of trust. It has no physical form to lose, no device dependency, and isn't discoverable by someone accessing a safe or USB.

Two independent recovery paths exist after total local loss:

1. **Physical copies** - the master database on USB drives kept separately. Staleness doesn't matter: the master database never changes (password and sub-db keys are fixed), so any old copy is as good as the latest one.
2. **Identity-based recovery** - one of the three cloud providers (Google) is recoverable via phone-based OTP across two independent phone numbers, without needing the stored password at all.

The four subordinate databases are **not** kept on USB because they _do_ change and a stale copy could be misleading.

## The irreducible single point of failure

Every backup strategy has a stopping point. The residual risk here is the simultaneous failure of: both USB copies, all three cloud providers, and all three phone numbers tied to Google account recovery. This compound scenario is accepted as the stopping point.