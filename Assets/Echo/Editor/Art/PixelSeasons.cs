using System;
using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Seasonal art (seasons doc). Reskins of world art are repaints of the original's opaque pixels only
    /// (<see cref="Repaint"/>), so a hazard keeps its exact outline and size (seasons doc ground rule: reskinned hazards
    /// keep their outline; SeasonArtTests checks every one). Hazards and layers stay greyscale so the views tint them.
    /// This is the finished art (a user decision), in the game's own pixel style.
    /// </summary>
    public static class PixelSeasons
    {
        /// <summary>A copy of <paramref name="source"/> with each opaque pixel recoloured; transparent pixels stay clear.</summary>
        public static PixelCanvas Repaint(PixelCanvas source, Func<int, int, Color32, Color32> paint)
        {
            var c = new PixelCanvas(source.Width, source.Height);
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    if (!source.Opaque(x, y)) continue;
                    Color32 p = paint(x, y, source[x, y]);
                    p.a = source[x, y].a;
                    c[x, y] = p;
                }
            return c;
        }

        /// <summary>Lightens the top few pixels of every column: a dusting on the edges (scenery layers, the floor).</summary>
        public static PixelCanvas Capped(PixelCanvas source, int depth, float lift) => Repaint(source, (x, y, p) =>
        {
            int below = 0;
            while (below < depth && source.Opaque(x, y + below + 1)) below++;
            if (below >= depth) return p;
            float v = Mathf.Lerp(p.r / 255f, 1f, lift * (1f - below / (float)depth));
            return Grey(v);
        });

        /// <summary><see cref="Capped"/> for art in its own colours: the top pixels of each column turn to snow.</summary>
        public static PixelCanvas Snowed(PixelCanvas source, int depth) => Repaint(source, (x, y, p) =>
        {
            int below = 0;
            while (below < depth && source.Opaque(x, y + below + 1)) below++;
            if (below >= depth) return p;
            return below == 0 || (x * 7 + below * 3) % 5 != 0 ? Hex(below == depth - 1 ? 0xD6E6F2u : 0xF4FAFFu) : p;
        });

        // ---- Christmas ----

        /// <summary>The spike with snow on its tip: the top 5 rows of its point turn white (still greyscale).</summary>
        public static PixelCanvas ChristmasSpike() => Repaint(PixelWorld.Spike(), (x, y, p) => y >= 11 ? Grey(1f) : y == 10 && x % 2 == 0 ? Grey(0.97f) : p);

        public static PixelCanvas ChristmasBlock() => Capped(PixelWorld.Block(), 3, 0.95f);
        public static PixelCanvas ChristmasGroundTop() => Capped(PixelWorld.GroundTop(), 3, 0.9f);
        public static PixelCanvas ChristmasFar() => Capped(PixelWorld.Mountains(), 7, 0.8f);
        public static PixelCanvas ChristmasNear() => Capped(PixelWorld.GraveyardHills(), 3, 0.75f);
        public static PixelCanvas ChristmasHeadstone() => Snowed(PixelWorld.Headstone(), 4);

        /// <summary>A snowman for a gravestone, 12 x 16 like the stone, in its own colours; the mound is a snowdrift.</summary>
        public static PixelCanvas Snowman()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "....kkkk....",
                "....kkkk....",
                "...rrrrrr...",
                "....wwww....",
                "...wkwwkw...",
                "...wwwwoo...",
                "....wwww....",
                "..RRRRRRRR..",
                "..wwwwwRww..",
                ".wwwwkwRwww.",
                ".wwwwwwwwww.",
                ".wwwwkwwwww.",
                "..wwwwwwww..",
                "...wwwwww...",
                "dddddddddddd",
                "dddddddddddd",
            }, new Dictionary<char, Color32>
            {
                ['k'] = Hex(0x1A1C22), ['r'] = Hex(0xD8323C), ['R'] = Hex(0xC02030), ['w'] = Hex(0xF4F8FC),
                ['o'] = Hex(0xF08A24), ['d'] = Hex(0xDCE8F2),
            }, 0, 0);
            c.Outline(Hex(0x3A4A5A));
            return c;
        }

        /// <summary>A Santa hat for every ghost, drooping back to a bobble; pivot just above its trim so it sits on the head.</summary>
        public static PixelCanvas SantaHat()
        {
            var c = new PixelCanvas(13, 9);
            c.Blit(new[]
            {
                "w...........",
                "wwrr........",
                "..rrrrr.....",
                "..rrrrrRR...",
                "..rrrrrrRR..",
                ".wwwwwwwwww.",
                ".wwwwwwwwww.",
            }, new Dictionary<char, Color32> { ['r'] = Hex(0xE02838), ['R'] = Hex(0xB01828), ['w'] = Hex(0xF8F8F8) }, 0, 1);
            c.Outline(Hex(0x2A0A10));
            return c;
        }

        /// <summary>The Nemesis's glowing red nose (Rudolph), a 3 x 3 bulb with a shine, in place of its horns.</summary>
        public static PixelCanvas RedNose()
        {
            var c = new PixelCanvas(5, 5);
            c.Blit(new[] { ".R.", "RwR", "RRR" }, new Dictionary<char, Color32> { ['R'] = Hex(0xFF2A24), ['w'] = Hex(0xFFD0C8) }, 1, 1);
            c[1, 2] = Hex(0xFF2A24);
            c.Outline(Hex(0x5A0A08, 200));
            return c;
        }

        /// <summary>A snowflake: a 2 x 2 flake or a single soft point.</summary>
        public static PixelCanvas Snowflake(int kind)
        {
            var c = kind == 0 ? new PixelCanvas(2, 2) : new PixelCanvas(1, 1);
            c.Rect(0, 0, c.Width - 1, c.Height - 1, Hex(0xFFFFFF, kind == 0 ? (byte)230 : (byte)180));
            return c;
        }

        /// <summary>A firework spark, white for the theme's colours: a bright point with a soft cross.</summary>
        public static PixelCanvas Spark()
        {
            var c = new PixelCanvas(3, 3);
            c[1, 1] = Grey(1f);
            c[0, 1] = c[2, 1] = c[1, 0] = c[1, 2] = Grey(1f, 120);
            return c;
        }

        // ---- Naughty vs Nice ----

        /// <summary>The Halo (Nice's reward), a gold ring floating over the head, 14 x 6 with a soft glow.</summary>
        public static PixelCanvas Halo()
        {
            var c = new PixelCanvas(14, 6);
            c.Blit(new[]
            {
                "...gggggg...",
                ".gG......Gg.",
                "g..........g",
                ".gG......Gg.",
                "...gggggg...",
            }, new Dictionary<char, Color32> { ['g'] = Hex(0xFFE36A), ['G'] = Hex(0xFFF6C0) }, 1, 0);
            return c;
        }

        // ---- Valentine's ----

        /// <summary>The Heart gravestone (It's Complicated's reward), 12 x 16 like the stone, in its own colours.</summary>
        public static PixelCanvas HeartGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "............",
                "..pp....pp..",
                ".pwpp..pppp.",
                ".wppppppppP.",
                ".pppppppppP.",
                ".pppppppppP.",
                "..pppppppP..",
                "...ppppPP...",
                "....pppP....",
                ".....pP.....",
                ".....pP.....",
                ".....pP.....",
                "....sssss...",
                "...sssssss..",
                "mmmmmmmmmmmm",
                "mmmmmmmmmmmm",
            }, new Dictionary<char, Color32>
            {
                ['p'] = Hex(0xF2607A), ['P'] = Hex(0xC03A58), ['w'] = Hex(0xFFD8E0), ['s'] = Hex(0xB8C3CA), ['m'] = Hex(0x5A4636),
            }, 0, 0);
            c.Outline(Hex(0x3A1020));
            return c;
        }

        /// <summary>A heart over the Nemesis's head, in place of its horns.</summary>
        public static PixelCanvas NemesisHeart()
        {
            var c = new PixelCanvas(11, 9);
            c.Blit(new[]
            {
                ".rr.rr.",
                "rwrrrrr",
                "rrrrrrR",
                ".rrrrR.",
                "..rrR..",
                "...R...",
            }, new Dictionary<char, Color32> { ['r'] = Hex(0xFF3A5A), ['R'] = Hex(0xC01A38), ['w'] = Hex(0xFFD0D8) }, 2, 1);
            c.Outline(Hex(0x4A0A18));
            return c;
        }

        // ---- Halloween (switched off until next year) ----

        /// <summary>A jack-o'-lantern for a gravestone, 12 x 16 in its own colours.</summary>
        public static PixelCanvas PumpkinGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "............",
                "............",
                "............",
                ".....gg.....",
                "......g.....",
                "..ooOoOoo...",
                ".ooOooOoOoo.",
                "ooyyoOoyyOoo",
                "ooOoooOoooOo",
                "ooOyoyoyoyOo",
                "ooOyyyyyyyOo",
                ".ooOoooOooo.",
                "..ooOoOooo..",
                "....oooo....",
                "mmmmmmmmmmmm",
                "mmmmmmmmmmmm",
            }, new Dictionary<char, Color32>
            {
                ['o'] = Hex(0xF08A24), ['O'] = Hex(0xC0601A), ['g'] = Hex(0x3FA34D), ['y'] = Hex(0xFFE45C), ['m'] = Hex(0x4A3A2E),
            }, 0, 0);
            c.Outline(Hex(0x2A1408));
            return c;
        }

        /// <summary>A cobweb draped over a ghost's head from the back; pivot low so it hangs down over the head.</summary>
        public static PixelCanvas Cobweb()
        {
            var c = new PixelCanvas(12, 9);
            c.Blit(new[]
            {
                "w.w.w.w.....",
                ".w.www......",
                "wwwww.w.....",
                ".w.w.ww.....",
                "w.www.......",
                ".ww.w.......",
                "w.w.........",
                ".w..........",
            }, new Dictionary<char, Color32> { ['w'] = Hex(0xE8E8F0, 210) }, 0, 0);
            return c;
        }

        // ---- the test theme (development only) ----

        /// <summary>The spike painted in diagonal bands: same silhouette, still greyscale.</summary>
        public static PixelCanvas TestSpike() => Repaint(PixelWorld.Spike(), (x, y, p) => (x + y) / 3 % 2 == 0 ? p : Shade(p, 0.8f));

        /// <summary>The block in a checker of its own shades.</summary>
        public static PixelCanvas TestBlock() => Repaint(PixelWorld.Block(), (x, y, p) => (x / 4 + y / 4) % 2 == 0 ? p : Shade(p, 0.85f));

        public static PixelCanvas TestGroundTop() => Capped(PixelWorld.GroundTop(), 2, 0.6f);
        public static PixelCanvas TestFar() => Capped(PixelWorld.Mountains(), 4, 0.55f);
        public static PixelCanvas TestNear() => Capped(PixelWorld.GraveyardHills(), 3, 0.5f);

        /// <summary>The death screen's headstone in lilac, its face left plain for the epitaph.</summary>
        public static PixelCanvas TestHeadstone() => Repaint(PixelWorld.Headstone(), (x, y, p) =>
            new Color32((byte)Mathf.Min(255, p.r * 1.05f), (byte)(p.g * 0.86f), (byte)Mathf.Min(255, p.b * 1.12f), p.a));

        /// <summary>A gift-box grave in its own colours, 12 x 16 like the stone, mound in the bottom two rows.</summary>
        public static PixelCanvas TestGrave()
        {
            Color32 box = Hex(0xB46BE0), boxDark = Hex(0x8443B0), ribbon = Hex(0xFFD24A), mound = Hex(0x5A4636);
            var c = new PixelCanvas(12, 16);
            c.Rect(0, 0, 11, 1, mound);
            c.Rect(1, 2, 10, 10, box);
            c.Rect(9, 2, 10, 10, boxDark);
            c.Rect(5, 2, 6, 10, ribbon);
            c.Rect(1, 6, 10, 7, ribbon);
            c[4, 11] = c[3, 12] = c[7, 11] = c[8, 12] = ribbon;
            c.Outline(Hex(0x2A1838));
            return c;
        }

        /// <summary>A party hat for every ghost, 9 x 10 with outline; pivot just above the bottom so it sits on the head.</summary>
        public static PixelCanvas TestGhostAccessory()
        {
            var c = new PixelCanvas(9, 10);
            c.Blit(new[]
            {
                "...y...",
                "...p...",
                "..ppP..",
                "..pyP..",
                ".pppPP.",
                ".pypyP.",
                "ppppPPP",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                ['p'] = Hex(0xFF7AC8), ['P'] = Hex(0xD04E98), ['y'] = Hex(0xFFE36A),
            }, 1, 1);
            c.Outline(Hex(0x3A1A30));
            return c;
        }

        /// <summary>A crown for the Nemesis, 14 x 7 with outline, in place of its horns.</summary>
        public static PixelCanvas TestNemesisAccessory()
        {
            var c = new PixelCanvas(16, 9);
            c.Blit(new[]
            {
                "g.....g.....g.",
                "gg...ggg...gg.",
                "ggg.ggrgg.ggg.",
                "gggggggggggggG",
                "GgbgggGgggbgGG",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                ['g'] = Hex(0xFFD24A), ['G'] = Hex(0xC9962A), ['r'] = Hex(0xFF4A5A), ['b'] = Hex(0x5AB4FF),
            }, 1, 1);
            c.Outline(Hex(0x3A2A10));
            return c;
        }

        /// <summary>Confetti: a 2 x 2 square and a 1 x 3 strip, white for the theme's colours.</summary>
        public static PixelCanvas Confetti(int kind)
        {
            var c = kind == 0 ? new PixelCanvas(2, 2) : new PixelCanvas(1, 3);
            c.Rect(0, 0, c.Width - 1, c.Height - 1, Grey(1f));
            return c;
        }
    }
}
