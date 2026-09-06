using System;
using System.Collections.Generic;
using screentime.models;

namespace screentime.events
{
    // Turns raw open/close markers into per-day totals.
    static class Timeline
    {
        // Pair opens with closes into spans, clamped to [windowStart, windowEnd).
        // Duplicate opens collapse (state stays open); stray closes are ignored;
        // a span left open at the end closes at windowEnd (covers crashes / "now").
        public static List<ActiveSpan> BuildSpans(List<Marker> markers, DateTime windowStart, DateTime windowEnd)
        {
            var sorted = new List<Marker>(markers);
            sorted.Sort(delegate (Marker a, Marker b) { return a.Time.CompareTo(b.Time); });
            var spans = new List<ActiveSpan>();
            DateTime? open = null;
            foreach (Marker m in sorted)
            {
                if (m.Opens)
                {
                    if (open == null) open = m.Time;
                }
                else
                {
                    if (open != null)
                    {
                        DateTime s = open.Value < windowStart ? windowStart : open.Value;
                        if (m.Time > s)
                            spans.Add(new ActiveSpan { Start = s, End = m.Time });
                        open = null;
                    }
                }
            }
            if (open != null)
            {
                DateTime s = open.Value < windowStart ? windowStart : open.Value;
                if (windowEnd > s)
                    spans.Add(new ActiveSpan { Start = s, End = windowEnd });
            }
            return spans;
        }

        // Build definitely-locked spans from lock markers (4800 opens, 4801 closes).
        // Periods with no lock data are NOT locked - absence of evidence is unknown,
        // not locked (auditing may have been off, or the state simply never toggled).
        public static List<ActiveSpan> BuildLockedSpans(List<Marker> locks, DateTime windowStart, DateTime windowEnd)
        {
            var inverted = new List<Marker>(locks.Count);
            foreach (Marker m in locks)
                inverted.Add(new Marker { Time = m.Time, Opens = !m.Opens, IsBoot = false });
            return BuildSpans(inverted, windowStart, windowEnd);
        }

        // Subtract locked spans from awake spans. Both lists must be time-ordered
        // (BuildSpans emits chronologically, which this relies on).
        public static List<ActiveSpan> Subtract(List<ActiveSpan> awake, List<ActiveSpan> locked)
        {
            var result = new List<ActiveSpan>();
            int j = 0;
            foreach (ActiveSpan a in awake)
            {
                DateTime s = a.Start;
                while (j < locked.Count && locked[j].End <= s) j++;
                for (int k = j; k < locked.Count && locked[k].Start < a.End; k++)
                {
                    if (locked[k].Start > s)
                        result.Add(new ActiveSpan { Start = s, End = locked[k].Start });
                    if (locked[k].End > s)
                        s = locked[k].End;
                    if (s >= a.End) break;
                }
                if (s < a.End)
                    result.Add(new ActiveSpan { Start = s, End = a.End });
            }
            return result;
        }

        // Clip spans at local-midnight boundaries and sum active time per day.
        // Boots come from 6005 markers only (one per boot).
        public static List<DayReport> Aggregate(List<ActiveSpan> spans, List<Marker> markers, DateTime windowStart, int days)
        {
            var reports = new List<DayReport>();
            DateTime today = DateTime.Now.Date;
            for (int i = days - 1; i >= 0; i--)
                reports.Add(new DayReport { Day = today.AddDays(-i), Boots = 0, Active = TimeSpan.Zero });

            foreach (Marker m in markers)
            {
                if (!m.IsBoot) continue;
                DateTime d = m.Time.Date;
                foreach (DayReport r in reports)
                {
                    if (r.Day == d) { r.Boots++; break; }
                }
            }

            foreach (ActiveSpan span in spans)
            {
                DateTime s = span.Start;
                while (s < span.End)
                {
                    DateTime midnight = s.Date.AddDays(1);
                    DateTime e = span.End < midnight ? span.End : midnight;
                    foreach (DayReport r in reports)
                    {
                        if (r.Day == s.Date) { r.Active += e - s; break; }
                    }
                    s = e;
                }
            }
            return reports;
        }
    }
}
