using System.Collections.Generic;
using Echo.Core.Rules;

namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// Score-milestone unlocks (game spec: cosmetics can also be unlocked by play), except characters: most
    /// are purchase-only, and two unlock after a number of rewarded ads (user revisions).
    /// </summary>
    public static class CosmeticUnlocks
    {
        public static bool IsOwned(CosmeticItem item, int best, IList<string> owned) =>
            owned.Contains(item.Id) || (item.EarnedByScore && item.UnlockScore <= best);

        /// <summary>Adds every item whose milestone <paramref name="best"/> has reached to <paramref name="owned"/>; returns the new ones.</summary>
        public static List<CosmeticItem> Earn(CosmeticCatalog catalog, int best, IList<string> owned)
        {
            var earned = new List<CosmeticItem>();
            foreach (var item in catalog.Items)
            {
                if (!item.EarnedByScore || owned.Contains(item.Id) || item.UnlockScore > best) continue;
                owned.Add(item.Id);
                if (item.UnlockScore > 0) earned.Add(item);
            }
            return earned;
        }

        /// <summary>Adds every ad-unlocked item <paramref name="adsWatched"/> has reached; returns the new ones.</summary>
        public static List<CosmeticItem> EarnByAds(CosmeticCatalog catalog, int adsWatched, IList<string> owned)
        {
            var earned = new List<CosmeticItem>();
            foreach (var item in catalog.Items)
            {
                if (item.AdsToUnlock <= 0 || owned.Contains(item.Id) || adsWatched < item.AdsToUnlock) continue;
                owned.Add(item.Id);
                earned.Add(item);
            }
            return earned;
        }

        /// <summary>Adds every item whose lifetime stat has reached its target; returns the new ones.</summary>
        public static List<CosmeticItem> EarnByStats(CosmeticCatalog catalog, LifetimeStats stats, IList<string> owned)
        {
            var earned = new List<CosmeticItem>();
            foreach (var item in catalog.Items)
            {
                if (item.UnlockStat == CosmeticUnlockStat.None || owned.Contains(item.Id)) continue;
                if (StatValue(item.UnlockStat, stats) < item.UnlockTarget) continue;
                owned.Add(item.Id);
                earned.Add(item);
            }
            return earned;
        }

        public static int StatValue(CosmeticUnlockStat stat, LifetimeStats stats) => stat switch
        {
            CosmeticUnlockStat.Deaths => stats.Deaths,
            CosmeticUnlockStat.Distance => stats.MaxDistance,
            CosmeticUnlockStat.GhostsAtStart => stats.MaxGhostsAtStart,
            CosmeticUnlockStat.GhostsCleared => stats.MaxGhostsCleared,
            _ => 0,
        };

        /// <summary>Adds every item <paramref name="productId"/> unlocks; returns whether anything was new.</summary>
        public static bool Grant(CosmeticCatalog catalog, string productId, IList<string> owned)
        {
            bool changed = false;
            foreach (var item in catalog.Items)
            {
                if (!item.UnlockedBy(productId) || owned.Contains(item.Id)) continue;
                owned.Add(item.Id);
                changed = true;
            }
            return changed;
        }
    }
}
