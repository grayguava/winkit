using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class DownloadTool
{
    static string SanitizeUrl(string url)
    {
        if (url == null) return "";
        return url.Replace("\"", "").Trim();
    }

    public static int Run()
    {
        string baseDir = Mmu.ExeDir();
        string confDir = Path.GetFullPath(Path.Combine(baseDir, "..", "conf", "mmu"));

        string dotConfig = Path.Combine(confDir, ".conf");
        string ytdlpCfg = File.Exists(dotConfig) ? Mmu.ReadConfValue(dotConfig, "ytdlp") : null;
        string ffmpegCfg = File.Exists(dotConfig) ? Mmu.ReadConfValue(dotConfig, "ffmpeg") : null;

        string ytDlpExe = Mmu.ResolveYtDlp(baseDir, ytdlpCfg);
        if (ytDlpExe == null)
        {
            Console.Error.WriteLine("yt-dlp.exe not found.");
            Console.Error.WriteLine("  Set ytdlp=<path> in conf/mmu/.conf, place yt-dlp.exe alongside mmu.exe,");
            Console.Error.WriteLine("  or add it to PATH.");
            return 1;
        }

        Console.WriteLine();
        Console.Write(" Paste link: ");
        string url = Console.ReadLine();
        if (string.IsNullOrEmpty(url))
        {
            Console.Error.WriteLine("No link provided.");
            return 1;
        }
        url = SanitizeUrl(url);

        string title;
        string artist;
        FetchInfo(ytDlpExe, url, out title, out artist);
        if (string.IsNullOrEmpty(title) || title.Equals("NA", StringComparison.OrdinalIgnoreCase))
            title = "Unknown Title";
        if (string.IsNullOrEmpty(artist) || artist.Equals("NA", StringComparison.OrdinalIgnoreCase))
            artist = "Unknown Artist";

        string outputDir = Directory.GetCurrentDirectory();
        if (File.Exists(dotConfig))
        {
            string val = Mmu.ReadConfValue(dotConfig, "outdir");
            if (val != null) outputDir = val;
        }
        Directory.CreateDirectory(outputDir);

        string outputTemplate = "%(title)s.%(ext)s";

        string ytDlpConf = Path.Combine(confDir, "yt-dlp.conf");
        if (!File.Exists(ytDlpConf))
        {
            Console.Error.WriteLine("conf/mmu/yt-dlp.conf not found.");
            return 1;
        }

        string ffmpegArg = "";
        string ffmpegDir = Mmu.ResolveFfmpeg(baseDir, ffmpegCfg);
        if (ffmpegDir != null)
            ffmpegArg = "--ffmpeg-location \"" + ffmpegDir + "\" ";

        string outputPath = Path.Combine(outputDir, outputTemplate);
        string procArgs = "--config-location \"" + ytDlpConf + "\" "
            + ffmpegArg
            + "-o \"" + outputPath + "\" "
            + "\"" + url + "\"";

        Console.WriteLine();
        Console.WriteLine(" Downloading " + title + " by " + artist + "...");

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
                string stdOut = null;
                var stdoutThread = new Thread(() => { stdOut = process.StandardOutput.ReadToEnd(); });
                stdoutThread.Start();
                string stdErr = process.StandardError.ReadToEnd();
                stdoutThread.Join();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("\u2728 Done. Saved audio to - " + outputDir + "\\");
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

    static void FetchInfo(string ytDlpExe, string url, out string title, out string artist)
    {
        title = null;
        artist = null;
        string safeUrl = SanitizeUrl(url);
        try
        {
            var psi = new ProcessStartInfo(ytDlpExe, "--no-playlist --print \"%(title)s\" --print \"%(artist)s\" \"" + safeUrl + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                string output = null;
                var stdoutThread = new Thread(() => { output = p.StandardOutput.ReadToEnd(); });
                stdoutThread.Start();
                p.StandardError.ReadToEnd();
                stdoutThread.Join();
                p.WaitForExit();
                if (p.ExitCode == 0)
                {
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0) title = lines[0].Trim();
                    if (lines.Length > 1) artist = lines[1].Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Warning: could not fetch video info: " + ex.Message);
        }
    }
}
