using System;
using Echo.Core.Achievements;

namespace Echo.Game.Seasons
{
    /// <summary>
    /// The events counting towards seasonal achievements on a UK date (seasons doc): each one's window must hold the date
    /// and have its achievements switched on in the schedule. A window counts even while a more specific season shows
    /// (31 December is New Year's, but still December for Christmas's achievements).
    /// </summary>
    public static class SeasonalEvents
    {
        public static SeasonalContext On(SeasonSchedule schedule, DateTime ukDate)
        {
            string christmas = Key(schedule, SeasonIds.Christmas, ukDate);
            string newYear = ukDate.Month == 1 && ukDate.Day == 1 ? Key(schedule, SeasonIds.NewYear, ukDate) : null;
            string halloween = Key(schedule, SeasonIds.Halloween, ukDate);
            // It's Complicated asks for 14 February itself, inside Valentine's 13 to 15.
            string valentines = ukDate.Month == 2 && ukDate.Day == 14 ? Key(schedule, SeasonIds.Valentines, ukDate) : null;
            var s = new SeasonalContext(christmas, newYear, halloween, valentines);
            return s.Any ? s : SeasonalContext.None;
        }

        /// <summary>The event's key, its season id and the year its window opened (e.g. "newyear-2026"), or null.</summary>
        public static string Key(SeasonSchedule schedule, string id, DateTime ukDate)
        {
            foreach (SeasonWindow w in schedule.Seasons)
                if (w.Id == id && w.Enabled && w.Achievements && SeasonSchedule.Holds(w, ukDate, out _))
                    return $"{id}-{SeasonSchedule.OccurrenceStart(w, ukDate).Year}";
            return null;
        }
    }
}
