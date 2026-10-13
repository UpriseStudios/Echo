using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// 16-bit world tiles and scenery. Everything is greyscale so the views tint it from the run's palette,
    /// keeping the hue-shifting background (game spec art direction) and the palette cosmetics working.
    /// Shading uses a few flat steps and ordered dithering, as 16-bit consoles did.
    /// </summary>
    public static class PixelWorld
    {
        /// <summary>Ground edge: grass tufts above the line, a bright lip, then a band fading into the fill. 32 x 10, line at row 8.</summary>
        public static PixelCanvas GroundTop()
        {
            var c = new PixelCanvas(32, 10);
            for (int x = 0; x < 32; x++)
            {
                c.Rect(x, 0, x, 7, Grey(0.78f));
                c[x, 7] = Grey(1f);
                c[x, 6] = Grey(0.92f);
                if (Bayer(x, 5) < 0.5f) c[x, 5] = Grey(0.92f);
                if (Bayer(x, 2) < 0.25f) c[x, 2] = Grey(0.62f);
            }
            // Tufts: a few 1-2 px blades above the line, spaced unevenly so the repeat is hard to spot.
            foreach (int x in new[] { 2, 3, 9, 15, 16, 22, 27, 28 })
            {
                c[x, 8] = Grey(1f);
                if (x % 2 == 0) c[x, 9] = Grey(1f);
            }
            return c;
        }

        /// <summary>Stone bricks under the ground, offset every other course. 32 x 32, tiles both ways.</summary>
        public static PixelCanvas GroundFill()
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    int course = y / 8;
                    int bx = (x + (course % 2) * 8) % 16;
                    int by = y % 8;
                    float v;
                    if (by == 0 || bx == 0) v = 0.62f;                 // mortar
                    else if (by == 7) v = 0.9f;                        // top highlight of each brick
                    else if (bx == 15) v = 0.7f;                       // right shadow
                    else v = Bayer(x, y) < 0.12f ? 0.74f : 0.8f;       // speckled face
                    c[x, y] = Grey(v);
                }
            return c;
        }

        /// <summary>One spike, 16 x 16: lit left face, shaded right face, a bright edge on the tip.</summary>
        public static PixelCanvas Spike()
        {
            var c = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
            {
                int half = (16 - y) / 2;
                for (int x = 8 - half; x < 8 + half; x++)
                    c[x, y] = Grey(x < 8 ? 1f : 0.68f);
                if (half > 0) c[8 - half, y] = Grey(0.85f);
            }
            c.Rect(7, 13, 8, 15, Grey(1f));
            return c;
        }

        /// <summary>A bevelled block for movers and ceilings, 16 x 16, 9-sliced with a 4 px border.</summary>
        public static PixelCanvas Block()
        {
            var c = new PixelCanvas(16, 16);
            c.Rect(0, 0, 15, 15, Grey(0.8f));
            c.Rect(0, 15, 15, 15, Grey(1f));
            c.Rect(0, 0, 0, 15, Grey(0.94f));
            c.Rect(0, 0, 15, 0, Grey(0.55f));
            c.Rect(15, 0, 15, 15, Grey(0.6f));
            c.Rect(1, 14, 14, 14, Grey(0.9f));
            foreach (var (x, y) in new[] { (2, 2), (13, 2), (2, 13), (13, 13) }) c[x, y] = Grey(0.58f);
            return c;
        }

        /// <summary>Headstone, 12 x 16, with a cross; the bottom two rows are a mound below the ground line.</summary>
        public static PixelCanvas Grave()
        {
            var c = new PixelCanvas(12, 16);
            c.Rect(0, 0, 11, 1, Grey(0.7f));
            c.Rect(2, 2, 9, 11, Grey(0.92f));
            c.Rect(3, 12, 8, 12, Grey(0.92f));
            c.Rect(4, 13, 7, 13, Grey(0.92f));
            c.Rect(9, 2, 9, 11, Grey(0.7f));
            c.Rect(8, 12, 8, 12, Grey(0.7f));
            c.Rect(5, 5, 6, 11, Grey(0.45f));
            c.Rect(4, 9, 7, 9, Grey(0.45f));
            c.Outline(Grey(0.25f));
            return c;
        }

        /// <summary>
        /// The death screen's headstone (a user revision: the grave the camera zooms in to), 56 x 60 in full colour:
        /// a round-topped stone on a slab over a mound of earth, lit from the left, with a crack, moss and a carved
        /// cross. The face between the slab and the cross (rows 12 to 44) is left plain for the engraved epitaph.
        /// Drawn at the zoomed world's pixel size, so it matches the scene behind it.
        /// </summary>
        public static PixelCanvas Headstone()
        {
            const int w = 56, h = 60;
            Color32 light = Hex(0xD5DDE2), face = Hex(0xB8C3CA), speck = Hex(0xA6B2BA), shade = Hex(0x8794A0),
                carved = Hex(0x5E6A76), outline = Hex(0x222A30), moss = Hex(0x5E8A3E), mossDark = Hex(0x3F6A2E),
                dirt = Hex(0x4A3B2E), dirtDark = Hex(0x3A2E24), dirtSpeck = Hex(0x5C4A3A), grass = Hex(0x6FA84A);
            var c = new PixelCanvas(w, h);

            // Earth mound, narrowing upwards.
            for (int y = 0; y < 4; y++)
                for (int x = 1 + y * 2; x <= w - 2 - y * 2; x++)
                    c[x, y] = Hash(x, y) % 7 == 0 ? dirtSpeck : y == 3 ? dirt : Hash(x, y) % 3 == 0 ? dirtDark : dirt;

            // Slab.
            c.Rect(5, 4, w - 6, 9, face);
            c.Rect(5, 9, w - 6, 9, light);
            c.Rect(w - 7, 4, w - 6, 8, shade);
            c.Rect(5, 4, w - 6, 4, shade);

            // Body and arched top.
            const int left = 9, right = 46, top = 38;
            const float cx = 27.5f, radius = 19f;
            for (int y = 10; y < h - 1; y++)
            {
                float hw = y <= top ? radius : Mathf.Sqrt(Mathf.Max(0f, radius * radius - (y - top + 0.5f) * (y - top + 0.5f)));
                int x0 = Mathf.Max(left, Mathf.CeilToInt(cx - hw)), x1 = Mathf.Min(right, Mathf.FloorToInt(cx + hw));
                for (int x = x0; x <= x1; x++)
                {
                    bool edgeTop = y > top && (y + 1 >= h - 1 || Mathf.Sqrt(radius * radius - (y + 1 - top + 0.5f) * (y + 1 - top + 0.5f)) < Mathf.Abs(x + 0.5f - cx));
                    c[x, y] = x - x0 < 2 || edgeTop ? light : x1 - x < 2 ? shade : Hash(x, y) % 23 == 0 ? speck : face;
                }
            }

            // Crack down from the top right, and moss creeping up from the slab.
            int[] crack = { 41, 51, 40, 50, 40, 49, 39, 48, 39, 47, 38, 46, 38, 45, 37, 44 };
            for (int i = 0; i < crack.Length; i += 2) c[crack[i], crack[i + 1]] = carved;
            for (int x = left; x <= left + 9; x++)
                for (int y = 10; y <= 10 + (x * 5 % 4); y++) c[x, y] = (x + y) % 3 == 0 ? mossDark : moss;
            for (int x = 30; x <= 36; x++) c[x, 9] = x % 2 == 0 ? moss : mossDark;
            foreach (int gx in new[] { 3, 8, 14, 44, 49, 52 })
            {
                c[gx, 3] = grass;
                c[gx, 4] = grass;
                if (gx % 2 == 0) c[gx + 1, 5] = grass;
            }

            // A carved cross: dark grooves with a lit lower edge.
            c.Rect(27, 46, 28, 54, carved);
            c.Rect(24, 51, 31, 52, carved);
            c.Rect(27, 45, 28, 45, light);
            c.Rect(24, 50, 26, 50, light);
            c.Rect(29, 50, 31, 50, light);

            c.Outline(outline);
            return c;
        }

        static int Hash(int x, int y) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u);

        /// <summary>
        /// Distant mountain ranges, 512 x 224, tileable: straight-sided pixel peaks (each a stepped triangle),
        /// lit from the left, snow on the tall back range, a lower front range dithered towards its base.
        /// </summary>
        public static PixelCanvas Mountains()
        {
            const int w = 512, h = 224;
            var c = new PixelCanvas(w, h);
            int[] back = Range(w, new[] { (20, 178), (96, 150), (160, 196), (250, 160), (330, 188), (420, 146), (470, 172) }, 1.15f, out bool[] backLit);
            int[] front = Range(w, new[] { (60, 112), (130, 92), (205, 120), (290, 100), (370, 124), (450, 96) }, 0.9f, out bool[] frontLit);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (y <= front[x])
                    {
                        float k = 1f - y / (float)Mathf.Max(1, front[x]);
                        float v = frontLit[x] ? 0.9f : 0.85f;
                        c[x, y] = Grey(Bayer(x, y) < k * 0.5f ? v - 0.04f : v);
                    }
                    else if (y <= back[x])
                    {
                        bool snow = back[x] > 165 && y > back[x] - 10 + (x * 7 % 5);
                        c[x, y] = snow ? Grey(1f) : Grey(backLit[x] ? 0.96f : 0.92f);
                    }
                }
            return c;
        }

        // Height of a row of peaks: each falls away at `slope` px per px; the tallest wins. Peaks repeat every
        // width so the tile wraps. `lit` is true on a peak's left (sunward) face.
        static int[] Range(int w, (int x, int h)[] peaks, float slope, out bool[] lit)
        {
            var height = new int[w];
            lit = new bool[w];
            for (int x = 0; x < w; x++)
            {
                float best = 0f;
                foreach (var p in peaks)
                    foreach (int shift in new[] { -w, 0, w })
                    {
                        float d = x - (p.x + shift);
                        float v = p.h - Mathf.Abs(d) * slope;
                        if (v <= best) continue;
                        best = v;
                        lit[x] = d < 0;
                    }
                height[x] = Mathf.RoundToInt(best);
            }
            return height;
        }

        /// <summary>Near hills with round trees, 512 x 112, tileable.</summary>
        public static PixelCanvas Hills()
        {
            const int w = 512, h = 112;
            var c = new PixelCanvas(w, h);
            int[] top = new int[w];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                top[x] = Mathf.RoundToInt(44 + 16 * Mathf.Sin(t * 2 + 0.7f) + 8 * Mathf.Sin(t * 5 + 1.9f));
                for (int y = 0; y <= top[x]; y++)
                    c[x, y] = Grey(y >= top[x] - 1 ? 1f : Bayer(x, y) < 0.3f * y / top[x] ? 0.95f : 0.88f);
            }
            // Trees: a trunk and a stepped round crown, at fixed spots so the tile repeats cleanly.
            foreach (int tx in new[] { 30, 95, 104, 180, 262, 300, 311, 390, 455 })
            {
                int baseY = top[tx];
                int r = 7 + tx % 5;
                c.Rect(tx, baseY, tx + 1, baseY + 6, Grey(0.8f));
                c.Disc(tx + 1, baseY + 6 + r, r, Grey(0.96f));
                c.Disc(tx - 1, baseY + 7 + r, r - 3, Grey(1f));
            }
            return c;
        }

        // ---- Themed Biomes (value-adds doc): near layers, 512 x 112 and tileable, greyscale for tinting ----

        /// <summary>Graveyard: low hills with leaning headstones, crosses, two dead trees and a stretch of iron fence.</summary>
        public static PixelCanvas GraveyardHills()
        {
            const int w = 512, h = 112;
            var c = new PixelCanvas(w, h);
            int[] top = new int[w];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                top[x] = Mathf.RoundToInt(30 + 10 * Mathf.Sin(t * 2 + 0.3f) + 5 * Mathf.Sin(t * 7 + 1.1f));
                for (int y = 0; y <= top[x]; y++)
                    c[x, y] = Grey(y >= top[x] - 1 ? 1f : Bayer(x, y) < 0.3f * y / top[x] ? 0.95f : 0.88f);
            }
            foreach (int gx in GraveyardStones)
            {
                int b = top[gx];
                if (gx % 3 == 0)
                {
                    c.Rect(gx + 2, b, gx + 3, b + 12, Grey(0.97f));     // a cross
                    c.Rect(gx, b + 8, gx + 5, b + 9, Grey(0.97f));
                }
                else
                {
                    int lean = gx % 2 == 0 ? 1 : 0;                  // a round-topped stone, some leaning
                    for (int y = 0; y < 9; y++) c.Rect(gx + (y > 5 ? lean : 0), b + y, gx + 5 + (y > 5 ? lean : 0), b + y, Grey(0.97f));
                    c.Rect(gx + 1 + lean, b + 9, gx + 4 + lean, b + 9, Grey(0.97f));
                }
            }
            foreach (int tx in new[] { 105, 390 })
            {
                int b = top[tx];
                c.Rect(tx, b, tx + 2, b + 26, Grey(0.94f));
                c.Line(tx + 1, b + 18, tx + 9, b + 28, Grey(0.94f), 1);
                c.Line(tx + 1, b + 22, tx - 7, b + 31, Grey(0.94f), 1);
                c.Line(tx + 5, b + 22, tx + 4, b + 33, Grey(0.94f), 1);
                c.Line(tx - 4, b + 27, tx - 6, b + 35, Grey(0.94f), 1);
            }
            for (int x = 280; x < 320; x += 4)
            {
                int b = top[x];
                c.Rect(x, b, x, b + 13, Grey(0.97f));
                c[x, b + 14] = Grey(0.97f);
                if (x + 4 < 320)
                {
                    c.Rect(x, b + 4, x + 4, b + 4, Grey(0.97f));
                    c.Rect(x, b + 10, x + 4, b + 10, Grey(0.97f));
                }
            }
            return c;
        }

        /// <summary>Where GraveyardHills has its headstones, so the Wisps flourish rises from them.</summary>
        public static readonly int[] GraveyardStones = { 20, 48, 70, 140, 165, 230, 262, 330, 352, 420, 470, 495 };

        /// <summary>The height of GraveyardHills' ground at a column, for the wisps.</summary>
        public static int GraveyardTop(int x)
        {
            float t = x / 512f * Mathf.PI * 2f;
            return Mathf.RoundToInt(30 + 10 * Mathf.Sin(t * 2 + 0.3f) + 5 * Mathf.Sin(t * 7 + 1.1f));
        }

        /// <summary>The Castle: a curtain wall with crenellations, a gate and three towers (TowerTops).</summary>
        public static PixelCanvas CastleWalls()
        {
            const int w = 512, h = 112;
            var c = new PixelCanvas(w, h);
            const int wall = 46;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y <= wall; y++) c[x, y] = Grey(Bayer(x, y) < 0.25f * y / wall ? 0.93f : 0.88f);
                if (x % 12 < 6) c.Rect(x, wall + 1, x, wall + 6, Grey(0.88f));
            }
            foreach (Vector2Int t in TowerTops)
            {
                c.Rect(t.x - 12, 0, t.x + 12, t.y, Grey(0.92f));
                c.Rect(t.x + 9, 0, t.x + 12, t.y, Grey(0.86f));
                for (int x = t.x - 14; x <= t.x + 14; x++)
                    if ((x - t.x + 14) % 7 < 4) c.Rect(x, t.y + 1, x, t.y + 6, Grey(0.92f));
                c.Rect(t.x - 14, t.y - 2, t.x + 14, t.y, Grey(0.95f));
                // Arrow slits: dark, lit at night by the RoyalBanners flourish.
                c.Rect(t.x - 1, t.y - 22, t.x + 1, t.y - 14, Grey(0.6f));
                c.Rect(t.x - 1, t.y - 42, t.x + 1, t.y - 34, Grey(0.6f));
            }
            c.Rect(250, 0, 266, 22, Grey(0.6f));
            c.Disc(258, 22, 8, Grey(0.6f));
            return c;
        }

        /// <summary>The gate torches' x in CastleBanners (flames at y 21 to 25), so effects can light them.</summary>
        public static readonly int[] GateTorches = { 244, 272 };

        /// <summary>The top of CastleWalls' curtain wall, in pixels (crenellations above it).</summary>
        public const int CastleWallTop = 52;

        /// <summary>Tower centres and tops in CastleWalls, so the banners overlay lines up with them.</summary>
        public static readonly Vector2Int[] TowerTops = { new Vector2Int(70, 86), new Vector2Int(200, 98), new Vector2Int(400, 80) };

        /// <summary>The RoyalBanners flourish over CastleWalls, in colour: a pennant on every tower, lit windows, gate torches.</summary>
        public static PixelCanvas CastleBanners()
        {
            var c = new PixelCanvas(512, 112);
            Color32 pole = Hex(0x6B4426), red = Hex(0xD8323C), redDark = Hex(0xA8202A), gold = Hex(0xFFD040), lit = Hex(0xFFC860);
            foreach (Vector2Int t in TowerTops)
            {
                c.Rect(t.x, t.y + 1, t.x, t.y + 18, pole);
                for (int y = 0; y < 7; y++)
                    c.Rect(t.x + 1, t.y + 11 + y, t.x + 1 + (7 - Mathf.Abs(y - 3) * 2), t.y + 11 + y, y == 3 ? gold : y > 3 ? red : redDark);
                c.Rect(t.x - 1, t.y - 22, t.x + 1, t.y - 14, lit);
                c.Rect(t.x - 1, t.y - 42, t.x + 1, t.y - 34, lit);
            }
            foreach (int tx in GateTorches)
            {
                c.Rect(tx, 14, tx + 1, 20, pole);
                c.Rect(tx - 1, 21, tx + 2, 23, Hex(0xFF8A2A));
                c.Rect(tx, 24, tx + 1, 25, Hex(0xFFE45C));
            }
            return c;
        }

        /// <summary>Space: a cratered moon surface with rocks floating above it.</summary>
        public static PixelCanvas MoonSurface()
        {
            const int w = 512, h = 112;
            var c = new PixelCanvas(w, h);
            int[] top = new int[w];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                top[x] = Mathf.RoundToInt(22 + 6 * Mathf.Sin(t * 3 + 0.4f) + 3 * Mathf.Sin(t * 11 + 2f));
                for (int y = 0; y <= top[x]; y++) c[x, y] = Grey(y >= top[x] - 1 ? 1f : 0.9f);
            }
            foreach (int cx in new[] { 40, 150, 260, 345, 450 })
            {
                int r = 6 + cx % 5, b = top[cx];
                for (int x = cx - r; x <= cx + r; x++)
                    for (int y = b - 3; y <= b; y++)
                        if ((x - cx) * (x - cx) * 4 + (y - b) * (y - b) * 25 <= r * r * 4) c[(x + w) % w, y] = Grey(0.78f);
            }
            foreach (var (fx, fy, fr) in new[] { (90, 70, 6), (210, 88, 4), (300, 64, 8), (380, 92, 3), (480, 76, 5) })
            {
                c.Disc(fx, fy, fr, Grey(0.94f));
                c.Disc(fx + 1, fy - 1, Mathf.Max(1, fr - 2), Grey(0.88f));
            }
            return c;
        }

        // ---- Space backdrop and the character flourishes (in colour) ----

        /// <summary>A ringed planet, 48 x 32, for the Space sky.</summary>
        public static PixelCanvas Planet()
        {
            var c = new PixelCanvas(48, 32);
            c.Disc(24, 16, 12, Hex(0xC07A5A));
            c.Disc(21, 19, 9, Hex(0xE09A6A));
            for (int x = 14; x <= 34; x += 3) c[x, 13 + x % 2] = Hex(0xA05A44);
            for (int x = 1; x < 47; x++)
            {
                int y = 16 + (x - 24) / 6;
                if (Mathf.Abs(x - 24) > 11 || y < 13)
                {
                    c[x, y] = Hex(0xF2D8A0);
                    c[x, y + 1] = Hex(0xC8A870);
                }
            }
            return c;
        }

        public static PixelCanvas Star()
        {
            var c = new PixelCanvas(3, 3);
            c[1, 1] = Hex(0xFFFFFF);
            c[0, 1] = c[2, 1] = c[1, 0] = c[1, 2] = Hex(0xBFE8FF, 140);
            return c;
        }

        public static PixelCanvas ShootingStar()
        {
            var c = new PixelCanvas(12, 4);
            for (int i = 0; i < 12; i++) c[i, i / 4] = Hex(0xFFFFFF, (byte)(40 + i * 18));
            return c;
        }

        public static PixelCanvas Ufo()
        {
            var c = new PixelCanvas(20, 14);
            c.Disc(10, 9, 4, Hex(0x9CE0FF, 220));
            c.Rect(2, 5, 17, 7, Hex(0x9AA3B5));
            c.Rect(0, 6, 19, 6, Hex(0xC9D1DE));
            foreach (int x in new[] { 3, 8, 13, 17 }) c[x, 5] = Hex(0xFFE45C);
            for (int y = 0; y < 5; y++) c.Rect(9 - y, y, 10 + y, y, Hex(0xB8FFB0, 60));
            return c;
        }

        public static PixelCanvas FullMoon()
        {
            var c = new PixelCanvas(40, 40);
            c.Disc(20, 20, 18, Hex(0xFFF4D0));
            foreach (var (x, y, r) in new[] { (14, 24, 4), (26, 14, 3), (24, 27, 2), (11, 13, 2) }) c.Disc(x, y, r, Hex(0xE8DCB0));
            return c;
        }

        public static PixelCanvas Bat(int frame)
        {
            var c = new PixelCanvas(11, 6);
            Color32 k = Hex(0x1A1424);
            c.Rect(4, 2, 6, 3, k);
            c[4, 4] = c[6, 4] = k;
            if (frame == 0)
            {
                c.Rect(0, 4, 3, 4, k);
                c.Rect(7, 4, 10, 4, k);
                c.Rect(1, 3, 3, 3, k);
                c.Rect(7, 3, 9, 3, k);
                c[0, 5] = c[10, 5] = k;
            }
            else
            {
                c.Rect(0, 1, 3, 1, k);
                c.Rect(7, 1, 10, 1, k);
                c.Rect(2, 2, 3, 2, k);
                c.Rect(7, 2, 8, 2, k);
                c[0, 0] = c[10, 0] = k;
            }
            c[5, 3] = Hex(0xFF3A3A);
            return c;
        }

        /// <summary>A soft round glow, white, 32 x 32, in three stepped rings like the Lights Out spotlight.</summary>
        public static PixelCanvas Glow()
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 16f;
                    byte a = d < 0.45f ? (byte)110 : d < 0.7f ? (byte)70 : d < 1f ? (byte)30 : (byte)0;
                    if (a > 0) c[x, y] = Hex(0xFFFFFF, a);
                }
            return c;
        }

        /// <summary>A headlight's beam, 40 x 16, widening to the right from its left edge.</summary>
        public static PixelCanvas Beam()
        {
            var c = new PixelCanvas(40, 16);
            for (int x = 0; x < 40; x++)
            {
                int half = 1 + x * 7 / 39;
                byte a = (byte)Mathf.Lerp(120, 15, x / 39f);
                for (int y = 8 - half; y < 8 + half; y++) c[x, y] = Hex(0xFFF6C0, a);
            }
            return c;
        }

        /// <summary>A 3 x 3 point of light with a soft cross: fireflies and sparkles.</summary>
        public static PixelCanvas Dot(uint rgb)
        {
            var c = new PixelCanvas(3, 3);
            c[1, 1] = Hex(rgb);
            c[0, 1] = c[2, 1] = c[1, 0] = c[1, 2] = Hex(rgb, 110);
            return c;
        }

        public static PixelCanvas Wisp()
        {
            var c = new PixelCanvas(5, 8);
            c.Disc(2, 2, 2, Hex(0x9CF0FF, 200));
            c[2, 5] = Hex(0x9CF0FF, 120);
            c[1, 6] = Hex(0x9CF0FF, 70);
            c[2, 7] = Hex(0x9CF0FF, 40);
            return c;
        }

        public static PixelCanvas RainDrop()
        {
            var c = new PixelCanvas(2, 6);
            for (int y = 0; y < 6; y++) c[y < 3 ? 1 : 0, y] = Hex(0xBFD8FF, (byte)(60 + y * 25));
            return c;
        }

        public static PixelCanvas FogPuff()
        {
            var c = new PixelCanvas(48, 14);
            foreach (var (x, y, r) in new[] { (10, 5, 5), (20, 7, 7), (31, 6, 6), (40, 5, 4) }) c.Disc(x, y, r, Hex(0xE8F0F0, 70));
            return c;
        }

        /// <summary>A distant lightning bolt, 9 x 28, white-blue: the Storm's flourish, far off in the sky.</summary>
        public static PixelCanvas Bolt()
        {
            var c = new PixelCanvas(9, 28);
            int x = 5;
            for (int y = 27; y >= 0; y--)
            {
                c[x, y] = Hex(0xEAF4FF);
                c[x + 1, y] = Hex(0xA8C8FF, 150);
                if (y % 5 == 0)
                {
                    // Each zig steps two across: join it so the bolt reads as one stroke.
                    int to = Mathf.Clamp(x + (y % 10 == 0 ? -2 : 2), 1, 7);
                    for (int s = Mathf.Min(x, to); s <= Mathf.Max(x, to); s++) c[s, y] = Hex(0xEAF4FF);
                    x = to;
                }
                if (y == 14) { c[x + 1, y] = Hex(0xEAF4FF, 160); c[x + 2, y - 1] = Hex(0xEAF4FF, 120); }
            }
            return c;
        }

        /// <summary>A faint horizontal glint, 14 x 1: the Tide's horizon shimmer.</summary>
        public static PixelCanvas Shimmer()
        {
            var c = new PixelCanvas(14, 1);
            for (int x = 0; x < 14; x++) c[x, 0] = Hex(0xFFFFFF, (byte)(200 - System.Math.Abs(x - 7) * 26));
            return c;
        }

        /// <summary>A cap of frost, 6 x 2, that sits on a headstone or a merlon: Frost in the Graveyard and Castle.</summary>
        public static PixelCanvas FrostCap()
        {
            var c = new PixelCanvas(6, 2);
            c.Rect(0, 0, 5, 0, Hex(0xCFEFFF));
            c.Rect(1, 1, 4, 1, Hex(0xF4FCFF));
            c[2, 0] = Hex(0xFFFFFF);
            return c;
        }

        /// <summary>A row of icicles, 7 x 5, hung under a tower's lip: Frost in the Castle.</summary>
        public static PixelCanvas Icicles()
        {
            var c = new PixelCanvas(7, 5);
            c.Rect(0, 4, 6, 4, Hex(0xE8F8FF));
            c.Rect(1, 1, 1, 3, Hex(0xBFE8FA));
            c.Rect(3, 2, 3, 3, Hex(0xBFE8FA));
            c.Rect(5, 0, 5, 3, Hex(0xBFE8FA));
            c[1, 3] = c[5, 3] = Hex(0xFFFFFF);
            return c;
        }

        /// <summary>A crisp four-point star, 5 x 5: Frost's sharper stars.</summary>
        public static PixelCanvas Glint()
        {
            var c = new PixelCanvas(5, 5);
            c.Rect(2, 0, 2, 4, Hex(0xBFE8FF, 200));
            c.Rect(0, 2, 4, 2, Hex(0xBFE8FF, 200));
            c[2, 2] = Hex(0xFFFFFF);
            return c;
        }

        /// <summary>A wall torch, 4 x 12, like the gate torches in CastleBanners: Ember lights them in the Castle.</summary>
        public static PixelCanvas Torch()
        {
            var c = new PixelCanvas(4, 12);
            c.Rect(1, 0, 2, 6, Hex(0x6B4426));
            c.Rect(0, 7, 3, 9, Hex(0xFF8A2A));
            c.Rect(1, 10, 2, 11, Hex(0xFFE45C));
            return c;
        }

        public static PixelCanvas Petal()
        {
            var c = new PixelCanvas(3, 2);
            c[0, 0] = c[1, 0] = c[1, 1] = Hex(0xFFB0C8);
            c[2, 1] = Hex(0xFF8AB0);
            return c;
        }

        // ---- power-up icons (a user revision), 16 x 16, in colour ----

        /// <summary>Ghost Shield: a cyan bubble with a shine.</summary>
        public static PixelCanvas ShieldIcon()
        {
            var c = new PixelCanvas(16, 16);
            c.Disc(7.5f, 7.5f, 7f, Hex(0x2A8FA8));
            c.Disc(7.5f, 7.5f, 5.5f, Hex(0x73F2FF, 120));
            c.Rect(4, 9, 5, 11, Hex(0xFFFFFF));
            c[6, 11] = Hex(0xFFFFFF);
            c.Outline(Hex(0x0E2A33));
            return c;
        }

        /// <summary>Phase: a ghost drawn as a dotted outline, half there.</summary>
        public static PixelCanvas PhaseIcon()
        {
            var c = new PixelCanvas(16, 16);
            c.Blit(new[]
            {
                "................",
                ".....pppppp.....",
                "....p......p....",
                "...p........p...",
                "...p..k..k..p...",
                "...p........p...",
                "...p........p...",
                "...p........p...",
                "...p........p...",
                "...p........p...",
                "...p........p...",
                "...p.p.pp.p.p...",
                "...pp.p..p.pp...",
                "................",
                "................",
                "................",
            }, Pal(('p', Hex(0xC08CFF)), ('k', Hex(0xC08CFF))), 0, 0);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if ((x + y) % 2 == 1 && c[x, y].a > 0 && y > 2 && y < 11) c[x, y] = Hex(0xC08CFF, 90);
            return c;
        }

        /// <summary>Ghost Lantern: a little lantern with a warm flame.</summary>
        public static PixelCanvas LanternIcon()
        {
            var c = new PixelCanvas(16, 16);
            c.Blit(new[]
            {
                "......kkkk......",
                ".....k....k.....",
                "......kkkk......",
                ".....mmmmmm.....",
                "....mggggggm....",
                "....mgyyyygm....",
                "....mgyooygm....",
                "....mgyooygm....",
                "....mgyyyygm....",
                "....mggggggm....",
                ".....mmmmmm.....",
                "......mmmm......",
                "................",
                "................",
                "................",
                "................",
            }, Pal(('k', Hex(0x6B4426)), ('m', Hex(0x8A6A3A)), ('g', Hex(0xFFE7A0)), ('y', Hex(0xFFD040)), ('o', Hex(0xFF8A2A))), 0, 0);
            c.Outline(Hex(0x2A1A08));
            return c;
        }

        /// <summary>HUD ghost-count icon: the hood as a 1-px outline, 16 x 16.</summary>
        public static PixelCanvas GhostIcon()
        {
            var c = new PixelCanvas(16, 16);
            var fill = Pal('h', Grey(1f, 90));
            c.Blit(new[]
            {
                "................",
                ".....hhhhh......",
                "....hhhhhhh.....",
                "...hhhhhhhhh....",
                "...hhhhhh..h....",
                "..hhhhhh....h...",
                "..hhhhhh.h..h...",
                "..hhhhhh....h...",
                "..hhhhhhh..h....",
                "..hhhhhhhhh.....",
                "...hhhhhhhh.....",
                "..hhhhhhhhhh....",
                ".hhhhhhhhhhhh...",
                ".hhhhhhhhhhhh...",
                "................",
                "................",
            }, fill, 0, 0);
            c.Outline(Grey(1f));
            return c;
        }

        /// <summary>The Nemesis's horns, 14 x 5 plus outline, worn on top of its own skin (skins addendum).</summary>
        public static PixelCanvas Horns()
        {
            var c = new PixelCanvas(16, 7);
            c.Blit(new[]
            {
                "h............h",
                "hh..........hh",
                "Hhh........hhH",
                ".Hhh......hhH.",
                "..HHh....hHH..",
            }, new System.Collections.Generic.Dictionary<char, Color32> { ['h'] = Grey(1f), ['H'] = Grey(0.75f) }, 1, 1);
            c.Outline(Grey(0.3f));
            return c;
        }

        /// <summary>Ghost Bounties coin, 11 x 11: a ring, a skull-ish face and a shine, greyscale for tinting.</summary>
        public static PixelCanvas Coin()
        {
            var c = new PixelCanvas(13, 13);
            c.Blit(new[]
            {
                "...ooooo...",
                "..owwhhhoo.",
                ".owhhhhhhho",
                "owhhkhhkhho",
                "ohhhkhhkhho",
                "ohhhhhhhhho",
                "ohhkhkhkhho",
                "ohhhhhhhhho",
                ".ohhhhhhho.",
                "..ohhhhho..",
                "...ooooo...",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                ['o'] = Grey(0.7f), ['h'] = Grey(0.9f), ['w'] = Grey(1f), ['k'] = Grey(0.45f),
            }, 1, 1);
            c.Outline(Grey(0.3f));
            return c;
        }

        /// <summary>
        /// Lights Out twist: a dark square with a lit circle in the middle, edged with an ordered dither. 256 px
        /// at 8 px a unit covers any screen around the player; drawn black and tinted by alpha.
        /// </summary>
        public static PixelCanvas Spotlight()
        {
            const int size = 256;
            var c = new PixelCanvas(size, size);
            const float lit = 17f, dark = 25f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                    float k = Mathf.Clamp01((d - lit) / (dark - lit));
                    if (k > Bayer(x, y)) c[x, y] = new Color32(0, 0, 0, 255);
                }
            return c;
        }

        /// <summary>The 1-UP heart: 9 x 8 plus a 1 px outline, greyscale with a highlight so the HUD can tint it.</summary>
        public static PixelCanvas Heart()
        {
            var c = new PixelCanvas(11, 10);
            c.Blit(new[]
            {
                ".hh...hh.",
                "hwhh.hhhh",
                "hwhhhhhhh",
                "hhhhhhhhh",
                ".hhhhhhh.",
                "..hhhhh..",
                "...hhh...",
                "....h....",
            }, new System.Collections.Generic.Dictionary<char, Color32> { ['h'] = Grey(0.82f), ['w'] = Grey(1f) }, 1, 1);
            c.Outline(Grey(0.35f));
            return c;
        }

        /// <summary>UI button and card frame, 12 x 12, 9-sliced with a 4 px border: chamfered corners and a bevel.</summary>
        public static PixelCanvas UiFrame()
        {
            var c = new PixelCanvas(12, 12);
            c.Rect(1, 0, 10, 11, Grey(1f));
            c.Rect(0, 1, 11, 10, Grey(1f));
            c.Rect(1, 0, 10, 1, Grey(0.72f));
            c[0, 1] = Grey(0.72f);
            c[11, 1] = Grey(0.72f);
            c.Rect(2, 10, 9, 10, Grey(1f));
            return c;
        }

        /// <summary>1024 px app icon: the hooded runner and two echoes on a banded, dithered dusk sky.</summary>
        public static PixelCanvas Icon()
        {
            // 64 px scaled 16x to 1024. The figures are drawn at 2x so they read at home-screen size.
            const int px = 64, scale = 16;
            var c = new PixelCanvas(px, px);
            var top = new Color(0.16f, 0.50f, 0.56f);
            var bottom = new Color(0.34f, 0.22f, 0.52f);
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float k = y / (float)(px - 1);
                    float band = Mathf.Floor(k * 8f + Bayer(x, y)) / 8f;
                    c[x, y] = Color.Lerp(bottom, top, band);
                }
            c.Rect(0, 0, px - 1, 4, Hex(0x1E2F3A));
            c.Rect(0, 5, px - 1, 5, Hex(0x5E7F8C));

            PixelCanvas runner = PixelRig.Frame(PixelCharacters.All[0], 0);
            PixelCanvas ghost = runner.Hollow(60).Scaled(2);
            var cyan = new Color32(115, 242, 255, 255);
            for (int e = 2; e >= 1; e--) Stamp(c, ghost, 20 - e * 14, 6, cyan, 1f - e * 0.3f);
            Stamp(c, runner.Scaled(2), 20, 6, new Color32(255, 255, 255, 255), 1f);
            return c.Scaled(scale);
        }

        // ---- Android adaptive icon: two 432 x 432 layers (108 dp at 4x), drawn on a 144-px grid and scaled 3x ----

        const int AdaptivePx = 144, AdaptiveScale = 3, EchoStep = 8;

        // Where the runner stands on the 108-px grid: the figures (runner plus two echoes trailing left) are centred
        // in the safe circle every launcher mask keeps (66 of 108 dp: 88 px here), and the floor meets their feet.
        static void AdaptiveLayout(out PixelCanvas runner, out int ox, out int oy)
        {
            runner = PixelRig.Frame(PixelCharacters.All[0], 0).Scaled(2);
            int minX = runner.Width, maxX = -1, minY = runner.Height, maxY = -1;
            for (int y = 0; y < runner.Height; y++)
                for (int x = 0; x < runner.Width; x++)
                    if (runner.Opaque(x, y))
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
            int width = maxX - minX + 1 + 2 * EchoStep, height = maxY - minY + 1;
            ox = AdaptivePx / 2 - width / 2 + 2 * EchoStep - minX;
            oy = AdaptivePx / 2 - height / 2 - minY;
        }

        /// <summary>Adaptive icon background: the sky gradient and the floor, full bleed (launchers crop and move it).</summary>
        public static PixelCanvas AdaptiveIconBackground()
        {
            AdaptiveLayout(out _, out _, out int oy);
            var c = new PixelCanvas(AdaptivePx, AdaptivePx);
            var top = new Color(0.16f, 0.50f, 0.56f);
            var bottom = new Color(0.34f, 0.22f, 0.52f);
            for (int y = 0; y < AdaptivePx; y++)
                for (int x = 0; x < AdaptivePx; x++)
                {
                    float k = y / (float)(AdaptivePx - 1);
                    float band = Mathf.Floor(k * 10f + Bayer(x, y)) / 10f;
                    c[x, y] = Color.Lerp(bottom, top, band);
                }
            c.Rect(0, 0, AdaptivePx - 1, oy - 2, Hex(0x1E2F3A));
            c.Rect(0, oy - 1, AdaptivePx - 1, oy - 1, Hex(0x5E7F8C));
            return c.Scaled(AdaptiveScale);
        }

        /// <summary>Adaptive icon foreground: the runner and its two echoes on transparency, inside the safe zone.</summary>
        public static PixelCanvas AdaptiveIconForeground()
        {
            AdaptiveLayout(out PixelCanvas runner, out int ox, out int oy);
            var c = new PixelCanvas(AdaptivePx, AdaptivePx);
            PixelCanvas ghost = PixelRig.Frame(PixelCharacters.All[0], 0).Hollow(60).Scaled(2);
            var cyan = new Color32(115, 242, 255, 255);
            for (int e = 2; e >= 1; e--) StampOver(c, ghost, ox - e * EchoStep, oy, cyan, 1f - e * 0.3f);
            StampOver(c, runner, ox, oy, new Color32(255, 255, 255, 255), 1f);
            return c.Scaled(AdaptiveScale);
        }

        // Like Stamp, but onto transparency: keeps the source's alpha instead of blending into an opaque background.
        static void StampOver(PixelCanvas dst, PixelCanvas src, int ox, int oy, Color32 tint, float alpha)
        {
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    Color32 s = src[x, y];
                    if (s.a == 0) continue;
                    Color sc = (Color)s * (Color)tint;
                    float a = s.a / 255f * alpha;
                    Color d = dst[x + ox, y + oy];
                    float outA = a + d.a * (1f - a);
                    Color rgb = outA > 0f ? (new Color(sc.r, sc.g, sc.b) * a + new Color(d.r, d.g, d.b) * d.a * (1f - a)) / outA : Color.clear;
                    dst[x + ox, y + oy] = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
        }

        static void Stamp(PixelCanvas dst, PixelCanvas src, int ox, int oy, Color32 tint, float alpha)
        {
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    Color32 s = src[x, y];
                    if (s.a == 0) continue;
                    Color sc = (Color)s * (Color)tint;
                    float a = s.a / 255f * alpha;
                    Color d = dst[x + ox, y + oy];
                    dst[x + ox, y + oy] = Color.Lerp(d, new Color(sc.r, sc.g, sc.b, 1f), a);
                }
        }

        static System.Collections.Generic.Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new System.Collections.Generic.Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        static System.Collections.Generic.Dictionary<char, Color32> Pal(char ch, Color32 c) =>
            new System.Collections.Generic.Dictionary<char, Color32> { [ch] = c };
    }
}
