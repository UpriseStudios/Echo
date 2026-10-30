using System.Collections.Generic;
using Echo.Game.Seasons;
using Echo.Game.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// A user revision (device testing: spikes vanished in Neon Night, gaps were hard to pick out): every world keeps
    /// its hazards and floor readable against its own sky, in distinct colours rather than shades of the sky, and the
    /// game's guard pushes any colour too close to the sky apart from it.
    /// </summary>
    public class WorldContrastTests
    {
        static List<SeasonTheme> Themes()
        {
            var themes = new List<SeasonTheme>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SeasonTheme)))
                themes.Add(AssetDatabase.LoadAssetAtPath<SeasonTheme>(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.IsNotEmpty(themes, "run Echo/Build Scene first");
            return themes;
        }

        static float Hue(Color c)
        {
            Color.RGBToHSV(c, out float h, out _, out _);
            return h;
        }

        static float HueGap(Color a, Color b)
        {
            float d = Mathf.Abs(Hue(a) - Hue(b));
            return Mathf.Min(d, 1f - d);
        }

        [Test]
        public void TheGuard_PushesAColourClearOfTheSky()
        {
            Color sky = new Color(0.23f, 0.1f, 0.35f);
            Color dull = new Color(0.2f, 0.1f, 0.3f);
            Color apart = ColourContrast.Apart(dull, sky, ColourContrast.HazardMin);
            Assert.GreaterOrEqual(Mathf.Abs(ColourContrast.Luminance(apart) - ColourContrast.Luminance(sky)), ColourContrast.HazardMin - 0.001f);
            Assert.Greater(ColourContrast.Luminance(apart), ColourContrast.Luminance(dull), "on a dark sky it goes lighter");
            Color bright = new Color(1f, 0.9f, 0.2f);
            Assert.AreEqual(bright, ColourContrast.Apart(bright, sky, ColourContrast.HazardMin), "a colour that already stands out is left alone");
            Color paleSky = new Color(0.85f, 0.9f, 0.95f);
            Assert.Less(ColourContrast.Luminance(ColourContrast.Apart(new Color(0.8f, 0.85f, 0.9f), paleSky, ColourContrast.FloorMin)),
                ColourContrast.Luminance(paleSky), "on a light sky it goes darker");
        }

        [Test]
        public void EveryWorld_HasItsOwnHazardAndFloorColours_ThatReadAgainstItsSky()
        {
            foreach (SeasonTheme t in Themes())
            {
                if (t.FarLayer == null) continue;   // a season without its own backdrop keeps the derived colours
                Assert.Greater(t.HazardColour.a, 0f, t.Id + ": its own hazard colour");
                Assert.Greater(t.FloorColour.a, 0f, t.Id + ": its own floor colour");
                // A world's own hazard colour is used as it is, so it must stand out from every sky colour by brightness, or
                // clearly by colour (Autumn's frost-cyan spikes against an orange sky).
                foreach (Color sky in t.PaletteStops)
                    Assert.IsTrue(Mathf.Abs(ColourContrast.Luminance(t.HazardColour) - ColourContrast.Luminance(sky)) >= ColourContrast.HazardMin
                                  || HueGap(t.HazardColour, sky) > 0.25f, t.Id + ": hazards stand out from the sky");
                // Spikes on the ground sit in front of the near layer, so they must stand out from it too: clearly lighter or
                // darker, or a strongly different colour.
                if (t.NearTint.a > 0f && t.NearTint != Color.white)   // white: a layer painted in its own colours
                    Assert.IsTrue(Mathf.Abs(ColourContrast.Luminance(t.HazardColour) - ColourContrast.Luminance(t.NearTint)) > 0.25f
                                  || HueGap(t.HazardColour, t.NearTint) > 0.25f, t.Id + ": hazards stand out from the near layer behind them");
                // Distinct colours, not shades: the hazard differs from the floor in hue or clearly in brightness.
                bool hueApart = HueGap(t.HazardColour, t.FloorColour) > 0.08f;
                bool lightApart = Mathf.Abs(ColourContrast.Luminance(t.HazardColour) - ColourContrast.Luminance(t.FloorColour)) > 0.25f;
                Assert.IsTrue(hueApart || lightApart, t.Id + ": hazards stand out from the floor they sit on");
            }
        }
    }
}
