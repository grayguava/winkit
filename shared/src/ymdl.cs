using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

class Program
{
    static string ExeDir()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ymdl <url> \"filename\"");
            Console.Error.WriteLine("  Downloads highest quality audio from YouTube/YT Music.");
            Console.Error.WriteLine("  Filename is without extension (ymdl adds it automatically).");
            return 1;
        }

        string url = args[0];
        string filename = SanitizeName(args[1]);

        string baseDir = ExeDir();
        string confDir = Path.GetFullPath(Path.Combine(baseDir, "..", "conf"));

        string dotYmdl = Path.Combine(confDir, ".ymdl");
        string ytdlpCfg = File.Exists(dotYmdl) ? ReadConfValue(dotYmdl, "ytdlp") : null;
        string ffmpegCfg = File.Exists(dotYmdl) ? ReadConfValue(dotYmdl, "ffmpeg") : null;

        string ytDlpExe = ResolveYtDlp(baseDir, ytdlpCfg);
        if (ytDlpExe == null)
        {
            Console.Error.WriteLine("yt-dlp.exe not found.");
            Console.Error.WriteLine("  Set ytdlp=<path> in conf/.ymdl, place yt-dlp.exe alongside ymdl.exe,");
            Console.Error.WriteLine("  or add it to PATH.");
            return 1;
        }

        string ffmpegArg = "";
        string ffmpegDir = ResolveFfmpeg(baseDir, ffmpegCfg);
        if (ffmpegDir != null)
            ffmpegArg = "--ffmpeg-location \"" + ffmpegDir + "\" ";

        string outputDir = Directory.GetCurrentDirectory();
        if (File.Exists(dotYmdl))
        {
            string val = ReadConfValue(dotYmdl, "outdir");
            if (val != null) outputDir = val;
        }
        Directory.CreateDirectory(outputDir);

        string ytDlpConf = Path.Combine(confDir, "yt-dlp.conf");
        if (!File.Exists(ytDlpConf))
        {
            Console.Error.WriteLine("conf/yt-dlp.conf not found.");
            return 1;
        }

        string outputPath = Path.Combine(outputDir, filename + ".%(ext)s");
        string procArgs = "--config-location \"" + ytDlpConf + "\" "
            + ffmpegArg
            + "-o \"" + outputPath + "\" "
            + "\"" + url + "\"";

        Console.WriteLine("Downloading...");

        var psi = new ProcessStartInfo(ytDlpExe, procArgs)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    string resultFile = FindOutputFile(stdOut + stdErr);
                    if (resultFile != null)
                        Console.WriteLine("\u2713 " + resultFile);
                    else
                        Console.WriteLine("\u2713 Download complete");
                    Console.WriteLine("Saved to " + outputDir);
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Download failed:");
                    Console.Error.WriteLine(stdErr);
                    return process.ExitCode;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    static string ResolveYtDlp(string baseDir, string configValue)
    {
        if (configValue != null && !configValue.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(configValue))
                return Path.GetFullPath(configValue);
            return null;
        }

        string local = FindLocalExe(baseDir, "yt-dlp.exe");
        if (local != null) return local;

        return FindOnPath("yt-dlp.exe");
    }

    static string FindLocalExe(string dir, string exe)
    {
        string path = Path.Combine(dir, exe);
        if (File.Exists(path)) return path;
        path = Path.Combine(dir, "yt-dlp", exe);
        if (File.Exists(path)) return path;
        return null;
    }

    static string FindOnPath(string exe)
    {
        try
        {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string path = Path.Combine(dir.Trim(), exe);
                if (File.Exists(path)) return path;
            }
        }
        catch { }
        return null;
    }

    static string ResolveFfmpeg(string baseDir, string configValue)
    {
        if (configValue != null && !configValue.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(configValue))
                return Path.GetFullPath(configValue);
            if (File.Exists(configValue))
                return Path.GetDirectoryName(Path.GetFullPath(configValue));
            return null;
        }

        string local = FindLocalExe(baseDir, "ffmpeg.exe");
        if (local != null) return Path.GetDirectoryName(local);
        local = FindLocalExe(baseDir, "ffprobe.exe");
        if (local != null) return Path.GetDirectoryName(local);

        string ffBin = Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe");
        if (File.Exists(ffBin)) return Path.Combine(baseDir, "ffmpeg", "bin");
        ffBin = Path.Combine(baseDir, "ffmpeg", "bin", "ffprobe.exe");
        if (File.Exists(ffBin)) return Path.Combine(baseDir, "ffmpeg", "bin");

        string onPath = FindOnPath("ffmpeg.exe");
        if (onPath != null) return Path.GetDirectoryName(onPath);
        onPath = FindOnPath("ffprobe.exe");
        if (onPath != null) return Path.GetDirectoryName(onPath);

        return null;
    }

    static string SanitizeName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (Array.IndexOf(invalid, c) >= 0)
                sb.Append('_');
            else
                sb.Append(c);
        }
        string result = sb.ToString().Trim();
        return result.Length == 0 ? "audio" : result;
    }

    static string ReadConfValue(string path, string key)
    {
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string k = line.Substring(0, eq).Trim();
            if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            string v = line.Substring(eq + 1).Trim();
            if (v.Length > 0) return v;
        }
        return null;
    }

    static string FindOutputFile(string log)
    {
        var m = Regex.Match(log, @"\[Merger\] Merging formats into ""(.+?)""");
        if (!m.Success)
            m = Regex.Match(log, @"\[ExtractAudio\] Destination: (.+)");
        if (!m.Success)
            m = Regex.Match(log, @"\[download\] (.+?) has already been downloaded");
        if (m.Success)
        {
            string path = m.Groups[1].Value.Trim();
            if (path.StartsWith("Destination: "))
                path = path.Substring("Destination: ".Length);
            path = path.Trim('"');
            return Path.GetFileName(path);
        }
        return null;
    }
}
