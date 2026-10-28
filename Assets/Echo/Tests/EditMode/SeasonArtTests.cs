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
