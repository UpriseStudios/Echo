using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc, phase 3: Dino Land in 8-bit, like an early handheld. Five colours and a flat orange sky, chunky
    /// pixels (drawn at half resolution and doubled, so each art pixel is 2x2 on the world's grid) and no gradients, only
    /// the checkerboard dither 8-bit art used. Smoking volcanoes, a low sun, a T-rex on the ridge, giant ferns and a
    /// pterodactyl. Painted in its own colours (white tints); the layers keep the sizes they replace (far 512 x 224,
    /// near 512 x 112).
    /// </summary>
    public static class PixelDino
    {
        // The palette: five colours, the sky (F29A4A) the sixth.
        public static readonly Color32 Night = Hex(0x2B1B2E);
        public static readonly Color32 Rock = Hex(0x7C2D25);
        public static readonly Color32 Lava = Hex(0xE2622C);
        public static readonly Color32 Sun = Hex(0xF6C35A);
        public static readonly Color32 Fern = Hex(0x3F6B36);
        public static readonly Color32[] Palette = { Night, Rock, Lava, Sun, Fern };
        public const uint Sky = 0xF29A4A;

        const int W = 256, FarH = 112, NearH = 56;

        static void Plot(PixelCanvas c, int x, int y, Color32 col)
        {
            if (y < 0 || y >= c.Height) return;
            c[((x % c.Width) + c.Width) % c.Width, y] = col;
        }

        static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 col)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Plot(c, x, y, col);
        }

        static void Disc(PixelCanvas c, float cx, float cy, float r, Color32 col)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Plot(c, x, y, col);
        }

        static bool Checker(int x, int y) => ((x + y) & 1) == 0;

        public static PixelCanvas Far() => FarHalf().Scaled(2);

        public static PixelCanvas Near() => NearHalf().Scaled(2);

        static PixelCanvas FarHalf()
        {
            var c = new PixelCanvas(W, FarH);
            // A big low sun, with dithered bands of heat across it.
            Disc(c, 70, 62, 22, Sun);
            for (int y = 50; y < 62; y += 4)
                for (int x = 48; x < 93; x++)
                    if (Checker(x, y)) Plot(c, x, y, Lava);

            Volcano(c, 150, 74, 66);
            Volcano(c, 30, 46, 40);
            Volcano(c, 226, 54, 46);

            // The ridge in front, with a T-rex on it.
            for (int x = 0; x < W; x++)
            {
                int top = 10 + (int)(3f * Mathf.Sin(x * 0.07f) + 2f * Mathf.Sin(x * 0.19f + 1f));
                for (int y = 0; y <= top; y++) Plot(c, x, y, Night);
            }
            TRex(c, 104, 12);
            return c;
        }

        static void Volcano(PixelCanvas c, int x, int height, int halfBase)
        {
            for (int y = 0; y < height; y++)
            {
                int half = halfBase * (height - y) / height + 3;
                Box(c, x - half, y, x + half, y, Rock);
                // The shadowed side, dithered at its edge: still only two colours.
                Box(c, x + 1, y, x + half, y, Night);
                for (int k = -2; k <= 0; k++)
                    if (Checker(x + k, y)) Plot(c, x + k, y, Night);
            }
            // A glowing crater and lava running down the lit side.
            Box(c, x - 3, height - 1, x + 3, height, Lava);
            for (int y = height - 2, dx = -1; y > height / 3; y -= 2, dx -= (y % 6 == 0 ? 1 : 0))
            {
                Plot(c, x + dx, y, Lava);
                Plot(c, x + dx, y - 1, Lava);
            }
            // Smoke: chunky puffs climbing and drifting, dithered thinner as they rise.
            for (int i = 0; i < 6; i++)
            {
                float px = x + i * 2 - (i % 2), py = height + 4 + i * 7, r = 3 + i;
                for (int yy = (int)(py - r); yy <= (int)(py + r); yy++)
                    for (int xx = (int)(px - r); xx <= (int)(px + r); xx++)
                        if ((xx - px) * (xx - px) + (yy - py) * (yy - py) <= r * r && (i < 3 || Checker(xx, yy)))
                            Plot(c, xx, yy, i < 2 ? Night : Rock);
            }
        }

        // A T-rex silhouette, facing left, roaring at the volcano.
        static void TRex(PixelCanvas c, int x, int g)
        {
            string[] rows =
            {
                "......####......",
                ".....######.....",
                "....###.####....",
                "....########....",
                "....####........",
                "...######.......",
                "..########......",
                ".##########.....",
                "############....",
                "#############...",
                ".###########....",
                "...########.....",
                "....###..##.....",
                "....##....#.....",
                "...###...##.....",
            };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    if (rows[r][i] == '#') Plot(c, x + rows[r].Length - 1 - i, g + rows.Length - 1 - r, Night);
            Plot(c, x + 8, g + 12, Sun);   // its eye
        }

        static PixelCanvas NearHalf()
        {
            var c = new PixelCanvas(W, NearH);
            for (int x = 0; x < W; x++)
            {
                int top = 4 + (int)(1.5f * Mathf.Sin(x * 0.11f));
                for (int y = 0; y <= top; y++) Plot(c, x, y, Night);
            }
            foreach (var (x, h, lean) in new[] { (14, 34, 1), (52, 22, -1), (96, 40, 1), (140, 26, 1), (176, 44, -1), (222, 30, 1) })
                GiantFern(c, x, 5, h, lean);
            foreach (int x in new[] { 38, 120, 200, 246 }) Cycad(c, x, 5);
            foreach (int x in new[] { 74, 160, 236 })
            {
                Box(c, x - 4, 4, x + 4, 7, Rock);
                Box(c, x - 2, 8, x + 2, 9, Rock);
                Plot(c, x - 3, 7, Lava);
            }
            return c;
        }

        // A tree fern: a trunk with fronds arcing out from its crown, lit edges in sun-yellow.
        static void GiantFern(PixelCanvas c, int x, int g, int h, int lean)
        {
            int top = g + h;
            for (int y = g; y < top; y++) Plot(c, x + lean * (y - g) / 12, y, Night);
            int cx = x + lean * h / 12;
            for (int f = 0; f < 6; f++)
            {
                int dir = f % 2 == 0 ? -1 : 1;
                int len = 10 + (f / 2) * 3;
                for (int i = 0; i < len; i++)
                {
                    int fx = cx + dir * i, fy = top - (i * i) / (len + 2) + f / 2;
                    Plot(c, fx, fy, Fern);
                    if (i % 2 == 0) Plot(c, fx, fy - 1, Fern);
                    if (i % 3 == 1) Plot(c, fx, fy + 1, Sun);
                }
            }
            Box(c, cx - 1, top - 1, cx + 1, top + 1, Fern);
        }

        static void Cycad(PixelCanvas c, int x, int g)
        {
            Box(c, x - 1, g, x + 1, g + 6, Night);
            for (int i = 0; i < 6; i++)
            {
                Plot(c, x - 2 - i, g + 7 + i / 2, Fern);
                Plot(c, x + 2 + i, g + 7 + i / 2, Fern);
                Plot(c, x, g + 7 + i, Fern);
            }
        }

        /// <summary>A pterodactyl, wings up and down (the flyer uses the first), chunky like the scenery.</summary>
        public static PixelCanvas Pterodactyl()
        {
            string[] rows =
            {
                "#..................#",
                ".##..............##.",
                "..###..........###..",
                "...####..##..####...",
                "....#####...#####...",
                "......########......",
                ".......######.##....",
                "........###...###...",
                "..............#.....",
            };
            var c = new PixelCanvas(rows[0].Length, rows.Length);
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    if (rows[r][i] == '#') c[i, rows.Length - 1 - r] = Night;
            return c.Scaled(2);
        }

        /// <summary>Ash and embers: one art pixel (2x2), white, tinted from the palette by the effect's colours.</summary>
        public static PixelCanvas Ember()
        {
            var c = new PixelCanvas(1, 1);
            c[0, 0] = Hex(0xFFFFFF);
            return c.Scaled(2);
        }
    }
}
