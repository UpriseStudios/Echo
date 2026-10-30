using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;
using static Echo.Editor.Art.PixelWorldsArt;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Four more store worlds (a user revision), painted in their own colours rather than greyscale to be tinted: a
    /// world with its own scenery stands for the whole run with no nightfall, so its layers are shown as drawn (their
    /// tints are white). Beach, Desert Dunes, Volcanic Waste (Pompeii) and Toy Town, the wild cartoon one, brightest
    /// of all. Same sizes as the layers they replace (far 512 x 224, near 512 x 112), tiling sideways.
    /// </summary>
    public static class PixelColourWorlds
    {
        static Color32 C(uint rgb) => Hex(rgb);

        static void Cloud(PixelCanvas c, int x, int y, int size, Color32 fill, Color32 shade)
        {
            Disc(c, x, y, size, fill);
            Disc(c, x + size, y + 2, size * 0.8f, fill);
            Disc(c, x - size, y + 1, size * 0.7f, fill);
            Disc(c, x + size / 2, y + size * 0.7f, size * 0.8f, fill);
            Box(c, x - size - 2, y - size / 2, x + size * 2 - 2, y - size / 2 + 1, shade);
        }

        static void Palm(PixelCanvas c, int x, int g, int h, int lean)
        {
            int topX = x;
            for (int y = 0; y < h; y++)
            {
                int px = x + lean * y * y / (h * 3);
                Box(c, px, g + y, px + 2, g + y, (y / 3) % 2 == 0 ? C(0x9A6A38) : C(0x7A5028));
                topX = px + 1;
            }
            int top = g + h;
            foreach (var (dx, dy) in new[] { (-14, -6), (-10, 3), (12, -5), (9, 4), (0, 6), (-6, -9), (7, -9) })
            {
                Line(c, topX, top, topX + dx, top + dy, C(0x2E9A48));
                Line(c, topX, top + 1, topX + dx, top + dy + 1, C(0x48C060));
            }
            Disc(c, topX - 1, top - 2, 1.6f, C(0x6A4A20));
            Disc(c, topX + 2, top - 2, 1.6f, C(0x6A4A20));
        }

        // ================= Beach =================

        public static PixelCanvas BeachFar()
        {
            var c = new PixelCanvas(W, FarH);
            Disc(c, 390, 150, 24, C(0xFFE6A0));                       // the sun, low and warm
            Disc(c, 390, 150, 19, C(0xFFF4C8));
            for (int y = 0; y < 62; y++)                              // the sea, lighter towards the horizon
                for (int x = 0; x < W; x++)
                {
                    Color32 sea = y > 56 ? C(0xBCE8F8) : y > 44 ? C(0x5AB4E0) : y > 24 ? C(0x3A92CC) : C(0x2A74B4);
                    if (y <= 56 && Hash(x, y) % 23 == 0) sea = C(0xBCE8F8);   // glints
                    c[x, y] = sea;
                }
            for (int y = 40; y < 62; y++) Box(c, 380 + (62 - y) / 3, y, 400 - (62 - y) / 3, y, C(0xFFE6A0));   // the sun's path
            // A little island with palms, and a sailing boat.
            for (int i = 0; i < 16; i++) Box(c, 90 + i, 62 + i / 2, 170 - i, 62 + i / 2, C(0x3A9A50));
            Palm(c, 120, 70, 24, 1);
            Palm(c, 140, 70, 18, -1);
            Box(c, 236, 60, 262, 63, C(0x9A4A2A));
            for (int i = 0; i < 22; i++) Box(c, 249 - i / 3, 64 + i, 249, 64 + i, C(0xFFFFFF));
            for (int i = 0; i < 14; i++) Box(c, 251, 64 + i, 251 + i / 2, 64 + i, C(0xFF5A5A));
            foreach (var (x, y, s) in new[] { (60, 170, 9), (200, 186, 7), (300, 196, 6), (470, 178, 8) })
                Cloud(c, x, y, s, C(0xFFFFFF), C(0xD8EEF8));
            return c;
        }

        public static PixelCanvas BeachNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] sand = Ridge(16, 4, 2, 2, 7, 0.5f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= sand[x]; y++)
                    c[x, y] = y >= sand[x] - 1 ? C(0xFFF0C8) : Bayer(x, y) < 0.25f ? C(0xE8C888) : C(0xF2D8A0);
            foreach (var (x, h, lean) in new[] { (40, 46, 2), (100, 38, -2), (300, 52, 3), (470, 42, -2) })
                Palm(c, x, sand[x], h, lean);
            // A striped umbrella over a towel; a sandcastle; a lifeguard's chair.
            int u = 190, ug = sand[u];
            Box(c, u, ug, u, ug + 26, C(0xFFFFFF));
            for (int i = 0; i < 10; i++)
                for (int x = u - 18 + i; x <= u + 18 - i; x++)
                    Plot(c, x, ug + 26 + i, ((x - u + 18) / 6) % 2 == 0 ? C(0xFF4A5A) : C(0xFFFFFF));
            Box(c, u - 14, ug, u + 4, ug + 1, C(0x3A8AE8));
            int s = 360, sg = sand[s];
            Box(c, s - 10, sg, s + 10, sg + 8, C(0xE0B868));
            foreach (int tx in new[] { s - 10, s - 1, s + 7 })
            {
                Box(c, tx, sg + 8, tx + 3, sg + 14, C(0xE0B868));
                Box(c, tx, sg + 15, tx, sg + 15, C(0xE0B868));
                Box(c, tx + 2, sg + 15, tx + 3, sg + 15, C(0xE0B868));
            }
            Box(c, s - 1, sg + 15, s, sg + 22, C(0x8A5A30));
            Box(c, s + 1, sg + 19, s + 6, sg + 21, C(0xFF4A5A));
            int l = 430, lg = sand[l];
            Line(c, l - 6, lg, l - 2, lg + 22, C(0xFFFFFF));
            Line(c, l + 6, lg, l + 2, lg + 22, C(0xFFFFFF));
            Box(c, l - 7, lg + 22, l + 7, lg + 32, C(0xE83A3A));
            Box(c, l - 4, lg + 25, l + 4, lg + 29, C(0xFFFFFF));
            return c;
        }

        public static PixelCanvas Gull() => Sprite(7, 3, "#.....#", ".#...#.", "..#.#..");

        public static PixelCanvas PlaneWithBanner()
        {
            var c = new PixelCanvas(60, 12);
            c.Blit(new[]
            {
                "......................................................b.....",
                "....................................................bbbbb...",
                "rrrrrrrrrrrrrrrrrrrrrrrrrrr..................wwwwwwwwwwwwbbb",
                "ryyyyyyyyyyyyyyyyyyyyyyyyyr..................wwwwwwwwwwwwwww",
                "ryyrryyrryyrryyrryyrryyyyyr-----------------wwwwwwwwwwwwwwww",
                "ryyyyyyyyyyyyyyyyyyyyyyyyyr..................wwwwwwwwwwwwww.",
                "rrrrrrrrrrrrrrrrrrrrrrrrrrr.......................bbbbb.....",
                "...........................................................",
            }, new Dictionary<char, Color32>
            {
                ['r'] = C(0xE83A3A), ['y'] = C(0xFFF4C8), ['w'] = C(0xFFFFFF), ['b'] = C(0x3A6AE8), ['-'] = C(0x6A6A6A),
            }, 0, 2);
            return c;
        }

        // ================= Desert Dunes =================

        public static PixelCanvas DesertFar()
        {
            var c = new PixelCanvas(W, FarH);
            Disc(c, 420, 168, 22, C(0xFFF6D8));                      // a pale, blazing sun
            // Mesas on the horizon.
            foreach (var (x, w, h) in new[] { (30, 60, 52), (330, 46, 44), (470, 50, 58) })
            {
                Box(c, x, 0, x + w, h, C(0xC8784A));
                Box(c, x + w - 8, 0, x + w, h, C(0xA85A38));
                Box(c, x - 3, h, x + w + 3, h + 3, C(0xD88A58));
            }
            int[] back = Ridge(40, 12, 2, 5, 5, 0.3f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= back[x]; y++)
                    c[x, y] = (back[(x + 1) % W] > back[x] ? C(0xD89A50) : C(0xEAB468));
            // The pyramids: lit faces and shaded faces.
            foreach (var (x, size) in new[] { (150, 54), (214, 34), (250, 22) })
            {
                int g = back[x] - 4;
                for (int i = 0; i < size; i++)
                {
                    Box(c, x - size + i, g + i, x, g + i, C(0xF0CC88));
                    Box(c, x + 1, g + i, x + size - i, g + i, C(0xB88A50));
                }
            }
            int[] front = Ridge(18, 8, 3, 3, 8, 1.6f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= front[x]; y++)
                    c[x, y] = front[(x + 1) % W] > front[x] ? C(0xC8843E) : C(0xE2A456);
            return c;
        }

        public static PixelCanvas DesertNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] sand = Ridge(14, 5, 2, 2, 6, 2.4f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= sand[x]; y++)
                    c[x, y] = Bayer(x, y) < 0.2f ? C(0xD8A058) : C(0xE8B868);
            // Saguaro cacti with their arms up.
            foreach (var (x, h) in new[] { (50, 40), (180, 30), (330, 46), (450, 34) })
            {
                int g = sand[x];
                Box(c, x, g, x + 4, g + h, C(0x3A9A48));
                Box(c, x + 1, g, x + 1, g + h, C(0x5ABA60));
                Box(c, x - 7, g + h / 2, x - 1, g + h / 2 + 3, C(0x3A9A48));
                Box(c, x - 7, g + h / 2, x - 4, g + h / 2 + 14, C(0x3A9A48));
                Box(c, x + 5, g + h / 3, x + 10, g + h / 3 + 3, C(0x3A9A48));
                Box(c, x + 7, g + h / 3, x + 10, g + h / 3 + 11, C(0x3A9A48));
                Disc(c, x + 2, g + h + 1, 2, C(0xFF6A8A));   // a flower on top
            }
            // Rocks, a bleached skull and a tumbleweed.
            foreach (int rx in new[] { 110, 260, 400 }) Disc(c, rx, sand[rx] + 3, 6, C(0xA8704A));
            int s = 230, sg = sand[s];
            Box(c, s - 4, sg, s + 4, sg + 5, C(0xF4ECDC));
            Box(c, s - 2, sg + 1, s - 1, sg + 2, C(0x5A4A3A));
            Box(c, s + 1, sg + 1, s + 2, sg + 2, C(0x5A4A3A));
            Line(c, s - 4, sg + 5, s - 10, sg + 9, C(0xF4ECDC));
            Line(c, s + 4, sg + 5, s + 10, sg + 9, C(0xF4ECDC));
            int t = 300, tg = sand[t];
            for (int i = 0; i < 18; i++)
                Plot(c, t + Mathf.RoundToInt(5 * Mathf.Cos(i * 2.1f)), tg + 6 + Mathf.RoundToInt(5 * Mathf.Sin(i * 1.7f)), C(0x9A7A48));
            return c;
        }

        public static PixelCanvas SandWisp() => PixelModeArt.Fog(1);

        public static PixelCanvas Vulture() => Sprite(17, 6,
            "##.............##",
            ".###.........###.",
            "..####.###.####..",
            "....#########....",
            "......#####......",
            ".......###.......");

        // ================= Volcanic Waste (Pompeii) =================

        public static PixelCanvas VolcanoFar()
        {
            var c = new PixelCanvas(W, FarH);
            // The volcano, with a smoke plume and lava running down its flanks.
            int v = 300;
            for (int y = 0; y < 150; y++)
            {
                int half = 150 - y;
                // A near-black silhouette against the red sky, so its lava and smoke read.
                Box(c, v - half, y, v + half, y, y > 140 ? C(0x3A1810) : C(0x1E1614));
                Box(c, v, y, v + half, y, C(0x140E0E));
            }
            Box(c, v - 10, 145, v + 10, 150, C(0xFF6A1A));
            foreach (int dir in new[] { -1, 1 })
                for (int y = 144; y > 30; y -= 2)
                    Box(c, v + dir * (150 - y) / 3 + dir * (y % 7) / 3, y, v + dir * (150 - y) / 3 + 1, y + 1, y % 6 < 3 ? C(0xFF8A2A) : C(0xFFC04A));
            foreach (var (dx, dy, r) in new[] { (0, 160, 9), (6, 172, 11), (-4, 186, 13), (10, 198, 14), (-2, 212, 15) })
                Disc(c, v + dx, dy, r, dy > 190 ? C(0x5A4A48) : C(0x6A5854));
            // The ruined city in front: walls, roofless houses and columns.
            int[] ground = Ridge(20, 4, 3, 2, 9, 0.8f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= ground[x]; y++) c[x, y] = C(0x2E2420);
            foreach (var (x, w, h) in new[] { (20, 40, 26), (80, 28, 18), (150, 36, 30), (420, 44, 24), (480, 26, 16) })
            {
                Box(c, x, ground[x], x + w, ground[x] + h, C(0x9A8070));
                Box(c, x + 4, ground[x] + 4, x + w - 4, ground[x] + h - 6, C(0x5A4038));   // roofless, open to the sky
                Box(c, x + w / 2 - 2, ground[x], x + w / 2 + 2, ground[x] + 10, C(0x2E2420));
            }
            foreach (var (x, h) in new[] { (120, 40), (130, 34), (140, 22), (390, 38), (402, 28) })
            {
                Box(c, x, ground[x], x + 4, ground[x] + h, C(0xB8A090));
                Box(c, x - 1, ground[x] + h, x + 5, ground[x] + h + 1, C(0xB8A090));
            }
            return c;
        }

        public static PixelCanvas VolcanoNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] rock = Ridge(16, 6, 3, 3, 8, 1.1f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= rock[x]; y++)
                    c[x, y] = Bayer(x, y) < 0.25f ? C(0x3A2E2C) : C(0x2A2222);
            // Glowing cracks of lava through the rock.
            foreach (int x0 in new[] { 30, 140, 250, 370, 460 })
            {
                int x = x0, y = 2;
                for (int i = 0; i < 16 && y < rock[x0] - 1; i++)
                {
                    Plot(c, x, y, i % 3 == 0 ? C(0xFFC04A) : C(0xFF6A1A));
                    x += (Hash(x0, i) % 3) - 1;
                    y++;
                }
            }
            // Broken columns, a fallen capital and an amphora in the ash.
            foreach (var (x, h) in new[] { (80, 30), (96, 18), (300, 36), (420, 22) })
            {
                Box(c, x, rock[x], x + 6, rock[x] + h, C(0xC8B4A0));
                Box(c, x + 2, rock[x], x + 2, rock[x] + h, C(0xA8947E));
                for (int i = 0; i < 4; i++) Plot(c, x + 1 + i, rock[x] + h + 1 + (i % 2), C(0xC8B4A0));   // the jagged break
            }
            int f = 200, fg = rock[f];
            Box(c, f - 10, fg, f + 10, fg + 4, C(0xC8B4A0));
            Box(c, f - 12, fg + 5, f + 12, fg + 6, C(0xC8B4A0));
            int a = 350, ag = rock[a];
            Disc(c, a, ag + 5, 5, C(0xC0683A));
            Box(c, a - 2, ag + 10, a + 2, ag + 14, C(0xC0683A));
            return c;
        }

        public static PixelCanvas Ash(int kind) => kind == 0 ? Sprite(2, 2, "##", "##") : Sprite(1, 1, "#");

        public static PixelCanvas LavaBomb() => Sprite(14, 6,
            "..........####",
            ".......+######",
            "...++++#######",
            "+++++++#######",
            "...++++######.",
            "........###...");

        // ================= Toy Town (the wild cartoon world) =================

        public static PixelCanvas ToyFar()
        {
            var c = new PixelCanvas(W, FarH);
            // A rainbow.
            Color32[] bands = { C(0xFF4A4A), C(0xFF9A2A), C(0xFFE03A), C(0x5ADA4A), C(0x3A9AFF), C(0x9A5AFF) };
            for (int b = 0; b < bands.Length; b++)
                for (int a = 0; a <= 180; a++)
                {
                    float rad = a * Mathf.Deg2Rad;
                    for (int t = 0; t < 4; t++)
                    {
                        float r = 96 - b * 4 - t;
                        Plot(c, 260 + Mathf.RoundToInt(Mathf.Cos(rad) * r), 40 + Mathf.RoundToInt(Mathf.Sin(rad) * r), bands[b]);
                    }
                }
            // A smiling sun.
            Disc(c, 70, 176, 22, C(0xFFE03A));
            Disc(c, 62, 182, 2.5f, C(0x2A1A3A));
            Disc(c, 78, 182, 2.5f, C(0x2A1A3A));
            for (int i = -8; i <= 8; i++) Plot(c, 70 + i, 170 - (64 - i * i) / 16, C(0x2A1A3A));
            foreach (var (x, y, s) in new[] { (170, 190, 9), (360, 182, 10), (470, 200, 7) })
                Cloud(c, x, y, s, C(0xFFFFFF), C(0xC8E8FF));
            // Rolling hills in candy colours, polka-dotted.
            foreach (var (ridge, fill, dot) in new[]
                     {
                         (Ridge(64, 18, 2, 8, 5, 0.5f), C(0xFF8AC8), C(0xFFC8E8)),
                         (Ridge(36, 12, 3, 6, 7, 2.0f), C(0x6AE05A), C(0xB8FF9A)),
                     })
                for (int x = 0; x < W; x++)
                    for (int y = 0; y <= ridge[x]; y++)
                        c[x, y] = y == ridge[x] ? C(0x2A1A3A) : (x % 14 < 3 && y % 12 < 3 && (x / 14 + y / 12) % 2 == 0) ? dot : fill;
            // A candy castle: striped towers with blue cones and flags.
            int k = 420, kg = 50;
            Box(c, k - 20, kg, k + 20, kg + 30, C(0xFFF0F8));
            for (int y = kg; y < kg + 30; y += 6) Box(c, k - 20, y, k + 20, y + 2, C(0xFF8AC8));
            foreach (int tx in new[] { k - 26, k + 18 })
            {
                Box(c, tx, kg, tx + 8, kg + 46, C(0xFFF0F8));
                for (int y = kg; y < kg + 46; y += 6) Box(c, tx, y, tx + 8, y + 2, C(0x8AD8FF));
                for (int i = 0; i < 12; i++) Box(c, tx - 1 + i / 2, kg + 47 + i, tx + 9 - i / 2, kg + 47 + i, C(0x3A6AFF));
                Box(c, tx + 4, kg + 59, tx + 4, kg + 66, C(0x2A1A3A));
                Box(c, tx + 5, kg + 63, tx + 9, kg + 66, C(0xFFE03A));
            }
            Box(c, k - 4, kg, k + 4, kg + 12, C(0xFF4A8A));
            return c;
        }

        public static PixelCanvas ToyNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] grass = Ridge(16, 4, 3, 2, 9, 0.9f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= grass[x]; y++)
                    c[x, y] = y >= grass[x] - 1 ? C(0x2A8A3A) : y >= grass[x] - 3 ? C(0x7AF05A) : C(0x4ACB3A);
            // Giant spotted mushrooms.
            foreach (var (x, h, cap) in new[] { (40, 20, 0xFF3A4A), (230, 26, 0x9A5AFF), (420, 18, 0xFF9A2A) })
            {
                int g = grass[x];
                Box(c, x - 3, g, x + 3, g + h, C(0xFFF4DC));
                for (int i = 0; i < 10; i++) Box(c, x - 14 + i, g + h + i, x + 14 - i, g + h + i, C((uint)cap));
                foreach (var (dx, dy) in new[] { (-7, 3), (3, 5), (8, 2), (-2, 8) }) Disc(c, x + dx, g + h + dy, 1.6f, C(0xFFFFFF));
                Line(c, x - 15, g + h, x + 15, g + h, C(0x2A1A3A));
            }
            // Swirly lollipops on sticks.
            foreach (var (x, colour) in new[] { (120, 0xFF5AC8), (310, 0x3AC8FF), (480, 0xFFE03A) })
            {
                int g = grass[x];
                Box(c, x, g, x + 1, g + 24, C(0xFFFFFF));
                Disc(c, x, g + 32, 9, C((uint)colour));
                for (int i = 0; i < 28; i++)
                {
                    float a = i * 0.45f, r = i * 0.3f;
                    Plot(c, x + Mathf.RoundToInt(Mathf.Cos(a) * r), g + 32 + Mathf.RoundToInt(Mathf.Sin(a) * r), C(0xFFFFFF));
                }
            }
            // Big daisies in every colour.
            uint[] petals = { 0xFF4A8A, 0xFFE03A, 0x9A5AFF, 0x3AC8FF, 0xFF9A2A };
            for (int i = 0, x = 70; x < W; x += 55, i++)
            {
                int g = grass[x], h = 10 + Hash(x, 81) % 8;
                Box(c, x, g, x, g + h, C(0x2A8A3A));
                foreach (var (dx, dy) in new[] { (-3, 0), (3, 0), (0, 3), (0, -3), (-2, 2), (2, 2), (-2, -2), (2, -2) })
                    Disc(c, x + dx, g + h + dy, 1.8f, C(petals[i % petals.Length]));
                Disc(c, x, g + h, 1.6f, C(0xFFFFFF));
            }
            return c;
        }

        public static PixelCanvas Star() => Sprite(5, 5, "..#..", ".###.", "#####", ".###.", ".#.#.");

        public static PixelCanvas Heart() => Sprite(5, 4, ".#.#.", "#####", ".###.", "..#..");

        public static PixelCanvas Balloon()
        {
            var c = new PixelCanvas(16, 24);
            Color32[] stripes = { C(0xFF3A4A), C(0xFFE03A), C(0x3AC8FF), C(0x5ADA4A) };
            for (int y = 0; y < 16; y++)
            {
                int half = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, 64 - (y - 8) * (y - 8) * (y < 8 ? 1.4f : 1f))));
                for (int x = 8 - half; x < 8 + half; x++) c[x, 8 + y] = stripes[((x + 16) / 4) % stripes.Length];
            }
            Line(c, 4, 9, 6, 4, C(0x8A6A4A));
            Line(c, 11, 9, 9, 4, C(0x8A6A4A));
            c.Rect(5, 0, 10, 3, C(0x9A6A38));
            c.Rect(5, 3, 10, 3, C(0x7A5028));
            return c;
        }
    }
}
