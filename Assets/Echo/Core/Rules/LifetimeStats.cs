namespace Echo.Core.Rules
{
    public sealed class LifetimeStats
    {
        public int TotalRuns;
        public int NemesesDefeated;
        public int WorstNemesisKills;
        public string WorstNemesisName = "";

        // Skins addendum unlocks.
        /// <summary>Every death, including ones a Continue or a 1-UP undid.</summary>
        public int Deaths;
        /// <summary>Furthest single run, in metres.</summary>
        public int MaxDistance;
        /// <summary>Most ghosts a run has started with.</summary>
        public int MaxGhostsAtStart;
        /// <summary>Most ghosts one new best has cleared.</summary>
        public int MaxGhostsCleared;
    }
}
