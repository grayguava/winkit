using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using screentime.config;
using screentime.events;
using screentime.models;
using screentime.output;

class Program
{
    static string BaseDir()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    static bool IsElevated()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        if (identity == null) return false;
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static int Fail(string logPath, string message, int code)
    {
        LogWriter.AppendError(logPath, message);
        return code;
    }

    // 0 = ok, 1 = runtime error, 2 = usage/config/elevation error.
    // winexe: no console, so results and errors go to logs/screentime.log.
    [STAThread]
    static int Main(string[] args)
    {
        string baseDir = BaseDir();
        string logPath = Path.GetFullPath(Path.Combine(baseDir, "..", "logs", "screentime.log"));
        Settings.Load(Path.Combine(baseDir, ".conf"));

        int days = Settings.Days;
        if (args.Length >= 1)
        {
            if (!int.TryParse(args[0], out days) || days < 1 || days > 365)
                return Fail(logPath, "Usage: screentime [days] (1-365).", 2);
        }

        if (Settings.ElevatedMode && !IsElevated())
            return Fail(logPath,
                "elevatedMode=true needs admin rights (Security log is not readable otherwise). "
                + "Re-run elevated, or set elevatedMode=false for System-log-only mode.", 2);

        DateTime windowEnd = DateTime.Now;
        DateTime windowStart = windowEnd.Date.AddDays(-(days - 1));

        List<Marker> markers;
        try
        {
            markers = EventReader.ReadSystemMarkers(windowStart, windowEnd);
        }
        catch (Exception ex)
        {
            return Fail(logPath, "Could not read the System event log: " + ex.Message, 1);
        }

        List<ActiveSpan> spans = Timeline.BuildSpans(markers, windowStart, windowEnd);

        if (Settings.ElevatedMode)
        {
            // Look back an extra day so a lock period already in progress
            // when the window opens is still seeded correctly.
            DateTime lockWindowStart = windowStart.AddDays(-1);
            List<Marker> locks;
            try
            {
                locks = EventReader.ReadLockMarkers(lockWindowStart, windowEnd);
            }
            catch (Exception ex)
            {
                return Fail(logPath, "Could not read the Security event log: " + ex.Message, 1);
            }
            if (locks.Count == 0)
            {
                LogWriter.AppendError(logPath, "Warning: elevatedMode=true but no lock/unlock events found "
                    + "(lock auditing may be off). Falling back to awake time.");
            }
            else
            {
                List<ActiveSpan> locked = Timeline.BuildLockedSpans(locks, lockWindowStart, windowEnd);
                spans = Timeline.Subtract(spans, locked);
            }
        }

        List<DayReport> reports = Timeline.Aggregate(spans, markers, windowStart, days);
        try
        {
            LogWriter.AppendReport(logPath, reports);
        }
        catch (Exception ex)
        {
            return Fail(logPath, "Could not write log file: " + ex.Message, 1);
        }
        return 0;
    }
}
