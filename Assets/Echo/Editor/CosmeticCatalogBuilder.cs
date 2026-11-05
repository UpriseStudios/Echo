using System.Collections.Generic;
using System.IO;
using Echo.Core.Achievements;
using Echo.Editor.Art;
using Echo.Game.Cosmetics;
using Echo.Game.Seasons;
using Echo.Services.Iap;
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
                // World Time Run doc, phase 4: Fossil Record's amber coin trail.
                Seasonal(Trail("trail_amber", "Amber", 0, "", 1f, 0.66f, 0.2f), AchievementIds.FossilRecord, "Time Run: Fossil Record",
                    false, Hex(0x8A4A10), Hex(0xE8901E), Hex(0xFFC24A), Hex(0xFFE08A), Hex(0xE8901E), Hex(0x8A4A10)),

                // A user revision: worlds. The normal one is everyone's; Christmas Town is sold (and free to see every
                // December); the Haunted Graveyard is the Community Graveyard's reward, never sold.
                new CosmeticItem { Id = "world_echo", DisplayName = "Echo", Type = CosmeticType.World, Color = Hex(0x3A5A8A) },
                StoreWorld("world_christmas", "Christmas Town", 0x5A86B0, ProductIds.WorldChristmas, SeasonIds.Christmas),
                StoreWorld("world_halloween", "Halloween Night", 0x5A2A7A, ProductIds.WorldHalloween, SeasonIds.Halloween),
                StoreWorld("world_neon", "Neon Night", 0x5A2A6A, ProductIds.WorldNeon, SeasonIds.NeonNight),
                StoreWorld("world_sunken", "Sunken Ruins", 0x1E5A6A, ProductIds.WorldSunken, SeasonIds.SunkenRuins),
                StoreWorld("world_autumn", "Autumn Woods", 0xC0703A, ProductIds.WorldAutumn, SeasonIds.AutumnWoods),
                StoreWorld("world_beach", "Beach", 0x7AC8F0, ProductIds.WorldBeach, SeasonIds.Beach),
                StoreWorld("world_desert", "Desert Dunes", 0xF0B868, ProductIds.WorldDesert, SeasonIds.DesertDunes),
                StoreWorld("world_volcano", "Volcanic Waste", 0x5A2A22, ProductIds.WorldVolcano, SeasonIds.VolcanicWaste),
                StoreWorld("world_toytown", "Toy Town", 0xFF8AC8, ProductIds.WorldToyTown, SeasonIds.ToyTown),
                // World Time Run doc, phase 4: the top price point on its own (never in the Worlds Bundle), 3 free runs first.
                TimeRunWorld(),
                new CosmeticItem { Id = "world_graveyard", DisplayName = "Haunted Graveyard", Type = CosmeticType.World, Color = Hex(0x4A6450),
                    ServerReward = true, UnlockText = "Community Graveyard: finish a week in the top 10",
                    World = AssetDatabase.LoadAssetAtPath<SeasonTheme>(SeasonThemes.Dir + "/" + SeasonIds.Graveyard + ".asset") },

                // Modes doc, Naughty vs Nice: rewards granted by the server to the winning side.
                new CosmeticItem { Id = "acc_none", DisplayName = "None", Type = CosmeticType.Accessory, Color = Hex(0x3A4A54) },
                new CosmeticItem { Id = "acc_halo", DisplayName = "Halo", Type = CosmeticType.Accessory, Color = Hex(0xFFE36A), ServerReward = true,
                    UnlockText = "Naughty vs Nice: win as Nice", Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.HaloPath) },
                new CosmeticItem { Id = "trail_coal", DisplayName = "Coal", Type = CosmeticType.Trail, Color = Hex(0x2A2A2A), ServerReward = true,
                    UnlockText = "Naughty vs Nice: win as Naughty", TrailStriped = false,
                    TrailStops = new[] { Hex(0x141414), Hex(0x3A2A22), Hex(0xFF6A2A), Hex(0x1C1C1C), Hex(0x3A2A22), Hex(0xFFA040) } },

                // Modes doc, Ghost Duels: a trail for each final tier (Phantom's flows), granted by the server once a season
                // ends; Wraith (the top 100) also earns the Wraith character.
                DuelTrail("bronze", "Bronze Duellist", 0xD8925A, false, Hex(0x8A4A22), Hex(0xD8925A), Hex(0xF2C08A)),
                DuelTrail("silver", "Silver Duellist", 0xD8E0E8, false, Hex(0x8A96A4), Hex(0xD8E0E8), Hex(0xFFFFFF)),
                DuelTrail("gold", "Gold Duellist", 0xFFD24A, false, Hex(0xC9962A), Hex(0xFFD24A), Hex(0xFFF0A0)),
                DuelTrail("phantom", "Phantom", 0xB08AFF, true, Hex(0x6A3AE8), Hex(0xB08AFF), Hex(0x73F2FF), Hex(0xFFFFFF), Hex(0x73F2FF), Hex(0xB08AFF)),

                // Modes doc, One Life: Christmas Day 2026's gravestones, granted by the server.
                new CosmeticItem { Id = "grave_xmasday_2026", DisplayName = "Christmas Day 2026", Type = CosmeticType.Grave, Color = Hex(0xC8D2DA),
                    ServerReward = true, UnlockText = "One Life: finish your run on Christmas Day 2026",
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.ChristmasDayGravePath(false)) },
                new CosmeticItem { Id = "grave_xmasday_2026_gold", DisplayName = "Christmas Day 2026 Gold", Type = CosmeticType.Grave, Color = Hex(0xF2C230),
                    ServerReward = true, UnlockText = "One Life: top 3 on Christmas Day 2026",
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.ChristmasDayGravePath(true)) },

                // Seasons doc: gravestones. The stone is everyone's; the Heart is It's Complicated's reward.
                new CosmeticItem { Id = "grave_stone", DisplayName = "Stone", Type = CosmeticType.Grave, Color = Hex(0x8A9AA4), Tintable = true,
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EchoArt.GravePath) },
                new CosmeticItem { Id = "grave_heart", DisplayName = "Heart", Type = CosmeticType.Grave, Color = Hex(0xF2607A),
                    UnlockAchievement = AchievementIds.ItsComplicated, UnlockText = "Valentine's: It's Complicated",
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.HeartGravePath) },
                // World Time Run doc, phase 4: Extinct's Fossil gravestone, usable in any world.
                new CosmeticItem { Id = "grave_fossil", DisplayName = "Fossil", Type = CosmeticType.Grave, Color = Hex(0xD8BC88),
                    UnlockAchievement = AchievementIds.Extinct, UnlockText = "Time Run: Extinct",
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.FossilGravePath) },

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
                ServerReward = c.ServerReward,
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

        // A user revision: a world sold in the store, on its own and in the Worlds Bundle; never won in a mode.
        static CosmeticItem TimeRunWorld()
        {
            CosmeticItem t = StoreWorld(SeasonIds.TimeRun, "Time Run", 0xF29A4A, ProductIds.WorldTimeRun, SeasonIds.TimeRun);
            t.BundleProductId = "";
            t.TrialRuns = 3;
            return t;
        }

        static CosmeticItem StoreWorld(string id, string name, int swatch, string productId, string themeId) => new CosmeticItem
        {
            Id = id, DisplayName = name, Type = CosmeticType.World, Color = Hex(swatch), PurchaseOnly = true,
            ProductId = productId, BundleProductId = ProductIds.WorldsBundle,
            World = AssetDatabase.LoadAssetAtPath<SeasonTheme>(SeasonThemes.Dir + "/" + themeId + ".asset"),
        };

        static CosmeticItem Runner(string id, string name, int score, string product, float r, float g, float b) =>
            new CosmeticItem { Id = id, DisplayName = name, Type = CosmeticType.Runner, UnlockScore = score, ProductId = product, Color = new Color(r, g, b) };

        static CosmeticItem DuelTrail(string tier, string name, int colour, bool animated, params Color[] stops) => new CosmeticItem
        {
            Id = "trail_duel_" + tier, DisplayName = name, Type = CosmeticType.Trail, Color = Hex(colour), ServerReward = true,
            UnlockText = tier == "phantom" ? "Ghost Duels: finish a season Phantom or Wraith" : $"Ghost Duels: finish a season {char.ToUpperInvariant(tier[0]) + tier.Substring(1)}",
            TrailStops = stops, TrailStriped = false, TrailAnimated = animated,
        };

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
