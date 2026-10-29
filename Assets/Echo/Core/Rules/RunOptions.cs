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

        /// <summary>
        /// Daily Haunt (version 2.0): today's shared course, scored on distance, with today's leaders and the
        /// player's own daily ghosts. No Continue, banish, duel, bounty, twist or power-ups. Touches no progress.
        /// </summary>
        public bool Daily;

        /// <summary>
        /// Modes doc, One Life: one run a day, played under the daily rules (Daily is set too) with no 1-UPs, since it's
        /// one life.
        /// </summary>
        public bool OneLife;

        /// <summary>Modes doc, the Community Graveyard: the week's course against the community's ghosts (Daily is set too).</summary>
        public bool Graveyard;
    }
}
