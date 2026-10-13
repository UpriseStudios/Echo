namespace Echo.Core.Courses
{
    /// <summary>Distance at which each tier unlocks (tech spec 6). Part of the content version.</summary>
    public static class TierSchedule
    {
        public const int MaxTier = 4;

        // Index = tier; value = start x in sub-units. Tier 0 is the fixed tutorial and never drawn.
        static readonly int[] UnlockX = { 0, 0, 200_000, 400_000, 700_000 };

        public static int UnlockXFor(int tier) => UnlockX[tier];

        public static int MaxTierAt(int x)
        {
            int tier = 1;
            for (int t = 2; t <= MaxTier; t++)
                if (x >= UnlockX[t]) tier = t;
            return tier;
        }
    }
}
