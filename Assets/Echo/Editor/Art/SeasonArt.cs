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
        public static string FossilGravePath => $"{Dir}/grave_fossil.png";

        /// <summary>The Halo, Naughty vs Nice's reward for the winning Nice side (a runner accessory).</summary>
        public static string HaloPath => $"{Dir}/acc_halo.png";

        /// <summary>One Life's Christmas Day 2026 gravestones (finishers; the top 3 in gold).</summary>
        public static string ChristmasDayGravePath(bool gold) => $"{Dir}/grave_xmasday_2026{(gold ? "_gold" : "")}.png";

        /// <summary>Santa's sleigh crossing the Christmas sky (a user revision).</summary>
        public static string SleighPath => $"{Dir}/fx_sleigh.png";

        /// <summary>A world's or season's silhouette crossing the sky (a user revision).</summary>
        public static string FlyerPath(string id) => PathFor(id, "flyer");

        /// <summary>The Easter egg hunt's egg (seasons doc).</summary>
        public static string EggPath => $"{Dir}/fx_egg.png";

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
            EchoArt.Write(FossilGravePath, PixelTimeRunCast.FossilGrave());
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
            // Easter: painted-egg spikes, an egg gravestone, bunny ears, blossom, and the egg hunt's egg.
            const string ea = Game.Seasons.SeasonIds.Easter;
            EchoArt.Write(PathFor(ea, Spike), PixelEaster.Spike());
            EchoArt.Write(PathFor(ea, Grave), PixelEaster.Grave());
            EchoArt.Write(PathFor(ea, GhostAccessory), PixelEaster.BunnyEars());
            EchoArt.Write(EffectPath(ea, 0), PixelEaster.Petal(0));
            EchoArt.Write(EffectPath(ea, 1), PixelEaster.Petal(1));
            EchoArt.Write(EggPath, PixelEaster.Egg());
            // World: Time Run's eras (phase 2): each era's hazards, gap, grave and coin, and the fossil Nemesis.
            WriteEras();

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
            EchoArt.ImportSprite(FossilGravePath, EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
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

            EchoArt.ImportSprite(PathFor(ea, Spike), 16f, Vector2.zero);
            EchoArt.ImportSprite(PathFor(ea, Grave), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
            // The ears' base sits over the top of the head, like the Santa hat's trim.
            EchoArt.ImportSprite(PathFor(ea, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 11f));
            foreach (string fx in new[] { EffectPath(ea, 0), EffectPath(ea, 1), EggPath })
                EchoArt.ImportSprite(fx, EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));

            ImportEras();
            ImportAs(t);
            EchoArt.ImportSprite(PathFor(t, GhostAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 10f));
            EchoArt.ImportSprite(PathFor(t, NemesisAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 9f));
            EchoArt.ImportSprite(EffectPath(t, 0), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            EchoArt.ImportSprite(EffectPath(t, 1), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
        }

        public const string Gap = "gap", Coin = "coin";

        static readonly (string Era, System.Func<PixelCanvas> Spike, System.Func<PixelCanvas> Block, System.Func<PixelCanvas> Gap,
            System.Func<PixelCanvas> Grave, System.Func<PixelCanvas> Coin)[] Eras =
        {
            (Game.Seasons.SeasonIds.EraDino, PixelTimeRun.RockSpike, PixelTimeRun.CliffBlock, PixelDino.Gap, PixelTimeRun.SlabGrave, PixelTimeRun.AmberCoin),
            (Game.Seasons.SeasonIds.EraMedieval, null, PixelTimeRun.PortcullisBlock, PixelTimeRun.MoatGap, null, null),
            (Game.Seasons.SeasonIds.EraToday, PixelTimeRun.ConeSpike, PixelTimeRun.BillboardBlock, PixelTimeRun.RooftopGap, PixelTimeRun.ModernGrave, null),
            (Game.Seasons.SeasonIds.EraFuture, PixelTimeRun.EnergySpike, PixelTimeRun.HoverBlock, PixelTimeRun.VoidGap, PixelTimeRun.HoloGrave, PixelTimeRun.DataChip),
        };

        public const string Sky = "sky", Moons = "moons", Back = "back", Mid = "mid", Floor = "floor", FloorTop = "floor_top", Ceiling = "ceiling";

        static void WriteEras()
        {
            // Phase 3, art styles: 8-bit Dino Land, Today's 32-bit layers and the Far Future in HD.
            string dino = Game.Seasons.SeasonIds.EraDino, today = Game.Seasons.SeasonIds.EraToday, future = Game.Seasons.SeasonIds.EraFuture;
            WriteWorld(dino, PixelDino.Far(), PixelDino.Near(), PixelDino.Pterodactyl(), PixelDino.Ember());
            // Art pass phase 2: Dino Land's own 8-bit floor, floor edge and lava pit, and its rock overhang.
            EchoArt.Write(PathFor(dino, Floor), PixelDino.Floor());
            EchoArt.Write(PathFor(dino, FloorTop), PixelDino.FloorTop());
            EchoArt.Write(PathFor(dino, Ceiling), PixelDino.Overhang());
            EchoArt.Write(PathFor(today, Back), PixelToday.BackSkyline());
            EchoArt.Write(PathFor(today, Mid), PixelToday.MidCity());
            WriteWorld(future, HdFuture.Far(), HdFuture.Near(), HdFuture.FlyingCar(), HdFuture.Rain());
            EchoArt.Write(PathFor(future, Sky), HdFuture.Sky());
            EchoArt.Write(PathFor(future, Back), HdFuture.Back());
            EchoArt.Write(PathFor(future, Moons), HdFuture.Moons());
            EchoArt.Write(PathFor(future, Floor), HdFuture.GridFloor());

            foreach (var e in Eras)
            {
                if (e.Spike != null) EchoArt.Write(PathFor(e.Era, Spike), e.Spike());
                if (e.Block != null) EchoArt.Write(PathFor(e.Era, Block), e.Block());
                if (e.Gap != null) EchoArt.Write(PathFor(e.Era, Gap), e.Gap());
                if (e.Grave != null) EchoArt.Write(PathFor(e.Era, Grave), e.Grave());
                if (e.Coin != null) EchoArt.Write(PathFor(e.Era, Coin), e.Coin());
            }
            EchoArt.Write(PathFor(Game.Seasons.SeasonIds.EraDino, NemesisAccessory), PixelTimeRun.DinoSkull());
        }

        /// <summary>Called after the refresh: each era's art with the original it stands in for's import settings.</summary>
        static void ImportEras()
        {
            string dino = Game.Seasons.SeasonIds.EraDino, today = Game.Seasons.SeasonIds.EraToday, future = Game.Seasons.SeasonIds.EraFuture;
            EchoArt.ImportSprite(PathFor(dino, Far), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(dino, Near), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(FlyerPath(dino), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            EchoArt.ImportSprite(EffectPath(dino, 0), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f));
            EchoArt.ImportSprite(PathFor(dino, Floor), EchoArt.WorldPpu, new Vector2(0f, 1f), default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(dino, FloorTop), EchoArt.WorldPpu, new Vector2(0f, 0.8f), default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(dino, Ceiling), EchoArt.WorldPpu, new Vector2(0f, 1f), default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(today, Back), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            EchoArt.ImportSprite(PathFor(today, Mid), EchoArt.WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            const FilterMode smooth = FilterMode.Bilinear;
            EchoArt.ImportSprite(PathFor(future, Far), HdFuture.Ppu, Vector2.zero, default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(PathFor(future, Near), HdFuture.Ppu, Vector2.zero, default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(PathFor(future, Back), HdFuture.Ppu, Vector2.zero, default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(PathFor(future, Moons), HdFuture.Ppu, Vector2.zero, default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(PathFor(future, Sky), 32f, Vector2.zero, default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(PathFor(future, Floor), 128f, new Vector2(0f, 1f), default, TextureWrapMode.Repeat, smooth);
            EchoArt.ImportSprite(FlyerPath(future), HdFuture.Ppu, new Vector2(0.5f, 0.5f), default, TextureWrapMode.Clamp, smooth);
            EchoArt.ImportSprite(EffectPath(future, 0), HdFuture.Ppu, new Vector2(0.5f, 0.5f), default, TextureWrapMode.Clamp, smooth);

            foreach (var e in Eras)
            {
                if (e.Spike != null) EchoArt.ImportSprite(PathFor(e.Era, Spike), 16f, Vector2.zero);
                if (e.Block != null) EchoArt.ImportSprite(PathFor(e.Era, Block), EchoArt.WorldPpu, new Vector2(0.5f, 0.5f), new Vector4(4, 4, 4, 4));
                if (e.Gap != null) EchoArt.ImportSprite(PathFor(e.Era, Gap), EchoArt.WorldPpu, new Vector2(0f, 1f), default, TextureWrapMode.Repeat);
                if (e.Grave != null) EchoArt.ImportSprite(PathFor(e.Era, Grave), EchoArt.WorldPpu, new Vector2(0.5f, 2f / 16f));
                if (e.Coin != null) EchoArt.ImportSprite(PathFor(e.Era, Coin), EchoArt.WorldPpu, new Vector2(0.5f, 0f));
            }
            // Like the horns: its base row over the top of the head.
            EchoArt.ImportSprite(PathFor(Game.Seasons.SeasonIds.EraDino, NemesisAccessory), EchoArt.WorldPpu, new Vector2(0.5f, 1f / 9f));
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
