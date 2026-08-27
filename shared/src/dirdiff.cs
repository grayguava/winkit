using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
#if WINDOWS
using System.Windows.Forms;
#endif

class Program {
    static string HR = new string('\u2500', 50);
    static int MaxThreads = 8;

    class FileEntry {
        public string AbsPath;
        public long Size;
    }

    [STAThread]
    static int Main(string[] args) {
        string sourceRoot, destRoot;

        LoadConfig();

        if (args.Length >= 2) {
            sourceRoot = args[0];
            destRoot   = args[1];
        } else {
#if WINDOWS
            Console.WriteLine();
            Console.Write("  Source:     ");
            sourceRoot = PickFolder("SOURCE directory");
            Console.Write("\r  Source:      " + sourceRoot + "\n");

            Console.Write("  Dest:       ");
            destRoot = PickFolder("DESTINATION directory (the copy)");
            Console.Write("\r  Dest:        " + destRoot + "\n");
#else
            Console.Error.WriteLine("Usage: dirdiff <source> <destination>");
            return 1;
#endif
        }

        Console.WriteLine();
        Console.WriteLine("  " + HR);
        Console.WriteLine();

        var srcMap = BuildFileMap(sourceRoot);
        var dstMap = BuildFileMap(destRoot);

        var srcPaths = new HashSet<string>(srcMap.Keys);
        var dstPaths = new HashSet<string>(dstMap.Keys);

        var inBoth  = srcPaths.Intersect(dstPaths).OrderBy(x => x).ToList();
        var missing = srcPaths.Except(dstPaths).OrderBy(x => x).ToList();
        var extra   = dstPaths.Except(srcPaths).OrderBy(x => x).ToList();

        int nTotal   = srcMap.Count;
        int nPresent = inBoth.Count;

        int sizeOk  = 0;
        foreach (var relPath in inBoth) {
            long srcSize = srcMap[relPath].Size;
            long dstSize = dstMap[relPath].Size;
            if (srcSize == dstSize)
                sizeOk++;
        }

        int hashOk  = 0;
        int hashBad = 0;
        int done    = 0;
        int nHash   = inBoth.Count;
        object lockObj = new object();

        Parallel.ForEach(inBoth, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, (relPath) => {
            string sh = HashFile(srcMap[relPath].AbsPath);
            string dh = HashFile(dstMap[relPath].AbsPath);
            lock (lockObj) {
                done++;
                if (sh != null && dh != null && sh == dh)
                    hashOk++;
                else
                    hashBad++;
                Console.Write("  Comparing directories... " + done + "/" + nHash + "\r");
            }
        });
        Console.Write("  Comparing directories... " + nHash + "/" + nHash);
        Console.WriteLine();
        Console.WriteLine();

        if (nPresent == 0) {
            Metric("Files present:", "0 / " + nTotal, "0.00%");
            Metric("Sizes matched:", "0 / 0", "N/A");
            Metric("Hashes matched:", "0 / 0", "N/A");
        } else {
            Metric("Files present:", nPresent + " / " + nTotal, Pct(nPresent, nTotal));
            Metric("Sizes matched:", sizeOk + " / " + nPresent, Pct(sizeOk, nPresent));
            Metric("Hashes matched:", hashOk + " / " + nHash, Pct(hashOk, nHash));
        }
        Metric("Missing files:", missing.Count.ToString(), null);
        Metric("Extra files:", extra.Count.ToString(), null);
        Console.WriteLine();
        Console.WriteLine("  " + HR);
        Console.WriteLine();

        if (UnreadableFiles > 0) {
            Console.WriteLine("  Note: " + UnreadableFiles + " unreadable entr" + (UnreadableFiles == 1 ? "y was" : "ies were") + " skipped.");
            Console.WriteLine();
        }

        int nIssues = missing.Count + extra.Count + (nPresent - sizeOk) + hashBad;
        if (nIssues == 0 && UnreadableFiles == 0) {
            Console.WriteLine("  All " + nTotal + " files verified OK.");
        } else {
            Console.WriteLine("  Issue(s) found:");
            Console.WriteLine();
            if (missing.Count > 0) Console.WriteLine("    - " + missing.Count + " items missing");
            if (extra.Count > 0)   Console.WriteLine("    + " + extra.Count + " items extra");
            if (nPresent - sizeOk > 0) Console.WriteLine("    ! " + (nPresent - sizeOk) + " items size mismatch");
            if (hashBad > 0)       Console.WriteLine("    ! " + hashBad + " items hash mismatch");
            if (UnreadableFiles > 0) Console.WriteLine("    ? " + UnreadableFiles + " items unreadable");
        }

        return nIssues == 0 ? 0 : 1;
    }

    static string Pct(int n, int total) {
        if (total == 0) return "N/A";
        return (n * 100.0 / total).ToString("00.00") + "%";
    }

    static void Metric(string label, string value, string pct) {
        string right = value.PadLeft(16);
        string br = pct == null ? "" : ("  [" + pct + "]").PadLeft(10);
        Console.WriteLine("  " + label.PadRight(17) + right + br);
    }

    static string PickFolder(string title) {
        using (var dlg = new FolderBrowserDialog()) {
            dlg.Description = title;
            if (dlg.ShowDialog() == DialogResult.OK)
                return dlg.SelectedPath;
        }
        Console.WriteLine("\n  No folder selected (cancelled). Exiting.");
        Environment.Exit(1);
        return null;
    }

    static int UnreadableFiles;

    static Dictionary<string, FileEntry> BuildFileMap(string root) {
        var map = new Dictionary<string, FileEntry>();
        Walk(root, root, map);
        return map;
    }

    static void Walk(string root, string dir, Dictionary<string, FileEntry> map) {
        string[] files;
        try {
            files = Directory.GetFiles(dir);
        } catch {
            UnreadableFiles++;
            return;
        }

        foreach (string file in files) {
            try {
                var fi = new FileInfo(file);
                map[file.Substring(root.Length).TrimStart('\\', '/')] = new FileEntry {
                    AbsPath = file,
                    Size    = fi.Length,
                };
            } catch {
                UnreadableFiles++;
            }
        }

        string[] subdirs;
        try {
            subdirs = Directory.GetDirectories(dir);
        } catch {
            return;
        }

        foreach (string sub in subdirs) {
            try {
                if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
                    continue;
                Walk(root, sub, map);
            } catch {
            }
        }
    }

    static void LoadConfig() {
        string confPath = Path.Combine(
            Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "conf", ".thr");
        if (!File.Exists(confPath)) return;
        int t;
        if (int.TryParse(File.ReadAllText(confPath).Trim(), out t) && t > 0)
            MaxThreads = Math.Min(t, 32);
    }

    const int ChunkSize = 1024 * 1024;

    static string HashFile(string path) {
        try {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize))
            using (var sha = SHA256.Create()) {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLower();
            }
        } catch {
            return null;
        }
    }
}
