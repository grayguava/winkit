using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--remind" || args[0] == "/remind"))
            return Remind.Show();

        var config = Config.Load(Path.Combine(Config.BaseDir(), "config.ini"));
        string logsDir = Path.GetFullPath(Path.Combine(Config.BaseDir(), "..", "logs"));
        string runDir = Path.Combine(logsDir, DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss"));

        // Run all checks
        foreach (string drive in config.Drives)
        {
            string d = drive.TrimEnd(':');
            string o;
            CommandRunner.Run("fsutil", "dirty query " + d + ":", out o);
            SaveRaw(runDir, "fsutil_" + d, 0, o);

            int code;
            code = CommandRunner.Run("chkdsk", d + ": /scan", out o);
            SaveRaw(runDir, "chkdsk_" + d, code, o);
        }

        foreach (string device in config.SmartDevices)
        {
            string label = device.StartsWith("/dev/") ? device.Substring(5) : device;
            string o;
            int code = CommandRunner.Run(config.SmartCtlPath, "-x \"" + device + "\"", out o);
            SaveRaw(runDir, "smartctl_" + label, code, o);
        }

        {
            string o = ReadWininitLog();
            SaveRaw(runDir, "wininit", 0, o ?? "");
        }

        // Build and compare state
        var prev = MasterStateManager.Load();
        var curr = MasterStateManager.Build(config, runDir);
        MasterStateManager.Save(curr);

        // Keep only latest 5 run directories
        var dirs = new List<string>(Directory.GetDirectories(logsDir));
        dirs.Sort();
        while (dirs.Count > 5)
        {
            Directory.Delete(dirs[0], true);
            dirs.RemoveAt(0);
        }

        var changes = MasterStateManager.Diff(prev, curr);

        // Print verdict
        foreach (var kv in curr.Drives)
        {
            string icon = kv.Value.Filesystem == "clean" ? "\u2713" : "!";
            Console.WriteLine("  " + icon + " " + kv.Key + ": " + kv.Value.Filesystem
                + (kv.Value.BadSectorsKb > 0 ? "  bad sectors " + kv.Value.BadSectorsKb + " KB" : ""));
        }

        foreach (var kv in curr.Smart)
        {
            Console.WriteLine("  \u2713 " + kv.Key + "  " + kv.Value.Health
                + (kv.Value.Endurance >= 0 ? "  endurance " + kv.Value.Endurance + "%" : ""));
        }

        Console.WriteLine("  \u2713 No repairs");

        if (changes.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Changes detected:");
            foreach (string c in changes)
                Console.WriteLine("    " + c);
            Console.WriteLine();
            Remind.Show();
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("  No changes since last run.");
        Remind.Show();
        return 0;
    }

    static void SaveRaw(string dir, string name, int exitCode, string output)
    {
        var r = new ResultFile
        {
            LastRun = DateTime.Now.ToString("o"),
            ExitCode = exitCode,
            Output = output ?? ""
        };
        Results.Save(dir, name, r);
    }

    static string ReadWininitLog()
    {
        string[] logNames = {
            "Microsoft-Windows-Wininit/Operational",
            "System",
            "Application"
        };

        foreach (string logName in logNames)
        {
            try
            {
                using (var log = new System.Diagnostics.EventLog(logName))
                {
                    var sb = new System.Text.StringBuilder();
                    int count = Math.Min(log.Entries.Count, 50);

                    for (int i = log.Entries.Count - 1; i >= log.Entries.Count - count && i >= 0; i--)
                    {
                        var entry = log.Entries[i];
                        bool isRepair = (entry.Source != null
                                         && entry.Source.IndexOf("wininit", StringComparison.OrdinalIgnoreCase) >= 0
                                         && (entry.InstanceId == 262 || entry.InstanceId == 264
                                             || (entry.Message != null
                                                 && entry.Message.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0)))
                                     || (entry.EntryType == System.Diagnostics.EventLogEntryType.Warning
                                         && entry.Message != null
                                         && entry.Message.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0
                                         && entry.Message.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (isRepair)
                        {
                            sb.AppendLine(entry.TimeGenerated.ToString("yyyy-MM-dd HH:mm")
                                + "  " + entry.Source + "  " + (entry.Message ?? "").Replace("\r\n", " ").Replace("\n", " "));
                        }
                    }

                    if (sb.Length > 0)
                    {
                        sb.Insert(0, "Source log: " + logName + "\n");
                        return sb.ToString().TrimEnd();
                    }
                }
            }
            catch { continue; }
        }

        return "";
    }
}
