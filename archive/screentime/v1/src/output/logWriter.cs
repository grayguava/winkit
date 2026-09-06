using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using screentime.models;

namespace screentime.output
{
    // File output layer (winexe has no console, so everything lands here).
    // Single log file, no retention: each run upserts its days by [date] header,
    // so re-running never duplicates blocks - today's numbers just refresh in place.
    // Non-report lines (e.g. timestamped ERRORs) are preserved verbatim.
    static class LogWriter
    {
        static readonly Regex DayHeader = new Regex(@"^\[\d{4}-\d{2}-\d{2}\]$");

        public static void AppendReport(string path, List<DayReport> reports)
        {
            Dictionary<string, DayReport> fresh = new Dictionary<string, DayReport>();
            foreach (DayReport r in reports)
                fresh[r.Day.ToString("yyyy-MM-dd")] = r;
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            StringBuilder output = new StringBuilder();
            HashSet<string> replaced = new HashSet<string>();
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int i = 0;
                while (i < lines.Length)
                {
                    string header = lines[i].Trim();
                    Match m = DayHeader.Match(header);
                    if (m.Success)
                    {
                        string date = header.Substring(1, 10);
                        DayReport report;
                        List<string> consumed = new List<string>();
                        consumed.Add(lines[i]);
                        i++; // consume the header itself
                        // Consume the old report body: blank lines and known
                        // "Boot count:" / "Screentime estimate:" lines only.
                        // Anything else (ERRORs, notes, next header) is left
                        // for normal processing so it can never be swallowed.
                        while (i < lines.Length
                            && (lines[i].Trim().Length == 0
                                || lines[i].TrimStart().StartsWith("Boot count:")
                                || lines[i].TrimStart().StartsWith("Screentime estimate:")))
                        {
                            consumed.Add(lines[i]);
                            i++;
                        }
                        if (fresh.TryGetValue(date, out report) && !replaced.Contains(date))
                        {
                            // History protection for past days: their true numbers are
                            // fixed, and a fresh query can only confirm them or come back
                            // short (evicted events, sparse lock data) - never legitimately
                            // larger. So merge per-field maximum: history can only heal
                            // upward, never be zeroed. Boot count and active time are
                            // independent evidence channels, guarded separately - non-zero
                            // boots must not mask zeroed active time or vice versa.
                            // Today is exempt: its numbers legitimately accumulate.
                            if (date == today)
                            {
                                output.Append(FormatBlock(report));
                                output.AppendLine();
                            }
                            else
                            {
                                DayReport old = ParseBlock(consumed);
                                if (old == null)
                                {
                                    foreach (string kept in consumed) output.AppendLine(kept);
                                }
                                else
                                {
                                    DayReport merged = new DayReport {
                                        Day = report.Day,
                                        Boots = old.Boots > report.Boots ? old.Boots : report.Boots,
                                        Active = old.Active > report.Active ? old.Active : report.Active,
                                    };
                                    output.Append(FormatBlock(merged));
                                    output.AppendLine();
                                }
                            }
                            replaced.Add(date);
                        }
                        else if (!replaced.Contains(date))
                        {
                            // Date outside this run's window: keep history as-is.
                            foreach (string kept in consumed) output.AppendLine(kept);
                        }
                        // A duplicate header for an already-replaced date (leftover
                        // from the old append era) is dropped along with its body,
                        // which dedupes the file going forward.
                    }
                    else
                    {
                        output.AppendLine(lines[i]);
                        i++;
                    }
                }
            }

            foreach (DayReport r in reports)
            {
                string date = r.Day.ToString("yyyy-MM-dd");
                if (!replaced.Contains(date))
                {
                    if (output.Length > 0 && !output.ToString().EndsWith("\r\n\r\n"))
                    {
                        if (!output.ToString().EndsWith("\r\n")) output.AppendLine();
                        output.AppendLine();
                    }
                    output.Append(FormatBlock(r));
                    output.AppendLine();
                }
            }

            EnsureDir(path);
            File.WriteAllText(path, output.ToString(), new UTF8Encoding(false));
        }

        static string FormatBlock(DayReport r)
        {
            int minutes = (int)r.Active.TotalMinutes;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[" + r.Day.ToString("yyyy-MM-dd") + "]");
            sb.AppendLine("Boot count: " + r.Boots);
            sb.AppendLine("Screentime estimate: " + (minutes / 60) + "h " + (minutes % 60).ToString("00") + "m");
            return sb.ToString();
        }

        // Parses a recorded block into numbers. Returns null when the block has
        // no recognizable Boot/Screentime lines - unparseable content is kept
        // verbatim, never destroyed. A missing field counts as zero.
        static DayReport ParseBlock(List<string> lines)
        {
            int boots = 0;
            int minutes = 0;
            bool found = false;
            foreach (string raw in lines)
            {
                string t = raw.TrimStart();
                if (t.StartsWith("Boot count:"))
                {
                    found = true;
                    int n;
                    if (int.TryParse(t.Substring("Boot count:".Length).Trim(), out n)) boots = n;
                }
                else if (t.StartsWith("Screentime estimate:"))
                {
                    found = true;
                    minutes = ParseEstimate(t.Substring("Screentime estimate:".Length).Trim());
                }
            }
            if (!found) return null;
            return new DayReport {
                Day = DateTime.Today,
                Boots = boots < 0 ? 0 : boots,
                Active = TimeSpan.FromMinutes(minutes < 0 ? 0 : minutes),
            };
        }

        // Parses "10h 43m" (minutes may be zero-padded) into total minutes.
        static int ParseEstimate(string s)
        {
            try
            {
                int hEnd = s.IndexOf('h');
                int mEnd = s.IndexOf('m');
                if (hEnd < 0 || mEnd < 0 || mEnd < hEnd) return 0;
                int h, m;
                if (!int.TryParse(s.Substring(0, hEnd).Trim(), out h)) return 0;
                if (!int.TryParse(s.Substring(hEnd + 1, mEnd - hEnd - 1).Trim(), out m)) return 0;
                return h * 60 + m;
            }
            catch { return 0; }
        }

        public static void AppendError(string path, string message)
        {
            try
            {
                EnsureDir(path);
                File.AppendAllText(path,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] ERROR: " + message + "\r\n\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        static void EnsureDir(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
