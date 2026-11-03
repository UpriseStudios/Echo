using System.Collections.Generic;
using System.IO;
using Echo.Game.Seasons;
using Echo.Game.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Seasons doc ground rules for every season theme: a reskin lands on the same box as the art it replaces (same
    /// size, pixels per unit, pivot and border), and a reskinned hazard keeps the original's exact outline and stays
    /// greyscale, so it is tinted the hazard colour like the original.
    /// </summary>
    public class SeasonArtTests
    {
        const string Art = "Assets/Echo/Content/Art/";

        static List<SeasonTheme> Themes()
        {
            var themes = new List<SeasonTheme>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SeasonTheme)))
                themes.Add(AssetDatabase.LoadAssetAtPath<SeasonTheme>(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.IsNotEmpty(themes, "run Echo/Build Scene first");
            return themes;
        }

        static Sprite Original(string file)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
            Assert.IsNotNull(s, file);
            return s;
        }

        static Color32[] Pixels(Sprite s)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(s.texture)));
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        static void SameBox(string theme, string what, Sprite reskin, Sprite original)
        {
            if (reskin == null) return;
            string at = $"{theme} {what}";
            Assert.AreEqual(original.rect.size, reskin.rect.size, at + ": size");
            Assert.AreEqual(original.pixelsPerUnit, reskin.pixelsPerUnit, at + ": pixels per unit");
            Assert.AreEqual(original.pivot, reskin.pivot, at + ": pivot");
            Assert.AreEqual(original.border, reskin.border, at + ": border");
            Assert.AreEqual(original.texture.wrapMode, reskin.texture.wrapMode, at + ": wrap");
            Assert.AreEqual(FilterMode.Point, reskin.texture.filterMode, at + ": point filtered");
        }

        static void SameOutline(string theme, string what, Sprite reskin, Sprite original, bool greyscale)
        {
            if (reskin == null) return;
            Color32[] a = Pixels(original), b = Pixels(reskin);
            Assert.AreEqual(a.Length, b.Length, $"{theme} {what}: size");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].a, b[i].a, $"{theme} {what}: pixel {i} changes the outline");
                if (greyscale && b[i].a > 0)
                    Assert.IsTrue(b[i].r == b[i].g && b[i].g == b[i].b, $"{theme} {what}: pixel {i} is coloured, so it wouldn't take the hazard tint");
            }
        }

        [Test]
        public void Reskins_LandOnTheSameBox_AndHazardsKeepTheirOutline()
        {
            foreach (SeasonTheme t in Themes())
            {
                Assert.IsFalse(string.IsNullOrEmpty(t.Id), t.name);
                SameBox(t.Id, "spike", t.Spike, Original("spike.png"));
                SameBox(t.Id, "block", t.Block, Original("block.png"));
                SameBox(t.Id, "ground top", t.GroundTop, Original("ground_top.png"));
                SameBox(t.Id, "far layer", t.FarLayer, Original("bg_mountains.png"));
                SameBox(t.Id, "near layer", t.NearLayer, Original("bg_graveyard.png"));
                SameBox(t.Id, "grave", t.Grave, Original("grave.png"));
                SameBox(t.Id, "headstone", t.Headstone, Original("ui_headstone.png"));

                SameOutline(t.Id, "spike", t.Spike, Original("spike.png"), greyscale: true);
                SameOutline(t.Id, "block", t.Block, Original("block.png"), greyscale: true);
                SameOutline(t.Id, "ground top", t.GroundTop, Original("ground_top.png"), greyscale: true);
                // The epitaph is carved on the headstone's face, so its shape stays the stone's.
                SameOutline(t.Id, "headstone", t.Headstone, Original("ui_headstone.png"), greyscale: false);
            }
        }

        /// <summary>
        /// World: Time Run, phase 2: each era's pit tiles down the gap like the floor fill, its coin lands where the gold
        /// coin did, and the fossil Nemesis's skull sits on the head like the horns and is red (the Nemesis is always red
        /// and horned). The eras' spikes, blocks and graves are checked with every other reskin above.
        /// </summary>
        [Test]
        public void TimeRunEras_PitsCoinsAndTheFossilSkull()
        {
            SeasonTheme world = Themes().Find(x => x.Id == SeasonIds.TimeRun);
            Assert.IsNotNull(world);
            Assert.AreEqual(4, world.Eras.Length);
            Sprite fill = Original("ground_fill.png"), coin = Original("bounty_coin.png"), horns = Original("nemesis_horns.png");
            foreach (SeasonTheme era in world.Eras)
            {
                Assert.IsNotNull(era.Gap, era.Id + ": a pit");
                Assert.AreEqual(fill.pixelsPerUnit, era.Gap.pixelsPerUnit, era.Id);
                Assert.AreEqual(new Vector2(0f, era.Gap.rect.height), era.Gap.pivot, era.Id + ": hangs from its top-left, like the fill");
                Assert.AreEqual(TextureWrapMode.Repeat, era.Gap.texture.wrapMode, era.Id + ": tiles down the pit");
                if (era.Coin != null)
                {
                    Assert.AreEqual(coin.pixelsPerUnit, era.Coin.pixelsPerUnit, era.Id);
                    Assert.AreEqual(coin.pivot / coin.rect.size, era.Coin.pivot / era.Coin.rect.size, era.Id + ": the coin's pivot");
                }
                if (era.Grave != null) Assert.IsTrue(era.GraveInColour || IsGrey(era.Grave), era.Id + ": a stone grave takes the tint");
            }

            Sprite skull = world.Eras[0].NemesisAccessory;
            Assert.IsNotNull(skull, "the fossil Nemesis");
            Assert.AreEqual(horns.pixelsPerUnit, skull.pixelsPerUnit);
            int red = 0, opaque = 0;
            foreach (Color32 p in Pixels(skull))
            {
                if (p.a == 0) continue;
                opaque++;
                if (p.r > p.g + 40 && p.r > p.b + 40) red++;
            }
            Assert.Greater(red * 2, opaque, "the skull is mostly red");
            for (int i = 1; i < 4; i++) Assert.IsNull(world.Eras[i].NemesisAccessory, "the red horns in the other eras");
        }

        static bool IsGrey(Sprite s)
        {
            foreach (Color32 p in Pixels(s))
                if (p.a > 0 && (p.r != p.g || p.g != p.b)) return false;
            return true;
        }

        [Test]
        public void Effects_AreSetUpToShow()
        {
            foreach (SeasonTheme t in Themes())
            {
                if (t.Effect == SeasonEffect.None) continue;
                Assert.IsNotEmpty(t.EffectSprites, t.Id);
                foreach (Sprite s in t.EffectSprites) Assert.IsNotNull(s, t.Id);
                Assert.That(t.EffectCount, Is.InRange(1, SeasonEffectsView.MaxParticles), t.Id);
            }
        }

        [Test]
        public void TheGraveyardsLook_DriftsFogLow_AndIsNeverScheduled()
        {
            SeasonTheme t = Themes().Find(x => x.Id == SeasonIds.Graveyard);
            Assert.IsNotNull(t, "the Community Graveyard's look");
            Assert.AreEqual(SeasonEffect.Drifting, t.Effect);
            Assert.Less(t.EffectBand, 0.5f, "fog keeps low");
            Assert.IsNotNull(t.FarLayer);
            Assert.IsNotNull(t.NearLayer);
            Assert.IsNull(t.Spike, "obstacles are the normal ones");
            Assert.IsEmpty(t.MusicStems, "the music follows the season");
            string schedule = File.ReadAllText("Assets/Echo/Content/Seasons/seasons.json");
            StringAssert.DoesNotContain(SeasonIds.Graveyard, schedule);
        }

        [Test]
        public void ChristmasAndNewYear_HaveTheirOwnCarols_SixStemsInStep()
        {
            foreach (string id in new[] { SeasonIds.Christmas, SeasonIds.NewYear })
            {
                SeasonTheme t = Themes().Find(x => x.Id == id);
                Assert.AreEqual(6, t.MusicStems.Length, id + ": six stems, like the main tune");
                foreach (AudioClip clip in t.MusicStems)
                {
                    Assert.IsNotNull(clip, id);
                    Assert.AreEqual(t.MusicStems[0].samples, clip.samples, id + ": every stem the same length, so they stay in step");
                }
            }
            SeasonTheme christmas = Themes().Find(x => x.Id == SeasonIds.Christmas), newYear = Themes().Find(x => x.Id == SeasonIds.NewYear);
            Assert.AreEqual(8 * 4 * 60f / 132f, christmas.MusicStems[0].length, 0.01f, "Jingle Bells: 8 bars at 132 bpm");
            Assert.AreEqual(8 * 4 * 60f / 96f, newYear.MusicStems[0].length, 0.01f, "Auld Lang Syne: 8 bars at 96 bpm");
            StringAssert.Contains("melody", christmas.MusicStems[0].name, "the tune is stem 0, which always plays");
            Assert.IsNotNull(christmas.Flyer, "Santa's sleigh crosses the Christmas sky");
            Assert.IsNull(newYear.Flyer);
            Assert.IsTrue(Themes().FindAll(x => x.Id != SeasonIds.Christmas && x.Id != SeasonIds.NewYear).TrueForAll(x => x.MusicStems.Length == 0));
        }

        [Test]
        public void TheTestTheme_TurnsEveryOverrideOn()
        {
            SeasonTheme t = Themes().Find(x => x.Id == SeasonIds.Test);
            Assert.IsNotNull(t, "the test theme");
            Assert.IsNotEmpty(t.PaletteStops);
            Assert.IsNotNull(t.FarLayer);
            Assert.IsNotNull(t.NearLayer);
            Assert.AreEqual(SeasonEffect.Falling, t.Effect);
            Assert.IsNotNull(t.Spike);
            Assert.IsNotNull(t.Block);
            Assert.IsNotNull(t.GroundTop);
            Assert.IsNotNull(t.Grave);
            Assert.IsNotNull(t.Headstone);
            Assert.IsNotNull(t.GhostAccessory);
            Assert.IsNotNull(t.NemesisAccessory);
            Assert.IsNotEmpty(t.DeathLine);
            Assert.IsNotNull(t.Stinger);
        }
    }
}
