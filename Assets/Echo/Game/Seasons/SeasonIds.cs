namespace Echo.Game.Seasons
{
    /// <summary>Season ids, as the schedule (Remote Config key "seasons") and the theme assets name them.</summary>
    public static class SeasonIds
    {
        public const string Halloween = "halloween";
        public const string BonfireNight = "bonfire";
        public const string Christmas = "christmas";
        public const string NewYear = "newyear";
        public const string Valentines = "valentines";
        public const string Easter = "easter";
        public const string AprilFools = "aprilfools";

        /// <summary>
        /// A user revision: the Community Graveyard's own look, shown in its lobby and runs over any season. Never
        /// scheduled.
        /// </summary>
        public const string Graveyard = "mode_graveyard";

        /// <summary>A user revision: the store's worlds, looks that are never scheduled (Halloween Night is the Halloween season's).</summary>
        public const string NeonNight = "world_neon";
        public const string SunkenRuins = "world_sunken";
        public const string AutumnWoods = "world_autumn";
        public const string Beach = "world_beach";
        public const string DesertDunes = "world_desert";
        public const string VolcanicWaste = "world_volcano";
        public const string ToyTown = "world_toytown";

        /// <summary>Seasons phase 1: a theme that exercises every override. Never scheduled; only forced.</summary>
        public const string Test = "test";
    }
}
