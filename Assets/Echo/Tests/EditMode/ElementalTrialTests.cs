using System.Collections.Generic;
using Echo.Game.Cosmetics;
using Echo.Game.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The Elementals (a user revision): every achievement (all 30) unlocks them for good; 5 rewarded ads lend them
    /// for 5 runs, and no more ads count while a trial is running.
    /// </summary>
    public class ElementalTrialTests
    {
        static CosmeticItem Ember() => new CosmeticItem { Id = "char_ember", Type = CosmeticType.Character, AllAchievements = true };

        [Test]
        public void FiveAds_LendThePack_ForFiveRuns()
        {
            var cosmetics = new CosmeticsSaveData();
            CosmeticItem ember = Ember();
            for (int i = 1; i < ElementalTrial.AdsForTrial; i++) Assert.IsFalse(ElementalTrial.CountAd(cosmetics), $"ad {i}");
            Assert.IsFalse(ElementalTrial.Available(ember, 0, cosmetics));
            Assert.IsTrue(ElementalTrial.CountAd(cosmetics), "the fifth ad starts the trial");
            Assert.AreEqual(0, cosmetics.TrialAds);
            Assert.IsTrue(ElementalTrial.Available(ember, 0, cosmetics));

            Assert.IsFalse(ElementalTrial.CountAd(cosmetics), "ads do not stack up during a trial");
            Assert.AreEqual(0, cosmetics.TrialAds);

            for (int run = 0; run < ElementalTrial.TrialRuns; run++)
            {
                Assert.IsTrue(ElementalTrial.Available(ember, 0, cosmetics), $"run {run + 1}");
                ElementalTrial.UseRun(cosmetics);
            }
            Assert.IsFalse(ElementalTrial.Available(ember, 0, cosmetics), "back to locked after 5 runs");
            Assert.IsFalse(CosmeticUnlocks.IsOwned(ember, 0, cosmetics.Owned), "a trial never owns it");
        }

        [Test]
        public void EveryAchievement_UnlocksThemForGood_AndNothingLessDoes()
        {
            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            catalog.Items = new[] { Ember(), new CosmeticItem { Id = "char_ninja", Type = CosmeticType.Character } };
            var owned = new List<string>();
            Assert.AreEqual(30, ElementalTrial.AchievementsNeeded, "all 30, hidden ones included");
            Assert.IsEmpty(ElementalTrial.EarnIfComplete(catalog, 29, owned));
            Assert.AreEqual(1, ElementalTrial.EarnIfComplete(catalog, 30, owned).Count);
            CollectionAssert.AreEqual(new[] { "char_ember" }, owned);
            Assert.IsEmpty(ElementalTrial.EarnIfComplete(catalog, 30, owned), "only once");
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void TheTrial_RoundTripsInTheSave()
        {
            var data = new SaveData();
            data.Cosmetics.TrialAds = 3;
            data.Cosmetics.TrialRunsLeft = 2;
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "echo_trial_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var saves = new SaveSystem(path);
                saves.Save(data);
                SaveData back = saves.Load();
                Assert.AreEqual(3, back.Cosmetics.TrialAds);
                Assert.AreEqual(2, back.Cosmetics.TrialRunsLeft);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}
