namespace Echo.Core.Achievements
{
    /// <summary>One achievement (skins addendum): progress counts up to Target; hidden ones show once started.</summary>
    public sealed class AchievementDef
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly bool Hidden;
        public readonly int Target;

        /// <summary>Version 2.0: an online extra (the Daily Haunt). The Elementals need only the 30 single-player ones.</summary>
        public readonly bool Online;

        /// <summary>
        /// Seasons doc: the event a seasonal achievement belongs to (a season id), or null. Seasonal achievements count
        /// only during their event, start again each year until earned, and never count towards the 30 or the Elementals.
        /// </summary>
        public readonly string Season;

        public bool Seasonal => Season != null;

        /// <summary>
        /// A bonus extra (a user decision): shown and counted in Stats and on Play Games, but never one of the 30 the
        /// Elementals need, because it rests on rewarded ads.
        /// </summary>
        public readonly bool Bonus;

        /// <summary>One of the 30 single-player achievements the Elementals need.</summary>
        public bool Core => !Online && !Seasonal && !Bonus;

        public AchievementDef(string id, string title, string description, int target = 1, bool hidden = false, bool online = false,
                              string season = null, bool bonus = false)
        {
            Bonus = bonus;
            Online = online;
            Season = season;
            Id = id;
            Title = title;
            Description = description;
            Target = target;
            Hidden = hidden;
        }
    }
}
