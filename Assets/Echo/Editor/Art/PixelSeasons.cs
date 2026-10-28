using System;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Seasonal art (seasons doc). Reskins of world art are repaints of the original's opaque pixels only
    /// (<see cref="Repaint"/>), so a hazard keeps its exact outline and size (seasons doc ground rule: reskinned hazards
    /// keep their outline; SeasonArtTests checks every one). Hazards and layers stay greyscale so the views tint them.
    /// Seasons phase 1 has only the test theme, whose art is all placeholder.
    /// </summary>
    public static class PixelSeasons
    {
        /// <summary>A copy of <paramref name="source"/> with each opaque pixel recoloured; transparent pixels stay clear.</summary>
        public static PixelCanvas Repaint(PixelCanvas source, Func<int, int, Color32, Color32> paint)
        {
            var c = new PixelCanvas(source.Width, source.Height);
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    if (!source.Opaque(x, y)) continue;
                    Color32 p = paint(x, y, source[x, y]);
                    p.a = source[x, y].a;
                    c[x, y] = p;
                }
            return c;
        }

        /// <summary>Lightens the top few pixels of every column: a dusting on the edges (scenery layers, the floor).</summary>
        public static PixelCanvas Capped(PixelCanvas source, int depth, float lift) => Repaint(source, (x, y, p) =>
        {
            int below = 0;
            while (below < depth && source.Opaque(x, y + below + 1)) below++;
            if (below >= depth) return p;
            float v = Mathf.Lerp(p.r / 255f, 1f, lift * (1f - below / (float)depth));
            return Grey(v);
        });

        // ---- the test theme (placeholders) ----

        /// <summary>The spike painted in diagonal bands: same silhouette, still greyscale.</summary>
        public static PixelCanvas TestSpike() => Repaint(PixelWorld.Spike(), (x, y, p) => (x + y) / 3 % 2 == 0 ? p : Shade(p, 0.8f));

        /// <summary>The block in a checker of its own shades.</summary>
        public static PixelCanvas TestBlock() => Repaint(PixelWorld.Block(), (x, y, p) => (x / 4 + y / 4) % 2 == 0 ? p : Shade(p, 0.85f));

        public static PixelCanvas TestGroundTop() => Capped(PixelWorld.GroundTop(), 2, 0.6f);
        public static PixelCanvas TestFar() => Capped(PixelWorld.Mountains(), 4, 0.55f);
        public static PixelCanvas TestNear() => Capped(PixelWorld.GraveyardHills(), 3, 0.5f);

        /// <summary>The death screen's headstone in lilac, its face left plain for the epitaph.</summary>
        public static PixelCanvas TestHeadstone() => Repaint(PixelWorld.Headstone(), (x, y, p) =>
            new Color32((byte)Mathf.Min(255, p.r * 1.05f), (byte)(p.g * 0.86f), (byte)Mathf.Min(255, p.b * 1.12f), p.a));

        /// <summary>A gift-box grave in its own colours, 12 x 16 like the stone, mound in the bottom two rows.</summary>
        public static PixelCanvas TestGrave()
        {
            Color32 box = Hex(0xB46BE0), boxDark = Hex(0x8443B0), ribbon = Hex(0xFFD24A), mound = Hex(0x5A4636);
            var c = new PixelCanvas(12, 16);
            c.Rect(0, 0, 11, 1, mound);
            c.Rect(1, 2, 10, 10, box);
            c.Rect(9, 2, 10, 10, boxDark);
            c.Rect(5, 2, 6, 10, ribbon);
            c.Rect(1, 6, 10, 7, ribbon);
            c[4, 11] = c[3, 12] = c[7, 11] = c[8, 12] = ribbon;
            c.Outline(Hex(0x2A1838));
            return c;
        }

        /// <summary>A party hat for every ghost, 9 x 10 with outline; pivot just above the bottom so it sits on the head.</summary>
        public static PixelCanvas TestGhostAccessory()
        {
            var c = new PixelCanvas(9, 10);
            c.Blit(new[]
            {
                "...y...",
                "...p...",
                "..ppP..",
                "..pyP..",
                ".pppPP.",
                ".pypyP.",
                "ppppPPP",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                ['p'] = Hex(0xFF7AC8), ['P'] = Hex(0xD04E98), ['y'] = Hex(0xFFE36A),
            }, 1, 1);
            c.Outline(Hex(0x3A1A30));
            return c;
        }

        /// <summary>A crown for the Nemesis, 14 x 7 with outline, in place of its horns.</summary>
        public static PixelCanvas TestNemesisAccessory()
        {
            var c = new PixelCanvas(16, 9);
            c.Blit(new[]
            {
                "g.....g.....g.",
                "gg...ggg...gg.",
                "ggg.ggrgg.ggg.",
                "gggggggggggggG",
                "GgbgggGgggbgGG",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                ['g'] = Hex(0xFFD24A), ['G'] = Hex(0xC9962A), ['r'] = Hex(0xFF4A5A), ['b'] = Hex(0x5AB4FF),
            }, 1, 1);
            c.Outline(Hex(0x3A2A10));
            return c;
        }

        /// <summary>Confetti: a 2 x 2 square and a 1 x 3 strip, white for the theme's colours.</summary>
        public static PixelCanvas Confetti(int kind)
        {
            var c = kind == 0 ? new PixelCanvas(2, 2) : new PixelCanvas(1, 3);
            c.Rect(0, 0, c.Width - 1, c.Height - 1, Grey(1f));
            return c;
        }
    }
}
