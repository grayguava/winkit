using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
#if WINDOWS
using System.Windows.Forms;
#endif

partial class Program {
    static string HR = new string('\u2500', 50);
    static string ExeDir;

    [STAThread]
    static int Main(string[] args) {
        string sourceRoot, destRoot;

        LoadConfig();
        LoadDirDiffConfig();

        Console.WriteLine();
        int srcArg = 0;
        foreach (string a in args) {
            if (a == "-l" || a == "--links" || a == "-L") { FollowLinks = true; srcArg++; }
            else break;
        }
        if (args.Length - srcArg >= 2) {
            sourceRoot = args[srcArg];
            destRoot   = args[srcArg + 1];
            Console.WriteLine("  Source:      " + sourceRoot);
            Console.WriteLine("  Dest:        " + destRoot);
        } else {
#if WINDOWS
            Console.Write("  Source:      ");
            Console.Out.Flush();
            sourceRoot = PickFolder("SOURCE directory");
            Console.WriteLine("\r  Source:      " + sourceRoot);

            Console.Write("  Dest:        ");
            Console.Out.Flush();
            destRoot = PickFolder("DESTINATION directory (the copy)");
            Console.WriteLine("\r  Dest:        " + destRoot);
#else
            Console.Error.WriteLine("Usage: dirdiff <source> <destination>");
            return 1;
#endif
        }
        Console.WriteLine();
        Console.WriteLine("  " + HR);
        Console.WriteLine();
        Console.Out.Flush();

        Report.Clear();
        Report.AppendLine("  Source:      " + sourceRoot);
        Report.AppendLine("  Dest:        " + destRoot);
        Report.AppendLine();
        Report.AppendLine("  " + HR);
        Report.AppendLine();

        Console.Write("  Comparing directories...");
        Console.Out.Flush();
        var srcMap = BuildFileMap(sourceRoot);
        var dstMap = BuildFileMap(destRoot);
        Console.WriteLine();
        Console.WriteLine();
        Report.AppendLine("  Comparing directories...");
        Report.AppendLine();

        var prep = PrepareDiff(srcMap, dstMap);

        Metric("Files present:",     prep.DstMap.Count + " / " + prep.SrcMap.Count,          Pct(prep.DstMap.Count, prep.SrcMap.Count));
        Metric("Filenames matched:", prep.DestNamesMatched.ToString("00") + " / " + prep.SrcMap.Count, Pct(prep.DestNamesMatched, prep.SrcMap.Count));
        Metric("Sizes matched:",     prep.DestSizesMatched + " / " + prep.SrcMap.Count,      Pct(prep.DestSizesMatched, prep.SrcMap.Count));

        Console.Write(HashRow(0, prep.DstToHash.Count, true));
        Console.Out.Flush();
        DiffResult r = RunDiff(prep, (d, t) => {
            Console.Write("\r" + HashRow(d, t, true));
            Console.Out.Flush();
        });
        Console.Write("\r" + HashRow(r.DestHashesMatched, r.DestHashed, false));
        Console.WriteLine();
        Report.AppendLine(HashRow(r.DestHashesMatched, r.DestHashed, false));

        Metric("Missing files:",     r.Missing.ToString(), null);
        Metric("Extra files:",       r.Extra.ToString(),        null);
        Line("");

        if (UnreadableFiles > 0) {
            Line("  Note: " + UnreadableFiles + " unreadable entr" + (UnreadableFiles == 1 ? "y was" : "ies were") + " skipped.");
            Line("");
        }

        int nIssues = r.Missing + r.Extra;
        Line("  " + HR);
        Line("");
        if (nIssues == 0 && UnreadableFiles == 0) {
            Line("  All " + prep.SrcMap.Count + " files verified OK.");
        } else {
            Line("  Issue(s) found:");
            Line("");
            if (r.Missing > 0) Line("    - " + r.Missing + " items missing");
            if (r.Extra > 0)   Line("    + " + r.Extra + " items extra");
            if (UnreadableFiles > 0) Line("    ? " + UnreadableFiles + " items unreadable");
        }

        WriteRunLogs(r);
        Console.Out.Flush();

        return nIssues == 0 ? 0 : 1;
    }

    static string Pct(int n, int total) {
        if (total == 0) return "N/A";
        return (n * 100.0 / total).ToString("00.00") + "%";
    }

    static void Metric(string label, string value, string pct) {
        string v = value.PadLeft(12);
        string tail = (pct == null) ? v : v + (" [" + pct + "]").PadLeft(12);
        Line("  " + label.PadRight(20) + tail);
    }

    static string HashRow(int matched, int done, bool live) {
        string s = "  " + "Hashes matched:".PadRight(20) + (matched + " / " + done).PadLeft(12);
        if (!live) s += (" [" + Pct(matched, done) + "]").PadLeft(12);
        return s.PadRight(44);
    }

    static void Line(string s) {
        Console.WriteLine(s);
        Report.AppendLine(s);
        Console.Out.Flush();
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
        ExeDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        string confPath = Path.Combine(ExeDir, "..", "conf", ".thr");
        if (!File.Exists(confPath)) return;
        int t;
        if (int.TryParse(File.ReadAllText(confPath).Trim(), out t) && t > 0)
            MaxThreads = Math.Min(t, 32);
    }
}
