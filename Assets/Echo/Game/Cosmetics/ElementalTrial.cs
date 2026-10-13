using System.Collections.Generic;
using Echo.Core.Achievements;
using Echo.Game.Persistence;
using UnityEngine;

namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// The Elementals (a user revision): owned for good once every achievement is earned (all 30, hidden ones
    /// included); before that, 5 rewarded ads lend the whole pack for the next 5 runs. The ad count is its own, and
    /// it cannot be built up while a trial is running, so the pack can be tried but not kept without the achievements.
    /// </summary>
    public static class ElementalTrial
    {
        public const int AdsForTrial = 5;
        public const int TrialRuns = 5;

        public static int AchievementsNeeded => AchievementCatalog.All.Length;

        /// <summary>Each Elemental's colour, for the glow when ELEMENTS AWAKENED plays.</summary>
        public static Color ElementColor(string id) => id switch
        {
            "char_ember" => new Color(1f, 0.54f, 0.16f),
            "char_frost" => new Color(0.72f, 0.92f, 1f),
            "char_storm" => new Color(1f, 0.9f, 0.4f),
            "char_tide" => new Color(0.2f, 0.8f, 0.9f),
            _ => Color.white,
        };

        public static bool Active(CosmeticsSaveData cosmetics) => cosmetics.TrialRunsLeft > 0;

        /// <summary>Owned, or an Elemental while a trial is running.</summary>
        public static bool Available(CosmeticItem item, int best, CosmeticsSaveData cosmetics) =>
            CosmeticUnlocks.IsOwned(item, best, cosmetics.Owned) || (item.AllAchievements && Active(cosmetics));

        /// <summary>Counts a watched ad towards a trial; true when this one started it.</summary>
        public static bool CountAd(CosmeticsSaveData cosmetics)
        {
            if (Active(cosmetics)) return false;
            if (++cosmetics.TrialAds < AdsForTrial) return false;
            cosmetics.TrialAds = 0;
            cosmetics.TrialRunsLeft = TrialRuns;
            return true;
        }

        /// <summary>A run has started: one trial run used.</summary>
        public static void UseRun(CosmeticsSaveData cosmetics)
        {
            if (cosmetics.TrialRunsLeft > 0) cosmetics.TrialRunsLeft--;
        }

        /// <summary>Adds the Elementals to the owned list once every achievement is earned; returns the new ones.</summary>
        public static List<CosmeticItem> EarnIfComplete(CosmeticCatalog catalog, int achievementsEarned, IList<string> owned)
        {
            var earned = new List<CosmeticItem>();
            if (achievementsEarned < AchievementsNeeded) return earned;
            foreach (CosmeticItem item in catalog.Items)
            {
                if (!item.AllAchievements || owned.Contains(item.Id)) continue;
                owned.Add(item.Id);
                earned.Add(item);
            }
            return earned;
        }
    }
}
