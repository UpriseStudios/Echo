using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc (art pass phase 2): Dino Land in 8-bit, like an early handheld. Eight colours with the sky, no
    /// dithering and no gradients: flat volcanoes, a solid sun, solid plumes of smoke, a T-rex on the ridge, giant ferns
    /// and a pterodactyl. The backdrop, floor, floor edge and lava pit are drawn at half resolution and doubled, and the
    /// game renders them at half its internal resolution (LowResView), so every pixel is twice as big as the gameplay's.
    /// The rock overhang (the hanging ceiling) is gameplay, so it stays at normal resolution. Painted in its own colours
    /// (white tints); layers keep the sizes they replace (far 512 x 224, near 512 x 112).
    /// </summary>
    public static class PixelDino
    {
        // The palette: seven colours and the sky.
        public const uint Sky = 0xF29A4A;
        public static readonly Color32 Night = Hex(0x2B1B2E);
        public static readonly Color32 Rock = Hex(0x7C2D25);
        public static readonly Color32 Lava = Hex(0xE2522C);
        public static readonly Color32 Sun = Hex(0xF8D25A);
        public static readonly Color32 Fern = Hex(0x3F6B36);
        public static readonly Color32 Leaf = Hex(0x7AA83A);
        public static readonly Color32 Bone = Hex(0xEADCC0);
        public static readonly Color32[] Palette = { Hex(Sky), Night, Rock, Lava, Sun, Fern, Leaf, Bone };

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

        static PixelCanvas Rows(string[] rows, params (char ch, Color32 c)[] palette)
        {
            var c = new PixelCanvas(rows[0].Length, rows.Length);
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    foreach (var (ch, col) in palette)
                        if (rows[r][i] == ch) c[i, rows.Length - 1 - r] = col;
            return c;
        }

        public static PixelCanvas Far() => FarHalf().Scaled(2);
        public static PixelCanvas Near() => NearHalf().Scaled(2);

        static PixelCanvas FarHalf()
        {
            var c = new PixelCanvas(W, FarH);
            // A big low sun, one solid colour.
            Disc(c, 70, 64, 22, Sun);

            Volcano(c, 150, 74, 66);
            Volcano(c, 30, 46, 40);
            Volcano(c, 226, 54, 46);

            // The ridge in front, with a T-rex on it.
            for (int x = 0; x < W; x++)
            {
                int top = 10 + (int)(3f * Mathf.Sin(x * 0.07f) + 2f * Mathf.Sin(x * 0.19f + 1f));
                Box(c, x, 0, x, top, Night);
            }
            TRex(c, 104, 12);
            return c;
        }

        static void Volcano(PixelCanvas c, int x, int height, int halfBase)
        {
            for (int y = 0; y < height; y++)
            {
                int half = halfBase * (height - y) / height + 3;
                Box(c, x - half, y, x, y, Rock);
                Box(c, x + 1, y, x + half, y, Night);   // the shadowed side, one flat colour
            }
            // A glowing crater and a river of lava down the lit side, in steps.
            Box(c, x - 3, height - 1, x + 3, height, Lava);
            for (int y = height - 2, dx = -1; y > height / 3; y -= 2)
            {
                Box(c, x + dx - 1, y - 1, x + dx, y, Lava);
                if (y % 6 == 0) dx--;
            }
            // A solid plume of smoke: stacked flat puffs drifting right.
            for (int i = 0; i < 6; i++)
                Disc(c, x + i * 3 - (i % 2), height + 5 + i * 7, 3 + i, Rock);
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
            for (int x = 0; x < W; x++) Box(c, x, 0, x, 4 + (int)(1.5f * Mathf.Sin(x * 0.11f)), Night);
            foreach (var (x, h, lean) in new[] { (14, 34, 1), (52, 22, -1), (96, 40, 1), (140, 26, 1), (176, 44, -1), (222, 30, 1) })
                GiantFern(c, x, 5, h, lean);
            foreach (int x in new[] { 38, 120, 200, 246 }) Cycad(c, x, 5);
            foreach (int x in new[] { 74, 160, 236 })
            {
                Box(c, x - 4, 4, x + 4, 7, Rock);
                Box(c, x - 2, 8, x + 2, 9, Rock);
                Plot(c, x - 3, 7, Bone);
            }
            return c;
        }

        // A tree fern: a trunk with fronds arcing out from its crown, lit edges in leaf green.
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
                    if (i % 3 == 1) Plot(c, fx, fy + 1, Leaf);
                }
            }
            Box(c, cx - 1, top - 1, cx + 1, top + 1, Fern);
        }

        static void Cycad(PixelCanvas c, int x, int g)
        {
            Box(c, x - 1, g, x + 1, g + 6, Night);
            for (int i = 0; i < 6; i++)
            {
                Plot(c, x - 2 - i, g + 7 + i / 2, Leaf);
                Plot(c, x + 2 + i, g + 7 + i / 2, Leaf);
                Plot(c, x, g + 7 + i, Fern);
            }
        }

        /// <summary>
        /// The floor, 64 x 64 (2 x 2 units, so the pattern repeats rarely): packed red earth with dark stones of a few
        /// sizes and one buried bone, 2x2 pixels.
        /// </summary>
        public static PixelCanvas Floor()
        {
            var c = new PixelCanvas(32, 32);
            Box(c, 0, 0, 31, 31, Rock);
            foreach (var (x, y, w, h) in new[] { (3, 29, 1, 1), (12, 30, 2, 1), (27, 28, 1, 1), (20, 25, 2, 2), (2, 22, 2, 2), (9, 19, 1, 1),
                                                 (16, 17, 1, 1), (24, 20, 3, 2), (30, 23, 1, 1), (6, 26, 1, 1), (5, 12, 1, 1), (18, 10, 2, 1),
                                                 (28, 14, 1, 1), (11, 6, 2, 2), (23, 4, 1, 1), (1, 2, 1, 1), (15, 1, 1, 1), (29, 8, 2, 2) })
                Box(c, x, y, x + w - 1, y + h - 1, Night);
            // One buried bone a tile.
            Box(c, 20, 13, 24, 13, Bone);
            Plot(c, 19, 14, Bone);
            Plot(c, 19, 12, Bone);
            Plot(c, 25, 14, Bone);
            Plot(c, 25, 12, Bone);
            return c.Scaled(2);
        }

        /// <summary>The floor's top edge, 32 x 10 like the stone edge: a band of grass over a dark line, 2x2 pixels.</summary>
        public static PixelCanvas FloorTop() => Rows(new[]
        {
            "g.g...g..g...g.g",
            "gggggggggggggggg",
            "GGGGGGGGGGGGGGGG",
            "nnnnnnnnnnnnnnnn",
            "rrrrrrrrrrrrrrrr",
        }, ('g', Leaf), ('G', Fern), ('n', Night), ('r', Rock)).Scaled(2);

        /// <summary>A lava pit, 16 x 192 (6 units deep, so its dark rim shows once): flat lava with bubbles, 2x2 pixels.</summary>
        public static PixelCanvas Gap()
        {
            var c = new PixelCanvas(8, 96);
            Box(c, 0, 0, 7, 95, Lava);
            Box(c, 0, 95, 7, 95, Night);
            foreach (var (x, y) in new[] { (3, 92), (6, 88), (1, 84), (5, 76), (2, 66), (6, 54), (3, 40), (1, 24), (5, 10) }) Plot(c, x, y, Sun);
            return c.Scaled(2);
        }

        /// <summary>
        /// The rock overhang (the hanging ceiling), 32 x 32 at normal resolution since it's gameplay: dark rock with lit
        /// ledges and cracks, its underside rough where the stalactites (the era's spikes, upside down) hang.
        /// </summary>
        public static PixelCanvas Overhang()
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    // Rough bands of strata, lighter on top of each ledge; no gradient, three flat colours.
                    int band = (y + (x * 7 / 32) % 3) / 8;
                    bool ledge = (y + (x * 7 / 32) % 3) % 8 == 7;
                    c[x, y] = ledge ? Rock : band % 2 == 0 ? Night : Hex(0x4A2026);
                }
            foreach (var (x0, y0) in new[] { (5, 20), (18, 9), (25, 25) })
                for (int i = 0; i < 6; i++) c[(x0 + i / 2) % 32, Mathf.Clamp(y0 - i, 0, 31)] = Night;   // cracks
            return c;
        }

        /// <summary>A pterodactyl (the flyer), chunky like the scenery.</summary>
        public static PixelCanvas Pterodactyl() => Rows(new[]
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
        }, ('#', Night)).Scaled(2);

        /// <summary>Ash and embers: one art pixel (2x2), white, tinted from the palette by the effect's colours.</summary>
        public static PixelCanvas Ember()
        {
            var c = new PixelCanvas(1, 1);
            c[0, 0] = Hex(0xFFFFFF);
            return c.Scaled(2);
        }
    }
}
