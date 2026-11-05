using Echo.Game.Persistence;

namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// World Time Run doc, phase 4: every player can play Time Run 3 times free, so they see at least the first time
    /// rift before deciding. The runs used are counted in the save (a user decision), so Play Games cloud save carries
    /// them to other devices. A run counts as it starts; the run keeps the world even if it was the last free one.
    /// </summary>
    public static class WorldTrial
    {
        /// <summary>Free runs left of a world with a trial (0 for any other).</summary>
        public static int Left(CosmeticItem item, CosmeticsSaveData cosmetics) =>
            item == null || item.TrialRuns <= 0 ? 0 : System.Math.Max(0, item.TrialRuns - cosmetics.WorldTrialUsed);

        /// <summary>Owned, or free runs left to try it.</summary>
        public static bool Available(CosmeticItem item, int best, CosmeticsSaveData cosmetics) =>
            CosmeticUnlocks.IsOwned(item, best, cosmetics.Owned) || Left(item, cosmetics) > 0;

        /// <summary>On trial: worn without being owned, with runs left.</summary>
        public static bool OnTrial(CosmeticItem item, int best, CosmeticsSaveData cosmetics) =>
            !CosmeticUnlocks.IsOwned(item, best, cosmetics.Owned) && Left(item, cosmetics) > 0;

        /// <summary>A run has started wearing it on trial: one free run used; returns which run this is (1 to 3).</summary>
        public static int UseRun(CosmeticsSaveData cosmetics) => ++cosmetics.WorldTrialUsed;
    }
}
