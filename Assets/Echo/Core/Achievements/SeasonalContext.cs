namespace Echo.Core.Achievements
{
    /// <summary>
    /// The seasonal events counting towards achievements right now (seasons doc). The game fills it from the season
    /// schedule and the date (the server's when online), so Core never reads a clock. Each is an event key such as
    /// "christmas-2026", or null when that event isn't on or its achievements are switched off.
    /// </summary>
    public sealed class SeasonalContext
    {
        public static readonly SeasonalContext None = new SeasonalContext();

        /// <summary>Christmas (December).</summary>
        public readonly string Christmas;

        /// <summary>New Year, on 1 January itself.</summary>
        public readonly string NewYearsDay;

        public SeasonalContext(string christmas = null, string newYearsDay = null)
        {
            Christmas = christmas;
            NewYearsDay = newYearsDay;
        }

        public bool Any => Christmas != null || NewYearsDay != null;

        /// <summary>The event counting now for a season's achievements, or null.</summary>
        public string KeyFor(string season) =>
            season == AchievementCatalog.Christmas ? Christmas : season == AchievementCatalog.NewYear ? NewYearsDay : null;
    }
}
