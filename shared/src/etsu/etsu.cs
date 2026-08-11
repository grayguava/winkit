using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

class Etsu
{
    public static string ExifPath;
    public static string ExifVersion;
    public static string BaseDir;
    public static string LogDir;

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        BaseDir = Path.GetDirectoryName(typeof(Etsu).Assembly.Location);
        LogDir = Path.GetFullPath(Path.Combine(BaseDir, "..", "logs"));
        Directory.CreateDirectory(LogDir);

        if (!FindExiftool())
        {
            Console.Error.WriteLine("exiftool.exe not found on PATH or in " + BaseDir);
            return 1;
        }

        string cmd = args.Length > 0 ? args[0].ToLower() : "";
        switch (cmd)
        {
            case "read": return ReadTool.Run();
            case "clean": return CleanTool.Run();
            case "date": return DateTool.Run();
            default:
                Console.WriteLine("Usage: etsu read|clean|date");
                return 1;
        }
    }

    static bool FindExiftool()
    {
        try
        {
            string ps = RunTool("where", "exiftool.exe");
            if (!string.IsNullOrEmpty(ps))
            {
                string[] lines = ps.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    ExifPath = lines[0].Trim();
                    ExifVersion = RunTool(ExifPath, "-ver").Trim();
                    return true;
                }
            }
        }
        catch { }

        string local = Path.Combine(BaseDir, "exiftool.exe");
        if (File.Exists(local))
        {
            ExifPath = local;
            ExifVersion = RunTool(local, "-ver").Trim();
            return true;
        }

        return false;
    }

    public static string RunTool(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return o;
        }
    }

    public static void WriteHeader(string mode)
    {
        Console.WriteLine();
        Console.WriteLine(" " + '\u250C' + '\u2500' + " \U0001F43E ETSU   |   " + mode + "   |   Exiftool: v" + ExifVersion);
        Console.WriteLine(" " + new string('\u2500', 46));
        Console.WriteLine();
    }

    public static void WriteLine(string text, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine("  " + text);
        Console.ResetColor();
    }

    public static void WriteStep(int num, int total, string label, string status)
    {
        WriteLine("[" + num + "/" + total + "] - " + label + status, ConsoleColor.DarkGray);
    }

    public static void WriteSep()
    {
        Console.WriteLine();
        WriteLine(new string('\u2500', 46), ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    public static void WriteLog(string prefix, string outcome, List<string> lines)
    {
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logFile = Path.Combine(LogDir, prefix + "_" + ts + ".log");
        using (var w = new StreamWriter(logFile, false))
        {
            w.WriteLine("ExifTool " + prefix + " Log");
            w.WriteLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            w.WriteLine("Outcome   : " + outcome);
            w.WriteLine("----------------------------------------");
            w.WriteLine();
            foreach (string l in lines) w.WriteLine(l);
        }

        var all = new List<string>(Directory.GetFiles(LogDir, prefix + "_*.log"));
        all.Sort();
        while (all.Count > 10)
        {
            File.Delete(all[0]);
            all.RemoveAt(0);
        }
    }

    public static int WaitExit()
    {
        WriteLine("Press enter or spacebar to exit.", ConsoleColor.DarkGray);
        ConsoleKeyInfo k;
        do { k = Console.ReadKey(true); } while (k.Key != ConsoleKey.Enter && k.Key != ConsoleKey.Spacebar);
        return 0;
    }
}
