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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

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
