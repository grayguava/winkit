using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
#if WINDOWS
using System.Windows.Forms;
#endif

partial class Program {
    static string HR = new string('\u2500', 50);

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

        Console.Write("  Comparing directories...");
        DiffResult r = RunDiff(srcMap, dstMap);
        Console.WriteLine();
        Console.WriteLine();

        int destCount = dstMap.Count;
        int srcCount  = srcMap.Count;

        Metric("Files present:",     destCount + " / " + srcCount,         Pct(destCount, srcCount));
        Metric("Filenames matched:", r.DestNamesMatched.ToString("00") + " / " + srcCount, Pct(r.DestNamesMatched, srcCount));
        Metric("Sizes matched:",     r.DestSizesMatched + " / " + srcCount, Pct(r.DestSizesMatched, srcCount));
        Metric("Hashes matched:",    r.DestHashesMatched + " / " + r.DestHashed, Pct(r.DestHashesMatched, r.DestHashed));
        Metric("Missing files:",     r.Missing.ToString(), null);
        Metric("Extra files:",       r.Extra.ToString(),        null);
        Console.WriteLine();
        Console.WriteLine("  " + HR);
        Console.WriteLine();

        if (UnreadableFiles > 0) {
            Console.WriteLine("  Note: " + UnreadableFiles + " unreadable entr" + (UnreadableFiles == 1 ? "y was" : "ies were") + " skipped.");
            Console.WriteLine();
        }

        int nIssues = r.Missing + r.Extra;
        if (nIssues == 0 && UnreadableFiles == 0) {
            Console.WriteLine("  All " + srcCount + " files verified OK.");
        } else {
            Console.WriteLine("  Issue(s) found:");
            Console.WriteLine();
            if (r.Missing > 0) Console.WriteLine("    - " + r.Missing + " items missing");
            if (r.Extra > 0)   Console.WriteLine("    + " + r.Extra + " items extra");
            if (UnreadableFiles > 0) Console.WriteLine("    ? " + UnreadableFiles + " items unreadable");
        }

        return nIssues == 0 ? 0 : 1;
    }

    static string Pct(int n, int total) {
        if (total == 0) return "N/A";
        return (n * 100.0 / total).ToString("00.00") + "%";
    }

    static void Metric(string label, string value, string pct) {
        string v = value.PadLeft(12);
        string tail = (pct == null) ? v : v + (" [" + pct + "]").PadLeft(12);
        Console.WriteLine("  " + label.PadRight(20) + tail);
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
}
