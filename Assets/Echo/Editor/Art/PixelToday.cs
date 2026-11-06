using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc (art pass phase 4): Today in 32-bit, a new daytime-to-dusk city (nothing from the neon world).
    /// Smooth gradient skies for day and dusk (the dusk one fades in across the era); five parallax layers: a hazy distant
    /// skyline, glass and concrete towers with a crane and a mast, a flyover with traffic driving both ways and a digital
    /// billboard cycling its adverts, a high street of shops and terraces, and the same street's windows and street lamps
    /// lit, fading in as dusk falls. Its own floor (pavement and a kerb over asphalt) and hazards on the same colliders:
    /// traffic cones, scaffolding with anti-climb spikes for the hanging ceiling, open roadworks with a blinking barrier
    /// for the gaps, and pound coins. Soft shading and part-transparent pixels, still on the 32 px grid.
    /// </summary>
    public static class PixelToday
    {
        const int W = 512;

        static Color C(uint rgb, float a = 1f)
        {
            Color c = Hex(rgb);
            c.a = a;
            return c;
        }

        static int Hash(int x, int y) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) % 1000u);

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

        static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color col)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Blend(c, x, y, col);
        }

        static void Disc(PixelCanvas c, float cx, float cy, float r, Color col)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Blend(c, x, y, col);
        }

        static PixelCanvas Gradient(params (float at, uint rgb)[] stops)
        {
            var c = new PixelCanvas(64, 512);
            for (int y = 0; y < 512; y++)
            {
                float t = y / 511f;
                int k = 0;
                while (k < stops.Length - 2 && t > stops[k + 1].at) k++;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stops[k].at, stops[k + 1].at, t));
                Color32 col = Color.Lerp(C(stops[k].rgb), C(stops[k + 1].rgb), u);
                for (int x = 0; x < 64; x++) c[x, y] = col;
            }
            return c;
        }

        public const uint Sky = 0x8CC4F0;

        /// <summary>The day sky: a smooth gradient, pale at the horizon to deep blue overhead (64 x 512, 2 x 16 units).</summary>
        public static PixelCanvas SkyDay() => Gradient((0f, 0xD4ECF8), (0.3f, 0x9ACCF2), (0.7f, 0x5E9EE6), (1f, 0x3A78D4));

        /// <summary>The dusk sky, faded in over the day across the era: orange at the horizon through rose to indigo.</summary>
        public static PixelCanvas SkyDusk() => Gradient((0f, 0xFFB46A), (0.22f, 0xF08A6A), (0.45f, 0xB46A8C), (0.72f, 0x4A3E7A), (1f, 0x1E2252));

        /// <summary>A hazy distant skyline, 512 x 200, paler towards its foot.</summary>
        public static PixelCanvas Distant()
        {
            var c = new PixelCanvas(W, 200);
            for (int i = 0, x = 0; x < W; i++)
            {
                int w = 12 + Hash(i, 1) % 26, h = 50 + Hash(i, 2) % 130;
                for (int y = 0; y < h; y++)
                    for (int xx = x; xx < x + w; xx++) Blend(c, xx, y, C(0x8AA6C8, Mathf.Lerp(0.5f, 0.78f, y / (float)h)));
                if (Hash(i, 3) % 4 == 0)
                    for (int y = h; y < h + 14; y++) Blend(c, x + w / 2, y, C(0x8AA6C8, 0.7f));
                x += w + 2 + Hash(i, 4) % 5;
            }
            return c;
        }

        /// <summary>Glass and concrete towers, 512 x 224: soft vertical shading, a lit left edge, window grids with a few lit, rooftops, a crane and a mast.</summary>
        public static PixelCanvas Far()
        {
            var c = new PixelCanvas(W, 224);
            uint[] bodies = { 0x6A8AAA, 0xB8B4AC, 0xA8705A, 0x3E5470, 0x8A9AAA, 0xC8BCA4 };
            for (int i = 0, x = 4; x < W; i++)
            {
                int w = 28 + Hash(i, 11) % 36, h = 70 + Hash(i, 12) % 130;
                Color body = C(bodies[Hash(i, 13) % bodies.Length]);
                bool glass = Hash(i, 14) % 3 == 0;
                for (int y = 0; y < h; y++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        float lit = xx - x < 3 ? 1.18f : xx - x > w - 6 ? 0.8f : 1f;
                        Color col = Color.Lerp(body * 0.86f, body * 1.08f, y / (float)h) * lit;
                        col.a = 1f;
                        // Glass towers catch a diagonal streak of sky.
                        if (glass && ((xx - x) + y / 2) % 23 < 3) col = Color.Lerp(col, C(0xD8ECF8), 0.45f);
                        c[xx % W, y] = col;
                    }
                // Windows in a grid; a few already lit.
                for (int y = 8; y < h - 6; y += 7)
                    for (int xx = x + 4; xx < x + w - 5; xx += 6)
                    {
                        bool on = Hash(xx, y) % 9 == 0;
                        Box(c, xx, y, xx + 3, y + 3, on ? C(0xFFD890) : glass ? C(0x9AC4E4, 0.8f) : C(0x3A4458, 0.85f));
                    }
                Box(c, x, h, x + w - 1, h + 1, C(0x5A5E66));                       // a parapet
                if (Hash(i, 15) % 3 == 0) Box(c, x + w / 3, h + 2, x + w / 3 + 7, h + 8, C(0x6A6E76));   // a plant room
                if (Hash(i, 16) % 4 == 0)
                {
                    for (int y = h + 2; y < h + 22; y++) Blend(c, x + w - 8, y, C(0x6A6E76));     // an aerial
                    Blend(c, x + w - 8, h + 22, C(0xFF3A3A));
                }
                x += w + 3 + Hash(i, 17) % 8;
            }
            // A tower crane, yellow lattice, its jib across the sky.
            int cx = 380;
            for (int y = 0; y < 200; y++)
            {
                Blend(c, cx, y, C(0xF0C030));
                Blend(c, cx + 5, y, C(0xF0C030));
                if (y % 6 == 0) Box(c, cx, y, cx + 5, y, C(0xD8A820));
            }
            for (int x = cx - 60; x < cx + 30; x++)
            {
                Blend(c, x, 196, C(0xF0C030));
                Blend(c, x, 200, C(0xF0C030));
                if (x % 6 == 0) Box(c, x, 196, x, 200, C(0xD8A820));
            }
            Box(c, cx + 18, 186, cx + 28, 195, C(0x5A5E66));                           // its counterweight
            for (int y = 150; y < 196; y++) Blend(c, cx - 40, y, C(0x3A3E46, 0.8f));       // the hook line
            // A radio mast with red lights.
            for (int y = 0; y < 220; y++) Blend(c, 60, y, C(0x8A8E96));
            foreach (int y in new[] { 120, 170, 219 }) Blend(c, 60, y, C(0xFF3A3A));
            return c;
        }

        // ---- the flyover: deck, pillars, a gantry sign and the billboard's pole (its adverts are frames) ----

        /// <summary>Where the digital billboard's screen sits, in the mid layer's pixels from its bottom-left.</summary>
        public static readonly Vector2Int BillboardAt = new Vector2Int(420, 112);

        /// <summary>The top of the flyover's deck, in pixels: the traffic drives on it.</summary>
        public const int DeckTop = 82;

        public static PixelCanvas Mid()
        {
            var c = new PixelCanvas(W, 150);
            for (int x = 0; x < W; x++)
            {
                for (int y = 70; y < DeckTop; y++) c[x, y] = y < 72 ? C(0x5A5E66) : y > DeckTop - 3 ? C(0xB0B0B0) : C(0x9A9C9E);
                Box(c, x, DeckTop, x, DeckTop + 3, x % 4 == 0 ? C(0x7A7E86) : C(0xC8CACC));   // the crash barrier
            }
            for (int x = 30; x < W; x += 96)
                for (int y = 0; y < 70; y++)
                    for (int xx = x; xx < x + 12; xx++) c[xx % W, y] = xx - x < 3 ? C(0xB8B8B8) : C(0x8E9092);
            // A green motorway sign on a gantry.
            Box(c, 196, DeckTop, 197, DeckTop + 30, C(0x6A6E76));
            Box(c, 252, DeckTop, 253, DeckTop + 30, C(0x6A6E76));
            Box(c, 194, DeckTop + 30, 256, DeckTop + 31, C(0x6A6E76));
            Box(c, 200, DeckTop + 14, 250, DeckTop + 29, C(0x1E7A44));
            Box(c, 200, DeckTop + 14, 250, DeckTop + 14, C(0xFFFFFF));
            foreach (var (x, w) in new[] { (204, 18), (226, 10), (204, 14), (222, 20) }) Box(c, x, DeckTop + 18 + (w % 3) * 3, x + w, DeckTop + 19 + (w % 3) * 3, C(0xFFFFFF));
            Box(c, 240, DeckTop + 18, 246, DeckTop + 24, C(0xFFFFFF));
            // The billboard's pole.
            Box(c, BillboardAt.x - 1, 0, BillboardAt.x + 1, BillboardAt.y - 12, C(0x6A6E76));
            return c;
        }

        /// <summary>The digital billboard, frame <paramref name="f"/> of 4: its adverts in turn (a drink, a phone, a holiday, a sale).</summary>
        public static PixelCanvas Billboard(int f)
        {
            var c = new PixelCanvas(44, 26);
            Box(c, 0, 0, 43, 25, C(0x2A2E36));
            Color[] backs = { C(0xE83A4A), C(0x3A6AE8), C(0x3AB8D8), C(0xF0C030) };
            Box(c, 2, 2, 41, 23, backs[f]);
            switch (f)
            {
                case 0: Box(c, 8, 5, 13, 19, C(0xF4F4F4)); Box(c, 9, 19, 12, 21, C(0xC8C8C8)); Box(c, 18, 14, 38, 16, C(0xFFFFFF)); Box(c, 18, 9, 32, 11, C(0xFFFFFF)); break;
                case 1: Box(c, 8, 4, 17, 21, C(0x1E1E24)); Box(c, 9, 6, 16, 19, C(0x8AD8F8)); Box(c, 22, 14, 38, 16, C(0xFFFFFF)); Box(c, 22, 9, 34, 11, C(0xFFFFFF)); break;
                case 2: Disc(c, 12, 15, 5, C(0xFFE04A)); Box(c, 2, 2, 41, 7, C(0xE8D49A)); Box(c, 22, 14, 38, 16, C(0xFFFFFF)); break;
                default: Box(c, 6, 8, 37, 17, C(0xE83A3A)); Box(c, 10, 11, 33, 14, C(0xFFFFFF)); break;
            }
            return c;
        }

        /// <summary>Vehicle <paramref name="k"/> of 6, facing right: five cars and a taxi, with wheels and lit windows.</summary>
        public static PixelCanvas Car(int k)
        {
            uint[] paint = { 0xD83A3A, 0x3A6AC8, 0xEAEAEA, 0xF0C030, 0x2A2A30, 0x3A9A5A };
            var c = new PixelCanvas(22, 10);
            Color body = C(paint[k]);
            Box(c, 1, 2, 20, 5, body);
            Box(c, 5, 6, 15, 8, body);
            Box(c, 6, 6, 9, 7, C(0x9AC4E4));
            Box(c, 11, 6, 14, 7, C(0x9AC4E4));
            Box(c, 1, 5, 20, 5, Color.Lerp(body, Color.white, 0.3f));
            Box(c, 20, 3, 20, 4, C(0xFFF0B0));                                             // headlamp
            Box(c, 1, 3, 1, 4, C(0xE83A2A));                                               // tail lamp
            Disc(c, 5.5f, 1.5f, 1.8f, C(0x1E1E24));
            Disc(c, 16.5f, 1.5f, 1.8f, C(0x1E1E24));
            if (k == 3) Box(c, 9, 9, 11, 9, C(0x2A2A30));                                   // the taxi's sign
            return c;
        }

        // ---- the high street, and its lights ----

        struct Street
        {
            public List<RectInt> Windows;
            public List<Vector2Int> Lamps;
        }

        static Street s_street;

        // The street's buildings, drawn on c when given; the windows and lamps are kept for the lights layer.
        static Street Lay(PixelCanvas c)
        {
            var street = new Street { Windows = new List<RectInt>(), Lamps = new List<Vector2Int>() };
            uint[] brick = { 0xA8584A, 0xC8A070, 0x9A6A50, 0xB87A5A, 0xD8C8B0 };
            for (int i = 0, x = 0; x < W; i++)
            {
                int w = 46 + Hash(i, 21) % 30, h = 50 + Hash(i, 22) % 24;
                bool shop = Hash(i, 23) % 2 == 0;
                if (c != null)
                {
                    Color wall = C(brick[Hash(i, 24) % brick.Length]);
                    for (int y = 0; y < h; y++)
                        for (int xx = x; xx < x + w; xx++)
                            c[xx % W, y] = (y % 4 == 0 && wall.r > 0.6f) ? wall * 0.92f + new Color(0, 0, 0, 0.08f) : wall;
                    // Roof and chimney.
                    for (int r = 0; r < 10; r++) Box(c, x + r, h + r, x + w - 1 - r, h + r, C(0x4A4E5A));
                    Box(c, x + w - 12, h + 4, x + w - 7, h + 14, C(0x7A4A3A));
                }
                // Upstairs windows.
                for (int wx = x + 6; wx < x + w - 10; wx += 14) street.Windows.Add(new RectInt(wx, h - 18, 8, 10));
                if (shop)
                {
                    // A shopfront: big glass, an awning in stripes, a sign.
                    street.Windows.Add(new RectInt(x + 4, 4, w - 10, 16));
                    if (c != null)
                    {
                        uint[] awnings = { 0x2A8A5A, 0xD83A3A, 0x3A6AC8, 0xF0A030 };
                        uint aw = awnings[Hash(i, 25) % awnings.Length];
                        for (int xx = x + 2; xx < x + w - 4; xx++) Box(c, xx, 21, xx, 25, (xx / 4) % 2 == 0 ? C(aw) : C(0xF4F0E8));
                        Box(c, x + 6, 27, x + w - 12, 32, C(0x2A2E36));
                        Box(c, x + 9, 29, x + w - 15, 30, C(aw));
                    }
                }
                else
                {
                    street.Windows.Add(new RectInt(x + 6, 6, 9, 12));
                    if (c != null) Box(c, x + w / 2, 0, x + w / 2 + 6, 14, C(0x2A3A5A));    // a front door
                }
                x += w + 1;
            }
            foreach (int lx in new[] { 40, 170, 300, 430 }) street.Lamps.Add(new Vector2Int(lx, 44));
            if (c != null)
            {
                foreach (RectInt r in street.Windows)
                {
                    Box(c, r.x - 1, r.y - 1, r.x + r.width, r.y + r.height, C(0xF2F0EA));        // white frames
                    Box(c, r.x, r.y, r.x + r.width - 1, r.y + r.height - 1, C(0x8AB4D4));
                    Box(c, r.x, r.y + r.height / 2, r.x + r.width - 1, r.y + r.height - 1, C(0xB4D4EC));   // the sky in the glass
                }
                // Trees, street lamps, a bus shelter and a parked car.
                foreach (int tx in new[] { 100, 240, 370 })
                {
                    Box(c, tx, 0, tx + 1, 20, C(0x5A4030));
                    Disc(c, tx, 28, 9, C(0x3E8A3A));
                    Disc(c, tx - 3, 31, 5, C(0x5AAA4A));
                }
                foreach (Vector2Int l in street.Lamps)
                {
                    Box(c, l.x, 0, l.x + 1, l.y, C(0x3A3E46));
                    Box(c, l.x - 3, l.y, l.x + 4, l.y + 2, C(0x3A3E46));
                    Box(c, l.x - 2, l.y - 1, l.x + 3, l.y - 1, C(0xE8E0C8));
                }
                Box(c, 200, 0, 201, 22, C(0x6A6E76));
                Box(c, 224, 0, 225, 22, C(0x6A6E76));
                Box(c, 198, 22, 228, 24, C(0x4A4E5A));
                Box(c, 202, 2, 223, 21, C(0xB4D4EC, 0.45f));
            }
            return street;
        }

        /// <summary>The high street by day, 512 x 112.</summary>
        public static PixelCanvas Near()
        {
            var c = new PixelCanvas(W, 112);
            s_street = Lay(c);
            return c;
        }

        /// <summary>The same street's lights, 512 x 112, faded in over it as dusk falls: warm windows and lamps with soft halos.</summary>
        public static PixelCanvas NearLights()
        {
            var c = new PixelCanvas(W, 112);
            Street street = s_street.Windows != null ? s_street : Lay(null);
            for (int i = 0; i < street.Windows.Count; i++)
            {
                RectInt r = street.Windows[i];
                if (Hash(r.x, r.y) % 5 == 0) continue;                                   // a few stay dark
                Box(c, r.x - 2, r.y - 2, r.x + r.width + 1, r.y + r.height + 1, C(0xFFC864, 0.22f));
                Box(c, r.x, r.y, r.x + r.width - 1, r.y + r.height - 1, C(Hash(r.y, r.x) % 4 == 0 ? 0x9AD8FFu : 0xFFD27Au, 0.92f));
            }
            foreach (Vector2Int l in street.Lamps)
            {
                Disc(c, l.x + 0.5f, l.y - 2, 9, C(0xFFE0A0, 0.18f));
                Disc(c, l.x + 0.5f, l.y - 2, 4, C(0xFFE8B0, 0.4f));
                Box(c, l.x - 2, l.y - 1, l.x + 3, l.y - 1, C(0xFFF4D0));
            }
            return c;
        }

        // ---- the floor ----

        /// <summary>Asphalt, 64 x 64 (2 x 2 units): dark, speckled with aggregate, a crack or two.</summary>
        public static PixelCanvas Floor()
        {
            var c = new PixelCanvas(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int h = Hash(x, y) % 23;
                    c[x, y] = h == 0 ? C(0x55575F) : h == 1 ? C(0x2E3036) : C(0x3E4048);
                }
            foreach (var (x0, y0) in new[] { (10, 50), (40, 20) })
                for (int i = 0; i < 9; i++) c[(x0 + i) % 64, (y0 - i / 2 + (i % 3 == 0 ? 1 : 0) + 64) % 64] = C(0x26282C);
            return c;
        }

        /// <summary>The floor's top edge, 32 x 10 like the stone edge: pavement, the kerb's lit top and face, the gutter and a yellow line.</summary>
        public static PixelCanvas FloorTop()
        {
            var c = new PixelCanvas(32, 10);
            for (int x = 0; x < 32; x++)
            {
                c[x, 9] = c[x, 8] = x % 16 == 0 ? C(0xA8A49C) : C(0xC8C4BC);
                c[x, 7] = C(0xDAD6CE);
                for (int y = 4; y <= 6; y++) c[x, y] = x % 8 == 0 ? C(0x7A766E) : C(0x9A968E);
                c[x, 3] = C(0x2E3036);
                c[x, 2] = c[x, 0] = C(0x3E4048);
                c[x, 1] = C(0xF0C030);
            }
            return c;
        }

        // ---- hazards on the same colliders ----

        /// <summary>
        /// The scaffolding (the hanging ceiling), 32 x 32 at normal resolution: galvanised tubes and a brace over orange
        /// safety netting, scaffold boards along the bottom.
        /// </summary>
        public static PixelCanvas Scaffold()
        {
            var c = new PixelCanvas(32, 32);
            Box(c, 0, 0, 31, 31, C(0x3A3E46));
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    if ((x + y) % 4 == 0 || (x - y + 64) % 4 == 0) c[x, y] = C(0xE8701E);   // the netting
            Color tube = C(0xA8B0B8), shade = C(0x7A828A);
            foreach (int x in new[] { 2, 18 }) { Box(c, x, 0, x + 1, 31, tube); Box(c, x + 2, 0, x + 2, 31, shade); }
            foreach (int y in new[] { 14, 28 }) { Box(c, 0, y, 31, y + 1, tube); Box(c, 0, y - 1, 31, y - 1, shade); }
            for (int i = 0; i < 12; i++) { c[4 + i, 15 + i] = tube; c[5 + i, 15 + i] = shade; }
            Box(c, 0, 0, 31, 3, C(0xC89A5A));                                            // scaffold boards
            for (int x = 0; x < 32; x += 8) Box(c, x, 0, x, 3, C(0x8A6A3A));
            Box(c, 0, 3, 31, 3, C(0xE0B474));
            return c;
        }

        /// <summary>Anti-climb spikes under the scaffolding: a repaint of the spike's own pixels, steel with bright points.</summary>
        public static PixelCanvas Teeth() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            if (y >= 11) return Grey(1f);
            if (y <= 1) return Grey(0.36f);
            return x % 4 == 1 ? Shade(p, 1.1f) : Shade(p, 0.78f);
        });

        /// <summary>
        /// Open roadworks in a gap, 16 x 192 (6 units deep): cut tarmac at the top, earth walls with stones, a blue water
        /// main crossing it and darker earth below.
        /// </summary>
        public static PixelCanvas Roadworks()
        {
            var c = new PixelCanvas(16, 192);
            for (int y = 0; y < 192; y++)
                for (int x = 0; x < 16; x++)
                {
                    int d = 191 - y;
                    Color col = d < 3 ? C(0x2E3036)
                        : d < 30 ? (Hash(x, y) % 9 == 0 ? C(0x8A6A4A) : C(0x6A4A30))
                        : d < 38 ? (d < 31 || d > 36 ? C(0x24448A) : d < 33 ? C(0x6A9AE8) : C(0x3A6AC8))
                        : d < 90 ? (Hash(x, y) % 11 == 0 ? C(0x5A4430) : C(0x4E3A28))
                        : C(0x3A2C20);
                    c[x, y] = col;
                }
            return c;
        }

        /// <summary>A roadworks barrier on the trench's lip, frame <paramref name="f"/> of 2: red and white board, its amber lamp blinking.</summary>
        public static PixelCanvas Barrier(int f)
        {
            var c = new PixelCanvas(40, 22);
            for (int x = 2; x < 38; x++)
                for (int y = 6; y < 12; y++) c[x, y] = ((x + y) / 4) % 2 == 0 ? C(0xE83A3A) : C(0xF4F4F4);
            Box(c, 4, 0, 5, 6, C(0x3A3E46));
            Box(c, 34, 0, 35, 6, C(0x3A3E46));
            Box(c, 19, 12, 20, 15, C(0x3A3E46));
            Disc(c, 19.5f, 18f, 3f, f == 0 ? C(0xFFB020) : C(0x8A5A18));
            if (f == 0) Disc(c, 19.5f, 18f, 5f, C(0xFFC840, 0.3f));
            c.Outline(Hex(0x1E1E24));
            return c;
        }

        /// <summary>A pound coin, 13 x 13: twelve-sided, a gold ring round a silver centre.</summary>
        public static PixelCanvas PoundCoin()
        {
            var c = new PixelCanvas(13, 13);
            c.Blit(new[]
            {
                "...ooooo...",
                "..oyyyyyo..",
                ".oyyssssyo.",
                "oyysSSsssyo",
                "oyssSssssyo",
                "oysssssssyo",
                "oyssssssyyo",
                "oyysssssyyo",
                ".oyyssssyo.",
                "..oyyyyyo..",
                "...ooooo...",
            }, new Dictionary<char, Color32> { ['o'] = Hex(0x9A7A1A), ['y'] = Hex(0xE8C040), ['s'] = Hex(0xC8CCD0), ['S'] = Hex(0xF4F6F8) }, 1, 1);
            return c;
        }

        /// <summary>An airliner crossing high up, 44 x 12: white with a blue stripe and a red tail.</summary>
        public static PixelCanvas Airliner()
        {
            var c = new PixelCanvas(44, 12);
            Box(c, 4, 4, 40, 7, C(0xF4F4F4));
            Box(c, 40, 5, 43, 6, C(0xF4F4F4));
            Box(c, 4, 5, 38, 5, C(0x3A6AC8));
            Box(c, 0, 6, 7, 11, C(0xD83A3A));
            Box(c, 16, 1, 24, 4, C(0xD8D8DC));
            Box(c, 18, 8, 22, 9, C(0xC8C8CC));
            for (int x = 10; x < 38; x += 3) c[x, 6] = Hex(0x3A4458);
            c.Outline(Hex(0x5A5E66));
            return c;
        }
    }
}
