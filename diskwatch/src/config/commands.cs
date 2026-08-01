using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

static class CommandConfig
{
    public struct Command
    {
        public string Name;
        public string Exe;
        public string Args;
    }

    public static List<Command> Load(string path)
    {
        var commands = new List<Command>();
        if (!File.Exists(path)) return commands;
        string section = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.Substring(1, line.Length - 2);
                continue;
            }
            if (section == null) continue;
            int sep = line.IndexOf(' ');
            string exe = sep > 0 ? line.Substring(0, sep) : line;
            string args = sep > 0 ? line.Substring(sep + 1) : "";
            if (!IsAllowedExe(exe)) continue;
            if (!IsSafeArgs(args)) continue;
            string suffix = MakeSuffix(args);
            commands.Add(new Command { Name = section + "_" + suffix, Exe = exe, Args = args });
        }
        return commands;
    }

    static bool IsAllowedExe(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return false;
        string name = exe.Trim().ToLowerInvariant();
        return name == "fsutil" || name == "chkdsk" || name == "smartctl";
    }

    static bool IsSafeArgs(string args)
    {
        if (args == null) return false;
        if (args.IndexOfAny(new char[] { '\r', '\n', ';', '|', '&', '>', '<', '$' }) >= 0) return false;
        return Regex.IsMatch(args, @"^[A-Za-z0-9\s\.\-_:\/\\,""'=\+\(\)\[\]\*%]*$");
    }

    static string MakeSuffix(string args)
    {
        var m = Regex.Match(args, @"\b([A-Za-z]):");
        if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
        string[] parts = args.Split(' ');
        string last = parts[parts.Length - 1];
        int slash = last.LastIndexOfAny(new char[] { '/', '\\' });
        return slash >= 0 ? last.Substring(slash + 1) : last;
    }
}
