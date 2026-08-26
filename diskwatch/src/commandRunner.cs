using System;
using System.Diagnostics;
using System.Text;

static class CommandRunner
{
    public static int Run(string fileName, string arguments,
        int timeoutMs, out string stdout)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var output = new StringBuilder();
        var sync = new object();
        try
        {
            var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (sync) { output.AppendLine(e.Data); }
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (sync) { output.AppendLine(e.Data); }
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); }
                catch { }
                stdout = output + Environment.NewLine +
                    "[timed out after " + timeoutMs / 1000 + " s]";
                return -3;
            }
            process.WaitForExit();
            stdout = output.ToString();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            stdout = ex.Message;
            return -2;
        }
    }
}
