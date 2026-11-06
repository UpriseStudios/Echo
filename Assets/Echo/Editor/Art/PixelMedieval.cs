using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;
using static Echo.Editor.Art.PixelWorldsArt;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc (art pass phase 3): Medieval in 16-bit, a bright daytime village, all new (nothing from the
    /// Echo castle world). A banded blue sky with clouds; three parallax layers: green rolling hills with a cathedral and
    /// its spire, a windmill hill whose sails turn (four frames, drawn over the layer by BackgroundView), and thatched
    /// cottages with jousting pennants; now and then a dragon flies over, beating its wings. Its own floor (cobbles under a
    /// dirt path) and its own hazards on the same colliders: wooden stakes (a repaint of the spike's own pixels, so the
    /// outline is exact), a timber-framed jettied house overhang for the hanging ceiling, a millstream with a turning
    /// waterwheel for the gaps, and gold groats for coins. A richer palette than Dino Land, shading in two or three tones,
    /// still on the 32 px grid. Painted in its own colours (white tints).
    /// </summary>
    public static class PixelMedieval
    {
        static Color32 C(uint rgb) => Hex(rgb);

        public const uint Sky = 0x8ACCF8;

        // ---- the sky: bands of blue, paler towards the horizon (16-bit banding, no smooth gradient) ----

        /// <summary>64 x 512 (2 x 16 units), fixed in the sky.</summary>
        public static PixelCanvas SkyBands()
        {
            var c = new PixelCanvas(64, 512);
            uint[] bands = { 0xCDEBFB, 0xB4E0FA, 0x9CD4F8, 0x86C6F4, 0x70B6EE, 0x5CA6E6, 0x4C96DC };
            int[] tops = { 80, 150, 210, 270, 340, 420, 512 };
            for (int y = 0; y < 512; y++)
            {
                int b = 0;
                while (y >= tops[b]) b++;
                Color32 col = C(bands[b]);
                for (int x = 0; x < 64; x++) c[x, y] = col;
            }
            return c;
        }

        static void Cloud(PixelCanvas c, int x, int y, int r)
        {
            Disc(c, x, y, r, C(0xFFFFFF));
            Disc(c, x + r, y - 1, r * 0.8f, C(0xFFFFFF));
            Disc(c, x - r, y - 2, r * 0.7f, C(0xFFFFFF));
            Disc(c, x + r / 2, y + r * 0.6f, r * 0.75f, C(0xFFFFFF));
            Box(c, x - r * 2 + 2, y - r + 1, x + r * 2 - 2, y - r + 2, C(0xD6EAF6));   // a soft blue underside
        }

        // ---- the far layer: rolling hills and the cathedral, 512 x 224 ----

        public static PixelCanvas Far()
        {
            var c = new PixelCanvas(W, FarH);
            foreach (var (x, y, r) in new[] { (60, 190, 9), (210, 205, 7), (330, 180, 10), (450, 200, 8) }) Cloud(c, x, y, r);

            // The far ridge, hazy blue-green, then the near ridge in two greens.
            int[] back = Ridge(70, 14, 2, 6, 5, 0.3f);
            int[] front = Ridge(38, 10, 3, 5, 7, 1.7f);
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y <= back[x]; y++) c[x, y] = y > back[x] - 3 ? C(0xB0D8B8) : C(0x9CC8A8);
                for (int y = 0; y <= front[x]; y++) c[x, y] = y > front[x] - 3 ? C(0x8CCC6C) : (x + y) % 9 == 0 ? C(0x5E9E4C) : C(0x6CB25A);
            }
            // Fields in strips on the near ridge.
            for (int x = 0; x < W; x++)
                for (int y = 4; y < front[x] - 5; y++)
                    if ((x / 24 + y / 6) % 3 == 0) c[x, y] = C(0x7EBC5E);

            // The cathedral on the far ridge: a long nave, transepts, a tower and a tall spire.
            int cx = 150, g = back[cx] - 2;
            Box(c, cx - 30, g, cx + 26, g + 22, C(0xD8D0C0));          // the nave
            Box(c, cx + 8, g, cx + 26, g + 22, C(0xB8AE9E));            // its shaded end
            for (int i = 0; i < 9; i++) Box(c, cx - 30 + i, g + 23 + i / 2, cx + 26 - i, g + 23 + i / 2, C(0x6A7488));   // slate roof
            for (int x = cx - 26; x < cx + 22; x += 7) Box(c, x, g + 6, x + 2, g + 14, C(0x3E3A52));                       // tall windows
            Box(c, cx - 8, g, cx + 4, g + 44, C(0xE2DACA));             // the tower
            Box(c, cx - 1, g, cx + 4, g + 44, C(0xC0B6A6));
            Disc(c, cx - 2, g + 32, 3, C(0x3E3A52));                    // the rose window
            for (int y = 0; y < 52; y++)                                 // the spire
            {
                int half = (52 - y) * 7 / 52;
                Box(c, cx - 2 - half, g + 45 + y, cx - 2 + half, g + 45 + y, y % 2 == 0 ? C(0x5A6478) : C(0x6A7488));
            }
            Box(c, cx - 2, g + 97, cx - 2, g + 101, C(0xF0C83A));       // a gilded cross
            Box(c, cx - 4, g + 99, cx, g + 99, C(0xF0C83A));
            // A little town at its feet.
            foreach (int x in new[] { 106, 116, 124, 182, 192, 204 })
            {
                int gy = back[x] - 3;
                Box(c, x, gy, x + 7, gy + 6, C(0xE8DCC4));
                for (int i = 0; i < 4; i++) Box(c, x - 1 + i, gy + 7 + i, x + 8 - i, gy + 7 + i, C(0xB05A3A));
            }
            return c;
        }

        // ---- the mid layer: the windmill hill, 512 x 160 (its sails are their own frames) ----

        /// <summary>Where the windmill's sails turn, in the mid layer's pixels from its bottom-left.</summary>
        public static readonly Vector2Int SailsAt = new Vector2Int(330, 118);

        public static PixelCanvas Mid()
        {
            var c = new PixelCanvas(W, 160);
            int[] hill = Ridge(26, 34, 1, 8, 4, 0.9f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y <= hill[x]; y++)
                    c[x, y] = y > hill[x] - 3 ? C(0xA0DC72) : (x * 3 + y * 5) % 23 == 0 ? C(0x5AA040) : C(0x7CC254);
            // Hedgerows following the slope.
            for (int x = 0; x < W; x++) { int y = hill[x] / 2 + (x % 40 < 3 ? 1 : 0); c[x, y] = C(0x3E7A34); c[x, y + 1] = C(0x4E8E3E); }

            // The windmill: a tapering tower with a cap and a door, the sails' hub at SailsAt.
            int mx = SailsAt.x, mg = hill[mx] - 4;
            for (int y = mg; y < SailsAt.y - 2; y++)
            {
                int half = 10 - (y - mg) * 4 / (SailsAt.y - mg);
                Box(c, mx - half, y, mx + half, y, C(0xD2BC90));
                Box(c, mx + 2, y, mx + half, y, C(0xAE9468));
            }
            for (int i = 0; i < 8; i++) Box(c, mx - 9 + i, SailsAt.y - 2 + i, mx + 9 - i, SailsAt.y - 2 + i, C(0x7A4A2A));   // the cap
            Box(c, mx - 2, mg, mx + 1, mg + 7, C(0x5A3A22));            // the door
            Box(c, mx - 1, mg + 18, mx, mg + 20, C(0x3E3A52));          // a window

            // Round trees and a few sheep.
            foreach (int x in new[] { 60, 92, 200, 230, 420, 470 })
            {
                int ty = hill[x];
                Box(c, x, ty, x + 1, ty + 6, C(0x6A4426));
                Disc(c, x, ty + 11, 7, C(0x3E7A34));
                Disc(c, x - 2, ty + 13, 4, C(0x5A9A44));
            }
            foreach (int x in new[] { 130, 140, 156, 270, 380, 392 })
            {
                int sy = hill[x] - 2;
                Box(c, x, sy, x + 4, sy + 2, C(0xF4F4EC));
                c[x + 5, sy + 2] = C(0x2A2A2A);
            }
            return c;
        }

        /// <summary>The windmill's four sails, frame <paramref name="f"/> of 4 (a quarter turn, as the sails repeat every 90°).</summary>
        public static PixelCanvas Sails(int f)
        {
            var c = new PixelCanvas(64, 64);
            float a0 = f * 22.5f * Mathf.Deg2Rad;
            for (int k = 0; k < 4; k++)
            {
                float a = a0 + k * Mathf.PI / 2f;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), n = new Vector2(-d.y, d.x);
                // The cloth on a lattice to one side of the spar.
                for (float r = 7f; r <= 29f; r += 0.5f)
                    for (float w = 0.5f; w <= 6f; w += 0.5f)
                    {
                        Vector2 p = new Vector2(32f, 32f) + d * r + n * w;
                        bool lattice = Mathf.Repeat(r, 5.5f) < 1f || w > 5.4f;
                        Set(c, p, lattice ? C(0x8A6038) : C(0xF2E4C4));
                    }
                for (float r = 2f; r <= 30f; r += 0.5f) Set(c, new Vector2(32f, 32f) + d * r, C(0x5A3A22));   // the spar
            }
            c.Disc(32f, 32f, 2.5f, C(0x3A2416));
            return c;
        }

        static void Set(PixelCanvas c, Vector2 p, Color32 col)
        {
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            if (c.In(x, y)) c[x, y] = col;
        }

        // ---- the near layer: thatched cottages and the jousting lists, 512 x 112 ----

        public static PixelCanvas Near()
        {
            var c = new PixelCanvas(W, NearH);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < 6; y++) c[x, y] = y == 5 ? C(0x8CCC5C) : C(0x6CAE48);
            foreach (var (x, w) in new[] { (40, 70), (262, 58), (410, 76) }) Cottage(c, x, 6, w);

            // The jousting lists: a striped pavilion and a line of pennants between poles.
            int px = 150;
            for (int y = 6; y < 40; y++)
            {
                int half = 22 - (y - 6) * 22 / 44;
                for (int x = px - half; x <= px + half; x++) c[x, y] = ((x - px + 40) / 6) % 2 == 0 ? C(0xD83A3A) : C(0xF4EEDC);
            }
            for (int i = 0; i < 10; i++) Box(c, px - 1, 40 + i, px, 40 + i, C(0x6A4426));
            Box(c, px - 5, 48, px - 1, 50, C(0xF0C83A));                 // its flag
            foreach (int pole in new[] { 196, 238 })
                Box(c, pole, 6, pole + 1, 46, C(0x6A4426));
            Color32[] flags = { C(0xD83A3A), C(0xF0C83A), C(0x3A6AC8), C(0x3A9A4A) };
            for (int i = 0; i < 7; i++)
            {
                int fx = 199 + i * 6, fy = 44 - (i * (6 - i)) / 3;
                Line(c, fx - 1, fy + 1, fx + 5, fy + 1, C(0x5A3A22));
                for (int t = 0; t < 5; t++) Box(c, fx + t / 2, fy - t, fx + 4 - t / 2, fy - t, flags[i % flags.Length]);
            }
            // A fence, hay bales and a cart.
            for (int x = 0; x < W; x += 14) Box(c, x, 6, x + 1, 15, C(0x8A6034));
            for (int x = 0; x < W; x++) { c[x, 12] = C(0xA07440); c[x, 9] = C(0xA07440); }
            foreach (int x in new[] { 120, 132, 360 })
            {
                Box(c, x, 6, x + 9, 13, C(0xE0B850));
                Box(c, x, 9, x + 9, 9, C(0xB88A30));
            }
            Box(c, 470, 10, 492, 18, C(0x9A6A38));
            Disc(c, 474, 9, 3.5f, C(0x5A3A22));
            Disc(c, 488, 9, 3.5f, C(0x5A3A22));
            return c;
        }

        // White walls in a timber frame under a rounded thatch, with a chimney, shuttered windows and a door.
        static void Cottage(PixelCanvas c, int x, int g, int w)
        {
            int h = 30;
            Box(c, x, g, x + w, g + h, C(0xF2E6C8));
            Box(c, x + w - 6, g, x + w, g + h, C(0xDCCCA8));
            for (int bx = x; bx <= x + w; bx += 14) Box(c, bx, g, bx + 1, g + h, C(0x5A3A22));
            Box(c, x, g + h - 1, x + w, g + h, C(0x5A3A22));
            Box(c, x, g + 14, x + w, g + 15, C(0x5A3A22));
            Box(c, x + w / 2 - 4, g, x + w / 2 + 3, g + 12, C(0x6A4426));                 // the door
            foreach (int wx in new[] { x + 6, x + w - 16 })
            {
                Box(c, wx, g + 18, wx + 8, g + 25, C(0x3E3A52));
                Box(c, wx + 4, g + 18, wx + 4, g + 25, C(0x8A7A5A));
                Box(c, wx - 3, g + 18, wx - 1, g + 25, C(0x3A7A4A));                        // shutters
                Box(c, wx + 9, g + 18, wx + 11, g + 25, C(0x3A7A4A));
            }
            // The thatch: rounded, overhanging the walls, straw in three tones.
            for (int y = 0; y < 24; y++)
            {
                int half = (w / 2 + 6) - y * (w / 2 + 6) / 26;
                int mid = x + w / 2;
                for (int tx = mid - half; tx <= mid + half; tx++)
                    c[((tx % W) + W) % W, g + h + y] = y < 2 ? C(0xA88438) : (tx + y * 3) % 7 == 0 ? C(0xC09A48) : tx > mid + half / 3 ? C(0xC8A050) : C(0xE2BC64);
            }
            Box(c, x + w - 16, g + h + 10, x + w - 10, g + h + 24, C(0x9A6A4A));          // the chimney
        }

        // ---- the floor: cobbles under a dirt path ----

        /// <summary>
        /// Cobbles, 64 x 64 (2 x 2 units): rounded stones of mixed sizes and colours (grey, warm and mossy) packed in dark
        /// earth, laid out organically: each pixel belongs to the nearest of 36 jittered stone centres (wrapping, so the
        /// tile repeats seamlessly), mortar runs where two stones meet, and each stone is lit on top and shadowed below.
        /// </summary>
        public static PixelCanvas Floor()
        {
            const int n = 6, size = 64;
            var c = new PixelCanvas(size, size);
            Color32[] stone = { C(0x8C8478), C(0x9A8E7C), C(0x7E786E), C(0xA89C88), C(0x948C80), C(0x847C70), C(0x868A6A) };   // moss on one in seven
            var centres = new Vector2[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    centres[j * n + i] = new Vector2((i + 0.5f + (Hash(i, j) % 60 - 30) / 100f) * size / n + (j % 2) * 4f,
                                                     (j + 0.5f + (Hash(j + 9, i) % 60 - 30) / 100f) * size / n);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float best = float.MaxValue, second = float.MaxValue;
                    int which = 0;
                    Vector2 rel = Vector2.zero;
                    for (int k = 0; k < centres.Length; k++)
                    {
                        float dx = Mathf.Repeat(x - centres[k].x + size / 2f, size) - size / 2f;
                        float dy = Mathf.Repeat(y - centres[k].y + size / 2f, size) - size / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < best) { second = best; best = d; which = k; rel = new Vector2(dx, dy); }
                        else if (d < second) second = d;
                    }
                    if (second - best < 1.6f) { c[x, y] = C(0x3E3830); continue; }   // mortar between stones
                    Color32 s = stone[Hash(which, 3) % stone.Length];
                    float edge = second - best;
                    c[x, y] = rel.y > 1.5f && edge > 3f ? Shade(s, 1.2f) : rel.y < -2.5f || edge < 2.4f ? Shade(s, 0.74f) : s;
                }
            return c;
        }

        /// <summary>The floor's top edge, 32 x 10 like the stone edge: a packed dirt path with pebbles and grass tufts on top.</summary>
        public static PixelCanvas FloorTop()
        {
            var c = new PixelCanvas(32, 10);
            for (int x = 0; x < 32; x++)
            {
                c[x, 0] = c[x, 1] = C(0x7A5A3A);
                c[x, 2] = c[x, 3] = C(0x9A7448);
                for (int y = 4; y < 8; y++) c[x, y] = C(0xC8A070);
                c[x, 8] = C(0x5A9E3E);
                if (x % 5 == 1 || x % 7 == 3) c[x, 9] = C(0x6AAE4A);
            }
            foreach (var (x, y) in new[] { (3, 5), (11, 6), (19, 4), (26, 6), (29, 5), (7, 3), (22, 2) }) c[x, y] = C(0xA08050);
            return c;
        }

        // ---- hazards on the same colliders ----

        /// <summary>A sharpened wooden stake: a repaint of the spike's own pixels (same outline), greyscale for the hazard tint.</summary>
        public static PixelCanvas Stake() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            if (y >= 12) return Grey(0.96f);                                     // the freshly cut point
            if (y == 3 || y == 4) return Grey(0.34f);                            // a rope binding
            if (x % 3 == 0) return Shade(p, 0.66f);                              // the grain
            return Shade(p, 0.9f);
        });

        /// <summary>
        /// The jettied house overhang (the hanging ceiling), 32 x 32 at normal resolution: white plaster panels in an oak
        /// frame with diagonal braces and a leaded window; tiled, it reads as storeys of a timber house.
        /// </summary>
        public static PixelCanvas Overhang()
        {
            var c = new PixelCanvas(32, 32);
            c.Rect(0, 0, 31, 31, C(0xF2E8CC));
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    if ((x + y) % 11 == 0) c[x, y] = C(0xE0D2B0);                 // plaster texture
            Color32 oak = C(0x4A2E1A), lit = C(0x6A4426);
            c.Rect(0, 0, 31, 3, oak);                                             // the jetty beam
            c.Rect(0, 3, 31, 3, lit);
            c.Rect(0, 16, 31, 18, oak);
            foreach (int x in new[] { 0, 16 }) c.Rect(x, 0, x + 2, 31, oak);
            for (int i = 0; i < 12; i++)
            {
                c[3 + i, 4 + i] = oak;                                            // braces
                c[4 + i, 4 + i] = oak;
            }
            c.Rect(20, 21, 28, 29, C(0x3E3A52));                                  // a leaded window
            for (int i = 0; i < 9; i++) { c[20 + i, 21 + i] = C(0x8A8AA4); c[28 - i, 21 + i] = C(0x8A8AA4); }
            c.Rect(19, 20, 29, 20, oak);
            return c;
        }

        /// <summary>
        /// The millstream in a gap, 16 x 192 (6 units deep, so its surface shows once): an earth bank, a bright rippling
        /// surface and water deepening in bands.
        /// </summary>
        public static PixelCanvas Millstream()
        {
            var c = new PixelCanvas(16, 192);
            for (int y = 0; y < 192; y++)
                for (int x = 0; x < 16; x++)
                {
                    int d = 191 - y;
                    c[x, y] = d < 4 ? C(0x5A3E28)
                        : d < 7 ? ((x + d) % 5 == 0 ? C(0xFFFFFF) : C(0x8AD8F0))
                        : d < 24 ? ((x * 3 + d) % 13 == 0 ? C(0x8AD8F0) : C(0x4A9AD8))
                        : d < 70 ? C(0x3A88C8) : C(0x2A6AA8);
                }
            return c;
        }

        /// <summary>
        /// The waterwheel, frame <paramref name="f"/> of 4 (an eighth of a turn, as its eight paddles repeat): oak rim,
        /// spokes and paddles round an iron hub, its lower third under the water (the water line stays level).
        /// </summary>
        public static PixelCanvas Waterwheel(int f)
        {
            var c = new PixelCanvas(48, 48);
            float a0 = f * 11.25f * Mathf.Deg2Rad;
            Vector2 o = new Vector2(23.5f, 23.5f);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    Vector2 p = new Vector2(x, y) - o;
                    float r = p.magnitude, a = Mathf.Atan2(p.y, p.x) - a0;
                    float k = Mathf.Repeat(a, Mathf.PI / 4f) - Mathf.PI / 8f;     // angle from the nearest spoke
                    float across = Mathf.Abs(k) * r;
                    if (r >= 15.5f && r <= 18f) c[x, y] = C(0x7A4A26);
                    else if (r > 18f && r <= 23f && across < 2f) c[x, y] = C(0xC08A50);
                    else if (r < 15.5f && r > 3.5f && across < 1f) c[x, y] = C(0x8A5A30);
                    else if (r <= 3.5f) c[x, y] = r < 1.6f ? C(0x2A2A30) : C(0x4A4A52);
                }
            c.Outline(C(0x3A2416));
            for (int y = 0; y <= 15; y++)
                for (int x = 0; x < 48; x++)
                {
                    Color32 under = c[x, y];
                    if (under.a == 0) continue;
                    Color32 water = y == 15 ? C(0x8AD8F0) : C(0x3A88C8);
                    c[x, y] = Color32.Lerp(under, water, y == 15 ? 0.8f : 0.55f);
                }
            return c;
        }

        /// <summary>A gold groat, 13 x 13: a rim, a lit face and a long cross.</summary>
        public static PixelCanvas Groat()
        {
            var c = new PixelCanvas(13, 13);
            c.Blit(new[]
            {
                "...ooooo...",
                "..oyyYYyoo.",
                ".oyYYykyyyo",
                "oyyyykyyyyo",
                "oyyyykyyyyo",
                "okkkkkkkkko",
                "oyyyykyyyyo",
                "oyyyykyyyyo",
                ".oyyykyyyo.",
                "..oyyyyyo..",
                "...ooooo...",
            }, new Dictionary<char, Color32> { ['o'] = C(0xA8761A), ['y'] = C(0xF0C040), ['Y'] = C(0xFFE88A), ['k'] = C(0x8A5A10) }, 1, 1);
            return c;
        }

        // ---- the dragon ----

        /// <summary>A green dragon gliding over the hills, wings up; <see cref="DragonFlap"/> has them down.</summary>
        public static PixelCanvas Dragon() => DragonFrame(up: true);
        public static PixelCanvas DragonFlap() => DragonFrame(up: false);

        static PixelCanvas DragonFrame(bool up)
        {
            var c = new PixelCanvas(56, 28);
            Color32 body = C(0x3A8A4A), belly = C(0xA8D07A), wing = C(0x2A6A3A), bone = C(0x1E4A2A);
            // The tail, tapering left to a spade.
            for (int x = 2; x < 16; x++) c.Rect(x, 12 + (16 - x) / 6, x, 13 + (16 - x) / 6, body);
            c.Rect(0, 13, 3, 16, C(0xC83A3A));
            // The body and belly.
            for (int y = 9; y <= 16; y++)
                for (int x = 14; x <= 40; x++)
                {
                    float u = (x - 27f) / 13f, v = (y - 12.5f) / 3.8f;
                    if (u * u + v * v <= 1f) c[x, y] = y < 11 ? belly : body;
                }
            // The neck and the head, facing right, with a red eye and little horns.
            for (int i = 0; i < 8; i++) c.Rect(39 + i, 13 + i / 2, 40 + i, 15 + i / 2, body);
            c.Rect(46, 16, 53, 20, body);
            c.Rect(52, 16, 55, 18, body);
            c[50, 19] = C(0xE83A2A);
            c.Rect(47, 21, 47, 22, C(0xEADCC0));
            c.Rect(49, 21, 49, 23, C(0xEADCC0));
            // The wing: a membrane between bones, raised or lowered.
            Vector2 root = new Vector2(24f, 15f);
            Vector2[] tips = up ? new[] { new Vector2(14f, 27f), new Vector2(22f, 27f), new Vector2(31f, 25f) }
                                : new[] { new Vector2(12f, 2f), new Vector2(20f, 1f), new Vector2(30f, 4f) };
            for (int t = 0; t < tips.Length - 1; t++)
                for (float s = 0f; s <= 1f; s += 0.02f)
                    for (float q = 0f; q <= 1f; q += 0.05f)
                        Set(c, Vector2.Lerp(root, Vector2.Lerp(tips[t], tips[t + 1], s), q), wing);
            foreach (Vector2 tip in tips)
                for (float q = 0f; q <= 1f; q += 0.03f) Set(c, Vector2.Lerp(root, tip, q), bone);
            c.Outline(C(0x10281A));
            return c;
        }
    }
}
