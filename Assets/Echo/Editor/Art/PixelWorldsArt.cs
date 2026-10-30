using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The store's worlds (a user revision): Neon Night, Sunken Ruins, Autumn Woods and Halloween Night (also the
    /// Halloween season's look). Each is a far layer (512 x 224, like the mountains) and a near layer (512 x 112, like the
    /// graveyard hills) that tile sideways, small sprites for its effect, and a silhouette that crosses the sky now and
    /// then. Greyscale like every layer, so the views tint them; the lightest greys (windows, the moon, signs) glow.
    /// </summary>
    public static class PixelWorldsArt
    {
        internal const int W = 512, FarH = 224, NearH = 112;

        // Wrapping plots, so a tile's edges meet.
        internal static void Plot(PixelCanvas c, int x, int y, Color32 col) => c[((x % W) + W) % W, y] = col;

        internal static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 col)
        {
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
                for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
                    Plot(c, x, y, col);
        }

        internal static void Line(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 col)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= steps; i++)
                Plot(c, Mathf.RoundToInt(Mathf.Lerp(x0, x1, i / (float)steps)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, i / (float)steps)), col);
        }

        internal static void Disc(PixelCanvas c, float cx, float cy, float r, Color32 col)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Plot(c, x, y, col);
        }

        internal static int Hash(int x, int y) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u);

        // A wrapping ridge: the base height plus two sine waves.
        internal static int[] Ridge(float baseH, float a1, float f1, float a2, float f2, float phase)
        {
            var h = new int[W];
            for (int x = 0; x < W; x++)
            {
                float t = x / (float)W * Mathf.PI * 2f;
                h[x] = Mathf.RoundToInt(baseH + a1 * Mathf.Sin(t * f1 + phase) + a2 * Mathf.Sin(t * f2 + phase * 2.3f));
            }
            return h;
        }

        internal static void FillRidge(PixelCanvas c, int[] top, float v, float dither)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= top[x] && y < c.Height; y++)
                    c[x, y] = Grey(Bayer(x, y) < dither * (1f - y / (float)Mathf.Max(1, top[x])) ? v - 0.04f : v);
        }

        internal static PixelCanvas Sprite(int w, int h, params string[] rows)
        {
            var c = new PixelCanvas(w, h);
            c.Blit(rows, new Dictionary<char, Color32> { ['#'] = Grey(1f), ['+'] = Grey(1f, 150) }, 0, 0);
            return c;
        }

        // ================= Neon Night =================

        public static PixelCanvas NeonFar()
        {
            var c = new PixelCanvas(W, FarH);
            // Back towers, tall and pale; then a front row, darker, both with lit window grids.
            for (int pass = 0; pass < 2; pass++)
            {
                float v = pass == 0 ? 0.82f : 0.7f;
                int x = pass == 0 ? 0 : 9;
                while (x < W)
                {
                    int w = 18 + Hash(x, pass) % 22, h = pass == 0 ? 90 + Hash(x, 7) % 100 : 40 + Hash(x, 9) % 70;
                    Box(c, x, 0, x + w - 2, h, Grey(v));
                    if (Hash(x, 3) % 3 == 0) Box(c, x + w / 2 - 1, h, x + w / 2 - 1, h + 10 + Hash(x, 4) % 14, Grey(v));   // an antenna
                    if (Hash(x, 5) % 4 == 0) Box(c, x + 3, h + 1, x + w - 5, h + 4, Grey(v));                              // a stepped top
                    for (int wy = 6; wy < h - 4; wy += 6)
                        for (int wx = x + 3; wx < x + w - 4; wx += 4)
                            if (Hash(wx, wy + pass) % 5 < (pass == 0 ? 2 : 3)) Box(c, wx, wy, wx + 1, wy + 2, Grey(pass == 0 ? 0.92f : 0.98f));
                    x += w + (pass == 0 ? 2 : 6 + Hash(x, 11) % 10);
                }
            }
            return c;
        }

        public static PixelCanvas NeonNear()
        {
            var c = new PixelCanvas(W, NearH);
            int x = 0;
            while (x < W)
            {
                int w = 30 + Hash(x, 21) % 34, h = 18 + Hash(x, 22) % 40;
                Box(c, x, 0, x + w - 1, h, Grey(0.66f));
                Box(c, x, h, x + w - 1, h + 1, Grey(0.74f));   // the roof's lip
                // A neon sign: a bright strip with a frame, sometimes vertical.
                if (Hash(x, 23) % 2 == 0)
                {
                    int sx = x + 4, sy = h - 12;
                    Box(c, sx, sy, sx + 16, sy + 6, Grey(0.5f));
                    Box(c, sx + 2, sy + 2, sx + 14, sy + 4, Grey(1f));
                }
                else
                {
                    int sx = x + w - 8, sy = h - 30;
                    if (sy > 4)
                    {
                        Box(c, sx, sy, sx + 5, sy + 24, Grey(0.5f));
                        for (int k = 0; k < 4; k++) Box(c, sx + 2, sy + 3 + k * 5, sx + 3, sy + 5 + k * 5, Grey(1f));
                    }
                }
                // Rooftop clutter: a water tower or an air-con box and a satellite dish.
                if (Hash(x, 24) % 3 == 0)
                {
                    int tx = x + w / 2;
                    Box(c, tx - 1, h + 2, tx - 1, h + 8, Grey(0.6f));
                    Box(c, tx + 7, h + 2, tx + 7, h + 8, Grey(0.6f));
                    Box(c, tx - 2, h + 9, tx + 8, h + 18, Grey(0.62f));
                    Box(c, tx - 1, h + 19, tx + 7, h + 20, Grey(0.62f));
                    Box(c, tx + 3, h + 21, tx + 3, h + 21, Grey(0.62f));
                }
                else
                {
                    Box(c, x + 6, h + 2, x + 13, h + 6, Grey(0.6f));
                    Line(c, x + w - 10, h + 2, x + w - 6, h + 8, Grey(0.6f));
                    Box(c, x + w - 9, h + 7, x + w - 4, h + 8, Grey(0.6f));
                }
                // Fire-escape rails down the side.
                for (int fy = 8; fy < h - 6; fy += 10) Box(c, x + 1, fy, x + 8, fy, Grey(0.56f));
                x += w + 3 + Hash(x, 25) % 6;
            }
            return c;
        }

        public static PixelCanvas RainStreak() => Sprite(1, 5, "+", "#", "#", "#", "+");

        public static PixelCanvas HoverCar() => Sprite(34, 9,
            "..........######..................",
            ".......############...............",
            ".....################.............",
            "##################################",
            "##################################",
            ".################################.",
            "....######..............######....",
            ".....++++................++++.....",
            "......++..................++......");

        // ================= Sunken Ruins =================

        public static PixelCanvas SunkenFar()
        {
            var c = new PixelCanvas(W, FarH);
            int[] dunes = Ridge(30, 8, 2, 4, 7, 0.4f);
            FillRidge(c, dunes, 0.8f, 0.3f);
            // Shafts of light slanting down from the surface.
            foreach (int sx in new[] { 40, 170, 300, 420 })
                for (int y = 60; y < FarH; y++)
                    for (int k = 0; k < 10; k++)
                        if (Hash(sx + k, y) % 3 == 0) Plot(c, sx + k + (FarH - y) / 3, y, Grey(1f, 26));
            // A ruined temple: a pediment on four columns, one broken; and lone columns along the seabed.
            int t = 250, g = dunes[t];
            for (int col = 0; col < 4; col++)
            {
                int cx = t + col * 14, top = col == 3 ? g + 34 : g + 58;
                Box(c, cx, g, cx + 6, top, Grey(0.88f));
                Box(c, cx - 1, top, cx + 7, top + 2, Grey(0.9f));
                for (int fy = g + 4; fy < top; fy += 3) Plot(c, cx + 2, fy, Grey(0.82f));   // fluting
            }
            Box(c, t - 4, g + 61, t + 52, g + 64, Grey(0.9f));
            for (int i = 0; i < 14; i++) Box(c, t - 2 + i * 2, g + 65 + i, t + 32 - i * 2 + (i < 7 ? 0 : -2), g + 65 + i, Grey(0.9f));
            Box(c, t + 44, g + 65, t + 54, g + 70, Clear);   // the broken corner
            Box(c, t + 46, g, t + 58, g + 5, Grey(0.86f));    // the fallen piece
            foreach (var (x, h) in new[] { (60, 48), (96, 30), (140, 64), (380, 52), (458, 36) })
            {
                Box(c, x, dunes[x], x + 6, dunes[x] + h, Grey(0.86f));
                Box(c, x - 1, dunes[x] + h, x + 7, dunes[x] + h + 2, Grey(0.88f));
            }
            // An arch.
            int a = 180;
            Box(c, a, dunes[a], a + 6, dunes[a] + 40, Grey(0.86f));
            Box(c, a + 34, dunes[a], a + 40, dunes[a] + 40, Grey(0.86f));
            for (int i = 0; i <= 20; i++)
            {
                float k = i / 20f * Mathf.PI;
                int x = a + 3 + Mathf.RoundToInt((1 - Mathf.Cos(k)) * 17), y = dunes[a] + 40 + Mathf.RoundToInt(Mathf.Sin(k) * 14);
                Box(c, x - 2, y - 2, x + 2, y + 2, Grey(0.86f));
            }
            return c;
        }

        public static PixelCanvas SunkenNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] floor = Ridge(14, 4, 3, 2, 9, 1.3f);
            FillRidge(c, floor, 0.72f, 0.4f);
            // Kelp: tall wavy strands with leaves.
            foreach (int kx in new[] { 18, 30, 92, 104, 160, 236, 248, 330, 404, 416, 480 })
            {
                int height = 40 + Hash(kx, 31) % 50;
                for (int y = 0; y < height; y++)
                {
                    int x = kx + Mathf.RoundToInt(3 * Mathf.Sin((y + kx) * 0.18f));
                    Box(c, x, floor[kx] + y, x + 1, floor[kx] + y, Grey(0.62f));
                    if (y % 9 == 4) Box(c, x + 2, floor[kx] + y, x + 5, floor[kx] + y + 1, Grey(0.62f));
                    if (y % 9 == 0 && y > 0) Box(c, x - 4, floor[kx] + y, x - 1, floor[kx] + y + 1, Grey(0.62f));
                }
            }
            // Rocks, coral fans and an amphora half buried; an anchor on its side.
            foreach (int rx in new[] { 60, 200, 290, 450 })
                Disc(c, rx, floor[rx] + 2, 7 + Hash(rx, 33) % 5, Grey(0.66f));
            foreach (int fx in new[] { 128, 360, 500 })
                for (int i = 0; i < 7; i++)
                    Line(c, fx, floor[fx], fx - 9 + i * 3, floor[fx] + 10 + (i % 3) * 3, Grey(0.74f));
            int am = 270, ag = floor[am];
            Disc(c, am, ag + 6, 6, Grey(0.7f));
            Box(c, am - 2, ag + 11, am + 2, ag + 16, Grey(0.7f));
            Box(c, am - 4, ag + 16, am + 4, ag + 17, Grey(0.7f));
            int an = 430, gn = floor[an];
            Box(c, an, gn + 2, an + 22, gn + 3, Grey(0.6f));
            Line(c, an, gn + 2, an - 3, gn + 8, Grey(0.6f));
            Line(c, an, gn + 3, an - 3, gn - 1, Grey(0.6f));
            Disc(c, an + 24, gn + 3, 2.2f, Grey(0.6f));
            return c;
        }

        public static PixelCanvas Bubble(int kind) => kind == 0
            ? Sprite(4, 4, ".##.", "#..#", "#..#", ".##.")
            : Sprite(2, 2, "##", "##");

        public static PixelCanvas Whale() => Sprite(64, 20,
            "...............................................................#",
            "..............................................................##",
            ".............................................................###",
            "....................##############..........................####",
            "...............#########################..................#####.",
            "...........###############################...............######..",
            "........#####################################..........#######...",
            "......#########################################......########....",
            ".....###########################################...########.....",
            "....##########################################################...",
            "...#########################################################.....",
            "...########################################################......",
            "....######################################################.......",
            ".....###################################################.........",
            "......++++#############################################..........",
            "........++++++#####################################..............",
            "...........++++++++######.......########.........................",
            "..................+++++###.........#####.........................",
            "........................###.........###..........................",
            "................................................................");

        // ================= Autumn Woods =================

        public static PixelCanvas AutumnFar()
        {
            var c = new PixelCanvas(W, FarH);
            int[] back = Ridge(96, 22, 2, 8, 5, 0.9f), front = Ridge(58, 14, 3, 6, 8, 2.2f);
            FillRidge(c, back, 0.88f, 0.25f);
            // Tree crowns along each ridge: rows of little rounded tops.
            foreach (var (ridge, v, step) in new[] { (back, 0.88f, 7), (front, 0.78f, 6) })
                for (int x = 0; x < W; x += step)
                    Disc(c, x + Hash(x, 41) % 3, ridge[x] + 2, 4 + Hash(x, 42) % 3, Grey(v));
            FillRidge(c, front, 0.78f, 0.3f);
            for (int x = 0; x < W; x += 6) Disc(c, x + Hash(x, 43) % 3, front[x] + 2, 4 + Hash(x, 44) % 3, Grey(0.78f));
            // A windmill on the back ridge.
            int m = 300, g = back[m];
            Box(c, m - 4, g, m + 4, g + 26, Grey(0.94f));
            for (int i = 0; i < 6; i++) Box(c, m - 4 + i, g + 27 + i, m + 4 - i, g + 27 + i, Grey(0.94f));
            int hub = g + 26;
            foreach (var (dx, dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                Line(c, m, hub, m + dx * 14, hub + dy * 14, Grey(0.94f));
            return c;
        }

        public static PixelCanvas AutumnNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] ground = Ridge(16, 5, 2, 3, 6, 0.2f);
            FillRidge(c, ground, 0.72f, 0.35f);
            // Big trees: a trunk with branches and clumps of leaves.
            foreach (int tx in new[] { 40, 150, 260, 390, 470 })
            {
                int g = ground[tx], h = 34 + Hash(tx, 51) % 18;
                Box(c, tx - 2, g, tx + 2, g + h, Grey(0.6f));
                Line(c, tx, g + h - 8, tx - 10, g + h + 4, Grey(0.6f));
                Line(c, tx, g + h - 4, tx + 11, g + h + 6, Grey(0.6f));
                foreach (var (dx, dy, r) in new[] { (0, 12, 11), (-11, 6, 8), (11, 7, 9), (-5, 18, 8), (6, 19, 8) })
                    Disc(c, tx + dx, g + h + dy, r, Grey(Hash(tx + dx, dy) % 2 == 0 ? 0.8f : 0.86f));
            }
            // A split-rail fence, a haystack and a few leaves on the ground.
            for (int x = 190; x < 240; x++)
            {
                int g = ground[x];
                if ((x - 190) % 12 == 0) Box(c, x, g, x + 1, g + 12, Grey(0.66f));
                Plot(c, x, g + 5, Grey(0.66f));
                Plot(c, x, g + 10, Grey(0.66f));
            }
            int hs = 330, hg = ground[hs];
            for (int i = 0; i < 14; i++) Box(c, hs - 14 + i, hg + i, hs + 14 - i, hg + i, Grey(0.9f));
            for (int x = 0; x < W; x += 5)
                if (Hash(x, 55) % 3 == 0) Plot(c, x, ground[x] + 1, Grey(0.92f));
            return c;
        }

        public static PixelCanvas Leaf(int kind) => kind == 0
            ? Sprite(3, 2, "##.", ".##")
            : Sprite(3, 3, ".#.", "###", ".#.");

        public static PixelCanvas Geese() => Sprite(36, 13,
            "##..................................",
            ".##.................................",
            "..##....##..........................",
            "........###.........................",
            "..........##....##..................",
            "................###.................",
            "..................##..##............",
            "......................####..........",
            "..................##..##............",
            "................###.................",
            "..........##....##..................",
            "........###.........................",
            "..##....##..........................");

        // ================= Halloween Night =================

        public static PixelCanvas HalloweenFar()
        {
            var c = new PixelCanvas(W, FarH);
            Disc(c, 120, 168, 30, Grey(1f));                 // the full moon
            Disc(c, 110, 176, 5, Grey(0.94f));               // and its craters
            Disc(c, 132, 158, 4, Grey(0.94f));
            Disc(c, 124, 182, 3, Grey(0.95f));
            int[] hill = Ridge(54, 14, 2, 6, 5, 1.7f);
            FillRidge(c, hill, 0.62f, 0.3f);
            // The haunted manor: crooked towers with pointed roofs and lit windows.
            int m = 330, g = hill[m];
            Box(c, m - 30, g, m + 30, g + 34, Grey(0.62f));
            for (int i = 0; i < 16; i++) Box(c, m - 32 + i * 2, g + 35 + i, m + 32 - i * 2, g + 35 + i, Grey(0.62f));
            foreach (var (tx, th, lean) in new[] { (m - 38, 62, -1), (m + 30, 76, 1), (m - 4, 58, 0) })
            {
                Box(c, tx, g, tx + 10, g + th, Grey(0.62f));
                for (int i = 0; i < 20; i++)
                {
                    int half = Mathf.Max(0, 7 - i / 3), cx = tx + 5 + lean * i / 6;
                    Box(c, cx - half, g + th + 1 + i, cx + half, g + th + 1 + i, Grey(0.62f));
                }
                Box(c, tx + 3, g + th - 14, tx + 7, g + th - 8, Grey(0.98f));
            }
            foreach (var (wx, wy) in new[] { (m - 24, 10), (m - 12, 22), (m + 8, 10), (m + 18, 22) })
                Box(c, wx, g + wy, wx + 4, g + wy + 6, Grey(Hash(wx, wy) % 3 == 0 ? 0.68f : 0.98f));
            // Dead trees and a crooked fence on the hill.
            foreach (int tx in new[] { 40, 230, 450 })
            {
                int tg = hill[tx];
                Line(c, tx, tg, tx + 2, tg + 36, Grey(0.62f));
                Line(c, tx + 1, tg + 20, tx + 12, tg + 32, Grey(0.62f));
                Line(c, tx + 1, tg + 26, tx - 9, tg + 38, Grey(0.62f));
                Line(c, tx + 2, tg + 36, tx + 6, tg + 44, Grey(0.62f));
            }
            for (int x = 380; x < 430; x += 4)
                Line(c, x, hill[x], x + (Hash(x, 61) % 3) - 1, hill[x] + 9, Grey(0.62f));
            return c;
        }

        public static PixelCanvas HalloweenNear()
        {
            var c = new PixelCanvas(W, NearH);
            int[] ground = Ridge(14, 4, 3, 2, 7, 0.6f);
            FillRidge(c, ground, 0.6f, 0.35f);
            // A pumpkin patch: ribbed pumpkins on curling vines, some carved and glowing.
            for (int px = 20; px < W; px += 34 + Hash(px, 71) % 20)
            {
                int g = ground[px], r = 4 + Hash(px, 72) % 3;
                Disc(c, px, g + r, r + 1, Grey(0.78f));
                Plot(c, px, g + r, Grey(0.7f));
                Box(c, px, g + 2 * r + 1, px + 1, g + 2 * r + 3, Grey(0.6f));   // the stalk
                if (Hash(px, 73) % 2 == 0)
                {
                    Plot(c, px - 2, g + r + 1, Grey(1f));
                    Plot(c, px + 2, g + r + 1, Grey(1f));
                    Box(c, px - 2, g + r - 2, px + 2, g + r - 2, Grey(1f));
                }
                Line(c, px + r, g + 1, px + r + 8, g + 3, Grey(0.6f));
            }
            // A scarecrow.
            int s = 210, sg = ground[s];
            Box(c, s, sg, s + 1, sg + 34, Grey(0.66f));
            Box(c, s - 12, sg + 24, s + 13, sg + 25, Grey(0.66f));
            Box(c, s - 5, sg + 12, s + 6, sg + 26, Grey(0.72f));
            Disc(c, s + 0.5f, sg + 30, 4, Grey(0.82f));
            Box(c, s - 6, sg + 33, s + 7, sg + 34, Grey(0.6f));
            Box(c, s - 3, sg + 35, s + 4, sg + 39, Grey(0.6f));
            // Crooked gravestones.
            foreach (int gx in new[] { 120, 300, 360, 470 })
            {
                int g = ground[gx], lean = Hash(gx, 75) % 3 - 1;
                for (int y = 0; y < 12; y++) Box(c, gx + (y > 6 ? lean : 0), g + y, gx + 6 + (y > 6 ? lean : 0), g + y, Grey(0.74f));
                Box(c, gx + 1 + lean, g + 12, gx + 5 + lean, g + 12, Grey(0.74f));
            }
            return c;
        }

        public static PixelCanvas Bat() => Sprite(9, 4,
            "#.......#",
            "##.#.#.##",
            ".#######.",
            "...#.#...");

        public static PixelCanvas Witch() => Sprite(40, 18,
            "..........#.............................",
            ".........###............................",
            "........#####...........................",
            ".......#######..........................",
            "......#########.........................",
            "....#############.......................",
            ".......#####............................",
            "......#######...........................",
            ".....#########..........................",
            "....###########.........................",
            "...#############........................",
            "..###############.......................",
            "########################################",
            ".####################################...",
            "....#####...............................",
            "...######...............................",
            "..####..................................",
            "........................................");
    }
}
