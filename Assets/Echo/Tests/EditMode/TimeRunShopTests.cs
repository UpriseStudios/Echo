using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Persistence;
using Echo.Services.Iap;
using NUnit.Framework;
using UnityEditor;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// World Time Run doc, phase 4: the world sold on its own with 3 free runs counted in the save, its five achievements
    /// (bonus extras, a user decision) and their rewards.
    /// </summary>
    public class TimeRunShopTests
    {
        static CosmeticCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Echo/Content/Cosmetics/CosmeticCatalog.asset")
                          ?? AssetDatabase.FindAssets("t:" + nameof(CosmeticCatalog)).Select(g => AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(AssetDatabase.GUIDToAssetPath(g))).First();
            Assert.IsNotNull(catalog, "run Echo/Build Scene first");
            return catalog;
        }

        [Test]
        public void TimeRun_IsSoldOnItsOwn_WithThreeFreeRuns()
        {
            CosmeticItem world = Catalog().Items.Single(i => i.Id == "world_timerun");
            Assert.AreEqual(CosmeticType.World, world.Type);
            Assert.IsTrue(world.PurchaseOnly);
            Assert.AreEqual(ProductIds.WorldTimeRun, world.ProductId);
            Assert.IsEmpty(world.BundleProductId, "never in the Worlds Bundle");
            CollectionAssert.DoesNotContain(ProductIds.Worlds, ProductIds.WorldTimeRun);
            CollectionAssert.Contains(ProductIds.All, ProductIds.WorldTimeRun);
            Assert.AreEqual(3, world.TrialRuns);
            Assert.IsNotNull(world.World);
            Assert.AreEqual(5, world.World.Eras.Length);
        }

        [Test]
        public void TheFreeRuns_CountDown_ThenItMustBeBought()
        {
            CosmeticItem world = Catalog().Items.Single(i => i.Id == "world_timerun");
            var cosmetics = new CosmeticsSaveData();
            Assert.AreEqual(3, WorldTrial.Left(world, cosmetics));
            Assert.IsTrue(WorldTrial.OnTrial(world, 0, cosmetics));
            Assert.AreEqual(1, WorldTrial.UseRun(cosmetics));
            Assert.AreEqual(2, WorldTrial.UseRun(cosmetics));
            Assert.AreEqual(1, WorldTrial.Left(world, cosmetics));
            Assert.IsTrue(WorldTrial.Available(world, 0, cosmetics));
            WorldTrial.UseRun(cosmetics);
            Assert.AreEqual(0, WorldTrial.Left(world, cosmetics));
            Assert.IsFalse(WorldTrial.Available(world, 0, cosmetics), "the trial is over");
            Assert.IsTrue(CosmeticUnlocks.Grant(Catalog(), ProductIds.WorldTimeRun, cosmetics.Owned));
            Assert.IsTrue(WorldTrial.Available(world, 0, cosmetics), "bought");
            Assert.IsFalse(WorldTrial.OnTrial(world, 0, cosmetics));

            CosmeticItem neon = Catalog().Items.Single(i => i.Id == "world_neon");
            Assert.AreEqual(0, WorldTrial.Left(neon, new CosmeticsSaveData()), "other worlds have no trial");
        }

        [Test]
        public void TheTrial_TravelsInTheSave()
        {
            var save = new SaveData();
            save.Cosmetics.WorldTrialUsed = 2;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(save);
            StringAssert.Contains("\"worldTrialUsed\":2", json);
            Assert.AreEqual(2, Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json).Cosmetics.WorldTrialUsed);
            Assert.AreEqual(0, Newtonsoft.Json.JsonConvert.DeserializeObject<CosmeticsSaveData>("{}").WorldTrialUsed, "an older save has used none");
        }

        [Test]
        public void TheFiveAchievements_AreBonusExtras()
        {
            Assert.AreEqual(5, AchievementCatalog.TimeRun.Length);
            foreach (string id in AchievementCatalog.TimeRun)
            {
                AchievementDef def = AchievementCatalog.Find(id);
                Assert.IsNotNull(def, id);
                Assert.IsTrue(def.Bonus, id + " is a bonus extra");
                Assert.IsFalse(def.Core, id + " is never one of the Elementals' 30");
            }
            Assert.AreEqual(30, AchievementCatalog.SinglePlayerCount);
            Assert.AreEqual(25, AchievementCatalog.Find(AchievementIds.Extinct).Target);
        }

        [Test]
        public void TheRules_CountEachDeath()
        {
            var p = new Echo.Core.Rules.Progress();
            Assert.IsEmpty(AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 120, InDinoLand = true, MostGhostsOnScreenInDinoLand = 9 }));
            Assert.AreEqual(1, AchievementRules.ProgressOf(p, AchievementIds.Extinct));
            CollectionAssert.AreEqual(new[] { AchievementIds.FossilRecord },
                AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 200, InDinoLand = true, MostGhostsOnScreenInDinoLand = 10 }));
            for (int i = 0; i < 22; i++) AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 50, InDinoLand = true });
            CollectionAssert.AreEqual(new[] { AchievementIds.Extinct }, AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 50, InDinoLand = true }));

            CollectionAssert.IsEmpty(AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 899 }), "899 m is still Today");
            CollectionAssert.AreEqual(new[] { AchievementIds.TimeTraveller }, AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 910, InFarFuture = true }));
            CollectionAssert.IsEmpty(AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 1000, InFarFuture = true, ByOwnGhost = false }), "a spike is no paradox");
            CollectionAssert.AreEqual(new[] { AchievementIds.Year3026, AchievementIds.Paradox },
                AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 1500, InFarFuture = true, ByOwnGhost = true }));
            Assert.IsEmpty(AchievementRules.OnTimeRunDeath(p, new TimeRunDeath { Distance = 1600, InFarFuture = true, ByOwnGhost = true }), "only once");
        }

        [Test]
        public void TheRewards_AreEarnedWithTheAchievements()
        {
            CosmeticCatalog catalog = Catalog();
            (string item, string achievement, CosmeticType type)[] rewards =
            {
                ("char_caveman", AchievementIds.TimeTraveller, CosmeticType.Character),
                ("char_chrono", AchievementIds.Year3026, CosmeticType.Character),
                ("grave_fossil", AchievementIds.Extinct, CosmeticType.Grave),
                ("trail_amber", AchievementIds.FossilRecord, CosmeticType.Trail),
            };
            foreach (var (id, achievement, type) in rewards)
            {
                CosmeticItem item = catalog.Items.Single(i => i.Id == id);
                Assert.AreEqual(type, item.Type, id);
                Assert.AreEqual(achievement, item.UnlockAchievement, id);
                Assert.IsFalse(item.PurchaseOnly, id + " is never sold");
            }
            CollectionAssert.IsEmpty(catalog.Items.Where(i => i.UnlockAchievement == AchievementIds.Paradox), "Paradox is bragging rights only");
            var owned = new System.Collections.Generic.List<string>();
            CollectionAssert.AreEquivalent(new[] { "char_caveman", "grave_fossil" },
                CosmeticUnlocks.EarnByAchievements(catalog, new[] { AchievementIds.TimeTraveller, AchievementIds.Extinct }, owned).Select(i => i.Id));
            CosmeticItem caveman = catalog.Items.Single(i => i.Id == "char_caveman");
            Assert.AreEqual(4, caveman.RunFrames.Length, "the standard skin template");
            Assert.AreEqual(4, caveman.GhostFrames.Length, "so it works as a ghost too");
        }
    }
}
