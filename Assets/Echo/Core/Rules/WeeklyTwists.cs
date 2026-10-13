namespace Echo.Core.Rules
{
    /// <summary>
    /// Which twist runs in a given week (value-adds doc: one rule change a week, rotating automatically).
    /// The week number comes from the game's clock; this rotation is pure so it is testable.
    /// </summary>
    public static class WeeklyTwists
    {
        public const int CrowdControlMetres = 300;
        public const int GiantGhostPct = 150;

        static readonly Twist[] Rotation =
        {
            Twist.ShadowWeek, Twist.Mirror, Twist.CrowdControl, Twist.LightsOut, Twist.GiantGhosts, Twist.Copycat,
        };

        public static Twist ForWeek(int week) => Rotation[((week % Rotation.Length) + Rotation.Length) % Rotation.Length];

        /// <summary>Weeks since 1970-01-05, the first Monday, from whole days since 1970-01-01.</summary>
        public static int WeekOf(int daysSinceEpoch) => (daysSinceEpoch - 4) / 7;

        public static string Name(Twist t) => t switch
        {
            Twist.ShadowWeek => "Shadow Week",
            Twist.Mirror => "Mirror",
            Twist.LightsOut => "Lights Out",
            Twist.CrowdControl => "Crowd Control",
            Twist.GiantGhosts => "Giant Ghosts",
            Twist.Copycat => "Copycat",
            _ => "Classic",
        };

        public static string Description(Twist t) => t switch
        {
            Twist.ShadowWeek => "Ghosts appear only when they're close",
            Twist.Mirror => "The course runs right to left",
            Twist.LightsOut => "Only the ground near you is lit",
            Twist.CrowdControl => "Ghosts can't hurt you until 300 m",
            Twist.GiantGhosts => "Ghosts are 1.5x the size",
            Twist.Copycat => "A mirror-you runs at you, copying your taps 0.5 s late",
            _ => "",
        };
    }
}
