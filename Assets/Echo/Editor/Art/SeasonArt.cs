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

        /// <summary>Santa's sleigh crossing the Christmas sky (a user revision).</summary>
        public static string SleighPath => $"{Dir}/fx_sleigh.png";

        /// <summary>A world's or season's silhouette crossing the sky (a user revision).</summary>
        public static string FlyerPath(string id) => PathFor(id, "flyer");

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
            EchoArt.Write(SleighPath, PixelChristmasTown.Sleigh());
            // Valentine's, and the Heart gravestone (a cosmetic earned then, worn all year).
            const string v = Game.Seasons.SeasonIds.Valentines;
            EchoArt.Write(PathFor(v, NemesisAccessory), PixelSeasons.NemesisHeart());
            EchoArt.Write(HeartGravePath, PixelSeasons.HeartGrave());
            EchoArt.Write(HaloPath, PixelSeasons.Halo());
            EchoArt.Write(ChristmasDayGravePath(false), PixelSeasons.ChristmasDayGrave(false));
            EchoArt.Write(ChristmasDayGravePath(true), PixelSeasons.ChristmasDayGrave(true));
            // A user revision: the Community Graveyard's own look.
            const string g = Game.Seasons.SeasonIds.Graveyard;
            EchoArt.Write(PathFor(g, Far), PixelModeArt.GraveyardFar());
            EchoArt.Write(PathFor(g, Near), PixelModeArt.GraveyardNear());
            EchoArt.Write(EffectPath(g, 0), PixelModeArt.Fog(0));
            EchoArt.Write(EffectPath(g, 1), PixelModeArt.Fog(1));
            EchoArt.Write(EffectPath(g, 2), PixelModeArt.Wisp());
            // A user revision: the store's worlds, and Halloween Night (the Halloween season's look, and a world).
            const string n = Game.Seasons.SeasonIds.NeonNight, s = Game.Seasons.SeasonIds.SunkenRuins, au = Game.Seasons.SeasonIds.AutumnWoods,
                hw = Game.Seasons.SeasonIds.Halloween;
            WriteWorld(n, PixelWorldsArt.NeonFar(), PixelWorldsArt.NeonNear(), PixelWorldsArt.HoverCar(), PixelWorldsArt.RainStreak());
            WriteWorld(s, PixelWorldsArt.SunkenFar(), PixelWorldsArt.SunkenNear(), PixelWorldsArt.Whale(), PixelWorldsArt.Bubble(0), PixelWorldsArt.Bubble(1));
            WriteWorld(au, PixelWorldsArt.AutumnFar(), PixelWorldsArt.AutumnNear(), PixelWorldsArt.Geese(), PixelWorldsArt.Leaf(0), PixelWorldsArt.Leaf(1));
            WriteWorld(hw, PixelWorldsArt.HalloweenFar(), PixelWorldsArt.HalloweenNear(), PixelWorldsArt.Witch(), PixelWorldsArt.Bat());
            // A user revision: four more, painted in their own colours.
            const string be = Game.Seasons.SeasonIds.Beach, de = Game.Seasons.SeasonIds.DesertDunes, vo = Game.Seasons.SeasonIds.VolcanicWaste,
                to = Game.Seasons.SeasonIds.ToyTown;
            WriteWorld(be, PixelColourWorlds.BeachFar(), PixelColourWorlds.BeachNear(), PixelColourWorlds.PlaneWithBanner(), PixelColourWorlds.Gull());
            WriteWorld(de, PixelColourWorlds.DesertFar(), PixelColourWorlds.DesertNear(), PixelColourWorlds.Vulture(), PixelColourWorlds.SandWisp());
            WriteWorld(vo, PixelColourWorlds.VolcanoFar(), PixelColourWorlds.VolcanoNear(), PixelColourWorlds.LavaBomb(), PixelColourWorlds.Ash(0), PixelColourWorlds.Ash(1));
            WriteWorld(to, PixelColourWorlds.ToyFar(), PixelColourWorlds.ToyNear(), PixelColourWorlds.Balloon(), PixelColourWorlds.Star(), PixelColourWorlds.Heart());
            // Halloween (switched off until next year).
            const string h = Game.Seasons.SeasonIds.Halloween;
            EchoArt.Write(PathFor(h, Grave), PixelSeasons.PumpkinGrave());
            EchoArt.Write(PathFor(h, GhostAccessory), PixelSeasons.Cobweb());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ImportAs(x);
            // The hat's trim sits on the head; the nose lands on the face, 5 px forward and 6 down from the head's top.
            EchoArt.ImportSprite(PathFor(x, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 9f));
            EchoArt.ImportSprite(PathFor(x, NemesisAccessory), EchoArt.WorldPpu, new Vector2((2.5f - 5f) / 5f, (2.5f + 6f) / 5f));
            foreach (string fx in new[] { EffectPath(x, 0), EffectPath(x, 1), SparkPath, SleighPath })
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

            EchoArt.ImportSprite(PathFor(g, Far), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(g, Near), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            for (int i = 0; i < 3; i++) EchoArt.ImportSprite(EffectPath(g, i), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));

            foreach (var (id, effects) in new[] { (n, 1), (s, 2), (au, 2), (hw, 1), (be, 1), (de, 1), (vo, 2), (to, 2) })
            {
                EchoArt.ImportSprite(PathFor(id, Far), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
                EchoArt.ImportSprite(PathFor(id, Near), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
                EchoArt.ImportSprite(FlyerPath(id), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < effects; i++) EchoArt.ImportSprite(EffectPath(id, i), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            }

            ImportAs(t);
            EchoArt.ImportSprite(PathFor(t, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 10f));
            EchoArt.ImportSprite(PathFor(t, NemesisAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 9f));
            EchoArt.ImportSprite(EffectPath(t, 0), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            EchoArt.ImportSprite(EffectPath(t, 1), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
        }

        static void WriteWorld(string id, PixelCanvas far, PixelCanvas near, PixelCanvas flyer, params PixelCanvas[] effects)
        {
            EchoArt.Write(PathFor(id, Far), far);
            EchoArt.Write(PathFor(id, Near), near);
            EchoArt.Write(FlyerPath(id), flyer);
            for (int i = 0; i < effects.Length; i++) EchoArt.Write(EffectPath(id, i), effects[i]);
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
