using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using screentime.models;

namespace screentime.events
{
    // Reads boot / sleep / wake / shutdown markers out of the event logs.
    // System log: readable as a standard user. Security log: needs elevation.
    static class EventReader
    {
        public static List<Marker> ReadSystemMarkers(DateTime windowStart, DateTime windowEnd)
        {
            var markers = new List<Marker>();
            string query = "*[System[TimeCreated[@SystemTime>='"
                + windowStart.ToUniversalTime().ToString("o")
                + "' and @SystemTime<='"
                + windowEnd.ToUniversalTime().ToString("o") + "']]]";
            EventLogQuery q = new EventLogQuery("System", PathType.LogName, query);
            using (EventLogReader reader = new EventLogReader(q))
            {
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        if (record.TimeCreated == null) continue;
                        DateTime t = record.TimeCreated.Value;
                        string provider = record.ProviderName ?? "";
                        int id = record.Id;
                        Marker m = ClassifySystem(provider, id, t);
                        if (m != null) markers.Add(m);
                    }
                }
            }
            return markers;
        }

        // provider+id -> open/close marker. Unknown events return null (ignored).
        static Marker ClassifySystem(string provider, int id, DateTime t)
        {
            // Boot markers (also open an active span).
            if (id == 6005 && provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = true, IsBoot = true };
            if (id == 12 && provider.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = true, IsBoot = false };
            // Resume-from-sleep markers.
            if (id == 1 && provider.Equals("Microsoft-Windows-Power-Troubleshooter", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = true, IsBoot = false };
            if (id == 107 && provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = true, IsBoot = false };
            // Sleep / shutdown markers (close an active span).
            if (id == 42 && provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = false, IsBoot = false };
            if ((id == 6006 || id == 6008) && provider.Equals("EventLog", StringComparison.OrdinalIgnoreCase))
                return new Marker { Time = t, Opens = false, IsBoot = false };
            return null;
        }

        // Lock/unlock markers. Throws on access-denied when not elevated;
        // callers check elevation first so this stays a fail-fast path.
        public static List<Marker> ReadLockMarkers(DateTime windowStart, DateTime windowEnd)
        {
            var markers = new List<Marker>();
            string query = "*[System[TimeCreated[@SystemTime>='"
                + windowStart.ToUniversalTime().ToString("o")
                + "' and @SystemTime<='"
                + windowEnd.ToUniversalTime().ToString("o") + "'] and (EventID=4800 or EventID=4801)]]";
            EventLogQuery q = new EventLogQuery("Security", PathType.LogName, query);
            using (EventLogReader reader = new EventLogReader(q))
            {
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        if (record.TimeCreated == null) continue;
                        DateTime t = record.TimeCreated.Value;
                        if (record.Id == 4801)
                            markers.Add(new Marker { Time = t, Opens = true, IsBoot = false });
                        else if (record.Id == 4800)
                            markers.Add(new Marker { Time = t, Opens = false, IsBoot = false });
                    }
                }
            }
            return markers;
        }
    }
}
