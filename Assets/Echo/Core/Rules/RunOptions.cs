namespace Echo.Core.Rules
{
    /// <summary>Per-run switches from feature flags and the calendar (value-adds doc). Default: a classic run.</summary>
    public struct RunOptions
    {
        /// <summary>One seeded ghost carries a bounty: near-missing it scores 50 instead of 5.</summary>
        public bool Bounties;

        /// <summary>This week's twist, or None for a classic run.</summary>
        public Twist Twist;
        public int TwistWeek;

        /// <summary>A Nemesis Duel: only the Nemesis, no ghost recorded, no best set.</summary>
        public bool Duel;

        /// <summary>A Ghost Challenge: a friend's run as the only ghost, on its course. Touches no progress.</summary>
        public bool Challenge;
    }
}
