namespace Echo.Core.Scoring
{
    /// <summary>Score formula (tech spec 8). Integer maths throughout.</summary>
    public static class ScoreRules
    {
        public const int SubUnitsPerMetre = 1000;
        public const int NearMissPoints = 5;
        public const int MultiplierPctPerGhost = 5;

        /// <summary>Ghost Bounties (value-adds doc): the bounty ghost's near-miss is worth 50, so 45 on top.</summary>
        public const int BountyPoints = 50;

        public static int Distance(int maxX) => maxX / SubUnitsPerMetre;

        public static int Score(int distance, int nearMisses, int ghostsAtStart, int bounties = 0) =>
            (distance + NearMissPoints * nearMisses + (BountyPoints - NearMissPoints) * bounties)
            * (100 + MultiplierPctPerGhost * ghostsAtStart) / 100;
    }
}
