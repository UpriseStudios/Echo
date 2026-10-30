using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Christmas's far layer and sky (a user revision: "north pole, a Christmassy town, and the odd silhouette of Santa's
    /// sleigh"): a snowy town of gabled houses with lit windows and smoking chimneys, a church, a great decorated tree and
    /// the North Pole workshop with its striped pole, in front of the snow-capped mountains; and the sleigh with its
    /// reindeer as one silhouette for the sky. Greyscale like every layer, so the views tint it: the town is darker than
    /// the mountains behind it, and windows, snow and baubles are the lightest greys, so they glow.
    /// </summary>
    public static class PixelChristmasTown
    {
        const int W = 512, H = 224;

        static readonly Color32 Wall = Grey(0.68f), WallShade = Grey(0.62f), Roof = Grey(0.58f), Snow = Grey(1f), Lit = Grey(0.98f),
            Tree = Grey(0.5f), Smoke = Grey(0.9f, 150);

        public static PixelCanvas Far()
        {
            PixelCanvas c = PixelSeasons.Capped(PixelWorld.Mountains(), 7, 0.8f);
            // A snowy ground the town stands on.
            for (int x = 0; x < W; x++)
                for (int y = 0; y < 14; y++)
                    c[x, y] = y >= 12 ? Snow : Grey(Bayer(x, y) < 0.3f ? 0.9f : 0.94f);
            const int ground = 13;
            // Houses: (left, width, wall height, chimney side).
            foreach (var (x, w, h, chimney) in new[]
            {
                (6, 24, 18, 1), (34, 18, 14, -1), (56, 28, 22, 1), (128, 22, 16, -1), (154, 26, 20, 1),
                (300, 20, 15, 1), (324, 26, 21, -1), (392, 24, 17, 1), (420, 18, 13, -1), (470, 30, 20, 1),
            })
                House(c, x, ground, w, h, chimney);
            Church(c, 96, ground);
            BigTree(c, 216, ground);
            Workshop(c, 236, ground);
            NorthPole(c, 284, ground);
            return c;
        }

        // A gabled house: snow on the roof ridge and eaves, two lit windows, a door, and a chimney with smoke.
        static void House(PixelCanvas c, int x, int g, int w, int h, int chimney)
        {
            c.Rect(x, g, x + w - 1, g + h, Wall);
            c.Rect(x + w - 3, g, x + w - 1, g + h, WallShade);
            int peak = w / 2 + 2;
            for (int i = 0; i <= peak; i++)
            {
                int y = g + h + 1 + i;
                c.Rect(x - 2 + i, y, x + w + 1 - i, y, i >= peak - 1 ? Snow : Roof);
                c[x - 2 + i, y] = Snow;   // snow along both slopes
                c[x + w + 1 - i, y] = Snow;
            }
            c.Rect(x - 2, g + h + 1, x + w + 1, g + h + 1, Snow);   // the eaves
            int cx = chimney > 0 ? x + w - 7 : x + 4;
            int roofAt = g + h + 1 + Mathf.Min(peak, chimney > 0 ? w + 1 - (cx - x) : cx - x + 2);
            c.Rect(cx, roofAt - 2, cx + 3, roofAt + 5, Roof);
            c.Rect(cx, roofAt + 5, cx + 3, roofAt + 5, Snow);
            foreach (var (dx, dy) in new[] { (1, 7), (0, 10), (2, 13), (1, 16), (3, 19) })   // a curl of smoke
                c.Rect(cx + dx, roofAt + dy, cx + dx + 1, roofAt + dy + 1, Smoke);
            int win = h >= 16 ? 4 : 3;
            c.Rect(x + 3, g + h - 4 - win, x + 3 + win, g + h - 4, Lit);
            if (w >= 20) c.Rect(x + w - 6 - win, g + h - 4 - win, x + w - 6, g + h - 4, Lit);
            c.Rect(x + w / 2 - 2, g, x + w / 2 + 1, g + 7, WallShade);
            c[x + w / 2 + 1, g + 4] = Lit;   // the door knob catching the light
        }

        // A church: a nave with tall lit windows and a steeple with a snowy spire.
        static void Church(PixelCanvas c, int x, int g)
        {
            c.Rect(x, g, x + 24, g + 24, Wall);
            for (int i = 0; i <= 14; i++) c.Rect(x - 1 + i, g + 25 + i, x + 25 - i, g + 25 + i, i > 12 ? Snow : Roof);
            c.Rect(x - 1, g + 25, x + 25, g + 25, Snow);
            for (int wx = x + 4; wx <= x + 18; wx += 7)
            {
                c.Rect(wx, g + 8, wx + 2, g + 17, Lit);
                c[wx + 1, g + 18] = Lit;
            }
            c.Rect(x + 26, g, x + 34, g + 46, Wall);
            c.Rect(x + 28, g + 36, x + 32, g + 41, Lit);
            for (int i = 0; i < 18; i++) c.Rect(x + 26 + i / 3, g + 47 + i, x + 34 - i / 3, g + 47 + i, i < 3 ? Snow : Roof);
            c.Rect(x + 30, g + 65, x + 30, g + 71, Snow);
            c.Rect(x + 28, g + 69, x + 32, g + 69, Snow);
        }

        // The town's great tree: tiers of dark branches dusted with snow, light baubles, and a star on top.
        static void BigTree(PixelCanvas c, int x, int g)
        {
            c.Rect(x - 1, g, x + 1, g + 4, WallShade);
            for (int tier = 0; tier < 4; tier++)
            {
                int bottom = g + 4 + tier * 12, half = 14 - tier * 3;
                for (int i = 0; i < 16; i++)
                {
                    int span = half - i * half / 16;
                    c.Rect(x - span, bottom + i, x + span, bottom + i, Tree);
                    c[x - span, bottom + i] = Snow;
                    c[x + span, bottom + i] = Snow;
                }
            }
            foreach (var (dx, dy) in new[] { (-8, 8), (5, 11), (-3, 16), (7, 20), (-6, 24), (2, 28), (-2, 34), (4, 38), (-4, 44), (1, 49) })
                c[x + dx, g + dy] = Lit;
            int top = g + 4 + 3 * 12 + 16;
            c.Rect(x, top, x, top + 4, Lit);   // the star
            c.Rect(x - 2, top + 2, x + 2, top + 2, Lit);
            c[x - 1, top + 3] = Lit;
            c[x + 1, top + 3] = Lit;
            c[x - 1, top + 1] = Lit;
            c[x + 1, top + 1] = Lit;
        }

        // The toy workshop: long and low, a row of lit windows, a big door and a sign over it.
        static void Workshop(PixelCanvas c, int x, int g)
        {
            c.Rect(x, g, x + 42, g + 20, Wall);
            c.Rect(x + 40, g, x + 42, g + 20, WallShade);
            for (int i = 0; i <= 10; i++) c.Rect(x - 3 + i * 2, g + 21 + i, x + 45 - i * 2, g + 21 + i, i >= 9 ? Snow : Roof);
            c.Rect(x - 3, g + 21, x + 45, g + 21, Snow);
            for (int wx = x + 3; wx < x + 40; wx += 8)
                if (wx < x + 16 || wx > x + 26) c.Rect(wx, g + 10, wx + 4, g + 15, Lit);
            c.Rect(x + 17, g, x + 25, g + 13, WallShade);   // the big door
            c.Rect(x + 21, g, x + 21, g + 13, Roof);
            c.Rect(x + 15, g + 15, x + 27, g + 18, Lit);    // the sign
            for (int sx = x + 16; sx <= x + 26; sx += 2) c[sx, g + 16] = Roof;
        }

        // The North Pole itself: a striped pole with a snowball on top and a little drift at its foot.
        static void NorthPole(PixelCanvas c, int x, int g)
        {
            for (int y = g; y < g + 40; y++)
                c.Rect(x, y, x + 2, y, (y - g + x) / 3 % 2 == 0 ? Snow : Roof);
            c.Disc(x + 1, g + 42, 2.6f, Snow);
            for (int i = 0; i < 4; i++) c.Rect(x - 4 + i, g + i, x + 6 - i, g + i, Snow);
        }

        /// <summary>
        /// Santa's sleigh for the sky, one silhouette (white, for the theme's colour), facing right: two reindeer at a
        /// gallop on the reins, then the sleigh with Santa and his sack.
        /// </summary>
        public static PixelCanvas Sleigh()
        {
            var c = new PixelCanvas(60, 16);
            c.Blit(new[]
            {
                ".....................................a.a..............a.a.",
                "......................................a................a..",
                ".....SS.............................aaa..............aaa..",
                "....SSSS.......................... aaaaa............aaaaa.",
                "...SSSkS..........................aaaa.............aaaa...",
                "..SSSSSSS.....................rrrrrr.........rrrrrrr......",
                ".kSSSSSSSk..............rrrrrr..aaaaaaaaa.......aaaaaaaa...",
                "kkkkkkkkkkk.......rrrrrr.......aaaaaaaaaa......aaaaaaaaa..",
                "kkkkkkkkkkkk..rrrr............aaaaaaaaa......aaaaaaaaa....",
                ".kkkkkkkkkkkrr................a..a...a.a.....a..a...a.a...",
                "..kkkkkkkkkk..................a...a..a..a...a....a..a..a..",
                "...........................................................",
                ".k........k...............................................",
                "..kkkkkkkkkk..............................................",
            }, new System.Collections.Generic.Dictionary<char, Color32> { ['a'] = Grey(1f), ['S'] = Grey(1f), ['k'] = Grey(1f), ['r'] = Grey(1f) }, 0, 1);
            return c;
        }
    }
}
