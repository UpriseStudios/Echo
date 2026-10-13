using System.Linq;
using Echo.Game.Cosmetics;
using Echo.Services.Iap;
using NUnit.Framework;
using UnityEditor;

namespace Echo.Tests.EditMode
{
    /// <summary>The shipped catalog (written by the scene build) against the skins addendum roster.</summary>
    public class CharacterCatalogTests
    {
        const string CatalogPath = "Assets/Echo/Content/Cosmetics/CosmeticCatalog.asset";

        static CosmeticCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "run the scene build first");
            return catalog;
        }

        static CosmeticItem[] Characters() => Catalog().Items.Where(i => i.Type == CosmeticType.Character).ToArray();

        [Test]
        public void EveryCharacterHasFramesGhostsAJumpAndAHeadTop()
        {
            foreach (CosmeticItem c in Characters())
            {
                Assert.AreEqual(4, c.RunFrames.Length, c.Id);
                Assert.IsTrue(c.RunFrames.All(f => f != null), c.Id);
                Assert.AreEqual(4, c.GhostFrames.Length, c.Id);
                Assert.IsTrue(c.GhostFrames.All(f => f != null), c.Id);
                Assert.IsNotNull(c.JumpFrame, c.Id);
                Assert.IsNotNull(c.GhostJumpFrame, c.Id);
                Assert.That(c.HeadTop, Is.InRange(0.6f, 1.1f), c.Id);
            }
        }

        [Test]
        public void HoodedAndTheEightBaseSkinsAreFree()
        {
            string[] free = Characters().Where(c => c.EarnedByScore && c.UnlockScore == 0).Select(c => c.Id).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "char_hooded", "char_ninja", "char_knight", "char_wizard", "char_princess",
                "char_cat", "char_skeleton", "char_astronaut", "char_robot",
            }, free);
        }

        [Test]
        public void PumpkinTakesFiveAds_TheRestAreEarnedFromStats()
        {
            CosmeticItem[] characters = Characters();
            Assert.AreEqual(15 + 32 + 4, characters.Length, "the base cast, 8 packs of 4 and the Elementals");
            Assert.AreEqual(5, characters.Single(c => c.Id == "char_pumpkin").AdsToUnlock);

            var earned = characters.Where(c => c.UnlockStat != CosmeticUnlockStat.None)
                .ToDictionary(c => c.Id, c => (c.UnlockStat, c.UnlockTarget));
            CollectionAssert.AreEquivalent(new[] { "char_shadow", "char_glitch", "char_negative", "char_golden", "char_reaper" }, earned.Keys);
            Assert.AreEqual((CosmeticUnlockStat.GhostsAtStart, 15), earned["char_shadow"]);
            Assert.AreEqual((CosmeticUnlockStat.Distance, 1000), earned["char_glitch"]);
            Assert.AreEqual((CosmeticUnlockStat.GhostsCleared, 15), earned["char_negative"]);
            Assert.AreEqual((CosmeticUnlockStat.GhostsCleared, 20), earned["char_golden"]);
            Assert.AreEqual((CosmeticUnlockStat.Deaths, 250), earned["char_reaper"]);
            Assert.IsTrue(characters.Where(c => !c.EarnedByScore).All(c => !string.IsNullOrEmpty(c.UnlockText)), "every locked skin says how to get it");
        }

        [Test]
        public void PackCharacters_AreSoldFourToAPack_AndAllInTheSeasonBundle()
        {
            CosmeticItem[] sold = Characters().Where(c => c.PurchaseOnly).ToArray();
            Assert.AreEqual(32, sold.Length);
            var byPack = sold.GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.Count());
            CollectionAssert.AreEquivalent(ProductIds.Packs, byPack.Keys);
            Assert.IsTrue(byPack.Values.All(n => n == 4));
            Assert.IsTrue(sold.All(c => c.BundleProductId == ProductIds.SeasonOfEcho));
            Assert.IsTrue(Characters().Where(c => !c.PurchaseOnly).All(c => c.ProductId == "" && c.BundleProductId == ""),
                "the base cast is never sold");
            CollectionAssert.IsSubsetOf(ProductIds.Packs.Append(ProductIds.SeasonOfEcho), ProductIds.All);
            CollectionAssert.AllItemsAreUnique(ProductIds.All);
        }

        [Test]
        public void BuyingAPack_GrantsItsFour_AndTheSeasonGrantsAllThirtyTwo()
        {
            var owned = new System.Collections.Generic.List<string>();
            Assert.IsTrue(CosmeticUnlocks.Grant(Catalog(), ProductIds.PackRoyal, owned));
            CollectionAssert.AreEquivalent(new[] { "char_king", "char_queen", "char_jester", "char_guard" }, owned);

            var all = new System.Collections.Generic.List<string>();
            CosmeticUnlocks.Grant(Catalog(), ProductIds.SeasonOfEcho, all);
            Assert.AreEqual(32, all.Count);
        }

        [Test]
        public void TheCuratedCharacters_PlayWithTheScenery()
        {
            var items = Characters().ToDictionary(c => c.Id);
            Assert.AreEqual(CharacterReaction.Stars, items["char_astronaut"].Reaction);
            Assert.AreEqual(CharacterReaction.FullMoon, items["char_werewolf"].Reaction);
            Assert.AreEqual(CharacterReaction.Bats, items["char_vampire"].Reaction);
            Assert.AreEqual(CharacterReaction.Rain, items["char_frog"].Reaction);
            Assert.AreEqual(CharacterReaction.RoyalBanners, items["char_queen"].Reaction);
            Assert.AreEqual(CharacterReaction.None, items["char_hooded"].Reaction);
            Assert.AreEqual(28, Characters().Count(c => c.Reaction != CharacterReaction.None), "24 and the 4 Elementals");
        }

        [Test]
        public void TheElementals_AreEarnedByEveryAchievement_NeverSoldOrEarnedByScore()
        {
            CosmeticItem[] elementals = Characters().Where(c => c.AllAchievements).ToArray();
            CollectionAssert.AreEquivalent(new[] { "char_ember", "char_frost", "char_storm", "char_tide" }, elementals.Select(c => c.Id));
            foreach (CosmeticItem c in elementals)
            {
                Assert.IsFalse(c.PurchaseOnly, c.Id);
                Assert.IsFalse(c.EarnedByScore, c.Id);
                Assert.AreNotEqual(CharacterReaction.None, c.Reaction, c.Id);
                StringAssert.Contains("achievements", c.UnlockText, c.Id);
            }
        }

        [Test]
        public void MinorRewards_AreEarnedFromStats()
        {
            var items = Catalog().Items.ToDictionary(i => i.Id);
            Assert.AreEqual((CosmeticUnlockStat.GhostsAtStart, 1), (items["trail_starter"].UnlockStat, items["trail_starter"].UnlockTarget));
            Assert.AreEqual((CosmeticUnlockStat.Distance, 100), (items["palette_forest"].UnlockStat, items["palette_forest"].UnlockTarget));
            Assert.AreEqual((CosmeticUnlockStat.Deaths, 50), (items["trail_bone"].UnlockStat, items["trail_bone"].UnlockTarget));
            Assert.AreEqual((CosmeticUnlockStat.Distance, 500), (items["trail_prism"].UnlockStat, items["trail_prism"].UnlockTarget));
        }
    }
}
