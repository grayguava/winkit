using System;
using System.IO;
using System.Reflection;
using System.Threading;

static class FailLog
{
    static readonly object Lock = new object();

    public static void Append(string message)
    {
        try
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Directory.CreateDirectory(Path.Combine(exeDir, "logs"));
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;
            lock (Lock)
            {
                File.AppendAllText(Path.Combine(exeDir, "logs", "fail.log"), line);
            }
        }
        catch { }
    }
}
