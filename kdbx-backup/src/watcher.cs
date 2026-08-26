using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace kdbxWatch
{
    internal static class Program
    {
        private static string SourceDir = "";
        private static string DestDir = "";
        private static int DebounceMs = 5000;
        private static string LogFile = "";
        private static string LastKnownGoodFile = "";
        private static int MaxSnapshotsPerHour = 10;

        private static readonly Dictionary<string, string> LastHashes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Timer> DebounceTimers =
            new Dictionary<string, Timer>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DateTime> SnapshotTimes =
            new List<DateTime>();
        private static readonly object StateLock = new object();
        private static readonly object LogLock = new object();
        private static string CurrentLogDay = "";

        private static Mutex SingleInstanceMutex;

        private static void Main()
        {
            bool isNewInstance;
            SingleInstanceMutex = new Mutex(true, @"Local\kdbxWatchSingleInstance", out isNewInstance);

            if (!isNewInstance)
            {
                TryLog("Another instance is already running (mutex held). Exiting.");
                return;
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                LoadConfig(Path.Combine(baseDir, ".watch.conf"), baseDir);
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile) ?? baseDir);
                Directory.CreateDirectory(DestDir);

                Log("Started. Watching: " + SourceDir);

                TakeBaselineSnapshot();

                using (var watcher = new FileSystemWatcher(SourceDir, "*.kdbx"))
                {
                    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                    watcher.InternalBufferSize = 64 * 1024;
                    watcher.Error += OnWatcherError;
                    watcher.Changed += OnFileEvent;
                    watcher.Created += OnFileEvent;
                    watcher.Renamed += OnFileRenamed;
                    watcher.EnableRaisingEvents = true;

                    new ManualResetEvent(false).WaitOne();
                }
            }
            catch (Exception ex)
            {
                TryLog("FATAL: " + ex.GetType().Name + ": " + ex.Message);
                Environment.ExitCode = 1;
            }
            finally
            {
                SingleInstanceMutex.ReleaseMutex();
                SingleInstanceMutex.Dispose();
            }
        }

        private static void TryLog(string msg)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
                Directory.CreateDirectory(logDir);
                string fallbackLog = Path.Combine(logDir, "watch.log");
                string time = DateTime.Now.ToString("hh:mm:ss tt");
                File.AppendAllText(fallbackLog, time + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        private static void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Log("Watcher error: " + e.GetException().Message);
        }

        private static void LoadConfig(string configPath, string baseDir)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                values[key] = val;
            }

            SourceDir = RequireValue(values, "sourceDir");

            int secs;
            string raw;
            if (!values.TryGetValue("DebounceSeconds", out raw)) raw = "5";
            if (!int.TryParse(raw, out secs) || secs < 1) secs = 5;
            if (secs > 2000000) secs = 2000000;
            DebounceMs = secs * 1000;

            string destRaw = values.ContainsKey("DestDir") ? values["DestDir"] : "snapshots";
            DestDir = Path.IsPathRooted(destRaw) ? destRaw : Path.Combine(baseDir, destRaw);

            string logDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
            LogFile = Path.Combine(logDir, "watch.log");

            int rateLimit;
            string rateRaw;
            if (!values.TryGetValue("MaxSnapshotsPerHour", out rateRaw) || !int.TryParse(rateRaw, out rateLimit))
                rateLimit = MaxSnapshotsPerHour;
            if (rateLimit < 0) rateLimit = 0;
            MaxSnapshotsPerHour = rateLimit;

            string lkgRaw = values.ContainsKey("LastKnownGoodFile") ? values["LastKnownGoodFile"] : "";
            string lkgDefault = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "kdbxWatch", "hash-history.txt");
            LastKnownGoodFile = string.IsNullOrEmpty(lkgRaw)
                ? lkgDefault
                : (Path.IsPathRooted(lkgRaw) ? lkgRaw : Path.Combine(baseDir, lkgRaw));
        }

        private static string RequireValue(Dictionary<string, string> values, string key)
        {
            if (!values.ContainsKey(key) || values[key].Length == 0)
                throw new InvalidOperationException("Missing required config value: " + key);
            return values[key];
        }

        private static void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            ScheduleDebounce(e.Name);
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            ScheduleDebounce(e.Name);
        }

        private static void ScheduleDebounce(string fileName)
        {
            if (fileName == null) return;

            lock (StateLock)
            {
                Timer existing;
                if (DebounceTimers.TryGetValue(fileName, out existing))
                {
                    existing.Change(DebounceMs, Timeout.Infinite);
                    return;
                }

                var timer = new Timer(OnDebounceElapsed, fileName, DebounceMs, Timeout.Infinite);
                DebounceTimers[fileName] = timer;
            }
        }

        private static void OnDebounceElapsed(object state)
        {
            string fileName = (string)state;
            string fullPath = Path.Combine(SourceDir, fileName);

            lock (StateLock)
            {
                Timer timer;
                if (DebounceTimers.TryGetValue(fileName, out timer))
                {
                    timer.Dispose();
                    DebounceTimers.Remove(fileName);
                }

                if (!File.Exists(fullPath))
                {
                    Log("File no longer exists, skipping: " + fileName);
                    return;
                }

                string newHash;
                try
                {
                    newHash = ComputeHash(fullPath);
                }
                catch (IOException)
                {
                    Log("File locked, rescheduling: " + fileName);
                    ScheduleDebounce(fileName);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Log("File access denied, rescheduling: " + fileName);
                    ScheduleDebounce(fileName);
                    return;
                }

                string oldHash;
                if (LastHashes.TryGetValue(fileName, out oldHash) && oldHash == newHash)
                {
                    Log("Hash unchanged, skipping: " + fileName);
                    return;
                }

                Log("Change detected: " + fileName);
                TakeSnapshot();
            }
        }

        private static string ComputeHash(string path)
        {
            using (HashAlgorithm algo = SHA256.Create())
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = algo.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void TakeBaselineSnapshot()
        {
            lock (StateLock)
            {
                string[] files = Directory.GetFiles(SourceDir, "*.kdbx");

                if (files.Length == 0)
                {
                    Log("No .kdbx files found at startup; baseline is empty.");
                    return;
                }

                var currentHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    currentHashes[Path.GetFileName(f)] = ComputeHash(f);
                }

                Dictionary<string, string> lastSnapshotHashes = LoadMostRecentSnapshotHashes();

                if (lastSnapshotHashes != null && HashesMatch(currentHashes, lastSnapshotHashes))
                {
                    foreach (var pair in currentHashes) LastHashes[pair.Key] = pair.Value;
                    Log("Baseline unchanged since last run, skipping snapshot (" + currentHashes.Count + " files).");
                    return;
                }

                string snapshotDir = CreateSnapshotDir();
                Dictionary<string, string> hashes;
                CopyAllKdbxFiles(snapshotDir, out hashes);

                foreach (var pair in hashes) LastHashes[pair.Key] = pair.Value;

                AppendHashHistory(hashes);
                Log("Baseline snapshot created: " + snapshotDir.Substring(DestDir.Length).TrimStart(Path.DirectorySeparatorChar) + " (" + hashes.Count + " files)");
            }
        }

        private static bool HashesMatch(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var pair in a)
            {
                string otherHash;
                if (!b.TryGetValue(pair.Key, out otherHash)) return false;
                if (otherHash != pair.Value) return false;
            }
            return true;
        }

        private static Dictionary<string, string> LoadMostRecentSnapshotHashes()
        {
            if (!Directory.Exists(DestDir)) return null;

            string newestLeaf = FindNewestLeaf(DestDir, 3);
            if (newestLeaf == null) return null;

            string[] sumsFiles = Directory.GetFiles(newestLeaf, "SHA256SUMS.txt");
            if (sumsFiles.Length == 0) return null;

            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string rawLine in File.ReadAllLines(sumsFiles[0]))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0) continue;

                    int sep = line.IndexOf(':');
                    if (sep <= 0) continue;

                    string fileName = line.Substring(0, sep).Trim();
                    string hash = line.Substring(sep + 1).Trim();
                    hashes[fileName] = hash;
                }
            }
            catch (IOException)
            {
                return null;
            }

            return hashes;
        }

        private static string FindNewestLeaf(string dir, int depth)
        {
            string[] children = Directory.GetDirectories(dir);
            if (children.Length == 0) return null;

            Array.Sort(children, StringComparer.OrdinalIgnoreCase);
            string newest = children[children.Length - 1];

            if (depth <= 1) return newest;
            return FindNewestLeaf(newest, depth - 1);
        }

        private static void TakeSnapshot()
        {
            if (MaxSnapshotsPerHour > 0)
            {
                DateTime cutoff = DateTime.Now.AddHours(-1);
                SnapshotTimes.RemoveAll(t => t < cutoff);
                SnapshotTimes.Add(DateTime.Now);
                if (SnapshotTimes.Count > MaxSnapshotsPerHour)
                    Log("WARNING: " + SnapshotTimes.Count + " snapshots in the last hour - possible mass-modification event.");
            }

            string snapshotDir = CreateSnapshotDir();
            Dictionary<string, string> hashes;
            int count = CopyAllKdbxFiles(snapshotDir, out hashes);

            foreach (var pair in hashes) LastHashes[pair.Key] = pair.Value;

            AppendHashHistory(hashes);
            Log("Snapshot created: " + snapshotDir.Substring(DestDir.Length).TrimStart(Path.DirectorySeparatorChar) + " (" + count + " files)");
        }

        private static string CreateSnapshotDir()
        {
            DateTime now = DateTime.Now;
            string fullPath = Path.Combine(
                DestDir,
                now.ToString("MM"),
                now.ToString("dd"),
                now.ToString("HHmmss"));
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        private static int CopyAllKdbxFiles(string snapshotDir, out Dictionary<string, string> hashes)
        {
            hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;

            foreach (string f in Directory.GetFiles(SourceDir, "*.kdbx"))
            {
                string fileName = Path.GetFileName(f);
                string dest = Path.Combine(snapshotDir, fileName);

                string srcHash = null;
                try { srcHash = ComputeHash(f); }
                catch (IOException) { Log("Source locked during hashing, skipping integrity check: " + fileName); }
                catch (UnauthorizedAccessException) { Log("Source access denied during hashing: " + fileName); }

                File.Copy(f, dest, overwrite: true);

                string copyHash = ComputeHash(dest);

                if (srcHash != null && copyHash != srcHash)
                {
                    Log("Copy mismatch for " + fileName + ", retrying");
                    File.Copy(f, dest, overwrite: true);
                    copyHash = ComputeHash(dest);
                    if (copyHash != srcHash)
                        Log("ERROR: copy of " + fileName + " still differs from source after retry");
                }

                hashes[fileName] = copyHash;
                count++;
            }

            WriteSumsFile(snapshotDir, hashes);
            return count;
        }

        private static void WriteSumsFile(string snapshotDir, Dictionary<string, string> hashes)
        {
            string sumsFileName = "SHA256SUMS.txt";
            string sumsPath = Path.Combine(snapshotDir, sumsFileName);

            var fileNames = new List<string>(hashes.Keys);
            fileNames.Sort(StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            foreach (string fileName in fileNames)
            {
                sb.Append(fileName).Append(": ").Append(hashes[fileName]).Append(Environment.NewLine);
            }

            File.WriteAllText(sumsPath, sb.ToString());
        }

        private static void AppendHashHistory(Dictionary<string, string> hashes)
        {
            try
            {
                string dir = Path.GetDirectoryName(LastKnownGoodFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var fileNames = new List<string>(hashes.Keys);
                fileNames.Sort(StringComparer.OrdinalIgnoreCase);

                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(Environment.NewLine);
                foreach (string n in fileNames)
                    sb.Append("  ").Append(n).Append(": ").Append(hashes[n]).Append(Environment.NewLine);

                File.AppendAllText(LastKnownGoodFile, sb.ToString());
            }
            catch { }
        }

        private static void Log(string message)
        {
            string day = DateTime.Now.ToString("dd-MM-yyyy");
            string time = DateTime.Now.ToString("hh:mm:ss tt");

            lock (LogLock)
            {
                var sb = new StringBuilder();
                if (day != CurrentLogDay)
                {
                    if (CurrentLogDay.Length > 0)
                        sb.Append(Environment.NewLine);
                    sb.Append("[").Append(day).Append("]").Append(Environment.NewLine);
                    CurrentLogDay = day;
                }
                sb.Append(time).Append("  ").Append(message).Append(Environment.NewLine);
                File.AppendAllText(LogFile, sb.ToString());
            }
        }
    }
}
