using System.Collections.Generic;
using System.IO;
using Echo.Core.Achievements;
using Echo.Editor.Art;
using Echo.Game.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// Writes the cosmetic catalog (tech spec 16): 47 characters, 6 runner colours, 7 trails, 4 palettes.
    /// Colours, trails and palettes unlock by best score; the Neon, Mono and Gold colour packs (once sold, tech spec
    /// 15) also unlock after enough rewarded ads (a user revision). Characters follow the skins addendum: the hooded runner and the 8 base skins are free, Pumpkin
    /// unlocks after 5 rewarded ads (a user revision), and five more are earned from lifetime stats. The 32 pack
    /// characters (a user revision: sold at launch) are bought by pack or with the Season of Echo bundle.
    /// Rebuilt on every build.
    /// </summary>
    public static class CosmeticCatalogBuilder
    {
        public const string CatalogPath = "Assets/Echo/Content/Cosmetics/CosmeticCatalog.asset";
        // Colour packs (a user revision: no longer sold): a score milestone, or this many rewarded ads in total.
        const int MonoAds = 3, NeonAds = 6, GoldAds = 10;

        public static CosmeticCatalog Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            if (catalog == null)
            {
                string dir = Path.GetDirectoryName(CatalogPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Echo/Content", "Cosmetics");
                catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var items = new List<CosmeticItem>();
            foreach (PixelCharacter c in PixelCharacters.All) items.Add(Character(c));
            items.AddRange(new[]
            {
                Runner("runner_default", "White", 0, "", 1f, 1f, 1f),
                Runner("runner_mint", "Mint", 100, "", 0.62f, 1f, 0.82f),
                Runner("runner_sun", "Sunbeam", 250, "", 1f, 0.86f, 0.32f),
                Runner("runner_coral", "Coral", 500, "", 1f, 0.52f, 0.46f),
                Ads(Runner("runner_neon", "Neon", 1000, "", 0.35f, 1f, 0.35f), "Neon", NeonAds),
                Ads(Runner("runner_gold", "Gold", 3000, "", 1f, 0.78f, 0.18f), "Gold", GoldAds),

                Trail("trail_default", "Cyan", 0, "", 0.45f, 0.95f, 1f),
                // Skins addendum minor rewards, earned from lifetime stats.
                Stat(Trail("trail_starter", "Starter", 0, "", 0.91f, 0.96f, 1f), CosmeticUnlockStat.GhostsAtStart, 1, "Meet your first ghost"),
                Stat(Trail("trail_bone", "Bone", 0, "", 0.93f, 0.91f, 0.85f), CosmeticUnlockStat.Deaths, 50, "Die 50 times"),
                Stat(Trail("trail_prism", "Prism", 0, "", 0.73f, 0.55f, 1f), CosmeticUnlockStat.Distance, 500, "Reach 500 m"),
                Trail("trail_magenta", "Magenta", 150, "", 1f, 0.45f, 0.95f),
                Ads(Trail("trail_lime", "Lime", 400, "", 0.7f, 1f, 0.3f), "Neon", NeonAds),
                Ads(Trail("trail_gold", "Gold", 3000, "", 1f, 0.82f, 0.3f), "Gold", GoldAds),
                // Seasons doc: earned with seasonal achievements, kept for good.
                Seasonal(Trail("trail_candy_cane", "Candy Cane", 0, "", 1f, 0.94f, 0.94f), AchievementIds.SnowedUnder, "Christmas: Snowed Under",
                    true, Hex(0xE8283A), Hex(0xFFFFFF), Hex(0xE8283A), Hex(0xFFFFFF), Hex(0xE8283A), Hex(0xFFFFFF)),
                Seasonal(Trail("trail_fireworks", "Fireworks", 0, "", 1f, 0.86f, 0.45f), AchievementIds.FirstFooter, "New Year: First Footer",
                    false, Hex(0xFFD24A), Hex(0xFF5A8A), Hex(0x8A6AFF), Hex(0x4AD8FF), Hex(0x8AFF6A)),

                // Seasons doc: gravestones. The stone is everyone's; the Heart is It's Complicated's reward.
                new CosmeticItem { Id = "grave_stone", DisplayName = "Stone", Type = CosmeticType.Grave, Color = Hex(0x8A9AA4), Tintable = true,
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EchoArt.GravePath) },
                new CosmeticItem { Id = "grave_heart", DisplayName = "Heart", Type = CosmeticType.Grave, Color = Hex(0xF2607A),
                    UnlockAchievement = AchievementIds.ItsComplicated, UnlockText = "Valentine's: It's Complicated",
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.HeartGravePath) },

                Palette("palette_default", "Teal to Red", 0, "", Hex(0x297380), Hex(0x5B3C8C), Hex(0x8C2E3A)),
                Ads(Palette("palette_mono", "Monochrome", 300, "", Hex(0x5A5A5A), Hex(0x3D3D3D), Hex(0x222222)), "Mono", MonoAds),
                Stat(Palette("palette_forest", "Forest", 0, "", Hex(0x2F6B3A), Hex(0x6B6A2F), Hex(0x6B3A2F)), CosmeticUnlockStat.Distance, 100, "Reach 100 m"),
                Ads(Palette("palette_supporter", "Night", 3000, "", Hex(0x1E2A5A), Hex(0x3A1E5A), Hex(0x5A1E3A)), "Gold", GoldAds),
            });
            catalog.Items = items.ToArray();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static CosmeticItem Character(PixelCharacter c)
        {
            var run = new Sprite[PixelRig.RunFrames];
            var ghost = new Sprite[PixelRig.RunFrames];
            for (int f = 0; f < PixelRig.RunFrames; f++)
            {
                run[f] = Load(EchoArt.CharacterPath(c.Id, f));
                ghost[f] = Load(EchoArt.GhostPath(c.Id, f));
            }
            bool sold = !string.IsNullOrEmpty(c.ProductId);
            return new CosmeticItem
            {
                Id = c.Id,
                DisplayName = c.Name,
                Type = CosmeticType.Character,
                // Skins addendum: the base cast is free, Pumpkin takes ads, five are earned. Pack characters are sold.
                PurchaseOnly = sold,
                ProductId = c.ProductId,
                // Seasons doc: the Christmas Pack is sold on its own, never in the Season of Echo bundle.
                BundleProductId = sold && InSeasonBundle(c.ProductId) ? PixelPacks.Season : "",
                UnlockAchievement = c.UnlockAchievement ?? "",
                AdsToUnlock = c.AdsToUnlock,
                UnlockStat = c.UnlockStat,
                UnlockTarget = c.UnlockTarget,
                UnlockText = sold ? PackName(c.ProductId) + " Pack" : c.AdsToUnlock > 0 ? $"Watch {c.AdsToUnlock} ads" : c.UnlockText ?? "",
                Color = (Color)c.Torso,
                RunFrames = run,
                JumpFrame = Load(EchoArt.CharacterJumpPath(c.Id)),
                GhostFrames = ghost,
                GhostJumpFrame = Load(EchoArt.GhostJumpPath(c.Id)),
                Tintable = c.Tintable,
                ScarfColor = c.ScarfColor,
                ScarfWidth = c.ScarfWidth,
                // Rig pixels to units from the feet: the sprite pivot is the bottom centre.
                ScarfAnchor = new Vector2((c.ScarfAnchor.x - PixelRig.W / 2f) / PixelRig.PixelsPerUnit,
                                          c.ScarfAnchor.y / PixelRig.PixelsPerUnit),
                HeadTop = HeadTopPixels(c) / PixelRig.PixelsPerUnit,
                Reaction = CharacterReactions.For(c.Id),
                AllAchievements = c.AllAchievements,
            };
        }

        static bool InSeasonBundle(string productId)
        {
            foreach (PixelPack p in PixelPacks.All)
                if (p.ProductId == productId) return true;
            return false;
        }

        static string PackName(string productId)
        {
            foreach (PixelPack p in PixelPacks.All)
                if (p.ProductId == productId) return p.Name;
            if (PixelSeasonalCast.ChristmasPack.ProductId == productId) return PixelSeasonalCast.ChristmasPack.Name;
            throw new System.InvalidOperationException($"no pack sells {productId}");
        }

        // The first row above the figure in its first run frame: where the Nemesis's horns sit.
        static float HeadTopPixels(PixelCharacter c)
        {
            PixelCanvas frame = PixelRig.Frame(c, 0);
            for (int y = frame.Height - 1; y >= 0; y--)
                for (int x = 0; x < frame.Width; x++)
                    if (frame.Opaque(x, y)) return y + 1;
            return frame.Height;
        }

        static Sprite Load(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new System.InvalidOperationException($"sprite missing: {path}");

        static CosmeticItem Stat(CosmeticItem item, CosmeticUnlockStat stat, int target, string text)
        {
            item.UnlockStat = stat;
            item.UnlockTarget = target;
            item.UnlockText = text;
            return item;
        }

        static CosmeticItem Ads(CosmeticItem item, string pack, int ads)
        {
            item.AdPack = pack;
            item.AdsToUnlock = ads;
            return item;
        }

        static CosmeticItem Runner(string id, string name, int score, string product, float r, float g, float b) =>
            new CosmeticItem { Id = id, DisplayName = name, Type = CosmeticType.Runner, UnlockScore = score, ProductId = product, Color = new Color(r, g, b) };

        static CosmeticItem Trail(string id, string name, int score, string product, float r, float g, float b) =>
            new CosmeticItem { Id = id, DisplayName = name, Type = CosmeticType.Trail, UnlockScore = score, ProductId = product, Color = new Color(r, g, b) };

        static CosmeticItem Seasonal(CosmeticItem trail, string achievement, string text, bool striped, params Color[] stops)
        {
            trail.UnlockAchievement = achievement;
            trail.UnlockText = text;
            trail.TrailStriped = striped;
            trail.TrailStops = stops;
            return trail;
        }

        static CosmeticItem Palette(string id, string name, int score, string product, params Color[] stops) =>
            new CosmeticItem { Id = id, DisplayName = name, Type = CosmeticType.Palette, UnlockScore = score, ProductId = product, PaletteStops = stops, Color = stops[0] };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
