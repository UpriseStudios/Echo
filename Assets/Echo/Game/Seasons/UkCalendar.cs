using System;

namespace Echo.Game.Seasons
{
    /// <summary>
    /// UK local time from UTC (seasons doc: the dates are UK local), without the device's time zone database: British
    /// Summer Time runs from 01:00 UTC on the last Sunday in March to 01:00 UTC on the last Sunday in October.
    /// </summary>
    public static class UkCalendar
    {
        public static DateTime Date(DateTime utc) => (IsSummerTime(utc) ? utc.AddHours(1) : utc).Date;

        public static bool IsSummerTime(DateTime utc)
        {
            DateTime starts = LastSunday(utc.Year, 3).AddHours(1);
            DateTime ends = LastSunday(utc.Year, 10).AddHours(1);
            return utc >= starts && utc < ends;
        }

        static DateTime LastSunday(int year, int month)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
            return last.AddDays(-(int)last.DayOfWeek);
        }
    }
}
