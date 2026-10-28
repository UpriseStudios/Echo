using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Echo.Game.Seasons
{
    /// <summary>
    /// When each season runs (seasons doc, "Season schedule"): the Remote Config key "seasons", with a copy built in
    /// (Content/Seasons/seasons.json) for offline players. One season at a time; where two overlap (New Year inside
    /// Christmas) the shorter, more specific one wins.
    /// </summary>
    public sealed class SeasonSchedule
    {
        // Required, so a Remote Config value without it is refused rather than read as "no seasons".
        [JsonProperty("seasons", Required = Required.Always)] public List<SeasonWindow> Seasons = new List<SeasonWindow>();

        /// <summary>Reads a schedule, dropping any window whose id or dates don't read; false if the JSON doesn't.</summary>
        public static bool TryParse(string json, out SeasonSchedule schedule)
        {
            schedule = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                schedule = JsonConvert.DeserializeObject<SeasonSchedule>(json);
            }
            catch (JsonException)
            {
                return false;
            }
            if (schedule?.Seasons == null)
            {
                schedule = null;
                return false;
            }
            // A leap year, so "02-29" reads.
            var check = new DateTime(2028, 1, 1);
            schedule.Seasons.RemoveAll(w => w == null || string.IsNullOrEmpty(w.Id)
                || !TryDate(w.Start, check.Year, out _, out bool startYearly) || !TryDate(w.End, check.Year, out _, out bool endYearly)
                || startYearly != endYearly);
            return true;
        }

        /// <summary>The season on this UK date, or null.</summary>
        public SeasonWindow ActiveOn(DateTime ukDate)
        {
            SeasonWindow best = null;
            int bestDays = int.MaxValue;
            foreach (SeasonWindow w in Seasons)
            {
                if (!w.Enabled || !Holds(w, ukDate, out int days)) continue;
                if (days < bestDays)
                {
                    best = w;
                    bestDays = days;
                }
            }
            return best;
        }

        /// <summary>Whether the window holds the date, and how many days it lasts.</summary>
        public static bool Holds(SeasonWindow w, DateTime date, out int days)
        {
            days = 0;
            date = date.Date;
            if (!TryDate(w.Start, date.Year, out DateTime start, out bool yearly) || !TryDate(w.End, date.Year, out DateTime end, out _))
                return false;
            if (!yearly || end >= start)
            {
                days = (end - start).Days + 1;
                return days > 0 && date >= start && date <= end;
            }
            // Over New Year: from the start to the end of the year, and from New Year to the end.
            days = (end.AddYears(1) - start).Days + 1;
            return date >= start || date <= end;
        }

        // "MM-dd" in the given year, or "yyyy-MM-dd".
        static bool TryDate(string text, int year, out DateTime date, out bool yearly)
        {
            date = default;
            yearly = text != null && text.Length == 5;
            if (yearly)
                return DateTime.TryParseExact($"{year:D4}-{text}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
            return text != null && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
