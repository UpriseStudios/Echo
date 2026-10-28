namespace Echo.Core.Modes
{
    /// <summary>The competitive modes (modes doc), as the "modes" Remote Config key and the server name them.</summary>
    public static class ModeIds
    {
        public const string NaughtyNice = "naughty_nice";
        public const string OneLife = "one_life";
        public const string Graveyard = "graveyard";

        /// <summary>Ghost Duels; "Ranked" in code (the Nemesis Duel already owns "Duel"), and shown as ranked to players.</summary>
        public const string Ranked = "ranked";

        public static readonly string[] All = { NaughtyNice, OneLife, Graveyard, Ranked };
    }
}
