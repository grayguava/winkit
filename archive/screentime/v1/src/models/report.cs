using System;
using System.Collections.Generic;

namespace screentime.models
{
    // A single "machine was up" span, local time, end-exclusive.
    class ActiveSpan
    {
        public DateTime Start;
        public DateTime End;
    }

    // One raw event-log marker: something that opens or closes an active span.
    class Marker
    {
        public DateTime Time;
        public bool Opens; // true = active-start (boot/resume/unlock), false = active-end (sleep/shutdown/lock)
        public bool IsBoot; // true = counts toward boots-per-day (6005 only)
    }

    // Aggregated per-day numbers for the report.
    class DayReport
    {
        public DateTime Day;
        public int Boots;
        public TimeSpan Active;
    }
}
