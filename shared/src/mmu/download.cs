using System;
using System.Diagnostics;
using System.IO;

class DownloadTool
{
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

        Console.Write("Paste link: ");
        string url = Console.ReadLine();
        if (string.IsNullOrEmpty(url))
        {
            Console.Error.WriteLine("No link provided.");
            return 1;
        }

        Console.Write("Filename (Enter to use the title): ");
        string name = Console.ReadLine();

        string outputDir = Directory.GetCurrentDirectory();
        if (File.Exists(dotConfig))
        {
            string val = Mmu.ReadConfValue(dotConfig, "outdir");
            if (val != null) outputDir = val;
        }
        Directory.CreateDirectory(outputDir);

        string outputTemplate = string.IsNullOrEmpty(name)
            ? "%(title)s.%(ext)s"
            : Mmu.SanitizeName(name) + ".%(ext)s";

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
                    Console.WriteLine();
                    Console.WriteLine("✨ Done. Saved to \"" + outputDir + "\"");
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
}
