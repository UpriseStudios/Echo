using System;
using System.Collections.Generic;

namespace Echo.Core.Modes
{
    /// <summary>
    /// Whether a mode is open (modes doc: only when switched on and inside its dates), shared by the game and Cloud Code
    /// so both decide the same way. Times are whole seconds since 1970, UTC.
    /// </summary>
    public static class ModeRules
    {
        public const long Day = 86400;

        public static bool IsOpen(ModeWindow w, long now) => w.Enabled && now >= w.Start && now < w.End && OnItsDay(w, now);

        /// <summary>On the Modes screen: open, or closed but still showing its result.</summary>
        public static bool IsShown(ModeWindow w, long now) => w.Enabled && now >= w.Start && now < Math.Max(w.End, w.ShowUntil);

        static bool OnItsDay(ModeWindow w, long now) => w.Weekdays == 0 || (w.Weekdays & (1 << Weekday(now))) != 0;

        /// <summary>0 Sunday to 6 Saturday (1 January 1970 was a Thursday).</summary>
        public static int Weekday(long now) => (int)((FloorDiv(now, Day) + 4) % 7);

        /// <summary>
        /// Open: seconds until it closes (the end of its day for a weekday mode). Closed: seconds until it next opens
        /// inside its window, or -1 if it won't.
        /// </summary>
        public static long SecondsLeft(ModeWindow w, long now)
        {
            if (!w.Enabled) return -1;
            if (IsOpen(w, now))
                return w.Weekdays == 0 ? w.End - now : Math.Min(w.End, (FloorDiv(now, Day) + 1) * Day) - now;
            long from = Math.Max(now, w.Start);
            for (int d = 0; d < 8; d++)
            {
                long day = d == 0 ? from : (FloorDiv(from, Day) + d) * Day;
                if (day >= w.End) return -1;
                if (w.Weekdays == 0 || (w.Weekdays & (1 << Weekday(day))) != 0) return day - now;
            }
            return -1;
        }

        /// <summary>The window to describe a mode by: an open one, else the next to open, else one still showing; or null.</summary>
        public static ModeWindow Pick(IReadOnlyList<ModeWindow> windows, string id, long now)
        {
            ModeWindow next = null, shown = null;
            long soonest = long.MaxValue;
            foreach (ModeWindow w in windows)
            {
                if (w.Id != id || !w.Enabled) continue;
                if (IsOpen(w, now)) return w;
                long wait = SecondsLeft(w, now);
                if (wait >= 0 && wait < soonest)
                {
                    soonest = wait;
                    next = w;
                }
                if (shown == null && IsShown(w, now)) shown = w;
            }
            return shown ?? next;
        }

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
