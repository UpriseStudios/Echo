using UnityEditor;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Writes each season's sprites under Content/Art/Seasons (seasons doc), imported exactly like the art they replace
    /// (same pixels per unit, pivot, border and wrap), so a reskin lands on the same box. Run by EchoArt.Generate.
    /// </summary>
    public static class SeasonArt
    {
        public const string Dir = EchoArt.Dir + "/Seasons";

        public static string PathFor(string season, string part) => $"{Dir}/{season}_{part}.png";

        public const string Spike = "spike", Block = "block", GroundTop = "ground_top", Far = "far", Near = "near",
            Grave = "grave", Headstone = "headstone", GhostAccessory = "ghost_accessory", NemesisAccessory = "nemesis_accessory";

        public static string EffectPath(string season, int i) => PathFor(season, "fx_" + i);

        /// <summary>The Heart gravestone, a cosmetic (It's Complicated's reward).</summary>
        public static string HeartGravePath => $"{Dir}/grave_heart.png";

        /// <summary>The Halo, Naughty vs Nice's reward for the winning Nice side (a runner accessory).</summary>
        public static string HaloPath => $"{Dir}/acc_halo.png";

        /// <summary>One Life's Christmas Day 2026 gravestones (finishers; the top 3 in gold).</summary>
        public static string ChristmasDayGravePath(bool gold) => $"{Dir}/grave_xmasday_2026{(gold ? "_gold" : "")}.png";

        /// <summary>A firework spark, shared by every season with fireworks.</summary>
        public static string SparkPath => $"{Dir}/fx_spark.png";

        public static void Generate()
        {
            EchoArt.EnsureFolder(Dir);
            const string t = Game.Seasons.SeasonIds.Test;
            EchoArt.Write(PathFor(t, Spike), PixelSeasons.TestSpike());
            EchoArt.Write(PathFor(t, Block), PixelSeasons.TestBlock());
            EchoArt.Write(PathFor(t, GroundTop), PixelSeasons.TestGroundTop());
            EchoArt.Write(PathFor(t, Far), PixelSeasons.TestFar());
            EchoArt.Write(PathFor(t, Near), PixelSeasons.TestNear());
            EchoArt.Write(PathFor(t, Grave), PixelSeasons.TestGrave());
            EchoArt.Write(PathFor(t, Headstone), PixelSeasons.TestHeadstone());
            EchoArt.Write(PathFor(t, GhostAccessory), PixelSeasons.TestGhostAccessory());
            EchoArt.Write(PathFor(t, NemesisAccessory), PixelSeasons.TestNemesisAccessory());
            EchoArt.Write(EffectPath(t, 0), PixelSeasons.Confetti(0));
            EchoArt.Write(EffectPath(t, 1), PixelSeasons.Confetti(1));
            // Christmas; New Year wears the same snow and adds fireworks.
            const string x = Game.Seasons.SeasonIds.Christmas;
            EchoArt.Write(PathFor(x, Spike), PixelSeasons.ChristmasSpike());
            EchoArt.Write(PathFor(x, Block), PixelSeasons.ChristmasBlock());
            EchoArt.Write(PathFor(x, GroundTop), PixelSeasons.ChristmasGroundTop());
            EchoArt.Write(PathFor(x, Far), PixelSeasons.ChristmasFar());
            EchoArt.Write(PathFor(x, Near), PixelSeasons.ChristmasNear());
            EchoArt.Write(PathFor(x, Grave), PixelSeasons.Snowman());
            EchoArt.Write(PathFor(x, Headstone), PixelSeasons.ChristmasHeadstone());
            EchoArt.Write(PathFor(x, GhostAccessory), PixelSeasons.SantaHat());
            EchoArt.Write(PathFor(x, NemesisAccessory), PixelSeasons.RedNose());
            EchoArt.Write(EffectPath(x, 0), PixelSeasons.Snowflake(0));
            EchoArt.Write(EffectPath(x, 1), PixelSeasons.Snowflake(1));
            EchoArt.Write(SparkPath, PixelSeasons.Spark());
            // Valentine's, and the Heart gravestone (a cosmetic earned then, worn all year).
            const string v = Game.Seasons.SeasonIds.Valentines;
            EchoArt.Write(PathFor(v, NemesisAccessory), PixelSeasons.NemesisHeart());
            EchoArt.Write(HeartGravePath, PixelSeasons.HeartGrave());
            EchoArt.Write(HaloPath, PixelSeasons.Halo());
            EchoArt.Write(ChristmasDayGravePath(false), PixelSeasons.ChristmasDayGrave(false));
            EchoArt.Write(ChristmasDayGravePath(true), PixelSeasons.ChristmasDayGrave(true));
            // Halloween (switched off until next year).
            const string h = Game.Seasons.SeasonIds.Halloween;
            EchoArt.Write(PathFor(h, Grave), PixelSeasons.PumpkinGrave());
            EchoArt.Write(PathFor(h, GhostAccessory), PixelSeasons.Cobweb());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ImportAs(x);
            // The hat's trim sits on the head; the nose lands on the face, 5 px forward and 6 down from the head's top.
            EchoArt.ImportSprite(PathFor(x, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 9f));
            EchoArt.ImportSprite(PathFor(x, NemesisAccessory), EchoArt.WorldPpu, new Vector2((2.5f - 5f) / 5f, (2.5f + 6f) / 5f));
            foreach (string fx in new[] { EffectPath(x, 0), EffectPath(x, 1), SparkPath })
                EchoArt.ImportSprite(fx, EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));

            EchoArt.ImportSprite(PathFor(v, NemesisAccessory), EchoArt.WorldPpu, new Vector2(0.5f, -1f / 9f));
            EchoArt.ImportSprite(HeartGravePath, EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
            // Floats 2 px over the top of the head.
            EchoArt.ImportSprite(HaloPath, EchoArt.WorldPpu, new Vector2(0.5f, -2f / 6f));
            foreach (bool gold in new[] { false, true })
                EchoArt.ImportSprite(ChristmasDayGravePath(gold), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
            EchoArt.ImportSprite(PathFor(h, Grave), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
            // The web's top row sits 3 px above the head's top, so it drapes down over the back of the head.
            EchoArt.ImportSprite(PathFor(h, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 5f / 9f));

            ImportAs(t);
            EchoArt.ImportSprite(PathFor(t, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 10f));
            EchoArt.ImportSprite(PathFor(t, NemesisAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 9f));
            EchoArt.ImportSprite(EffectPath(t, 0), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            EchoArt.ImportSprite(EffectPath(t, 1), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
        }

        // The reskins, with the originals' import settings (EchoArt.Generate).
        static void ImportAs(string season)
        {
            EchoArt.ImportSprite(PathFor(season, Spike), 16f, Vector2.zero);
            EchoArt.ImportSprite(PathFor(season, Block), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f), new Vector4(4, 4, 4, 4));
            EchoArt.ImportSprite(PathFor(season, GroundTop), EchoArt.WorldPpu, new Vector2(0f, 0.8f));
            EchoArt.ImportSprite(PathFor(season, Far), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(season, Near), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(season, Grave), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
            EchoArt.ImportSprite(PathFor(season, Headstone), 100f, new Vector2(0.5f, 0f));
        }
    }
}
