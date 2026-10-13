using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// A small RGBA pixel grid for 16-bit style art, origin bottom-left. Shapes are plotted pixel by pixel
    /// (no anti-aliasing) so every sprite stays on the 32-px-per-unit grid.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width, Height;
        readonly Color32[] _px;

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            _px = new Color32[width * height];
        }

        public Color32 this[int x, int y]
        {
            get => In(x, y) ? _px[y * Width + x] : Clear;
            set { if (In(x, y)) _px[y * Width + x] = value; }
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool Opaque(int x, int y) => this[x, y].a > 0;

        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                    this[x, y] = c;
        }

        /// <summary>Bresenham line with a square brush of <paramref name="thickness"/> pixels.</summary>
        public void Line(int x0, int y0, int x1, int y1, Color32 c, int thickness = 1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Rect(x0, y0, x0 + thickness - 1, y0 + thickness - 1, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) this[x, y] = c;
        }

        /// <summary>
        /// Stamps a picture written as text rows, top row first, with its bottom-left at (x, y). Each character
        /// is looked up in <paramref name="palette"/>; '.' and ' ' are transparent.
        /// </summary>
        public void Blit(string[] rows, IReadOnlyDictionary<char, Color32> palette, int x, int y, bool mirror = false)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                int py = y + rows.Length - 1 - r;
                for (int i = 0; i < row.Length; i++)
                {
                    char ch = row[i];
                    if (ch == '.' || ch == ' ') continue;
                    if (!palette.TryGetValue(ch, out Color32 c))
                        throw new ArgumentException($"no colour for '{ch}' in row {r}: {row}");
                    this[mirror ? x + row.Length - 1 - i : x + i, py] = c;
                }
            }
        }

        /// <summary>Every clear pixel touching an opaque one (4-way) becomes <paramref name="c"/>: the classic 1-px sprite outline.</summary>
        public void Outline(Color32 c)
        {
            var ring = new List<int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (!Opaque(x, y) && (Opaque(x - 1, y) || Opaque(x + 1, y) || Opaque(x, y - 1) || Opaque(x, y + 1)))
                        ring.Add(y * Width + x);
            foreach (int i in ring) _px[i] = c;
        }

        /// <summary>The silhouette as a hollow outline: edge pixels white, the inside a faint wash. Used for ghosts.</summary>
        public PixelCanvas Hollow(byte fillAlpha)
        {
            var o = new PixelCanvas(Width, Height);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (!Opaque(x, y)) continue;
                    bool edge = !Opaque(x - 1, y) || !Opaque(x + 1, y) || !Opaque(x, y - 1) || !Opaque(x, y + 1);
                    o[x, y] = edge ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, fillAlpha);
                }
            return o;
        }

        /// <summary>A copy scaled up by a whole number, for icons and previews.</summary>
        public PixelCanvas Scaled(int k)
        {
            var o = new PixelCanvas(Width * k, Height * k);
            for (int y = 0; y < o.Height; y++)
                for (int x = 0; x < o.Width; x++)
                    o[x, y] = this[x / k, y / k];
            return o;
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(_px);
            tex.Apply();
            return tex;
        }

        // ---- colour helpers ----

        public static Color32 Hex(uint rgb, byte a = 255) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

        public static Color32 Grey(float v, byte a = 255)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            return new Color32(b, b, b, a);
        }

        public static Color32 Shade(Color32 c, float k) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * k), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * k), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * k), 0, 255), c.a);

        /// <summary>4x4 ordered (Bayer) dither threshold in [0, 1): the 16-bit way to blend two shades.</summary>
        public static float Bayer(int x, int y)
        {
            int[] m = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            return (m[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
        }
    }
}
