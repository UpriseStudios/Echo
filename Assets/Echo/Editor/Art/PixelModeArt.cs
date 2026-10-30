using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The Community Graveyard's own look (a user revision: "a touch more eerie"): a far layer of dead trees and a ruined
    /// chapel in place of the mountains, a crowded near graveyard with crooked crosses, an iron fence and a mausoleum,
    /// and low fog with will-o'-wisps drifting through. Same sizes as the layers they replace (SeasonArtTests), greyscale
    /// so the views tint them, in the game's own pixel style.
    /// </summary>
    public static class PixelModeArt
    {
        // ---- the far layer: 512 x 224 like the mountains ----

        public static PixelCanvas GraveyardFar()
        {
            const int w = 512, h = 224;
            var c = new PixelCanvas(w, h);
            int[] ridge = new int[w];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                ridge[x] = Mathf.RoundToInt(58 + 10 * Mathf.Sin(t * 2 + 0.8f) + 4 * Mathf.Sin(t * 9 + 2.1f));
                for (int y = 0; y <= ridge[x]; y++)
                    c[x, y] = Grey(Bayer(x, y) < 0.35f * (1f - y / (float)ridge[x]) ? 0.82f : 0.86f);
            }
            foreach (var (x, height, lean) in new[] { (36, 74, 1), (118, 58, -1), (196, 86, 1), (384, 66, -1), (452, 80, 1) })
                DeadTree(c, x, ridge[x], height, lean, Grey(0.9f));
            Chapel(c, 280, ridge[280]);
            return c;
        }

        // A bare tree: a trunk that bends as it rises and forks into thin crooked branches.
        static void DeadTree(PixelCanvas c, int x, int ground, int height, int lean, Color32 col)
        {
            int topX = x + lean * height / 8;
            c.Line(x - 1, ground, topX - 1, ground + height, col, 2);
            c.Line(x - 3, ground, x - 1, ground + 4, col, 1);   // roots
            c.Line(x + 2, ground, x, ground + 4, col, 1);
            for (int b = 0; b < 4; b++)
            {
                int y = ground + height * (45 + b * 14) / 100;
                int bx = x + (topX - x) * (y - ground) / height;
                int dir = b % 2 == 0 ? 1 : -1;
                int len = height / 3 - b * 3;
                int ex = bx + dir * len, ey = y + len * 2 / 3;
                c.Line(bx, y, ex, ey, col, 1);
                c.Line(ex, ey, ex + dir * 3, ey + 5, col, 1);     // a twig crooked upward
                c.Line(ex - dir * len / 2, y + len / 3, ex - dir * len / 2 - dir * 2, y + len / 3 + 6, col, 1);
            }
            c.Line(topX, ground + height, topX + lean * 4, ground + height + 7, col, 1);
        }

        // A ruined chapel: walls with pointed windows, a broken gable and a crooked spire.
        static void Chapel(PixelCanvas c, int x, int ground)
        {
            Color32 wall = Grey(0.92f), dark = Grey(0.84f);
            c.Rect(x - 24, ground, x + 20, ground + 38, wall);
            for (int i = 0; i < 22; i++) c.Rect(x - 24 + i, ground + 39 + i, x + 20 - i, ground + 39 + i, wall);   // the gable
            c.Rect(x + 6, ground + 46, x + 20, ground + 62, Clear);                                                // its broken corner
            for (int wx = x - 18; wx <= x + 10; wx += 14)
            {
                c.Rect(wx, ground + 12, wx + 5, ground + 26, dark);
                c[wx + 2, ground + 27] = dark;
                c[wx + 3, ground + 27] = dark;
                c[wx + 2, ground + 28] = dark;
            }
            c.Rect(x - 4, ground, x + 2, ground + 10, dark);   // the door
            // The tower and its spire, leaning a little.
            c.Rect(x - 36, ground, x - 26, ground + 70, wall);
            c.Rect(x - 33, ground + 52, x - 30, ground + 60, dark);
            for (int i = 0; i < 34; i++)
            {
                int half = Mathf.Max(0, 6 - i / 6);
                int cx = x - 31 + i / 12;
                c.Rect(cx - half, ground + 71 + i, cx + half, ground + 71 + i, wall);
            }
            int tip = x - 31 + 3;
            c.Rect(tip, ground + 105, tip, ground + 113, wall);   // the cross on top
            c.Rect(tip - 2, ground + 110, tip + 2, ground + 110, wall);
        }

        // ---- the near layer: 512 x 112 like the graveyard hills ----

        public static PixelCanvas GraveyardNear()
        {
            PixelCanvas c = PixelWorld.GraveyardHills();
            int[] top = new int[c.Width];
            for (int x = 0; x < c.Width; x++)
            {
                float t = x / (float)c.Width * Mathf.PI * 2f;
                top[x] = Mathf.RoundToInt(30 + 10 * Mathf.Sin(t * 2 + 0.3f) + 5 * Mathf.Sin(t * 7 + 1.1f));
            }
            Color32 stone = Grey(0.97f);
            // Crooked crosses crowded between the stones that are already there.
            foreach (var (x, lean) in new[] { (33, 1), (88, -1), (122, 1), (190, -1), (208, 1), (246, -1), (372, 1), (404, -1), (440, 1), (485, -1) })
            {
                int b = top[x];
                c.Line(x, b, x + lean * 2, b + 11, stone, 2);
                int armY = b + 7;
                int armX = x + lean;
                c.Line(armX - 3, armY + lean, armX + 4, armY - lean, stone, 1);
            }
            // A mausoleum: a stone house with columns, a pediment and a dark doorway.
            int m = 150, g = top[m];
            c.Rect(m - 2, g, m + 26, g + 20, stone);
            for (int i = 0; i < 9; i++) c.Rect(m - 3 + i * 2, g + 21 + i, m + 27 - i * 2, g + 21 + i, stone);
            c.Rect(m + 8, g, m + 16, g + 13, Grey(0.86f));
            c.Rect(m + 1, g + 2, m + 2, g + 18, Grey(0.9f));
            c.Rect(m + 22, g + 2, m + 23, g + 18, Grey(0.9f));
            // An iron fence with spear tips, leaning in places.
            for (int x = 336; x < 368; x += 3)
            {
                int b = top[x];
                int tilt = x % 9 == 0 ? 1 : 0;
                c.Line(x, b, x + tilt, b + 15, Grey(0.94f), 1);
                c[x + tilt, b + 16] = Grey(0.94f);
            }
            for (int x = 336; x < 366; x++)
            {
                c[x, top[x] + 5] = Grey(0.94f);
                c[x, top[x] + 12] = Grey(0.94f);
            }
            // A dead tree over the graves.
            DeadTreeNear(c, 260, top[260]);
            return c;
        }

        static void DeadTreeNear(PixelCanvas c, int x, int ground)
        {
            Color32 col = Grey(0.93f);
            c.Line(x, ground, x + 2, ground + 40, col, 3);
            c.Line(x + 2, ground + 24, x + 16, ground + 38, col, 1);
            c.Line(x + 16, ground + 38, x + 20, ground + 37, col, 1);
            c.Line(x + 1, ground + 30, x - 12, ground + 44, col, 1);
            c.Line(x - 12, ground + 44, x - 15, ground + 50, col, 1);
            c.Line(x + 3, ground + 40, x + 7, ground + 52, col, 1);
            c.Line(x + 3, ground + 40, x - 2, ground + 50, col, 1);
            c.Line(x + 9, ground + 31, x + 11, ground + 44, col, 1);
            // A crow perched on a branch.
            c.Rect(x + 13, ground + 39, x + 16, ground + 41, Grey(0.88f));
            c[x + 17, ground + 41] = Grey(0.88f);
            c[x + 12, ground + 40] = Grey(0.88f);
        }

        // ---- the effects: fog and will-o'-wisps, drifting ----

        /// <summary>A wisp of fog, white for the theme's colour: a long soft band, densest in the middle, dithered at the edges.</summary>
        public static PixelCanvas Fog(int kind)
        {
            int w = kind == 0 ? 56 : 36, h = kind == 0 ? 10 : 7;
            var c = new PixelCanvas(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float dx = (x - (w - 1) / 2f) / (w / 2f), dy = (y - (h - 1) / 2f) / (h / 2f);
                    float d = dx * dx + dy * dy * (1f + 0.4f * Mathf.Sin(x * 0.45f + kind));
                    if (d >= 1f) continue;
                    float density = 1f - d;
                    if (Bayer(x, y) > density * 1.3f) continue;
                    c[x, y] = Grey(1f, (byte)(60 + 80 * density));
                }
            return c;
        }

        /// <summary>A will-o'-wisp: a bright point with a soft glow, white for the theme's colour.</summary>
        public static PixelCanvas Wisp()
        {
            var c = new PixelCanvas(5, 5);
            c[2, 2] = Grey(1f);
            foreach (var (x, y) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) }) c[x, y] = Grey(1f, 170);
            foreach (var (x, y) in new[] { (1, 1), (3, 1), (1, 3), (3, 3), (0, 2), (4, 2), (2, 0), (2, 4) }) c[x, y] = Grey(1f, 60);
            return c;
        }
    }
}
