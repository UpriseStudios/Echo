using System;
using System.IO;
using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Seasons;
using Echo.Services.Iap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Progress = Echo.Core.Rules.Progress;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Seasonal achievements (seasons doc): only during their event, starting again next time if unfinished, kept once
    /// earned, outside the 30 and the Elementals; Santa, Rudolph and the two trails unlock with them; the Christmas Pack
    /// sells its own four.
    /// </summary>
    public class SeasonalAchievementTests
    {
        static readonly SeasonalContext Christmas2026 = new SeasonalContext(christmas: "christmas-2026");
        static readonly SeasonalContext Christmas2027 = new SeasonalContext(christmas: "christmas-2027");

        static SeasonSchedule BuiltIn()
        {
            Assert.IsTrue(SeasonSchedule.TryParse(File.ReadAllText(Path.Combine(Application.dataPath, "Echo", "Content", "Seasons", "seasons.json")),
                out SeasonSchedule s));
            return s;
        }

        [Test]
        public void TheEvents_CountOnTheirOwnDays()
        {
            SeasonSchedule s = BuiltIn();
            SeasonalContext dec = SeasonalEvents.On(s, new DateTime(2026, 12, 10));
            Assert.AreEqual("christmas-2026", dec.Christmas);
            Assert.IsNull(dec.NewYearsDay);
            SeasonalContext eve = SeasonalEvents.On(s, new DateTime(2026, 12, 31));
            Assert.AreEqual("christmas-2026", eve.Christmas, "still December for Christmas's achievements, though New Year shows");
            Assert.IsNull(eve.NewYearsDay, "First Footer is 1 January only");
            SeasonalContext jan1 = SeasonalEvents.On(s, new DateTime(2027, 1, 1));
            Assert.IsNull(jan1.Christmas);
            Assert.AreEqual("newyear-2026", jan1.NewYearsDay, "the New Year that started on 31 December 2026");
            Assert.IsFalse(SeasonalEvents.On(s, new DateTime(2027, 1, 2)).Any);
            Assert.IsFalse(SeasonalEvents.On(s, new DateTime(2026, 11, 30)).Any);

            Assert.IsTrue(SeasonSchedule.TryParse(@"{ ""seasons"": [ { ""id"": ""christmas"", ""start"": ""12-01"", ""end"": ""12-31"", ""achievements"": false } ] }",
                out SeasonSchedule off));
            Assert.IsFalse(SeasonalEvents.On(off, new DateTime(2026, 12, 10)).Any, "achievements switched off in the schedule");
        }

        [Test]
        public void TwelveDays_CountsDifferentDays_AndStartsAgainNextYear()
        {
            var p = new Progress();
            for (int day = 100; day < 111; day++)
            {
                Assert.IsEmpty(AchievementRules.OnSeasonalDailyRun(p, day, 0, Christmas2026));
                AchievementRules.OnSeasonalDailyRun(p, day, 0, Christmas2026);   // a second run that day doesn't count
            }
            Assert.AreEqual(11, AchievementRules.SeasonalProgressNow(p, AchievementIds.TwelveDays, Christmas2026));
            Assert.AreEqual(0, AchievementRules.SeasonalProgressNow(p, AchievementIds.TwelveDays, SeasonalContext.None), "between events");

            // Unfinished: next December starts from nothing.
            AchievementRules.OnSeasonalDailyRun(p, 465, 0, Christmas2027);
            Assert.AreEqual(1, AchievementRules.SeasonalProgressNow(p, AchievementIds.TwelveDays, Christmas2027));
            for (int day = 466; day < 476; day++) AchievementRules.OnSeasonalDailyRun(p, day, 0, Christmas2027);
            CollectionAssert.Contains(AchievementRules.OnSeasonalDailyRun(p, 476, 0, Christmas2027), AchievementIds.TwelveDays);

            // Earned is kept, whatever the next event brings.
            AchievementRules.OnSeasonalDailyRun(p, 830, 0, new SeasonalContext(christmas: "christmas-2028"));
            CollectionAssert.Contains(p.Achievements, AchievementIds.TwelveDays);
        }

        [Test]
        public void LeadTheWay_AddsUpLeaders_SnowedUnderNeedsAFullGraveyard_FirstFooterNewYearsDay()
        {
            var p = new Progress();
            AchievementRules.OnSeasonalDailyRun(p, 100, 10, Christmas2026);
            AchievementRules.OnSeasonalDailyRun(p, 101, 10, Christmas2026);
            Assert.AreEqual(20, AchievementRules.SeasonalProgressNow(p, AchievementIds.LeadTheWay, Christmas2026));
            CollectionAssert.Contains(AchievementRules.OnSeasonalDailyRun(p, 102, 5, Christmas2026), AchievementIds.LeadTheWay);

            Assert.IsEmpty(AchievementRules.OnSeasonalDailyRun(p, 103, 3, SeasonalContext.None));
            CollectionAssert.AreEqual(new[] { AchievementIds.FirstFooter },
                AchievementRules.OnSeasonalDailyRun(p, 400, 0, new SeasonalContext(newYearsDay: "newyear-2026")));

            Assert.AreEqual(30, AchievementRules.SnowedUnderGhosts);
            CollectionAssert.DoesNotContain(p.Achievements, AchievementIds.SnowedUnder);
        }

        [Test]
        public void TrickOrTreat_CountsTenRuns_AndItsComplicated_NeedsTheNemesisOnValentinesDay()
        {
            Assert.IsTrue(SeasonSchedule.TryParse(@"{ ""seasons"": [
                { ""id"": ""halloween"", ""start"": ""10-24"", ""end"": ""11-02"", ""achievements"": true },
                { ""id"": ""valentines"", ""start"": ""02-13"", ""end"": ""02-15"", ""achievements"": true } ] }", out SeasonSchedule on));
            SeasonalContext halloween = SeasonalEvents.On(on, new DateTime(2027, 10, 28));
            Assert.AreEqual("halloween-2027", halloween.Halloween);
            Assert.IsNull(SeasonalEvents.On(BuiltIn(), new DateTime(2026, 10, 28)).Halloween, "2.1 ships Halloween switched off");
            Assert.IsNull(SeasonalEvents.On(on, new DateTime(2027, 2, 13)).ValentinesDay, "It's Complicated is the 14th only");
            SeasonalContext fourteenth = SeasonalEvents.On(on, new DateTime(2027, 2, 14));
            Assert.AreEqual("valentines-2027", fourteenth.ValentinesDay);

            var p = new Progress();
            var run = new RunSimulation(TestCourses.Flat(), new Echo.Core.Ghosts.GhostSet(), 1, 100, Echo.Core.Sim.SimConfig.Default);
            for (int i = 0; i < 6; i++) AchievementRules.OnSeasonalRunEnded(p, run, halloween, false);
            for (int day = 0; day < 3; day++) AchievementRules.OnSeasonalDailyRun(p, 600 + day, 0, halloween);
            Assert.AreEqual(9, AchievementRules.SeasonalProgressNow(p, AchievementIds.TrickOrTreat, halloween));
            CollectionAssert.Contains(AchievementRules.OnSeasonalRunEnded(p, run, halloween, false), AchievementIds.TrickOrTreat);

            Assert.IsEmpty(AchievementRules.OnSeasonalRunEnded(p, run, fourteenth, false), "killed by something else");
            CollectionAssert.AreEqual(new[] { AchievementIds.ItsComplicated }, AchievementRules.OnSeasonalRunEnded(p, run, fourteenth, true));
        }

        [Test]
        public void Seasonal_NeverCountTowardsTheThirty()
        {
            Assert.AreEqual(30, AchievementCatalog.SinglePlayerCount);
            string[] seasonal = AchievementCatalog.All.Where(d => d.Seasonal).Select(d => d.Id).ToArray();
            Assert.AreEqual(0, AchievementCatalog.SinglePlayerEarned(seasonal));
            foreach (AchievementDef def in AchievementCatalog.All.Where(d => d.Seasonal))
                Assert.IsNotNull(new SeasonalContext("c", "n", "h", "v").KeyFor(def.Season), def.Id + " belongs to a known event");
        }

        static CosmeticCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Echo/Content/Cosmetics/CosmeticCatalog.asset");
            Assert.IsNotNull(catalog, "run the scene build first");
            return catalog;
        }

        [Test]
        public void SeasonalRewards_UnlockWithTheirAchievements_AndAreNeverSold()
        {
            CosmeticCatalog catalog = Catalog();
            var rewards = catalog.Items.Where(i => !string.IsNullOrEmpty(i.UnlockAchievement)).ToDictionary(i => i.Id, i => i.UnlockAchievement);
            Assert.AreEqual(AchievementIds.TwelveDays, rewards["char_santa"]);
            Assert.AreEqual(AchievementIds.LeadTheWay, rewards["char_rudolph"]);
            Assert.AreEqual(AchievementIds.SnowedUnder, rewards["trail_candy_cane"]);
            Assert.AreEqual(AchievementIds.FirstFooter, rewards["trail_fireworks"]);
            Assert.AreEqual(AchievementIds.TrickOrTreat, rewards["char_pumpkin"]);
            Assert.AreEqual(AchievementIds.ItsComplicated, rewards["grave_heart"]);
            Assert.AreEqual(CosmeticType.Grave, catalog.Items.First(i => i.Id == "grave_heart").Type);
            Assert.IsTrue(CosmeticUnlocks.IsOwned(catalog.Resolve(null, CosmeticType.Grave), 0, new System.Collections.Generic.List<string>()),
                "the stone is everyone's");
            foreach (CosmeticItem item in catalog.Items.Where(i => rewards.ContainsKey(i.Id)))
            {
                Assert.IsFalse(item.PurchaseOnly || item.EarnedByScore, item.Id);
                Assert.IsEmpty(item.ProductId, item.Id);
                Assert.IsFalse(CosmeticUnlocks.IsOwned(item, 100000, new System.Collections.Generic.List<string>()), item.Id + " isn't won by score");
            }
            Assert.AreEqual(CharacterReaction.NoseGlow, catalog.Items.First(i => i.Id == "char_rudolph").Reaction);
            Assert.Greater(catalog.Items.First(i => i.Id == "trail_candy_cane").TrailStops.Length, 1);

            var owned = new System.Collections.Generic.List<string>();
            CollectionAssert.AreEquivalent(new[] { "char_santa" },
                CosmeticUnlocks.EarnByAchievements(catalog, new[] { AchievementIds.TwelveDays }, owned).Select(i => i.Id));
            Assert.IsEmpty(CosmeticUnlocks.EarnByAchievements(catalog, new[] { AchievementIds.TwelveDays }, owned), "once");
        }

        [Test]
        public void TheChristmasPack_GrantsItsFour_AndTheSeasonBundleDoesnt()
        {
            var owned = new System.Collections.Generic.List<string>();
            Assert.IsTrue(CosmeticUnlocks.Grant(Catalog(), ProductIds.PackChristmas, owned));
            CollectionAssert.AreEquivalent(new[] { "char_elf", "char_snowman", "char_gingerbread", "char_nutcracker" }, owned);
            var bundle = new System.Collections.Generic.List<string>();
            CosmeticUnlocks.Grant(Catalog(), ProductIds.SeasonOfEcho, bundle);
            Assert.IsEmpty(bundle.Intersect(owned));
        }
    }
}
