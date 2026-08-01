using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

class Program
{
    static Mutex _mutex;

    static string BaseDir()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    static int Main(string[] args)
    {
        bool createdNew;
        _mutex = new Mutex(true, "diskwatch", out createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("diskwatch is already running.");
            return 1;
        }
        try
        {
            return MainBody(args);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    static int MainBody(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--remind" || args[0] == "/remind"))
            return Remind.Show();

        string baseDir = BaseDir();
        string logsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
        string runDir = Path.Combine(logsDir, DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss"));
        string runsDir = Path.Combine(runDir, "runs");

        var commands = CommandConfig.Load(Path.Combine(baseDir, ".cmds"));
        var smartAttrs = SmartAttrConfig.Load(Path.Combine(baseDir, ".smart"));

        foreach (var cmd in commands)
        {
            string output;
            int code = CommandRunner.Run(cmd.Exe, cmd.Args, out output);
            SaveRaw(runsDir, cmd.Name, code, output);
        }

        string evtLog = ReadWininitLog();
        SaveRaw(runsDir, "wininit", 0, evtLog ?? "");

        var prev = LoadPrevState(logsDir, runDir);
        var curr = MasterStateManager.Build(runsDir, smartAttrs);
        MasterStateManager.Save(Path.Combine(runDir, "result.json"), curr);

        var dirs = new List<string>(Directory.GetDirectories(logsDir));
        dirs.Sort();
        while (dirs.Count > 5)
        {
            Directory.Delete(dirs[0], true);
            dirs.RemoveAt(0);
        }

        var changes = MasterStateManager.Diff(prev, curr);

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
            Remind.Show(changes.Exists(c => !c.Contains(" extra ")));
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("  No changes since last run.");
        Remind.Show(false);
        return 0;
    }

    static void SaveRaw(string dir, string name, int exitCode, string output)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".json");
        File.WriteAllText(path,
            "{\"ExitCode\":" + exitCode + ",\"Output\":" + MasterStateManager.EncodeJson(output ?? "") + "}");
    }

    static string ReadWininitLog()
    {
        string[] logNames = {
            "Microsoft-Windows-Wininit/Operational",
            "System", "Application"
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
                            sb.AppendLine(entry.TimeGenerated.ToString("yyyy-MM-dd HH:mm")
                                + "  " + entry.Source + "  "
                                + (entry.Message ?? "").Replace("\r\n", " ").Replace("\n", " "));
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

    static MasterState LoadPrevState(string logsDir, string currentDir)
    {
        var dirs = new List<string>(Directory.GetDirectories(logsDir));
        dirs.Sort();
        for (int i = dirs.Count - 1; i >= 0; i--)
        {
            if (dirs[i] != currentDir)
                return MasterStateManager.Load(Path.Combine(dirs[i], "result.json"));
        }
        return null;
    }
}
