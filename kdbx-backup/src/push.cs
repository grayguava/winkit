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
        private static string RclonePath = "rclone";
        private static List<string> Remotes = new List<string>();
        private static string RemotePath = "kdbx-backup";
        private static string LogFile    = "";

        private static readonly object LogLock = new object();
        private static string CurrentLogDay = "";

        private static void Main()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            LoadConfig(Path.Combine(baseDir, ".push.conf"), baseDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile) ?? baseDir);

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

            RemotePath = values.ContainsKey("RemotePath") ? values["RemotePath"] : "kdbx-backup";

            if (values.ContainsKey("Remotes"))
            {
                foreach (string r in values["Remotes"].Split(','))
                {
                    string trimmed = r.Trim();
                    if (trimmed.Length > 0) Remotes.Add(trimmed);
                }
            }

            string logRaw = values.ContainsKey("logFile") ? values["logFile"] : "..\\logs\\push.log";
            LogFile = Path.IsPathRooted(logRaw)
                ? logRaw
                : Path.GetFullPath(Path.Combine(baseDir, logRaw));
        }

        private static void SyncRemote(string remote)
        {
            string destination = remote + ":" + RemotePath;
            Log("Pushing to " + remote);

            var psi = new ProcessStartInfo
            {
                FileName               = RclonePath,
                Arguments              = "copy \"" + SourceDir + "\" \"" + destination + "\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            try
            {
                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        Log("Push completed to " + remote);
                    else
                        Log("Push failed to " + remote + " (exit " + process.ExitCode + ")");
                }
            }
            catch (Exception ex)
            {
                Log("Push failed to " + remote + " (" + ex.Message + ")");
            }
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
                    sb.Append("[").Append(day).Append("]").Append(Environment.NewLine);
                    CurrentLogDay = day;
                }
                sb.Append(time).Append(": ").Append(message).Append(Environment.NewLine);
                File.AppendAllText(LogFile, sb.ToString());
            }
        }
    }
}
