using System;
using System.Diagnostics;
using System.Text;

public static class CommandRunner
{
    public static int Run(string fileName, string arguments, out string stdout)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var output = new StringBuilder();
        var errors = new StringBuilder();

        try
        {
            using (var process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errors.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                stdout = output.ToString();
                string stderr = errors.ToString().Trim();
                if (stderr.Length > 0)
                    stdout += stderr;

                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            stdout = ex.Message;
            return -2;
        }
    }
}
