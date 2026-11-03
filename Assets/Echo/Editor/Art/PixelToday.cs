using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc, phase 3: Today in 32-bit. The existing neon city (Neon Night's far and near layers) gains two
    /// parallax layers in a richer palette with softer shading: a distant hazy skyline behind it and a mid-distance block
    /// in front of its towers, both with soft window lighting (warm windows with a faint halo, part-transparent pixels the
    /// 16-bit layers never use). Still pixel art on the 32 px grid; 512 px wide tiles.
    /// </summary>
    public static class PixelToday
    {
        const int W = 512;

        static void Blend(PixelCanvas c, int x, int y, Color col)
        {
            if (y < 0 || y >= c.Height) return;
            x = ((x % c.Width) + c.Width) % c.Width;
            Color under = c[x, y];
            float a = col.a + under.a * (1f - col.a);
            if (a <= 0f) return;
            Color o = (col * col.a + under * under.a * (1f - col.a)) / a;
            o.a = a;
            c[x, y] = o;
        }

        static int Hash(int x, int y) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u);

        static Color C(uint rgb, float a = 1f)
        {
            Color c = Hex(rgb);
            c.a = a;
            return c;
        }

        /// <summary>A distant skyline, 512 x 176, fading into the haze towards its foot.</summary>
        public static PixelCanvas BackSkyline()
        {
            var c = new PixelCanvas(W, 176);
            int x = 0, i = 0;
            while (x < W)
            {
                int w = 14 + Hash(i, 1) % 22, h = 60 + Hash(i, 2) % 110;
                for (int yy = 0; yy < h; yy++)
                {
                    // Softer shading: a smooth fall-off into the haze at the foot rather than flat bands.
                    float haze = Mathf.Lerp(0.55f, 0.9f, yy / (float)h);
                    for (int xx = x; xx < x + w; xx++)
                    {
                        Color body = Color.Lerp(C(0x4A3E7A), C(0x5E4E90), (xx - x) / (float)w);
                        body.a = haze;
                        Blend(c, xx, yy, body);
                    }
                }
                if (Hash(i, 3) % 3 == 0)   // a spire or an aerial
                    for (int yy = h; yy < h + 10 + Hash(i, 4) % 14; yy++) Blend(c, x + w / 2, yy, C(0x5E4E90, 0.8f));
                for (int wy = 8; wy < h - 6; wy += 6)
                    for (int wx = x + 3; wx < x + w - 2; wx += 4)
                        if (Hash(wx, wy) % 5 == 0) Blend(c, wx, wy, C(0xFFD8A0, 0.55f));
                x += w + 1 + Hash(i, 5) % 4;
                i++;
            }
            return c;
        }

        /// <summary>The mid-distance block, 512 x 136: darker buildings in front of the towers with warm, glowing windows.</summary>
        public static PixelCanvas MidCity()
        {
            var c = new PixelCanvas(W, 136);
            int x = 0, i = 0;
            while (x < W)
            {
                int w = 30 + Hash(i, 11) % 40, h = 46 + Hash(i, 12) % 80;
                Color dark = C(0x241E44), lit = C(0x3A3266);
                for (int yy = 0; yy < h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        // A soft light from the left: three tones blended by position, the 32-bit look.
                        float t = Mathf.Clamp01(1f - (xx - x) / (w * 0.7f));
                        Blend(c, xx, yy, Color.Lerp(dark, lit, t * t));
                    }
                // A roof edge catching the neon.
                for (int xx = x; xx < x + w; xx++) Blend(c, xx, h - 1, C(0x8A70D8, 0.7f));
                if (Hash(i, 13) % 2 == 0)
                {
                    Box(c, x + w / 3, h, x + w / 3 + 6, h + 5, dark);   // a water tank
                    for (int yy = h; yy < h + 9; yy++) Blend(c, x + w - 6, yy, dark);
                }
                // Windows: warm light with a soft halo, some lit, some dark, some with a blue screen glow.
                for (int wy = 6; wy < h - 6; wy += 9)
                    for (int wx = x + 4; wx < x + w - 5; wx += 7)
                    {
                        int k = Hash(wx * 3, wy * 7) % 10;
                        if (k < 4) continue;
                        Color glass = k < 8 ? C(0xFFC864) : C(0x8AD8FF);
                        for (int dy = -1; dy <= 3; dy++)
                            for (int dx = -1; dx <= 3; dx++)
                            {
                                bool pane = dx >= 0 && dx <= 2 && dy >= 0 && dy <= 2;
                                Color g = glass;
                                g.a = pane ? (dy == 2 ? 0.75f : 0.95f) : 0.18f;
                                Blend(c, wx + dx, wy + dy, g);
                            }
                    }
                x += w + 2 + Hash(i, 14) % 6;
                i++;
            }
            return c;
        }

        static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color col)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Blend(c, x, y, col);
        }
    }
}
