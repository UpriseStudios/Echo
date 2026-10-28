using System.IO;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Generates every sprite as a PNG under Content/Art with its import settings: 16-bit pixel art at 32 px
    /// per world unit, point-filtered and uncompressed so pixels stay square. Characters come from
    /// <see cref="PixelCharacters"/> (frames and their ghost outlines); scenery from <see cref="PixelWorld"/>.
    /// </summary>
    public static class EchoArt
    {
        public const string Dir = "Assets/Echo/Content/Art";
        public const string CharDir = Dir + "/Characters";
        public const float WorldPpu = PixelRig.PixelsPerUnit;

        /// <summary>Ghost outline fill: the inside of a ghost is this faint (0-255).</summary>
        public const byte GhostFill = 34;

        public static string CharacterPath(string id, int frame) => $"{CharDir}/{id}_{frame}.png";
        public static string CharacterJumpPath(string id) => $"{CharDir}/{id}_jump.png";
        public static string GhostPath(string id, int frame) => $"{CharDir}/{id}_ghost_{frame}.png";
        public static string GhostJumpPath(string id) => $"{CharDir}/{id}_ghost_jump.png";

        public static string GroundTopPath => $"{Dir}/ground_top.png";
        public static string GroundFillPath => $"{Dir}/ground_fill.png";
        public static string SpikePath => $"{Dir}/spike.png";
        public static string BlockPath => $"{Dir}/block.png";
        public static string GravePath => $"{Dir}/grave.png";
        public static string MountainsPath => $"{Dir}/bg_mountains.png";
        public static string HillsPath => $"{Dir}/bg_hills.png";
        public static string GhostIconPath => $"{Dir}/ui_ghost_icon.png";
        public static string UiFramePath => $"{Dir}/ui_frame.png";
        public static string HeartIconPath => $"{Dir}/ui_heart.png";
        public static string HeadstonePath => $"{Dir}/ui_headstone.png";
        public static string ShieldIconPath => $"{Dir}/ui_power_shield.png";
        public static string PhaseIconPath => $"{Dir}/ui_power_phase.png";
        public static string LanternIconPath => $"{Dir}/ui_power_lantern.png";

        // Themed Biomes and the character flourishes.
        public static string GraveyardPath => $"{Dir}/bg_graveyard.png";
        public static string CastlePath => $"{Dir}/bg_castle.png";
        public static string CastleBannersPath => $"{Dir}/bg_castle_banners.png";
        public static string MoonSurfacePath => $"{Dir}/bg_moon_surface.png";
        public static string PlanetPath => $"{Dir}/fx_planet.png";
        public static string StarPath => $"{Dir}/fx_star.png";
        public static string ShootingStarPath => $"{Dir}/fx_shooting_star.png";
        public static string UfoPath => $"{Dir}/fx_ufo.png";
        public static string FullMoonPath => $"{Dir}/fx_full_moon.png";
        public static string BatPath(int frame) => $"{Dir}/fx_bat_{frame}.png";
        public static string GlowPath => $"{Dir}/fx_glow.png";
        public static string BeamPath => $"{Dir}/fx_beam.png";
        public static string FireflyPath => $"{Dir}/fx_firefly.png";
        public static string SparklePath => $"{Dir}/fx_sparkle.png";
        public static string WispPath => $"{Dir}/fx_wisp.png";
        public static string RainPath => $"{Dir}/fx_rain.png";
        public static string FogPath => $"{Dir}/fx_fog.png";
        public static string PetalPath => $"{Dir}/fx_petal.png";
        public static string BoltPath => $"{Dir}/fx_bolt.png";
        public static string ShimmerPath => $"{Dir}/fx_shimmer.png";
        public static string EmberPath => $"{Dir}/fx_ember.png";
        public static string FrostPath => $"{Dir}/fx_frost.png";
        public static string FrostCapPath => $"{Dir}/fx_frostcap.png";
        public static string IciclesPath => $"{Dir}/fx_icicles.png";
        public static string GlintPath => $"{Dir}/fx_glint.png";
        public static string TorchPath => $"{Dir}/fx_torch.png";
        public static string HornsPath => $"{Dir}/nemesis_horns.png";
        public static string CoinPath => $"{Dir}/bounty_coin.png";
        public static string SpotlightPath => $"{Dir}/lights_out.png";
        public static string IconPath => $"{Dir}/app_icon.png";
        public static string AdaptiveBackgroundPath => $"{Dir}/app_icon_adaptive_bg.png";
        public static string AdaptiveForegroundPath => $"{Dir}/app_icon_adaptive_fg.png";

        /// <summary>The 9-slice border of <see cref="UiFramePath"/>, in sprite pixels.</summary>
        public const int UiFrameBorder = 4;

        // The smooth vector-style art this pixel set replaced.
        static readonly string[] Obsolete =
        {
            "runner_0.png", "runner_1.png", "runner_2.png", "runner_3.png", "runner_jump.png",
            "ghost_0.png", "ghost_1.png", "ghost_2.png", "ghost_3.png", "ghost_jump.png",
            "ui_rounded.png", "soft_dot.png", "bg_monoliths.png",
        };

        [MenuItem("Echo/Generate Art")]
        public static void Generate()
        {
            EnsureFolder(Dir);
            EnsureFolder(CharDir);
            foreach (string name in Obsolete)
                if (File.Exists($"{Dir}/{name}")) AssetDatabase.DeleteAsset($"{Dir}/{name}");

            foreach (PixelCharacter c in PixelCharacters.All)
            {
                for (int f = 0; f < PixelRig.RunFrames; f++)
                {
                    PixelCanvas frame = PixelRig.Frame(c, f);
                    Write(CharacterPath(c.Id, f), frame);
                    Write(GhostPath(c.Id, f), frame.Hollow(GhostFill));
                }
                PixelCanvas jump = PixelRig.Jump(c);
                Write(CharacterJumpPath(c.Id), jump);
                Write(GhostJumpPath(c.Id), jump.Hollow(GhostFill));
            }

            Write(GroundTopPath, PixelWorld.GroundTop());
            Write(GroundFillPath, PixelWorld.GroundFill());
            Write(SpikePath, PixelWorld.Spike());
            Write(BlockPath, PixelWorld.Block());
            Write(GravePath, PixelWorld.Grave());
            Write(MountainsPath, PixelWorld.Mountains());
            Write(HillsPath, PixelWorld.Hills());
            Write(GhostIconPath, PixelWorld.GhostIcon());
            Write(UiFramePath, PixelWorld.UiFrame());
            Write(HeartIconPath, PixelWorld.Heart());
            Write(HeadstonePath, PixelWorld.Headstone());
            Write(ShieldIconPath, PixelWorld.ShieldIcon());
            Write(PhaseIconPath, PixelWorld.PhaseIcon());
            Write(LanternIconPath, PixelWorld.LanternIcon());
            Write(GraveyardPath, PixelWorld.GraveyardHills());
            Write(CastlePath, PixelWorld.CastleWalls());
            Write(CastleBannersPath, PixelWorld.CastleBanners());
            Write(MoonSurfacePath, PixelWorld.MoonSurface());
            Write(PlanetPath, PixelWorld.Planet());
            Write(StarPath, PixelWorld.Star());
            Write(ShootingStarPath, PixelWorld.ShootingStar());
            Write(UfoPath, PixelWorld.Ufo());
            Write(FullMoonPath, PixelWorld.FullMoon());
            Write(BatPath(0), PixelWorld.Bat(0));
            Write(BatPath(1), PixelWorld.Bat(1));
            Write(GlowPath, PixelWorld.Glow());
            Write(BeamPath, PixelWorld.Beam());
            Write(FireflyPath, PixelWorld.Dot(0xD8FF6A));
            Write(SparklePath, PixelWorld.Dot(0xFFE070));
            Write(WispPath, PixelWorld.Wisp());
            Write(RainPath, PixelWorld.RainDrop());
            Write(FogPath, PixelWorld.FogPuff());
            Write(PetalPath, PixelWorld.Petal());
            Write(BoltPath, PixelWorld.Bolt());
            Write(ShimmerPath, PixelWorld.Shimmer());
            Write(EmberPath, PixelWorld.Dot(0xFF9A3A));
            Write(FrostPath, PixelWorld.Dot(0xDDF6FF));
            Write(FrostCapPath, PixelWorld.FrostCap());
            Write(IciclesPath, PixelWorld.Icicles());
            Write(GlintPath, PixelWorld.Glint());
            Write(TorchPath, PixelWorld.Torch());
            Write(HornsPath, PixelWorld.Horns());
            Write(CoinPath, PixelWorld.Coin());
            Write(SpotlightPath, PixelWorld.Spotlight());
            Write(IconPath, PixelWorld.Icon());
            Write(AdaptiveBackgroundPath, PixelWorld.AdaptiveIconBackground());
            Write(AdaptiveForegroundPath, PixelWorld.AdaptiveIconForeground());

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var feet = new Vector2(0.5f, 0f);
            foreach (PixelCharacter c in PixelCharacters.All)
            {
                for (int f = 0; f < PixelRig.RunFrames; f++)
                {
                    ImportSprite(CharacterPath(c.Id, f), WorldPpu, feet);
                    ImportSprite(GhostPath(c.Id, f), WorldPpu, feet);
                }
                ImportSprite(CharacterJumpPath(c.Id), WorldPpu, feet);
                ImportSprite(GhostJumpPath(c.Id), WorldPpu, feet);
            }
            // Ground top: the walking line is 8 px up from the bottom of its 10 px.
            ImportSprite(GroundTopPath, WorldPpu, new Vector2(0f, 0.8f));
            ImportSprite(GroundFillPath, WorldPpu, new Vector2(0f, 1f));
            // Spikes are one unit square with a bottom-left pivot; CourseView scales them to each hazard box.
            ImportSprite(SpikePath, 16f, Vector2.zero);
            ImportSprite(BlockPath, WorldPpu, new Vector2(0.5f, 0.5f), new Vector4(4, 4, 4, 4));
            ImportSprite(GravePath, WorldPpu, new Vector2(0.5f, 2f / 16f));
            ImportSprite(MountainsPath, WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            ImportSprite(HillsPath, WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            ImportSprite(GhostIconPath, 16f, new Vector2(0.5f, 0.5f));
            ImportSprite(HeartIconPath, 16f, new Vector2(0.5f, 0.5f));
            ImportSprite(HeadstonePath, 100f, new Vector2(0.5f, 0f));
            foreach (string icon in new[] { ShieldIconPath, PhaseIconPath, LanternIconPath })
                ImportSprite(icon, 16f, new Vector2(0.5f, 0.5f));
            foreach (string layer in new[] { GraveyardPath, CastlePath, CastleBannersPath, MoonSurfacePath })
                ImportSprite(layer, WorldPpu, Vector2.zero, default, TextureWrapMode.Repeat);
            foreach (string fx in new[] { PlanetPath, StarPath, ShootingStarPath, UfoPath, FullMoonPath, BatPath(0), BatPath(1), GlowPath,
                         FireflyPath, SparklePath, WispPath, RainPath, FogPath, PetalPath, BoltPath, ShimmerPath, EmberPath, FrostPath, GlintPath })
                ImportSprite(fx, WorldPpu, new Vector2(0.5f, 0.5f));
            ImportSprite(BeamPath, WorldPpu, new Vector2(0f, 0.5f));
            // Bottom-left pivots, so these sit on scenery pixels exactly (the Elementals' per-biome touches).
            foreach (string fx in new[] { FrostCapPath, TorchPath })
                ImportSprite(fx, WorldPpu, Vector2.zero);
            ImportSprite(IciclesPath, WorldPpu, new Vector2(0f, 1f));
            ImportSprite(HornsPath, WorldPpu, new Vector2(0.5f, 1f / 7f));
            ImportSprite(CoinPath, WorldPpu, new Vector2(0.5f, 0f));
            ImportSprite(SpotlightPath, 8f, new Vector2(0.5f, 0.5f));
            ImportSprite(UiFramePath, 100f, new Vector2(0.5f, 0.5f), Vector4.one * UiFrameBorder);
            ImportIcon(IconPath);
            ImportIcon(AdaptiveBackgroundPath);
            ImportIcon(AdaptiveForegroundPath);
            SeasonArt.Generate();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static void Write(string path, PixelCanvas canvas)
        {
            var tex = canvas.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        internal static void ImportSprite(string path, float ppu, Vector2 pivot, Vector4 border = default, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void ImportIcon(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
