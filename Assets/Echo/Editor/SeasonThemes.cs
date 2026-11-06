using System;
using System.Collections.Generic;
using Echo.Editor.Art;
using Echo.Game.Seasons;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// Writes the <see cref="SeasonTheme"/> assets (seasons doc) from the art in Editor/Art/PixelSeasons. Rebuilt on
    /// every scene build, like the cosmetic catalog. Seasons phase 1 has one theme, "test", which turns every override on
    /// for testing; it is never in the schedule, only forced from Settings.
    /// </summary>
    public static class SeasonThemes
    {
        public const string Dir = "Assets/Echo/Content/Seasons/Themes";
        public const string SchedulePath = "Assets/Echo/Content/Seasons/seasons.json";

        public static SeasonTheme[] Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Echo/Content/Seasons", "Themes");
            var themes = new List<SeasonTheme>
            {
                Halloween(), BonfireNight(), Christmas(), NewYear(), Valentines(), Easter(), AprilFools(), Graveyard(), NeonNight(),
                SunkenRuins(), AutumnWoods(),
                Beach(), DesertDunes(), VolcanicWaste(), ToyTown(), Test(),
            };
            themes.AddRange(TimeRun(themes));
            foreach (SeasonTheme t in themes) EditorUtility.SetDirty(t);
            return themes.ToArray();
        }

        // Seasons doc: falling snow, snow-capped obstacles, ghosts in Santa hats, snowman graves, a red-nosed Nemesis.
        static SeasonTheme Christmas()
        {
            SeasonTheme t = Snowy(Load(SeasonIds.Christmas), "Christmas");
            // A user revision: now and then Santa's sleigh crosses the sky over the town, as a silhouette.
            t.Flyer = Load<Sprite>(SeasonArt.SleighPath);
            t.FlyerColour = new Color(0.08f, 0.1f, 0.18f, 0.85f);
            t.FlyerEvery = 40f;
            t.FlyerSpeed = 2.4f;
            return t;
        }

        // New Year inside Christmas: the same snow, plus fireworks, and a show when the Daily Haunt board resets.
        static SeasonTheme NewYear()
        {
            SeasonTheme t = Snowy(Load(SeasonIds.NewYear), "New Year");
            t.Fireworks = true;
            t.FireworkSpark = Load<Sprite>(SeasonArt.SparkPath);
            t.FireworkColours = new[] { Hex(0xFFD24A), Hex(0xFF5A8A), Hex(0x8A6AFF), Hex(0x4AD8FF), Hex(0x8AFF6A) };
            t.FireworksPerMinute = 5f;
            t.ResetShowSeconds = 120f;
            t.ResetShowToast = "HAPPY NEW YEAR!";
            t.Stinger = null;
            t.MusicStems = Carol(CarolMusic.AuldLangSyne);
            return t;
        }

        // Seasons doc: the Nemesis wears a heart, and when it gets you the death screen reads "It's complicated."
        static SeasonTheme Valentines()
        {
            SeasonTheme t = Plain(Load(SeasonIds.Valentines), "Valentine's");
            t.NemesisAccessory = Sprite(SeasonIds.Valentines, SeasonArt.NemesisAccessory);
            t.DeathLine = "It's complicated.";
            t.DeathLineNemesisOnly = true;
            return t;
        }

        // Seasons doc: spikes become painted eggs (same outline, still hazards), a pastel sky with blossom drifting down,
        // painted-egg gravestones and bunny ears on the ghosts; the egg hunt is the game's (EggHuntView).
        static SeasonTheme Easter()
        {
            const string id = SeasonIds.Easter;
            SeasonTheme t = Plain(Load(id), "Easter");
            t.PaletteStops = new[] { Hex(0x9AD8F0), Hex(0xF0C8E8), Hex(0xC8E8B0) };
            t.PaletteBlend = 0.6f;
            t.Effect = SeasonEffect.Falling;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(id, 0)), Load<Sprite>(SeasonArt.EffectPath(id, 1)) };
            t.EffectColours = new[] { Hex(0xFFD8EC), Hex(0xFFFFFF), Hex(0xF8B8D8) };
            t.EffectCount = 30;
            t.EffectFall = 0.6f;
            t.EffectSway = 0.5f;
            t.EffectParallax = 0.25f;
            t.Spike = Sprite(id, SeasonArt.Spike);
            t.Grave = Sprite(id, SeasonArt.Grave);
            t.GraveInColour = true;
            t.GhostAccessory = Sprite(id, SeasonArt.GhostAccessory);
            return t;
        }

        // Seasons doc: everyone runs as the Banana for the day; the player's own skin returns at midnight (runs still record
        // it, so the day's ghosts go back to their own skins too).
        static SeasonTheme AprilFools()
        {
            SeasonTheme t = Plain(Load(SeasonIds.AprilFools), "April Fools");
            t.ForceCharacter = "char_banana";
            t.RunToast = "APRIL FOOLS! EVERYONE'S A BANANA";
            return t;
        }

        // Seasons doc: a purple-orange sky, pumpkin gravestones, cobwebs on ghosts. Ships switched off until next year.
        static SeasonTheme Halloween()
        {
            SeasonTheme t = Plain(Load(SeasonIds.Halloween), "Halloween");
            t.PaletteStops = new[] { Hex(0x5A2A7A), Hex(0x8A3A5A), Hex(0xB0562A) };
            t.PaletteBlend = 0.75f;
            t.Grave = Sprite(SeasonIds.Halloween, SeasonArt.Grave);
            t.GraveInColour = true;
            t.GhostAccessory = Sprite(SeasonIds.Halloween, SeasonArt.GhostAccessory);
            // A user revision: Halloween Night, also a world in the store (free to see in its season, buyable to keep):
            // a lilac moonlit manor, black-purple pumpkin patch, cold slate ground and toxic slime-green spikes (orange ones
            // vanished where the sky turns orange further on).
            Colours(t, Hex(0xA884C0), Hex(0x24122C), Hex(0x5A5E74), Hex(0x8AFF3A));
            Backdrop(t, SeasonIds.Halloween, SeasonEffect.Drifting, 1, new[] { new Color(0.1f, 0.05f, 0.12f, 0.9f) }, 8, 1.1f, 0.3f, 1f, 0.6f,
                new Color(0.07f, 0.03f, 0.09f, 0.9f), 45f, 2.8f);
            return t;
        }

        // ---- the store's worlds (a user revision): looks that are never scheduled ----

        static SeasonTheme NeonNight()
        {
            SeasonTheme t = Plain(Load(SeasonIds.NeonNight), "Neon Night");
            t.PaletteStops = new[] { Hex(0x3A1A5A), Hex(0x5A2A6A), Hex(0x1A2A5A) };
            t.PaletteBlend = 0.85f;
            // Blue-violet towers, near-black rooftops, a steel-grey street and neon-yellow spikes.
            Colours(t, Hex(0x6A5AB8), Hex(0x1A1830), Hex(0x6A7494), Hex(0xFFE04A));
            Backdrop(t, SeasonIds.NeonNight, SeasonEffect.Falling, 1, new[] { new Color(0.6f, 0.95f, 1f, 0.55f), new Color(1f, 0.6f, 0.95f, 0.45f) },
                60, 9f, 0f, 1f, 0.2f, new Color(0.08f, 0.04f, 0.16f, 0.85f), 30f, 4f);
            return t;
        }

        static SeasonTheme SunkenRuins()
        {
            SeasonTheme t = Plain(Load(SeasonIds.SunkenRuins), "Sunken Ruins");
            t.PaletteStops = new[] { Hex(0x1E5A6A), Hex(0x0E3A5A), Hex(0x082A44) };
            t.PaletteBlend = 0.9f;
            // Sea-green ruins, dark kelp, a sandy floor and coral-red spikes.
            Colours(t, Hex(0x4A9A94), Hex(0x14403A), Hex(0xC8B48A), Hex(0xFF5A4A));
            // Bubbles rise: a negative fall.
            Backdrop(t, SeasonIds.SunkenRuins, SeasonEffect.Falling, 2, new[] { new Color(0.75f, 1f, 1f, 0.6f), new Color(0.75f, 1f, 1f, 0.45f) },
                30, -0.6f, 0.25f, 1f, 0.3f, new Color(0.03f, 0.08f, 0.14f, 0.7f), 55f, 1.2f);
            return t;
        }

        static SeasonTheme AutumnWoods()
        {
            SeasonTheme t = Plain(Load(SeasonIds.AutumnWoods), "Autumn Woods");
            t.PaletteStops = new[] { Hex(0xE09A5A), Hex(0xC0703A), Hex(0x7A4A5A) };
            t.PaletteBlend = 0.7f;
            // Hazy amber hills, deep brown woods, a mossy floor and frost-cyan spikes: the opposite of autumn's colours, so
            // they read against the dark woods they sit in front of and the bright sky alike.
            Colours(t, Hex(0xD8A070), Hex(0x5A3A24), Hex(0x5A6A30), Hex(0x6AE8F0));
            Backdrop(t, SeasonIds.AutumnWoods, SeasonEffect.Falling, 2, new[] { Hex(0xE8742A), Hex(0xC8321E), Hex(0xF2B630), Hex(0x8A4A22) },
                36, 0.7f, 0.5f, 1f, 0.3f, new Color(0.15f, 0.1f, 0.12f, 0.85f), 35f, 2.2f);
            return t;
        }

        // A user revision: a world's own colours, distinct hues rather than shades of its sky: the far and near layers,
        // the floor (so gaps stand out) and the hazards (so spikes read). The game still keeps them apart from the sky.
        static void Colours(SeasonTheme t, Color far, Color near, Color floor, Color hazard)
        {
            t.FarTint = far;
            t.NearTint = near;
            t.FloorColour = floor;
            t.HazardColour = hazard;
        }

        // ---- World: Time Run (World Time Run doc): four eras by distance (TimeEras) ----

        // Phase 1, the era system: each era dressed in the game's own art (the castle for Medieval, the neon city for
        // Today). Phase 3, art styles: 8-bit Dino Land, 16-bit Medieval, 32-bit Today and the Far Future in HD.
        static IEnumerable<SeasonTheme> TimeRun(List<SeasonTheme> built)
        {
            SeasonTheme Built(string id) => built.Find(t => t.Id == id);
            SeasonTheme Era(string id, string title, string label, SeasonTheme like)
            {
                SeasonTheme e = Plain(Load(id), title);
                e.EraLabel = label;
                if (like != null)
                {
                    e.PaletteStops = like.PaletteStops;
                    e.PaletteBlend = like.PaletteBlend;
                    e.FarLayer = like.FarLayer;
                    e.NearLayer = like.NearLayer;
                    Colours(e, like.FarTint, like.NearTint, like.FloorColour, like.HazardColour);
                    e.Effect = like.Effect;
                    e.EffectSprites = like.EffectSprites;
                    e.EffectColours = like.EffectColours;
                    e.EffectCount = like.EffectCount;
                    e.EffectFall = like.EffectFall;
                    e.EffectSway = like.EffectSway;
                    e.EffectBand = like.EffectBand;
                    e.EffectParallax = like.EffectParallax;
                    e.Flyer = like.Flyer;
                    e.FlyerColour = like.FlyerColour;
                    e.FlyerEvery = like.FlyerEvery;
                    e.FlyerSpeed = like.FlyerSpeed;
                }
                return e;
            }

            SeasonTheme dino = Era(SeasonIds.EraDino, "Dino Land", "65,000,000 BC", Built(SeasonIds.VolcanicWaste));
            SeasonTheme medieval = Era(SeasonIds.EraMedieval, "Medieval", "1200 AD", null);
            medieval.PaletteStops = new[] { Hex(0x5A6A9A), Hex(0x7A6A8A), Hex(0x4A4A6A) };
            medieval.PaletteBlend = 0.85f;
            medieval.FarLayer = Load<Sprite>(Art.EchoArt.MountainsPath);
            medieval.NearLayer = Load<Sprite>(Art.EchoArt.CastlePath);
            Colours(medieval, Hex(0x8A94B8), Hex(0x2A2A44), Hex(0x6A6070), Hex(0xFFD24A));
            SeasonTheme today = Era(SeasonIds.EraToday, "Today", "2026", Built(SeasonIds.NeonNight));
            SeasonTheme future = Era(SeasonIds.EraFuture, "Far Future", "3026", null);
            future.PaletteStops = new[] { Hex(0x0A1A3A), Hex(0x1A0A3A), Hex(0x0A2A4A) };
            future.PaletteBlend = 0.9f;
            future.FarLayer = Load<Sprite>(Art.EchoArt.MountainsPath);
            future.NearLayer = Load<Sprite>(Art.EchoArt.MoonSurfacePath);
            Colours(future, Hex(0x2A6A9A), Hex(0x1A2A4A), Hex(0x3A5A7A), Hex(0x4AFFE0));

            // Phase 2: each era's hazards, gap, grave, coin and ghosts (the look follows each thing's own position).
            foreach (SeasonTheme e in new[] { dino, medieval, today, future })
            {
                e.Spike = Optional(e.Id, SeasonArt.Spike);
                e.Block = Optional(e.Id, SeasonArt.Block);
                e.Gap = Optional(e.Id, SeasonArt.Gap);
                e.Grave = Optional(e.Id, SeasonArt.Grave);
                e.Coin = Optional(e.Id, SeasonArt.Coin);
            }
            dino.GhostTint = Hex(0xEDE4CC);              // bone-white fossils
            dino.NemesisTint = Hex(0xFF6A5A);
            dino.NemesisAccessory = Sprite(SeasonIds.EraDino, SeasonArt.NemesisAccessory);
            today.GraveInColour = true;                  // flowers and polished stone
            today.GhostGlow = true;                      // a faint phone-screen glow
            future.GraveInColour = true;                 // the memorial is light
            future.GhostTint = Hex(0x4AF0FF);            // cyan holograms
            future.NemesisTint = Hex(0xFF4A5A);          // a red hologram
            future.GhostHologram = true;

            // Phase 3: Dino Land in 8-bit, its own scenery in five colours under a flat sky (three equal stops: no gradient).
            Color dinoSky = Hex(PixelDino.Sky);
            dino.PaletteStops = new[] { dinoSky, dinoSky, dinoSky };
            dino.PaletteBlend = 1f;
            dino.FarLayer = Sprite(SeasonIds.EraDino, SeasonArt.Far);
            dino.NearLayer = Sprite(SeasonIds.EraDino, SeasonArt.Near);
            Colours(dino, Color.white, Color.white, Hex(0x5A3428), Color.white);   // pale stone shards on the orange sky
            dino.Effect = SeasonEffect.Falling;
            dino.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(SeasonIds.EraDino, 0)) };
            dino.EffectColours = new Color[] { PixelDino.Sun, PixelDino.Lava, PixelDino.Night };
            dino.EffectCount = 40;
            dino.EffectFall = 0.6f;
            dino.EffectSway = 0.3f;
            dino.EffectBand = 1f;
            dino.EffectParallax = 0.3f;
            dino.Flyer = Load<Sprite>(SeasonArt.FlyerPath(SeasonIds.EraDino));
            dino.FlyerColour = Color.white;
            dino.FlyerEvery = 18f;
            dino.FlyerSpeed = 3f;

            // Art pass phase 2: rendered at half resolution, with its own floor, edge and pit, and a rock overhang for ceilings.
            dino.HalfResolution = true;
            dino.Floor = Sprite(SeasonIds.EraDino, SeasonArt.Floor);
            dino.FloorTop = Sprite(SeasonIds.EraDino, SeasonArt.FloorTop);
            dino.Ceiling = Sprite(SeasonIds.EraDino, SeasonArt.Ceiling);

            // Today in 32-bit: the neon city with a distant skyline behind and a mid-distance block in front of its towers.
            today.ExtraLayers = new[]
            {
                new ExtraLayer { Sprite = Sprite(SeasonIds.EraToday, SeasonArt.Back), Parallax = 0.07f, Order = -22 },
                new ExtraLayer { Sprite = Sprite(SeasonIds.EraToday, SeasonArt.Mid), Parallax = 0.27f, Order = -20 },
            };

            // The Far Future in HD: its own sky, moons, chrome towers, wet street, neon grid floor, rain and flying car.
            future.HighDefinition = true;
            future.FarLayer = Sprite(SeasonIds.EraFuture, SeasonArt.Far);
            future.NearLayer = Sprite(SeasonIds.EraFuture, SeasonArt.Near);
            Colours(future, Color.white, Color.white, Hex(0x3A5A7A), Hex(0x4AFFE0));
            future.ExtraLayers = new[]
            {
                new ExtraLayer { Sprite = Sprite(SeasonIds.EraFuture, SeasonArt.Sky), Parallax = 0f, Order = -30 },
                new ExtraLayer { Sprite = Sprite(SeasonIds.EraFuture, SeasonArt.Moons), Parallax = 0f, Order = -26 },
                new ExtraLayer { Sprite = Sprite(SeasonIds.EraFuture, SeasonArt.Back), Parallax = 0.05f, Order = -22 },
            };
            future.Floor = Sprite(SeasonIds.EraFuture, SeasonArt.Floor);
            future.Effect = SeasonEffect.Falling;
            future.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(SeasonIds.EraFuture, 0)) };
            future.EffectColours = new[] { new Color(0.7f, 0.95f, 1f, 0.35f), new Color(1f, 0.6f, 0.95f, 0.25f) };
            future.EffectCount = 60;
            future.EffectFall = 9f;
            future.EffectSway = 0f;
            future.EffectBand = 1f;
            future.EffectParallax = 0.2f;
            future.Flyer = Load<Sprite>(SeasonArt.FlyerPath(SeasonIds.EraFuture));
            future.FlyerColour = Color.white;
            future.FlyerEvery = 14f;
            future.FlyerSpeed = 5f;
            future.GhostGlow = true;                     // holograms glow too

            // Art pass phase 1: the fifth era, the Far Far Future (1200 m on). Until phase 6 gives it its real-time 3D
            // backdrop it wears the Far Future's look, so the era system can be tried end to end.
            SeasonTheme farFar = Era(SeasonIds.EraFarFarFuture, "Far Far Future", "10,000 AD", future);
            farFar.HighDefinition = true;
            farFar.ExtraLayers = future.ExtraLayers;
            farFar.Floor = future.Floor;
            farFar.Spike = future.Spike;
            farFar.Block = future.Block;
            farFar.Gap = future.Gap;
            farFar.Grave = future.Grave;
            farFar.Coin = future.Coin;
            farFar.GraveInColour = future.GraveInColour;
            farFar.GhostTint = future.GhostTint;
            farFar.NemesisTint = future.NemesisTint;
            farFar.GhostHologram = future.GhostHologram;
            farFar.GhostGlow = future.GhostGlow;

            SeasonTheme world = Plain(Load(SeasonIds.TimeRun), "Time Run");
            world.Eras = new[] { dino, medieval, today, future, farFar };
            foreach (SeasonTheme t in new[] { dino, medieval, today, future, farFar, world }) EditorUtility.SetDirty(t);
            return new[] { dino, medieval, today, future, farFar, world };
        }

        // ---- four more (a user revision), painted in their own colours: white tints show them as drawn ----

        static SeasonTheme Beach()
        {
            SeasonTheme t = Plain(Load(SeasonIds.Beach), "Beach");
            t.PaletteStops = new[] { Hex(0x7AC8F0), Hex(0x5AB0E8), Hex(0x3A90D8) };
            t.PaletteBlend = 0.9f;
            Colours(t, Color.white, Color.white, Hex(0xD8B878), Hex(0xB0102A));
            Backdrop(t, SeasonIds.Beach, SeasonEffect.Drifting, 1, new[] { Color.white }, 6, 0.9f, 0.3f, 1f, 0.4f, Color.white, 35f, 2.6f);
            return t;
        }

        static SeasonTheme DesertDunes()
        {
            SeasonTheme t = Plain(Load(SeasonIds.DesertDunes), "Desert Dunes");
            t.PaletteStops = new[] { Hex(0xF8D898), Hex(0xF0B868), Hex(0xE0905A) };
            t.PaletteBlend = 0.85f;
            Colours(t, Color.white, Color.white, Hex(0xC08A4A), Hex(0x2A2A6A));
            Backdrop(t, SeasonIds.DesertDunes, SeasonEffect.Drifting, 1, new[] { new Color(0.95f, 0.82f, 0.6f, 0.55f) }, 14, 1.4f, 0.1f, 0.35f, 0.5f,
                new Color(0.2f, 0.12f, 0.1f, 0.9f), 30f, 1.6f);
            return t;
        }

        static SeasonTheme VolcanicWaste()
        {
            SeasonTheme t = Plain(Load(SeasonIds.VolcanicWaste), "Volcanic Waste");
            t.PaletteStops = new[] { Hex(0x5A2A22), Hex(0x3A1E1E), Hex(0x2A1414) };
            t.PaletteBlend = 0.9f;
            Colours(t, Color.white, Color.white, Hex(0x7A7070), Hex(0xFFE070));
            Backdrop(t, SeasonIds.VolcanicWaste, SeasonEffect.Falling, 2, new[] { new Color(0.62f, 0.6f, 0.6f, 0.8f), Hex(0xFF8A2A) },
                50, 0.5f, 0.3f, 1f, 0.3f, Hex(0xFF8A2A), 20f, 4f);
            return t;
        }

        // The wild cartoon world: the brightest of all, with floating stars and hearts and a striped balloon.
        static SeasonTheme ToyTown()
        {
            SeasonTheme t = Plain(Load(SeasonIds.ToyTown), "Toy Town");
            t.PaletteStops = new[] { Hex(0x6AD8FF), Hex(0x9AB0FF), Hex(0xFFA0E0) };
            t.PaletteBlend = 0.9f;
            Colours(t, Color.white, Color.white, Hex(0x6A3A2A), Hex(0x2A0A3A));
            Backdrop(t, SeasonIds.ToyTown, SeasonEffect.Falling, 2,
                new[] { Hex(0xFF4A8A), Hex(0xFFE03A), Hex(0x3AC8FF), Hex(0x5ADA4A), Hex(0x9A5AFF) }, 30, -0.5f, 0.4f, 1f, 0.3f, Color.white, 30f, 1.5f);
            return t;
        }

        // A backdrop from its art: the far and near layers, the effect and the flyer.
        static void Backdrop(SeasonTheme t, string id, SeasonEffect effect, int sprites, Color[] colours, int count, float fall, float sway,
            float band, float parallax, Color flyerColour, float flyerEvery, float flyerSpeed)
        {
            t.FarLayer = Sprite(id, SeasonArt.Far);
            t.NearLayer = Sprite(id, SeasonArt.Near);
            t.Effect = effect;
            t.EffectSprites = new Sprite[sprites];
            for (int i = 0; i < sprites; i++) t.EffectSprites[i] = Load<Sprite>(SeasonArt.EffectPath(id, i));
            t.EffectColours = colours;
            t.EffectCount = count;
            t.EffectFall = fall;
            t.EffectSway = sway;
            t.EffectBand = band;
            t.EffectParallax = parallax;
            t.Flyer = Load<Sprite>(SeasonArt.FlyerPath(id));
            t.FlyerColour = flyerColour;
            t.FlyerEvery = flyerEvery;
            t.FlyerSpeed = flyerSpeed;
        }

        // Seasons doc: fireworks bursting in the background. Ships switched off until next year.
        static SeasonTheme BonfireNight()
        {
            SeasonTheme t = Plain(Load(SeasonIds.BonfireNight), "Bonfire Night");
            t.Fireworks = true;
            t.FireworkSpark = Load<Sprite>(SeasonArt.SparkPath);
            t.FireworkColours = new[] { Hex(0xFFB03A), Hex(0xFF5A3A), Hex(0xFFE36A), Hex(0xFFFFFF), Hex(0x6AC8FF) };
            t.FireworksPerMinute = 14f;
            return t;
        }

        // A user revision: the Community Graveyard's own look, over any season: a sickly green dusk, dead trees and a
        // ruined chapel, a crowded graveyard, low fog and will-o'-wisps drifting through. Never scheduled.
        static SeasonTheme Graveyard()
        {
            const string id = SeasonIds.Graveyard;
            SeasonTheme t = Plain(Load(id), "Community Graveyard");
            t.PaletteStops = new[] { Hex(0x4A6450), Hex(0x34404E), Hex(0x2A2640) };
            t.PaletteBlend = 0.85f;
            t.FarLayer = Sprite(id, SeasonArt.Far);
            t.NearLayer = Sprite(id, SeasonArt.Near);
            t.Effect = SeasonEffect.Drifting;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(id, 0)), Load<Sprite>(SeasonArt.EffectPath(id, 1)), Load<Sprite>(SeasonArt.EffectPath(id, 2)) };
            t.EffectColours = new[] { new Color(0.82f, 0.9f, 0.84f, 0.75f), new Color(0.78f, 0.86f, 0.82f, 0.6f), Hex(0x9CFFB0) };
            t.EffectCount = 27;
            t.EffectFall = -0.35f;
            t.EffectSway = 0.12f;
            t.EffectBand = 0.4f;
            t.EffectParallax = 0.5f;
            // Pale grey-green ruins, a near-black graveyard, dark earth and bone-white spikes.
            Colours(t, Hex(0x8AA08A), Hex(0x1E2A22), Hex(0x4A3E32), Hex(0xE8E0C8));
            return t;
        }

        // Every override off, for a theme to switch on only what it needs.
        static SeasonTheme Plain(SeasonTheme t, string title)
        {
            t.Title = title;
            t.PaletteStops = new Color[0];
            t.PaletteBlend = 1f;
            t.FarLayer = t.NearLayer = null;
            Colours(t, Color.clear, Color.clear, Color.clear, Color.clear);
            t.Effect = SeasonEffect.None;
            t.EffectSprites = new Sprite[0];
            t.EffectColours = new Color[0];
            t.EffectBand = 1f;
            t.Fireworks = false;
            t.FireworkSpark = null;
            t.FireworkColours = new Color[0];
            t.ResetShowSeconds = 0f;
            t.ResetShowToast = "";
            t.Flyer = null;
            t.Spike = t.Block = t.GroundTop = null;
            t.Grave = t.Headstone = null;
            t.GraveInColour = false;
            t.GhostAccessory = t.NemesisAccessory = null;
            t.DeathLine = "";
            t.DeathLineNemesisOnly = false;
            t.Stinger = null;
            t.MusicStems = new AudioClip[0];
            t.ForceCharacter = "";
            t.RunToast = "";
            t.Eras = new SeasonTheme[0];
            t.EraLabel = "";
            t.Gap = t.Coin = null;
            t.GhostTint = t.NemesisTint = Color.clear;
            t.GhostGlow = t.GhostHologram = false;
            t.ExtraLayers = new ExtraLayer[0];
            t.Floor = null;
            t.HighDefinition = false;
            t.HalfResolution = false;
            t.FloorTop = t.Ceiling = null;
            return t;
        }

        static SeasonTheme Snowy(SeasonTheme t, string title)
        {
            const string art = SeasonIds.Christmas;
            t.Title = title;
            t.PaletteStops = new[] { Hex(0x5A86B0), Hex(0x3E5A8E), Hex(0x2A3A66) };
            t.PaletteBlend = 0.7f;
            t.FarLayer = Sprite(art, SeasonArt.Far);
            t.NearLayer = Sprite(art, SeasonArt.Near);
            Colours(t, Hex(0xDCE8F4), Hex(0x4A6688), Hex(0xEEF4FA), Hex(0xC8202E));
            t.Effect = SeasonEffect.Falling;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(art, 0)), Load<Sprite>(SeasonArt.EffectPath(art, 1)) };
            t.EffectColours = new Color[0];
            t.EffectCount = 56;
            t.EffectFall = 0.7f;
            t.EffectSway = 0.35f;
            t.EffectParallax = 0.25f;
            t.Fireworks = false;
            t.FireworkSpark = null;
            t.FireworkColours = new Color[0];
            t.ResetShowSeconds = 0f;
            t.ResetShowToast = "";
            t.Flyer = null;
            t.Spike = Sprite(art, SeasonArt.Spike);
            t.Block = Sprite(art, SeasonArt.Block);
            t.GroundTop = Sprite(art, SeasonArt.GroundTop);
            t.Grave = Sprite(art, SeasonArt.Grave);
            t.GraveInColour = true;
            t.Headstone = Sprite(art, SeasonArt.Headstone);
            t.GhostAccessory = Sprite(art, SeasonArt.GhostAccessory);
            t.NemesisAccessory = Sprite(art, SeasonArt.NemesisAccessory);
            t.DeathLine = "";
            t.DeathLineNemesisOnly = false;
            t.Stinger = Load<AudioClip>(PlaceholderAudio.PathFor(PlaceholderAudio.ChristmasStinger));
            // A user revision: "Jingle Bells" in place of the main tune (New Year swaps in "Auld Lang Syne").
            t.MusicStems = Carol(CarolMusic.JingleBells);
            return t;
        }

        static SeasonTheme Test()
        {
            const string id = SeasonIds.Test;
            SeasonTheme t = Load(id);
            t.Title = "Test Season";
            t.PaletteStops = new[] { Hex(0x6A3FA0), Hex(0x2F8C8C), Hex(0xA03F7A) };
            t.PaletteBlend = 0.6f;
            t.FarLayer = Sprite(id, SeasonArt.Far);
            t.NearLayer = Sprite(id, SeasonArt.Near);
            Colours(t, Hex(0xC8A0E0), Hex(0x1E3A3A), Hex(0x8A8AAA), Hex(0xFFE04A));
            t.Effect = SeasonEffect.Falling;
            t.EffectSprites = new[] { Load<Sprite>(SeasonArt.EffectPath(id, 0)), Load<Sprite>(SeasonArt.EffectPath(id, 1)) };
            t.EffectColours = new[] { Hex(0xFF7AC8), Hex(0xFFE36A), Hex(0x6AE0FF), Hex(0x9CFF6A) };
            t.EffectCount = 40;
            t.EffectFall = 1.2f;
            t.EffectSway = 0.3f;
            t.EffectParallax = 0.3f;
            t.Spike = Sprite(id, SeasonArt.Spike);
            t.Block = Sprite(id, SeasonArt.Block);
            t.GroundTop = Sprite(id, SeasonArt.GroundTop);
            t.Grave = Sprite(id, SeasonArt.Grave);
            t.GraveInColour = true;
            t.Headstone = Sprite(id, SeasonArt.Headstone);
            t.GhostAccessory = Sprite(id, SeasonArt.GhostAccessory);
            t.NemesisAccessory = Sprite(id, SeasonArt.NemesisAccessory);
            t.DeathLine = "Test season: every override is on.";
            t.DeathLineNemesisOnly = false;
            t.Stinger = Load<AudioClip>(PlaceholderAudio.PathFor(PlaceholderAudio.TestSeasonStinger));
            t.MusicStems = new AudioClip[0];
            return t;
        }

        static SeasonTheme Load(string id)
        {
            string path = $"{Dir}/{id}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<SeasonTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            theme.Id = id;
            return theme;
        }

        static AudioClip[] Carol(CarolMusic carol) => Array.ConvertAll(carol.StemNames, n => Load<AudioClip>(PlaceholderAudio.PathFor(n)));

        static Sprite Sprite(string season, string part) => Load<Sprite>(SeasonArt.PathFor(season, part));

        // An era keeps the original where it has no art of its own (Medieval keeps the spike, grave and coin).
        static Sprite Optional(string season, string part) => AssetDatabase.LoadAssetAtPath<Sprite>(SeasonArt.PathFor(season, part));

        static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"missing: {path}");

        static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
