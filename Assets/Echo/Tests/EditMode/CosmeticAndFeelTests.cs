using System.Collections.Generic;
using System.Linq;
using Echo.Game.Audio;
using Echo.Game.Cosmetics;
using Echo.Game.Views;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    public class CosmeticAndFeelTests
    {
        static CosmeticCatalog Catalog()
        {
            var c = ScriptableObject.CreateInstance<CosmeticCatalog>();
            c.Items = new[]
            {
                new CosmeticItem { Id = "runner_default", Type = CosmeticType.Runner, UnlockScore = 0 },
                new CosmeticItem { Id = "runner_mint", Type = CosmeticType.Runner, UnlockScore = 100 },
                new CosmeticItem { Id = "runner_gold", Type = CosmeticType.Runner, UnlockScore = 3000, ProductId = "echo.supporter" },
                new CosmeticItem { Id = "trail_default", Type = CosmeticType.Trail, UnlockScore = 0 },
                new CosmeticItem { Id = "char_hooded", Type = CosmeticType.Character },
                new CosmeticItem { Id = "char_knight", Type = CosmeticType.Character, PurchaseOnly = true, ProductId = "echo.char.knight", BundleProductId = "echo.char.pack" },
                new CosmeticItem { Id = "char_cat", Type = CosmeticType.Character, PurchaseOnly = true, ProductId = "echo.char.cat", BundleProductId = "echo.char.pack" },
                new CosmeticItem { Id = "char_pumpkin", Type = CosmeticType.Character, AdsToUnlock = 5 },
                new CosmeticItem { Id = "char_reaper", Type = CosmeticType.Character, AdsToUnlock = 10 },
            };
            return c;
        }

        [Test]
        public void Earn_UnlocksByBestScore_AndReportsOnlyNewMilestones()
        {
            var catalog = Catalog();
            var owned = new List<string>();
            var first = CosmeticUnlocks.Earn(catalog, 50, owned);
            Assert.IsEmpty(first, "free items are owned silently");
            CollectionAssert.AreEquivalent(new[] { "runner_default", "trail_default", "char_hooded" }, owned);

            var second = CosmeticUnlocks.Earn(catalog, 120, owned);
            Assert.AreEqual("runner_mint", second.Single().Id);
            Assert.IsEmpty(CosmeticUnlocks.Earn(catalog, 120, owned));
        }

        [Test]
        public void ProductItems_CanStillBeEarnedByPlay()
        {
            var catalog = Catalog();
            var owned = new List<string>();
            CosmeticUnlocks.Earn(catalog, 3000, owned);
            Assert.Contains("runner_gold", owned);
        }

        [Test]
        public void Characters_AreNeverEarnedByPlay()
        {
            var catalog = Catalog();
            var owned = new List<string>();
            CosmeticUnlocks.Earn(catalog, 1_000_000, owned);
            CollectionAssert.DoesNotContain(owned, "char_knight");
            Assert.IsFalse(CosmeticUnlocks.IsOwned(catalog.Find("char_knight"), 1_000_000, owned));
            Assert.IsTrue(CosmeticUnlocks.IsOwned(catalog.Find("char_hooded"), 0, owned), "the hooded runner is free");
        }

        [Test]
        public void Characters_UnlockByTheirOwnProduct_OrThePack()
        {
            var catalog = Catalog();
            var owned = new List<string>();
            Assert.IsTrue(CosmeticUnlocks.Grant(catalog, "echo.char.knight", owned));
            CollectionAssert.AreEquivalent(new[] { "char_knight" }, owned);
            Assert.IsFalse(CosmeticUnlocks.Grant(catalog, "echo.char.knight", owned), "already owned");

            Assert.IsTrue(CosmeticUnlocks.Grant(catalog, "echo.char.pack", owned));
            CollectionAssert.AreEquivalent(new[] { "char_knight", "char_cat" }, owned);
            Assert.IsFalse(CosmeticUnlocks.Grant(catalog, "", owned), "an empty product id unlocks nothing");
        }

        [Test]
        public void AdCharacters_UnlockAtTheirAdCount_NeverByScore()
        {
            var catalog = Catalog();
            var owned = new List<string>();
            CosmeticUnlocks.Earn(catalog, 1_000_000, owned);
            CollectionAssert.DoesNotContain(owned, "char_pumpkin");
            Assert.IsFalse(CosmeticUnlocks.IsOwned(catalog.Find("char_pumpkin"), 1_000_000, owned));

            Assert.IsEmpty(CosmeticUnlocks.EarnByAds(catalog, 4, owned));
            Assert.AreEqual("char_pumpkin", CosmeticUnlocks.EarnByAds(catalog, 5, owned).Single().Id);
            Assert.IsEmpty(CosmeticUnlocks.EarnByAds(catalog, 9, owned), "already owned, reaper not yet");
            Assert.AreEqual("char_reaper", CosmeticUnlocks.EarnByAds(catalog, 10, owned).Single().Id);
            Assert.IsFalse(CosmeticUnlocks.Grant(catalog, "echo.char.pack", new List<string> { "char_knight", "char_cat" }),
                "the Character Pack does not include ad characters");
        }

        [Test]
        public void Resolve_NeverFallsBackToAPurchaseOnlyCharacter()
        {
            var catalog = Catalog();
            Assert.AreEqual("char_hooded", catalog.Resolve("missing", CosmeticType.Character).Id, "not an ad or paid one");
            Assert.AreEqual("char_cat", catalog.Resolve("char_cat", CosmeticType.Character).Id);
        }

        [Test]
        public void Resolve_FallsBackToTheFreeItemOfThatType()
        {
            var catalog = Catalog();
            Assert.AreEqual("runner_mint", catalog.Resolve("runner_mint", CosmeticType.Runner).Id);
            Assert.AreEqual("runner_default", catalog.Resolve("missing", CosmeticType.Runner).Id);
            Assert.AreEqual("trail_default", catalog.Resolve("runner_mint", CosmeticType.Trail).Id, "wrong type");
        }

        [Test]
        public void Palette_BlendsEvery500m_AndHoldsTheLastStop()
        {
            var stops = new[] { Color.red, Color.green, Color.blue };
            Assert.AreEqual(Color.red, PaletteSampler.Sample(stops, 0));
            Assert.AreEqual(Color.Lerp(Color.red, Color.green, 0.5f), PaletteSampler.Sample(stops, 250));
            Assert.AreEqual(Color.green, PaletteSampler.Sample(stops, 500));
            Assert.AreEqual(Color.blue, PaletteSampler.Sample(stops, 1000));
            Assert.AreEqual(Color.blue, PaletteSampler.Sample(stops, 5000));
        }

        [Test]
        public void Palette_DarkerKeepsHue()
        {
            var bg = new Color(0.2f, 0.6f, 0.7f);
            Color.RGBToHSV(bg, out float h1, out _, out float v1);
            Color.RGBToHSV(PaletteSampler.Darker(bg, 0.7f), out float h2, out _, out float v2);
            Assert.AreEqual(h1, h2, 1e-3f);
            Assert.AreEqual(v1 * 0.7f, v2, 1e-3f);
        }

        // tech spec 12: stem k (1-5) joins once 5k ghosts are on screen.
        [TestCase(0, 0, true)]
        [TestCase(1, 4, false)]
        [TestCase(1, 5, true)]
        [TestCase(3, 14, false)]
        [TestCase(3, 15, true)]
        [TestCase(5, 25, true)]
        [TestCase(5, 24, false)]
        public void MusicLayers_FollowGhostCount(int stem, int ghosts, bool wanted)
        {
            Assert.AreEqual(wanted, MusicPlayer.LayerWanted(stem, ghosts));
        }

        // Colour packs (a user revision: no longer sold): the score milestone still works, and so do enough ads.
        [Test]
        public void AColourPackItem_UnlocksByScore_OrByAds()
        {
            var neon = new CosmeticItem { Id = "runner_neon", Type = CosmeticType.Runner, UnlockScore = 1000, AdsToUnlock = 6, AdPack = "Neon" };
            Assert.IsTrue(neon.EarnedByScore);
            Assert.IsTrue(CosmeticUnlocks.IsOwned(neon, 1000, new List<string>()));
            Assert.IsFalse(CosmeticUnlocks.IsOwned(neon, 999, new List<string>()));

            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            catalog.Items = new[] { neon };
            var owned = new List<string>();
            Assert.AreEqual(0, CosmeticUnlocks.EarnByAds(catalog, 5, owned).Count);
            Assert.AreEqual(1, CosmeticUnlocks.EarnByAds(catalog, 6, owned).Count);
            Assert.IsTrue(CosmeticUnlocks.IsOwned(neon, 0, owned));
            Object.DestroyImmediate(catalog);
        }
    }
}
