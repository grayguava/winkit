using System;
using System.Collections.Generic;
using System.IO;

partial class Program {
    static bool LogEnabled = true;
    static int LogKeep = 10;
    static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder();

    static void LoadDirDiffConfig() {
        string confPath = Path.Combine(ExeDir, "..", "conf", ".dirdiff");
        if (!File.Exists(confPath)) return;
        foreach (string rawLine in File.ReadAllLines(confPath)) {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            if (line.StartsWith("[") && line.EndsWith("]")) {
                string section = line.Substring(1, line.Length - 2).Trim();
                if (!section.Equals("General", StringComparison.OrdinalIgnoreCase))
                    return;
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim();
            if (key == "log")
                LogEnabled = val.Equals("true", StringComparison.OrdinalIgnoreCase);
            else if (key == "logkeep") {
                int n;
                if (int.TryParse(val, out n) && n >= 1) LogKeep = n;
            }
        }
    }

    static void WriteRunLogs(DiffResult r) {
        if (!LogEnabled) return;
        try {
            string dir = Path.Combine(ExeDir, "..", "logs", "dirdiff");
            string runDir = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "summary.log"), Report.ToString());
            File.WriteAllLines(Path.Combine(runDir, "missingFiles.txt"), r.MissingFiles);
            File.WriteAllLines(Path.Combine(runDir, "extraFiles.txt"), r.ExtraFiles);
            PruneLogs(dir);
        } catch {
        }
    }

    static void PruneLogs(string dir) {
        try {
            string[] dirs = Directory.GetDirectories(dir);
            if (dirs.Length <= LogKeep) return;
            Array.Sort(dirs, StringComparer.Ordinal);
            for (int i = 0; i < dirs.Length - LogKeep; i++)
                Directory.Delete(dirs[i], true);
        } catch {
        }
    }
}
