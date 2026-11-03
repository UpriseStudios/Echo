using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc, phase 3: the Far Future breaks out of pixel art. Full-resolution art at 64 px per unit, filtered
    /// smoothly: a deep gradient sky, two moons over a distant skyline, chrome towers with neon trim and rim highlights,
    /// a rain-wet street level with the neon reflected in it, a flying car, rain and a neon grid floor. Kept darker and
    /// calmer than the gameplay layer (the doc's readability rule), with small, bright neon accents for the lights and
    /// bloom to pick out. The far and near layers cover the same 16 x 7 and 16 x 3.5 units as the pixel layers.
    /// </summary>
    public static class HdFuture
    {
        public const float Ppu = 64f;
        const int W = 1024;

        static Color C(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        static readonly Color Cyan = C(0x3AF0FF), Magenta = C(0xFF3AD0), Amber = C(0xFFB84A);

        static float Noise(int x, int y) => (((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u) / 1000f;

        /// <summary>The sky: 64 x 512, stretched over 2 x 16 units by smooth filtering (32 px per unit), so it costs nothing.</summary>
        public static PixelCanvas Sky()
        {
            var c = new HdCanvas(64, 512);
            c.Fill((x, y) =>
            {
                float t = y / 511f;
                // Magenta haze on the horizon, deep blue above, near-black at the top.
                Color low = Color.Lerp(C(0x3A1650), C(0x141A44), Mathf.SmoothStep(0f, 1f, t * 2.2f));
                return Color.Lerp(low, C(0x04060F), Mathf.SmoothStep(0.35f, 1f, t));
            });
            return c.ToPixelCanvas();
        }

        /// <summary>
        /// The two moons, 10 x 12 units, fixed in the sky (parallax 0, wider than the 9-unit view so a seam never shows):
        /// a big pale one with soft shading and craters, a small warm one higher up, each in a soft halo.
        /// </summary>
        public static PixelCanvas Moons()
        {
            // Low over the towers and left of centre, clear of the score and the ghost count; dimmer than anything in play.
            var c = new HdCanvas(640, 768);
            c.Glow(170, 470, 140, C(0x6A8ACF), 0.22f);
            c.Disc(170, 470, 40, (u, v) =>
            {
                float light = Mathf.Clamp01(0.75f - 0.45f * u + 0.25f * v);
                return Color.Lerp(C(0x3E4A66), C(0xA8B8D4), light);
            });
            foreach (var (x, y, r) in new[] { (158f, 482f, 7f), (182f, 455f, 5f), (176f, 492f, 3.5f), (150f, 460f, 4f) })
                c.Disc(x, y, r, (u, v) => C(0x6A7894, 0.45f));
            c.Glow(300, 500, 50, Amber, 0.16f);
            c.Disc(300, 500, 12, (u, v) => Color.Lerp(C(0x7A4A2A), C(0xD8B080), Mathf.Clamp01(0.7f - 0.5f * u + 0.3f * v)));
            return c.ToPixelCanvas();
        }

        /// <summary>A distant hazy skyline, 16 x 12 units, drifting slowly behind the chrome towers.</summary>
        public static PixelCanvas Back()
        {
            var c = new HdCanvas(W, 768);

            // The distant skyline: tall, thin towers fading into the haze, with a few far lights.
            for (int i = 0, x = 0; x < W; i++)
            {
                int w = 22 + (int)(Noise(i, 1) * 40), h = 140 + (int)(Noise(i, 2) * 260);
                float x0 = x, x1 = x + w;
                c.Rect(x0, 0, x1, h, (u, v) => Color.Lerp(C(0x161C3E, 0.95f), C(0x0E1230, 0.95f), v));
                if (Noise(i, 3) > 0.5f) c.Rect(x0 + w * 0.45f, h, x0 + w * 0.55f, h + 30 + Noise(i, 4) * 40, C(0x161C3E, 0.95f));
                for (int k = 0; k < 6; k++)
                    if (Noise(i, 10 + k) > 0.55f) c.Glow(x0 + Noise(i, 20 + k) * w, 30 + Noise(i, 30 + k) * (h - 40), 3f, k % 2 == 0 ? Cyan : Magenta, 0.5f);
                x += w + 4 + (int)(Noise(i, 5) * 18);
            }
            // The horizon's haze over their feet.
            for (int y = 0; y < 160; y++)
                for (int x = 0; x < W; x++) c.Add(x, y, C(0x5A2A7A), 0.18f * (1f - y / 160f));
            return c.ToPixelCanvas();
        }

        /// <summary>Chrome towers, 16 x 7 units: brushed-metal shading, a bright rim where the neon catches, trim lines and holograms.</summary>
        public static PixelCanvas Far()
        {
            var c = new HdCanvas(W, 448);
            int i = 0;
            for (float x = 10; x < W; i++)
            {
                float w = 60 + Noise(i, 1) * 70, h = 180 + Noise(i, 2) * 240;
                float x0 = x, x1 = x + w;
                bool taper = Noise(i, 3) > 0.5f;
                c.Rect(x0, 0, x1, h, (u, v) =>
                {
                    // Chrome: dark body, a soft specular band a third across, a bright rim on the lit (left) edge.
                    float spec = Mathf.Exp(-Mathf.Pow((u - 0.3f) / 0.12f, 2f));
                    float rim = Mathf.Exp(-u / 0.035f);
                    Color body = Color.Lerp(C(0x141C34), C(0x26324E), v * 0.6f);
                    body = Color.Lerp(body, C(0x5A6E96), spec * 0.55f);
                    return Color.Lerp(body, C(0x9AD8F0), rim * 0.7f);
                });
                if (taper) c.Rect(x0 + w * 0.2f, h, x1 - w * 0.2f, h + 26, (u, v) => Color.Lerp(C(0x1C2640), C(0x3A4A6E), 1f - Mathf.Abs(u - 0.4f)));
                // Window bands: faint rows, a few lit.
                for (float y = 24; y < h - 16; y += 14)
                    for (float wx = x0 + 8; wx < x1 - 10; wx += 12)
                        if (Noise((int)wx, (int)y) > 0.82f) c.Rect(wx, y, wx + 6, y + 5, C(0xBFE8FF, 0.35f));
                // Neon trim up one edge and across a floor.
                Color trim = i % 3 == 0 ? Magenta : Cyan;
                c.Neon(x1 - 3, 8, x1 - 3, h - 8, 2f, trim, 9f);
                if (i % 2 == 0) c.Neon(x0 + 4, h * 0.62f, x1 - 4, h * 0.62f, 2f, trim, 8f);
                // An aerial with a red light.
                float ax = x0 + w * 0.5f, top = h + (taper ? 26 : 0);
                c.Rect(ax - 1, top, ax + 1, top + 24, C(0x3A4A6E));
                c.Glow(ax, top + 24, 7f, C(0xFF4A4A), 0.8f);
                // Now and then a holographic billboard on the face.
                if (Noise(i, 7) > 0.62f)
                {
                    float by = h * 0.3f, bh = 40, bx0 = x0 + 8, bx1 = x1 - 8;
                    c.Rect(bx0, by, bx1, by + bh, (u, v) => Color.Lerp(C(0x3AF0FF, 0.22f), C(0xFF3AD0, 0.22f), u) * (0.8f + 0.2f * Mathf.Sin(v * 40f)));
                    c.Neon(bx0, by, bx1, by, 1.5f, Cyan, 6f);
                    c.Neon(bx0, by + bh, bx1, by + bh, 1.5f, Cyan, 6f);
                }
                x = x1 + 14 + Noise(i, 8) * 30;
            }
            return c.ToPixelCanvas();
        }

        /// <summary>The street level, 16 x 3.5 units: walkway pillars and rails, neon signs, and the wet ground reflecting them.</summary>
        public static PixelCanvas Near()
        {
            var c = new HdCanvas(W, 224);
            // The wet plaza: a dark band that shines towards the viewer.
            c.Rect(0, 0, W, 44, (u, v) => Color.Lerp(C(0x0E1426), C(0x090C18), v));
            // A raised walkway with its rail along the top.
            c.Rect(0, 120, W, 132, (u, v) => Color.Lerp(C(0x1A2440), C(0x2A3858), v));
            c.Neon(0, 133, W, 133, 1.5f, Cyan, 7f);
            for (int k = 0; k < 16; k++)
            {
                float px = k * 64 + 10;
                // Pillars holding up the walkway, chrome-shaded.
                c.Rect(px, 40, px + 14, 120, (u, v) => Color.Lerp(C(0x101830), C(0x3A4C70), Mathf.Exp(-Mathf.Pow((u - 0.25f) / 0.2f, 2f))));
                for (float rx = px + 20; rx < px + 60; rx += 10) c.Rect(rx, 132, rx + 2, 150, C(0x2A3858));
                c.Rect(px + 16, 150, px + 64, 152, C(0x2A3858));
            }
            // Neon signs over the plaza, each reflected as a soft streak in the wet ground below it.
            foreach (var (x, y, w, col) in new[] { (60f, 70f, 70f, Magenta), (250f, 82f, 44f, Cyan), (420f, 64f, 90f, Amber), (640f, 78f, 60f, Magenta), (860f, 70f, 80f, Cyan) })
            {
                c.Rect(x - 4, y - 6, x + w + 4, y + 16, C(0x0A0E1C, 0.9f));
                c.Neon(x, y, x + w, y, 3f, col, 14f);
                c.Neon(x, y + 10, x + w * 0.6f, y + 10, 2f, col, 10f);
                for (int yy = 0; yy < 44; yy++)
                {
                    float fade = (1f - yy / 44f) * 0.55f;
                    for (float xx = x; xx < x + w; xx += 1f)
                        c.Add((int)xx, yy, col, fade * (0.6f + 0.4f * Mathf.Sin(xx * 0.9f + yy * 1.7f)) * 0.5f);
                }
            }
            // Puddle highlights catching the light.
            for (int k = 0; k < 40; k++)
            {
                float x = Noise(k, 1) * W, y = 4 + Noise(k, 2) * 34;
                c.Rect(x, y, x + 10 + Noise(k, 3) * 30, y + 1.5f, C(0x9ADFFF, 0.25f));
            }
            return c.ToPixelCanvas();
        }

        /// <summary>A flying car, 192 x 56: a sleek gradient body, a lit cockpit, a headlamp and a tail streak.</summary>
        public static PixelCanvas FlyingCar()
        {
            var c = new HdCanvas(192, 56);
            c.Neon(4, 26, 70, 26, 3f, Magenta, 10f);   // the tail streak
            c.Rect(56, 18, 170, 36, (u, v) => Color.Lerp(C(0x1A2440), C(0x7A90B8), Mathf.Exp(-Mathf.Pow((v - 0.7f) / 0.25f, 2f))));
            c.Disc(170, 27, 9, (u, v) => C(0x2A3858));
            c.Rect(100, 34, 150, 44, (u, v) => Color.Lerp(C(0x3AF0FF, 0.7f), C(0x8AF8FF, 0.9f), v));
            c.Neon(60, 18, 168, 18, 1.5f, Cyan, 6f);
            c.Glow(178, 26, 12f, C(0xFFF4D0), 0.9f);   // the headlamp
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

        /// <summary>A neon grid floor tile, 128 x 128 (1 unit at 128 px per unit): dark glass with glowing lines on two edges.</summary>
        public static PixelCanvas GridFloor()
        {
            var c = new HdCanvas(128, 128);
            c.Fill((x, y) => Color.Lerp(C(0x080C1E), C(0x0E1430), y / 127f));
            for (int k = 0; k < 128; k++)
                for (int d = 0; d < 10; d++)
                {
                    float a = d == 0 ? 0.95f : 0.4f * (1f - d / 10f) * (1f - d / 10f);
                    c.Add(k, 127 - d, Cyan, a);   // the line along the top
                    c.Add(d, k, Cyan, a * 0.8f);  // and down the left
                }
            return c.ToPixelCanvas();
        }
    }
}
