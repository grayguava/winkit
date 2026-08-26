using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace kdbxPushToRemote
{
    internal static class Program
    {
        private static string SourceDir  = "";
        private static string RclonePath = "null";
        private static List<string> Remotes = new List<string>();
        private static string RemotePath = "kdbx-backup";
        private static string LogFile    = "";

        private static readonly object LogLock = new object();
        private static string CurrentLogDay = "";

        private static void Main()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                LoadConfig(Path.Combine(baseDir, ".push.conf"), baseDir);
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile) ?? baseDir);
                InitLogDay();

                if (RclonePath.IndexOf('"') >= 0)
                    throw new InvalidOperationException("RclonePath contains illegal quote character.");

                if (RclonePath.IndexOf(Path.DirectorySeparatorChar) >= 0
                    || RclonePath.IndexOf(Path.AltDirectorySeparatorChar) >= 0
                    || Path.IsPathRooted(RclonePath))
                {
                    if (!File.Exists(RclonePath))
                    {
                        Log("Rclone not found at configured path: " + RclonePath);
                        Environment.ExitCode = 1;
                        return;
                    }
                }

                if (Remotes.Count == 0)
                {
                    Log("No remotes configured. Check Remotes= in .push.conf.");
                    return;
                }

                if (!Directory.Exists(SourceDir))
                {
                    Log("Source directory not found: " + SourceDir);
                    return;
                }

                foreach (string remote in Remotes)
                {
                    string trimmed = remote.Trim();
                    if (trimmed.Length == 0) continue;
                    SyncRemote(trimmed);
                }
            }
            catch (Exception ex)
            {
                TryLog("FATAL: " + ex.GetType().Name + ": " + ex.Message);
                Environment.ExitCode = 1;
            }
        }

        private static void TryLog(string msg)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
                Directory.CreateDirectory(logDir);
                string fallbackLog = Path.Combine(logDir, "push.log");
                string time = DateTime.Now.ToString("hh:mm:ss tt");
                File.AppendAllText(fallbackLog, time + ": " + msg + Environment.NewLine);
            }
            catch { }
        }

        private static string Q(string s)
        {
            s = s.TrimEnd(Path.DirectorySeparatorChar);
            if (s.IndexOf('"') >= 0)
                throw new InvalidOperationException("Config value contains illegal quote character: " + s);
            return "\"" + s + "\"";
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

            string sourceRaw = values.ContainsKey("sourceDir") ? values["sourceDir"] : "..\\databaseCopies";
            SourceDir = Path.IsPathRooted(sourceRaw)
                ? sourceRaw
                : Path.GetFullPath(Path.Combine(baseDir, sourceRaw));

            if (values.ContainsKey("RclonePath") && values["RclonePath"].Length > 0)
            {
                string raw = values["RclonePath"];
                if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
                    RclonePath = "rclone";
                else
                    RclonePath = raw;
            }

            RemotePath = values.ContainsKey("RemotePath") ? values["RemotePath"] : "kdbx-backup";

            if (values.ContainsKey("Remotes"))
            {
                foreach (string r in values["Remotes"].Split(','))
                {
                    string trimmed = r.Trim();
                    if (trimmed.Length > 0) Remotes.Add(trimmed);
                }
            }

            string logDir = Path.GetFullPath(Path.Combine(baseDir, "..", "logs"));
            LogFile = Path.Combine(logDir, "push.log");
        }

        private static void SyncRemote(string remote)
        {
            string destination = remote + ":" + RemotePath;
            Log("Pushing to " + remote);

            var output = new StringBuilder();
            var sync = new object();

            var psi = new ProcessStartInfo
            {
                FileName               = RclonePath,
                Arguments              = "copy " + Q(SourceDir) + " " + Q(destination),
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            try
            {
                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null) lock (sync) { output.AppendLine(e.Data); }
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null) lock (sync) { output.AppendLine(e.Data); }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        Log("Push completed to " + remote);
                    else
                        Log("Push failed to " + remote + " (exit " + process.ExitCode + ")");

                    if (output.Length > 0)
                        Log(remote + " output: " + output.ToString().TrimEnd());
                }
            }
            catch (Exception ex)
            {
                Log("Push failed to " + remote + " (" + ex.Message + ")");
            }
        }

        private static void InitLogDay()
        {
            try
            {
                if (!File.Exists(LogFile)) return;
                string today = "[" + DateTime.Now.ToString("dd-MM-yyyy") + "]";
                string[] lines = File.ReadAllLines(LogFile);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        if (line == today) CurrentLogDay = DateTime.Now.ToString("dd-MM-yyyy");
                        return;
                    }
                }
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
                sb.Append(time).Append(": ").Append(message).Append(Environment.NewLine);
                File.AppendAllText(LogFile, sb.ToString());
            }
        }
    }
}
