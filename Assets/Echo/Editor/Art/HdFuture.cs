using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc: the Far Future breaks out of pixel art. Full-resolution art at 64 px per unit, filtered smoothly.
    /// Art fix phase 2 makes it clearly HD next to Today: five layers of depth (stars and a shaded moon with a ringed
    /// planet, a far skyline, mid-distance towers, near chrome towers, the wet street), each hazier with distance so the
    /// far towers fade into the sky; and every light in it (neon trims, lit windows, signs, holograms, aerial lights, the
    /// moon's limb) also drawn on an emissive companion layer, shown through the HDR glow shader so the bloom catches it.
    /// The lit layers stay darker and calmer than the gameplay. The far and near layers cover the same 16 x 7 and 16 x 3.5
    /// units as the pixel layers.
    /// </summary>
    public static class HdFuture
    {
        public const float Ppu = 64f;
        const int W = 1024;

        static Color C(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        static readonly Color Cyan = C(0x3AF0FF), Magenta = C(0xFF3AD0), Amber = C(0xFFB84A), Haze = C(0x3A2A66);

        static float Noise(int x, int y) => (((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u) / 1000f;

        // Atmospheric haze: further layers (amount) and lower down (towards the horizon's glow) take more of the haze colour.
        static Color Hazed(Color c, float amount, float v) => Color.Lerp(c, Haze, Mathf.Clamp01(amount * (1.2f - 0.6f * v)));

        /// <summary>The sky: 64 x 512, stretched over 2 x 16 units by smooth filtering, so it costs nothing.</summary>
        public static PixelCanvas Sky()
        {
            var c = new HdCanvas(64, 512);
            c.Fill((x, y) =>
            {
                float t = y / 511f;
                // A magenta glow on the horizon, deep blue above, near-black at the top.
                Color low = Color.Lerp(C(0x5A2470), C(0x1A1E50), Mathf.SmoothStep(0f, 1f, t * 2.2f));
                return Color.Lerp(low, C(0x04060F), Mathf.SmoothStep(0.35f, 1f, t));
            });
            return c.ToPixelCanvas();
        }

        // ---- the sky's bodies: stars, a shaded moon and a ringed planet (10 x 12 units, fixed in the sky) ----

        const int MoonX = 170, MoonY = 470, MoonR = 44, PlanetX = 470, PlanetY = 560, PlanetR = 30;

        /// <summary>Stars, a detailed moon (limb darkening, a soft terminator, dark seas, rimmed craters) and a banded planet with rings.</summary>
        public static PixelCanvas Moons()
        {
            var c = new HdCanvas(640, 768);
            for (int k = 0; k < 140; k++)
            {
                float x = Noise(k, 1) * 640, y = 330 + Noise(k, 2) * 430, b = 0.25f + 0.6f * Noise(k, 3);
                c.Blend((int)x, (int)y, new Color(0.85f, 0.9f, 1f, b));
                if (b > 0.75f) { c.Blend((int)x + 1, (int)y, new Color(0.85f, 0.9f, 1f, b * 0.4f)); c.Blend((int)x, (int)y + 1, new Color(0.85f, 0.9f, 1f, b * 0.4f)); }
            }
            Moon(c);
            Planet(c);
            return c.ToPixelCanvas();
        }

        static void Moon(HdCanvas c)
        {
            Vector2 light = new Vector2(-0.55f, 0.35f).normalized;
            c.Disc(MoonX, MoonY, MoonR, (u, v) =>
            {
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
                float lambert = Mathf.Clamp01(u * light.x + v * light.y + z * 0.55f);
                float limb = Mathf.Pow(z, 0.35f);
                // Dark seas: a few soft blotches.
                float sea = 0f;
                foreach (var (sx, sy, sr) in new[] { (-0.25f, 0.2f, 0.32f), (0.2f, -0.15f, 0.24f), (0.05f, 0.45f, 0.18f) })
                    sea = Mathf.Max(sea, Mathf.Clamp01(1f - ((u - sx) * (u - sx) + (v - sy) * (v - sy)) / (sr * sr)));
                // Craters: a dark floor and a lit rim on the side facing the light.
                float crater = 0f;
                for (int k = 0; k < 14; k++)
                {
                    float cx = Noise(k, 41) * 1.6f - 0.8f, cy = Noise(k, 42) * 1.6f - 0.8f, cr = 0.05f + 0.1f * Noise(k, 43);
                    float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) / cr;
                    if (d < 1f) crater -= 0.12f * (1f - d);
                    else if (d < 1.25f) crater += 0.1f * Mathf.Clamp01((u - cx) * -light.x + (v - cy) * -light.y) * (1.25f - d) * 4f;
                }
                Color surface = Color.Lerp(C(0xC8D0E0), C(0x7A8296), sea * 0.7f);
                float lit = Mathf.SmoothStep(0.02f, 0.55f, lambert) * limb + crater;
                return Color.Lerp(C(0x1A1E30), surface, Mathf.Clamp01(lit + 0.06f));
            });
        }

        static void Planet(HdCanvas c)
        {
            // The far half of the rings, behind the planet.
            Ring(c, back: true);
            c.Disc(PlanetX, PlanetY, PlanetR, (u, v) =>
            {
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
                float band = 0.5f + 0.5f * Mathf.Sin(v * 9f + Mathf.Sin(u * 3f) * 0.6f);
                Color body = Color.Lerp(C(0xC86A4A), C(0xF2B07A), band);
                float lit = Mathf.Clamp01(-0.6f * u + 0.3f * v + z * 0.7f);
                return Color.Lerp(C(0x2A1420), body, Mathf.SmoothStep(0f, 1f, lit));
            });
            Ring(c, back: false);
        }

        // Tilted rings, as an ellipse of faint bands; the far half drawn before the planet, the near half after.
        static void Ring(HdCanvas c, bool back)
        {
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.004f)
            {
                bool far = Mathf.Sin(a) > 0f;
                if (far != back) continue;
                for (float r = 1.45f; r < 2.2f; r += 0.04f)
                {
                    float x = PlanetX + Mathf.Cos(a) * PlanetR * r, y = PlanetY + Mathf.Sin(a) * PlanetR * r * 0.28f;
                    float band = 0.35f + 0.25f * Mathf.Sin(r * 22f);
                    c.Blend(Mathf.RoundToInt(x - 6f * Mathf.Sin(a)), Mathf.RoundToInt(y), new Color(0.95f, 0.82f, 0.68f, band * 0.6f));
                }
            }
        }

        /// <summary>The moon's and the planet's glow (their emissive companion): a halo and the moon's bright limb.</summary>
        public static PixelCanvas MoonsGlow()
        {
            var c = new HdCanvas(640, 768);
            c.Glow(MoonX, MoonY, MoonR * 3.2f, C(0x6A8ACF), 0.32f);
            c.Glow(PlanetX, PlanetY, PlanetR * 3f, C(0xFF9A6A), 0.2f);
            Vector2 light = new Vector2(-0.55f, 0.35f).normalized;
            c.Disc(MoonX, MoonY, MoonR, (u, v) =>
            {
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
                float edge = Mathf.Clamp01((u * light.x + v * light.y) - 0.2f) * Mathf.Clamp01(1f - z * 1.6f);
                return new Color(0.85f, 0.92f, 1f, edge);
            });
            return c.ToPixelCanvas();
        }

        // ---- the city in five depths ----

        /// <summary>A distant skyline, 16 x 12 units, deep in the haze, barely darker than the sky behind it.</summary>
        public static PixelCanvas Back()
        {
            var c = new HdCanvas(W, 768);
            for (int i = 0, x = 0; x < W; i++)
            {
                int w = 22 + (int)(Noise(i, 1) * 40), h = 140 + (int)(Noise(i, 2) * 300);
                float x0 = x, x1 = x + w;
                c.Rect(x0, 0, x1, h, (u, v) => Hazed(C(0x161C3E), 0.75f, v * h / 400f));
                if (Noise(i, 3) > 0.5f) c.Rect(x0 + w * 0.45f, h, x0 + w * 0.55f, h + 30 + Noise(i, 4) * 40, Hazed(C(0x161C3E), 0.75f, 1f));
                x += w + 4 + (int)(Noise(i, 5) * 18);
            }
            // The horizon's haze glowing over their feet.
            for (int y = 0; y < 200; y++)
                for (int x = 0; x < W; x++) c.Add(x, y, C(0x6A2A8A), 0.22f * (1f - y / 200f));
            return c.ToPixelCanvas();
        }

        /// <summary>Mid-distance towers, 16 x 9 units: hazy, softly shaded, a scatter of dim windows.</summary>
        public static PixelCanvas MidFar()
        {
            var c = new HdCanvas(W, 576);
            for (int i = 0, x = 6; x < W; i++)
            {
                int w = 40 + (int)(Noise(i, 31) * 50), h = 150 + (int)(Noise(i, 32) * 330);
                float x0 = x, x1 = x + w;
                c.Rect(x0, 0, x1, h, (u, v) =>
                {
                    Color body = Color.Lerp(C(0x1A2240), C(0x2A3656), Mathf.Exp(-Mathf.Pow((u - 0.3f) / 0.18f, 2f)) * 0.7f);
                    return Hazed(body, 0.45f, v * h / 480f);
                });
                for (float y = 20; y < h - 12; y += 12)
                    for (float wx = x0 + 6; wx < x1 - 8; wx += 10)
                        if (Noise((int)wx + 3, (int)y) > 0.9f) c.Rect(wx, y, wx + 4, y + 4, Hazed(C(0xBFE0FF), 0.45f, 0.5f));
                x = (int)(x1 + 10 + Noise(i, 33) * 26);
            }
            return c.ToPixelCanvas();
        }

        // The near chrome towers and their lights, drawn together so the glow lines up with the art.
        static HdCanvas s_far, s_farGlow;

        static void FarLayers()
        {
            if (s_far != null) return;
            var c = new HdCanvas(W, 448);
            var g = new HdCanvas(W, 448);
            int i = 0;
            for (float x = 10; x < W; i++)
            {
                float w = 60 + Noise(i, 1) * 70, h = 180 + Noise(i, 2) * 240;
                float x0 = x, x1 = x + w;
                bool taper = Noise(i, 3) > 0.5f;
                c.Rect(x0, 0, x1, h, (u, v) =>
                {
                    // Chrome: dark body, a soft specular band, a bright rim on the lit edge, a touch of haze at the foot.
                    float spec = Mathf.Exp(-Mathf.Pow((u - 0.3f) / 0.12f, 2f));
                    float rim = Mathf.Exp(-u / 0.035f);
                    Color body = Color.Lerp(C(0x141C34), C(0x26324E), v * 0.6f);
                    body = Color.Lerp(body, C(0x5A6E96), spec * 0.55f);
                    body = Color.Lerp(body, C(0x9AD8F0), rim * 0.7f);
                    return Hazed(body, 0.18f, v);
                });
                if (taper) c.Rect(x0 + w * 0.2f, h, x1 - w * 0.2f, h + 26, (u, v) => Color.Lerp(C(0x1C2640), C(0x3A4A6E), 1f - Mathf.Abs(u - 0.4f)));
                // Windows: faint panes, and the lit ones drawn again on the glow layer.
                for (float y = 24; y < h - 16; y += 14)
                    for (float wx = x0 + 8; wx < x1 - 10; wx += 12)
                    {
                        float n = Noise((int)wx, (int)y);
                        if (n > 0.86f)
                        {
                            Color lit = n > 0.95f ? C(0x9AE8FF) : C(0xFFD49A);
                            c.Rect(wx, y, wx + 6, y + 5, Color.Lerp(lit, C(0x26324E), 0.4f));
                            g.Rect(wx, y, wx + 6, y + 5, new Color(lit.r, lit.g, lit.b, 0.75f));
                        }
                        else c.Rect(wx, y, wx + 6, y + 5, C(0x3A4A66, 0.4f));
                    }
                // Neon trim up one edge and across a floor: a dim tube on the art, the light itself on the glow layer.
                Color trim = i % 3 == 0 ? Magenta : Cyan;
                c.Neon(x1 - 3, 8, x1 - 3, h - 8, 2f, Color.Lerp(trim, C(0x141C34), 0.5f), 0f);
                g.Neon(x1 - 3, 8, x1 - 3, h - 8, 2f, trim, 10f);
                if (i % 2 == 0)
                {
                    c.Neon(x0 + 4, h * 0.62f, x1 - 4, h * 0.62f, 2f, Color.Lerp(trim, C(0x141C34), 0.5f), 0f);
                    g.Neon(x0 + 4, h * 0.62f, x1 - 4, h * 0.62f, 2f, trim, 9f);
                }
                // An aerial with a red light.
                float ax = x0 + w * 0.5f, top = h + (taper ? 26 : 0);
                c.Rect(ax - 1, top, ax + 1, top + 24, C(0x3A4A6E));
                g.Glow(ax, top + 24, 9f, C(0xFF4A4A), 1f);
                x = x1 + 14 + Noise(i, 8) * 30;
            }
            s_far = c;
            s_farGlow = g;
        }

        /// <summary>Chrome towers, 16 x 7 units: brushed-metal shading, a bright rim, dim neon tubes and panes.</summary>
        public static PixelCanvas Far()
        {
            FarLayers();
            return s_far.ToPixelCanvas();
        }

        /// <summary>The chrome towers' lights alone (their emissive companion): neon trims, lit windows and aerial lights.</summary>
        public static PixelCanvas FarGlow()
        {
            FarLayers();
            return s_farGlow.ToPixelCanvas();
        }

        /// <summary>Where the holographic billboards sit on the far layer, in its pixels from the bottom-left.</summary>
        public static readonly Vector2Int HologramAt = new Vector2Int(560, 300);

        /// <summary>A holographic billboard, frame <paramref name="f"/> of 4: a cyan and magenta panel with scanlines, flickering through adverts.</summary>
        public static PixelCanvas Hologram(int f)
        {
            var c = new HdCanvas(112, 64);
            float flicker = f == 2 ? 0.55f : 1f;
            c.Rect(0, 0, 111, 63, (u, v) =>
            {
                Color panel = Color.Lerp(Cyan, Magenta, u) * (0.35f + 0.1f * Mathf.Sin(v * 60f));
                panel.a = 0.5f * flicker * (((int)(v * 64)) % 3 == 0 ? 0.55f : 1f);
                return panel;
            });
            // Each advert a different shape: a bottle, a figure, a planet, a logo.
            Color ink = new Color(0.6f, 0.95f, 1f, 0.55f * flicker);   // through the glow material: bright, never blown out
            switch (f)
            {
                case 0: c.Rect(16, 10, 30, 50, ink); c.Rect(20, 50, 26, 56, ink); c.Rect(44, 34, 96, 40, ink); c.Rect(44, 22, 80, 26, ink); break;
                case 1: c.Disc(26, 44, 8, (u, v) => ink); c.Rect(20, 12, 32, 36, ink); c.Rect(46, 34, 98, 40, ink); break;
                case 2: c.Disc(30, 32, 18, (u, v) => ink); c.Rect(58, 30, 98, 36, ink); break;
                default: c.Neon(14, 14, 50, 50, 4f, Color.white, 0f); c.Neon(14, 50, 50, 14, 4f, Color.white, 0f); c.Rect(60, 28, 98, 36, ink); break;
            }
            c.Neon(0, 0, 111, 0, 1.5f, Cyan, 0f);
            c.Neon(0, 63, 111, 63, 1.5f, Cyan, 0f);
            return c.ToPixelCanvas();
        }

        // The street level and its lights, drawn together.
        static HdCanvas s_near, s_nearGlow;

        static void NearLayers()
        {
            if (s_near != null) return;
            var c = new HdCanvas(W, 224);
            var g = new HdCanvas(W, 224);
            c.Rect(0, 0, W, 44, (u, v) => Color.Lerp(C(0x0E1426), C(0x090C18), v));
            c.Rect(0, 120, W, 132, (u, v) => Color.Lerp(C(0x1A2440), C(0x2A3858), v));
            c.Neon(0, 133, W, 133, 1.5f, Color.Lerp(Cyan, C(0x1A2440), 0.5f), 0f);
            g.Neon(0, 133, W, 133, 1.5f, Cyan, 7f);
            for (int k = 0; k < 16; k++)
            {
                float px = k * 64 + 10;
                c.Rect(px, 40, px + 14, 120, (u, v) => Color.Lerp(C(0x101830), C(0x3A4C70), Mathf.Exp(-Mathf.Pow((u - 0.25f) / 0.2f, 2f))));
                for (float rx = px + 20; rx < px + 60; rx += 10) c.Rect(rx, 132, rx + 2, 150, C(0x2A3858));
                c.Rect(px + 16, 150, px + 64, 152, C(0x2A3858));
            }
            // Neon signs over the plaza: a dark box and a dim tube on the art; the light, and its streak in the wet ground, glowing.
            foreach (var (x, y, w, col) in new[] { (60f, 70f, 70f, Magenta), (250f, 82f, 44f, Cyan), (420f, 64f, 90f, Amber), (640f, 78f, 60f, Magenta), (860f, 70f, 80f, Cyan) })
            {
                c.Rect(x - 4, y - 6, x + w + 4, y + 16, C(0x0A0E1C, 0.9f));
                c.Neon(x, y, x + w, y, 3f, Color.Lerp(col, C(0x0A0E1C), 0.5f), 0f);
                g.Neon(x, y, x + w, y, 3f, col, 16f);
                g.Neon(x, y + 10, x + w * 0.6f, y + 10, 2f, col, 10f);
                for (int yy = 0; yy < 44; yy++)
                {
                    float fade = (1f - yy / 44f) * 0.4f;
                    for (float xx = x; xx < x + w; xx += 1f)
                        g.Add((int)xx, yy, col, fade * (0.6f + 0.4f * Mathf.Sin(xx * 0.9f + yy * 1.7f)) * 0.5f);
                }
            }
            for (int k = 0; k < 40; k++)
            {
                float x = Noise(k, 1) * W, y = 4 + Noise(k, 2) * 34;
                c.Rect(x, y, x + 10 + Noise(k, 3) * 30, y + 1.5f, C(0x9ADFFF, 0.2f));
            }
            s_near = c;
            s_nearGlow = g;
        }

        /// <summary>The street level, 16 x 3.5 units: walkway pillars and rails, sign boxes, the wet plaza.</summary>
        public static PixelCanvas Near()
        {
            NearLayers();
            return s_near.ToPixelCanvas();
        }

        /// <summary>The street's lights alone (its emissive companion): the rail's neon, the signs and their streaks in the wet ground.</summary>
        public static PixelCanvas NearGlow()
        {
            NearLayers();
            return s_nearGlow.ToPixelCanvas();
        }

        // ---- life: flying cars with light trails, searchlight beams, mist ----

        /// <summary>
        /// A flying car with its light trail, facing right: a sleek dark body, a white headlamp, red tail lamps and a long
        /// trail fading behind (drawn through the glow shader, so the lamps and trail bloom). <paramref name="size"/> is its
        /// length in pixels: 48, 72 and 112 for the far, middle and near lanes.
        /// </summary>
        public static PixelCanvas TrailCar(int k, int size)
        {
            int h = size / 4;
            var c = new HdCanvas(size * 3, h);
            float cx = size * 2.4f, cy = h * 0.5f;
            Color trail = k % 2 == 0 ? C(0xFF4A6A) : C(0x4AE8FF);
            for (int x = 0; x < (int)(cx - size * 0.4f); x++)
            {
                float t = x / (cx - size * 0.4f);
                c.Blend(x, (int)cy, new Color(trail.r, trail.g, trail.b, 0.55f * t * t));
                c.Blend(x, (int)cy - 1, new Color(trail.r, trail.g, trail.b, 0.25f * t * t));
            }
            c.Rect(cx - size * 0.4f, cy - h * 0.22f, cx + size * 0.5f, cy + h * 0.2f, (u, v) => Color.Lerp(C(0x080B16), C(0x26304A), Mathf.Exp(-Mathf.Pow((v - 0.75f) / 0.25f, 2f))));
            c.Rect(cx - size * 0.05f, cy + h * 0.18f, cx + size * 0.32f, cy + h * 0.4f, C(0x3A7A9A, 0.7f));
            c.Glow(cx + size * 0.5f, cy, h * 0.8f, C(0xFFF4D0), 0.9f);
            c.Glow(cx - size * 0.4f, cy, h * 0.5f, C(0xFF3A3A), 0.8f);
            return c.ToPixelCanvas();
        }

        /// <summary>A searchlight beam, 64 x 512: a soft cone, narrow at its foot and fading towards the sky (tinted, through the glow shader).</summary>
        public static PixelCanvas Beam()
        {
            var c = new HdCanvas(64, 512);
            c.Fill((x, y) =>
            {
                float t = y / 511f, half = Mathf.Lerp(2f, 31f, t);
                float d = Mathf.Abs(x - 31.5f) / half;
                float a = Mathf.Clamp01(1f - d) * (1f - t) * Mathf.Clamp01(t * 12f);
                return new Color(1f, 1f, 1f, a * a);
            });
            return c.ToPixelCanvas();
        }

        /// <summary>A wisp of mist, 128 x 48: a soft cloud, white for its tint.</summary>
        public static PixelCanvas Mist()
        {
            var c = new HdCanvas(128, 48);
            c.Fill((x, y) =>
            {
                float u = (x - 63.5f) / 63.5f, v = (y - 23.5f) / 23.5f;
                float d = Mathf.Sqrt(u * u + v * v * 1.4f);
                float n = 0.7f + 0.3f * Mathf.Sin(x * 0.21f + y * 0.37f) * Mathf.Sin(x * 0.07f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * n * 0.8f);
            });
            return c.ToPixelCanvas();
        }

        /// <summary>A rain streak, 3 x 48, brightest in the middle (tinted by the effect's colours).</summary>
        public static PixelCanvas Rain()
        {
            var c = new HdCanvas(3, 48);
            c.Fill((x, y) =>
            {
                float v = Mathf.Sin(Mathf.PI * y / 47f);
                return new Color(1f, 1f, 1f, (x == 1 ? 0.9f : 0.35f) * v);
            });
            return c.ToPixelCanvas();
        }

        /// <summary>A neon grid floor tile, 128 x 128 (1 unit at 128 px per unit): wet glass, part-transparent, with glowing lines.</summary>
        public static PixelCanvas GridFloor()
        {
            var c = new HdCanvas(128, 128);
            c.Fill((x, y) => { Color g = Color.Lerp(C(0x080C1E), C(0x0E1430), y / 127f); g.a = 0.38f; return g; });
            for (int k = 0; k < 128; k++)
                for (int d = 0; d < 10; d++)
                {
                    float a = d == 0 ? 0.95f : 0.4f * (1f - d / 10f) * (1f - d / 10f);
                    c.Add(k, 127 - d, Cyan, a);
                    c.Add(d, k, Cyan, a * 0.8f);
                }
            return c.ToPixelCanvas();
        }

        /// <summary>A rain splash on the floor, 32 x 32: a soft ring (scaled flat as it opens), white for its tint.</summary>
        public static PixelCanvas Splash()
        {
            var c = new HdCanvas(32, 32);
            c.Fill((x, y) =>
            {
                float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.2f);
                return new Color(1f, 1f, 1f, ring);
            });
            return c.ToPixelCanvas();
        }

        /// <summary>
        /// Art pass phase 7: the Far Future as one picture, 576 x 1024 (9 x 16 units at 64 px per unit, the floor line 293 px
        /// up), every layer stacked as the game stacks them and their lights added on, bright as they look under bloom: the
        /// last rift breaks this into shards that fly apart to show the 3D city behind.
        /// </summary>
        public static PixelCanvas Shatter()
        {
            const int w = 576, h = 1024, floor = 293;
            var c = new HdCanvas(w, h);
            c.Fill((x, y) =>
            {
                if (y < floor) return C(0x070A18);
                float t = (y - floor) / 1023f;
                Color low = Color.Lerp(C(0x5A2470), C(0x1A1E50), Mathf.SmoothStep(0f, 1f, t * 2.2f));
                return Color.Lerp(low, C(0x04060F), Mathf.SmoothStep(0.35f, 1f, t));
            });
            void Layer(PixelCanvas layer, float lights)
            {
                for (int y = 0; y < layer.Height && floor + y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color p = layer[x % layer.Width, y];
                        if (p.a <= 0f) continue;
                        if (lights > 0f) c.Add(x, floor + y, new Color(p.r, p.g, p.b), p.a * lights);
                        else c.Blend(x, floor + y, p);
                    }
            }
            Layer(Moons(), 0f);
            Layer(MoonsGlow(), 1.4f);
            Layer(Back(), 0f);
            Layer(MidFar(), 0f);
            Layer(Far(), 0f);
            Layer(FarGlow(), 1.6f);
            Layer(Near(), 0f);
            Layer(NearGlow(), 1.6f);
            return c.ToPixelCanvas();
        }

        /// <summary>The reflection's fade, 4 x 128: clear at the top to opaque at the bottom, white for its tint.</summary>
        public static PixelCanvas Fade()
        {
            var c = new HdCanvas(4, 128);
            c.Fill((x, y) => new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, 1f - y / 127f)));
            return c.ToPixelCanvas();
        }
    }
}
