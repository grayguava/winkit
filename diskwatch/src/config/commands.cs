using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace diskwatch.config
{
    public struct Command
    {
        public string Name;
        public string Exe;
        public string Args;
    }

    static class CommandConfig
    {
        static readonly string[] _allowed = { "chkdsk", "fsutil", "smartctl" };

        static bool IsAllowedExe(string bare)
        {
            foreach (string a in _allowed)
                if (bare == a) return true;
            return false;
        }

        static bool IsSafeArgs(string args)
        {
            if (args.Length == 0) return true;
            return Regex.IsMatch(args, @"^[A-Za-z0-9\s\.\-:,\/]+$");
        }

        static string MakeSuffix(string args)
        {
            string raw = args.Trim().Replace(" ", "_").Replace("\\", "-").Replace("/", "-");
            if (raw.Length > 32)
                raw = raw.Substring(0, 32);
            if (string.IsNullOrEmpty(raw))
                raw = "default";
            if (!Regex.IsMatch(raw, @"^[A-Za-z0-9]"))
                raw = "r" + raw;
            return raw;
        }

        static bool IsSafeName(string s)
        {
            return Regex.IsMatch(s, @"^[A-Za-z0-9][A-Za-z0-9._\-]*$");
        }

        static bool HasMutatingFlag(string exe, string args)
        {
            string low = " " + args.ToLowerInvariant() + " ";
            if (exe == "chkdsk")
            {
                foreach (string flag in new[] { "/f", "/r", "/x", "/spotfix", "/offlinescanandfix", "/b" })
                    if (low.Contains(" " + flag + " ")) return true;
            }
            else if (exe == "fsutil")
            {
                if (low.Contains(" dirty set ") || low.Contains(" usn deletejournal")) return true;
            }
            else if (exe == "smartctl")
            {
                if (low.Contains(" -t ") || low.Contains(" --test ")) return true;
                if (low.Contains(" -s ") || low.Contains(" --set ")) return true;
            }
            return false;
        }

        static string ResolveExe(string bare)
        {
            if (bare == "fsutil" || bare == "chkdsk")
            {
                string sysPath = Path.Combine(
                    System.Environment.SystemDirectory,
                    bare + ".exe");
                return File.Exists(sysPath) ? sysPath : null;
            }
            if (bare == "smartctl")
            {
                string pathVar =
                    System.Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string rawDir in pathVar.Split(';'))
                {
                    string dir = rawDir.Trim();
                    if (dir.Length == 0) continue;
                    string candidate = Path.Combine(dir, "smartctl.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        public static List<Command> Load(string path)
        {
            var commands = new List<Command>();
            string section = null;
            foreach (string line in Conf.Lines(path))
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }
                if (section == null || !IsSafeName(section)) continue;

                int sep = line.IndexOf(' ');
                string exe = sep > 0 ? line.Substring(0, sep) : line;
                string args = sep > 0 ? line.Substring(sep + 1) : "";
                if (!IsAllowedExe(exe)) continue;
                if (!IsSafeArgs(args)) continue;
                if (HasMutatingFlag(exe, args)) continue;

                string suffix = MakeSuffix(args);
                if (!IsSafeName(suffix)) continue;

                string resolved = ResolveExe(exe.ToLowerInvariant());
                if (resolved == null) continue;

                commands.Add(new Command
                {
                    Name = section + "_" + suffix,
                    Exe = resolved,
                    Args = args
                });
            }
            return commands;
        }
    }
}
