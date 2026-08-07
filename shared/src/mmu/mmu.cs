using System;
using System.IO;
using System.Reflection;

class Mmu
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: mmu -d");
            Console.Error.WriteLine("  -d   download audio from YouTube/YT Music (interactive)");
            return 1;
        }

        string cmd = args[0];
        if (cmd == "-d")
            return DownloadTool.Run();

        Console.Error.WriteLine("Unknown command: " + cmd);
        Console.Error.WriteLine("Usage: mmu -d");
        return 1;
    }

    public static string ExeDir()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    public static string ReadConfValue(string path, string key)
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

    public static string ResolveYtDlp(string baseDir, string configValue)
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

    public static string FindLocalExe(string dir, string exe)
    {
        string path = Path.Combine(dir, exe);
        if (File.Exists(path)) return path;
        path = Path.Combine(dir, "yt-dlp", exe);
        if (File.Exists(path)) return path;
        return null;
    }

    public static string FindOnPath(string exe)
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

    public static string ResolveFfmpeg(string baseDir, string configValue)
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
}
