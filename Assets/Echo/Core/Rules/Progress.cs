using Echo.Core.Ghosts;

namespace Echo.Core.Rules
{
    /// <summary>Everything the rules persist between runs (tech spec 10). Plain data; Game serialises it.</summary>
    public sealed class Progress
    {
        public int Best;
        public int Clears;
        public uint Seed;
        public int NextRunIndex = 1;
        public int CycleBanishes;
        public readonly GhostSet Ghosts = new GhostSet();
        public readonly LifetimeStats Stats = new LifetimeStats();
        public readonly System.Collections.Generic.List<string> Achievements = new System.Collections.Generic.List<string>();

        /// <summary>Progress towards each achievement (skins addendum), by id. Looked up, never iterated in order.</summary>
        public readonly System.Collections.Generic.Dictionary<string, int> AchievementProgress = new System.Collections.Generic.Dictionary<string, int>();

        // Value-adds doc.
        /// <summary>The week of the last twist run and its best score (twist runs have their own board).</summary>
        public int TwistWeek;
        public int TwistBest;

        /// <summary>The Nemesis Duel has been offered and taken this high-score cycle.</summary>
        public bool DuelUsed;

        /// <summary>Balance addendum: Banish 10 has been used this high-score cycle (separate from CycleBanishes).</summary>
        public bool BanishTenUsed;

        /// <summary>Classic runs since the last new best, for the Banish 10 analytics.</summary>
        public int RunsSinceBest;

        /// <summary>The run that set the current best, ready to send as a Ghost Challenge; null before any best.</summary>
        public Echo.Core.Challenges.Challenge BestRun;

        /// <summary>The best run with the ghosts it started against (balance addendum), for full challenges from
        /// Stats. Null before any best set since 2.0; those send Lite challenges.</summary>
        public Echo.Core.Reels.Reel BestReel;

        /// <summary>Defeated Nemeses, most kills first (Rogues' Gallery).</summary>
        public readonly System.Collections.Generic.List<GalleryEntry> Gallery = new System.Collections.Generic.List<GalleryEntry>();

        /// <summary>The highlights reel (a user revision): saved runs, at most ReelRules.Capacity, in the order saved.</summary>
        public readonly System.Collections.Generic.List<Echo.Core.Reels.Reel> Reels = new System.Collections.Generic.List<Echo.Core.Reels.Reel>();
    }
}
