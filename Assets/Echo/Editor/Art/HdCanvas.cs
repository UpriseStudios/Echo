using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// A full-resolution canvas for the Far Future (World Time Run doc, phase 3: the only HD art): smooth gradients, soft
    /// glows and anti-aliased shapes, in straight alpha. Wraps sideways so layers tile. y = 0 is the bottom row.
    /// </summary>
    public sealed class HdCanvas
    {
        public readonly int Width, Height;
        readonly Color[] _p;

        public HdCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            _p = new Color[width * height];
        }

        public Color this[int x, int y]
        {
            get => _p[y * Width + Wrap(x)];
            set => _p[y * Width + Wrap(x)] = value;
        }

        int Wrap(int x) => ((x % Width) + Width) % Width;

        /// <summary>Paints <paramref name="c"/> over what is there, by its alpha times <paramref name="cover"/>.</summary>
        public void Blend(int x, int y, Color c, float cover = 1f)
        {
            if (y < 0 || y >= Height) return;
            float a = c.a * Mathf.Clamp01(cover);
            if (a <= 0f) return;
            Color under = this[x, y];
            float outA = a + under.a * (1f - a);
            Color o = (c * a + under * under.a * (1f - a)) / outA;
            o.a = outA;
            this[x, y] = o;
        }

        /// <summary>Adds light: brightens what is there without covering it (glows, neon).</summary>
        public void Add(int x, int y, Color c, float amount)
        {
            if (y < 0 || y >= Height || amount <= 0f) return;
            Color under = this[x, y];
            Color o = under + c * amount;
            o.a = Mathf.Clamp01(under.a + c.a * amount);
            o.r = Mathf.Min(o.r, 1f);
            o.g = Mathf.Min(o.g, 1f);
            o.b = Mathf.Min(o.b, 1f);
            this[x, y] = o;
        }

        public void Fill(System.Func<int, int, Color> colour)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++) this[x, y] = colour(x, y);
        }

        /// <summary>An axis-aligned rectangle with soft (anti-aliased) edges, coloured per pixel.</summary>
        public void Rect(float x0, float y0, float x1, float y1, System.Func<float, float, Color> colour)
        {
            for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                {
                    float cover = Mathf.Clamp01(Mathf.Min(x + 1 - x0, x1 - x)) * Mathf.Clamp01(Mathf.Min(y + 1 - y0, y1 - y));
                    if (cover > 0f) Blend(x, y, colour((x - x0) / Mathf.Max(1f, x1 - x0), (y - y0) / Mathf.Max(1f, y1 - y0)), cover);
                }
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c) => Rect(x0, y0, x1, y1, (u, v) => c);

        public void Disc(float cx, float cy, float r, System.Func<float, float, Color> colour)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float cover = Mathf.Clamp01(r - d + 0.5f);
                    if (cover > 0f) Blend(x, y, colour((x + 0.5f - cx) / r, (y + 0.5f - cy) / r), cover);
                }
        }

        /// <summary>A soft round glow: light falling off smoothly to nothing at <paramref name="r"/>.</summary>
        public void Glow(float cx, float cy, float r, Color c, float strength)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d < 1f) Add(x, y, c, strength * (1f - d) * (1f - d));
                }
        }

        /// <summary>A line of light with a soft halo, anti-aliased.</summary>
        public void Neon(float x0, float y0, float x1, float y1, float width, Color c, float halo)
        {
            float minX = Mathf.Min(x0, x1) - halo, maxX = Mathf.Max(x0, x1) + halo;
            float minY = Mathf.Min(y0, y1) - halo, maxY = Mathf.Max(y0, y1) + halo;
            Vector2 a = new Vector2(x0, y0), ab = new Vector2(x1 - x0, y1 - y0);
            float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = (p - (a + ab * t)).magnitude;
                    float core = Mathf.Clamp01(width / 2f - d + 0.5f);
                    if (core > 0f) Blend(x, y, c, core);
                    else if (halo > 0f && d < halo) Add(x, y, c, 0.35f * (1f - d / halo) * (1f - d / halo));
                }
        }

        public PixelCanvas ToPixelCanvas()
        {
            var o = new PixelCanvas(Width, Height);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++) o[x, y] = _p[y * Width + x];
            return o;
        }
    }
}
