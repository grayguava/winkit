using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using diskwatch.config;

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
        _mutex = new Mutex(true, @"Global\diskwatch", out createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("diskwatch is already running.");
            return 3;
        }
        try
        {
            return MainBody();
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    static int MainBody()
    {
        string baseDir = BaseDir();
        string logsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
        string runsRoot = Path.Combine(logsDir, "runs");
        string stamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");
        string stampDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string runDir = Path.Combine(runsRoot, stamp);
        string commandsDir = Path.Combine(runDir, "commands");

        var commands = CommandConfig.Load(Path.Combine(baseDir, ".cmds"));
        if (commands.Count == 0)
        {
            Console.Error.WriteLine("No valid commands configured in .cmds.");
            return 2;
        }

        var smartAttrs = SmartAttrConfig.Load(Path.Combine(baseDir, ".smart"));
        SettingsConfig.Load(Path.Combine(baseDir, ".conf"));

        foreach (var cmd in commands)
        {
            string output;
            int code = CommandRunner.Run(cmd.Exe, cmd.Args,
                SettingsConfig.CommandTimeoutMinutes * 60 * 1000, out output);
            SaveRaw(commandsDir, cmd.Name, code, output);
        }

        if (Directory.Exists(commandsDir) && Directory.GetFiles(commandsDir, "*.json").Length == 0)
            Console.Error.WriteLine("Warning: no command produced output this run.");

        bool prevUnreadable;
        MasterState prev = LoadPrevState(runsRoot, runDir, out prevUnreadable);
        var curr = MasterStateManager.Build(commandsDir, smartAttrs);
        MasterStateManager.Save(Path.Combine(runDir, "result.json"), curr);
        PruneLogs(runsRoot);

        var changes = MasterStateManager.Diff(prev, curr);
        if (prevUnreadable)
            changes.Insert(0, "baseline: previous report(s) could not be read");

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

        bool hasImportantChange = changes.Exists(c => !c.Contains(" extra "));
        if (SettingsConfig.SaveChanges && hasImportantChange)
            AppendChangelog(logsDir, stampDisplay, changes);
        if (hasImportantChange)
        {
            Console.WriteLine();
            Console.WriteLine("  Changes detected:");
            foreach (string c in changes)
                Console.WriteLine("    " + c);
            Console.WriteLine();
            Window.Show(true);
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("  No changes since last run.");
        if (!SettingsConfig.WarnOnly)
            Window.Show(false);
        return 0;
    }

    static void SaveRaw(string dir, string name, int exitCode, string output)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".json");
        File.WriteAllText(path,
            "{\"ExitCode\":" + exitCode + ",\"Output\":" + MasterStateManager.EncodeJson(output ?? "") + "}");
    }

    static MasterState LoadPrevState(string logsDir, string currentDir,
        out bool unreadable)
    {
        unreadable = false;
        bool sawPrev = false;
        var dirs = new List<string>(Directory.GetDirectories(logsDir));
        dirs.Sort();
        for (int i = dirs.Count - 1; i >= 0; i--)
        {
            if (dirs[i] == currentDir) continue;
            sawPrev = true;
            MasterState s = MasterStateManager.Load(Path.Combine(dirs[i], "result.json"));
            if (s != null) return s;
        }
        unreadable = sawPrev;
        return null;
    }

    static void AppendChangelog(string logsDir, string stamp, List<string> changes)
    {
        try
        {
            Directory.CreateDirectory(logsDir);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[" + stamp + "]");
            foreach (string c in changes)
                sb.AppendLine(c);
            sb.AppendLine();
            File.AppendAllText(Path.Combine(logsDir, "changelog.txt"),
                sb.ToString(), new System.Text.UTF8Encoding(false));
        }
        catch { }
    }

    static void PruneLogs(string logsDir)
    {
        var valid = new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}$");
        var dirs = new List<string>();
        foreach (string d in Directory.GetDirectories(logsDir))
            if (valid.IsMatch(Path.GetFileName(d)))
                dirs.Add(d);
        dirs.Sort();
        while (dirs.Count > SettingsConfig.LogRetention)
        {
            string oldest = dirs[0];
            dirs.RemoveAt(0);
            try
            {
                var info = new DirectoryInfo(oldest);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                    Directory.Delete(oldest, true);
            }
            catch { }
        }
    }
}
