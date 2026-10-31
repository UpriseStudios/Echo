using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Easter (seasons doc): painted-egg spikes (the spike's own pixels repainted in bands and dots, so the outline is
    /// exact and it stays greyscale for the hazard tint), a painted-egg gravestone, bunny ears for the ghosts, blossom
    /// petals drifting down, the eggs of the egg hunt, and the Easter Chick, earned by finding 10 of them. The finished
    /// art, in the game's pixel style.
    /// </summary>
    public static class PixelEaster
    {
        /// <summary>The spike painted like an egg: a zigzag band across it and dots above, in its own greys.</summary>
        public static PixelCanvas Spike() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            int zig = x % 4 < 2 ? x % 4 : 3 - x % 4;
            if (y == 3 + zig || y == 4 + zig) return Shade(p, 0.5f);       // a bold zigzag band low down
            if (y == 9 + zig) return Shade(p, 0.55f);                      // and a thinner one higher up
            if (y > 5 && y < 8 && (x + y) % 3 == 0) return Grey(1f);        // white dots between them
            return p;
        });

        /// <summary>A painted egg standing in grass for a gravestone, 12 x 16 like the stone, in its own colours.</summary>
        public static PixelCanvas Grave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "....pppp....",
                "...pppppp...",
                "..pppppppp..",
                "..yyyyyyyy..",
                ".yYyyYyyYyy.",
                ".yyyyyyyyyy.",
                ".bbbbbbbbbb.",
                ".bwbbwbbwbb.",
                ".bbbbbbbbbb.",
                ".pppppppppp.",
                "..pppppppp..",
                "..pppppppp..",
                "...pppppp...",
                "....pppp....",
                "gGgGggGgGggG",
                "gggggggggggg",
            }, new Dictionary<char, Color32>
            {
                ['p'] = Hex(0xF2A8D8), ['y'] = Hex(0xFFE27A), ['Y'] = Hex(0xF2B830), ['b'] = Hex(0x8AC8FF), ['w'] = Hex(0xFFFFFF),
                ['g'] = Hex(0x6AC85A), ['G'] = Hex(0x4AA040),
            }, 0, 0);
            c.Outline(Hex(0x4A2A44));
            return c;
        }

        /// <summary>Bunny ears for every ghost, white with pink insides; pivot at their base so they stand on the head.</summary>
        public static PixelCanvas BunnyEars()
        {
            var c = new PixelCanvas(11, 11);
            c.Blit(new[]
            {
                ".ww.....ww.",
                "wwww...wwww",
                "wppw...wppw",
                "wppw...wppw",
                "wppw...wppw",
                ".wpw...wpw.",
                ".ww.....ww.",
                ".ww.....ww.",
                "..wwwwwww..",
            }, new Dictionary<char, Color32> { ['w'] = Hex(0xF8F4F4), ['p'] = Hex(0xF2A8C0) }, 0, 1);
            c.Outline(Hex(0x3A2A30));
            return c;
        }

        /// <summary>A blossom petal, white for the theme's colours: a 2 x 2 petal or a 1 x 2 sliver.</summary>
        public static PixelCanvas Petal(int kind)
        {
            var c = kind == 0 ? new PixelCanvas(2, 2) : new PixelCanvas(2, 1);
            c.Rect(0, 0, c.Width - 1, c.Height - 1, Grey(1f, 230));
            if (kind == 0) c[1, 1] = Grey(1f, 140);
            return c;
        }

        /// <summary>A hidden egg for the egg hunt, 7 x 9, striped and spotted, in its own colours.</summary>
        public static PixelCanvas Egg()
        {
            var c = new PixelCanvas(9, 11);
            c.Blit(new[]
            {
                "..ppp..",
                ".ppppp.",
                ".ywyyw.",
                "yyyyyyy",
                "bbbbbbb",
                "bwbbwbb",
                "ppppppp",
                ".ppppp.",
                "..ppp..",
            }, new Dictionary<char, Color32>
            {
                ['p'] = Hex(0xFF8AC8), ['y'] = Hex(0xFFE24A), ['b'] = Hex(0x5AC8FF), ['w'] = Hex(0xFFFFFF),
            }, 1, 1);
            c.Outline(Hex(0x4A1A3A));
            return c;
        }

        /// <summary>The Easter Chick (Egg Hunt's reward): a fluffy chick with a bit of its shell still on its head.</summary>
        public static PixelCharacter Chick()
        {
            var c = new PixelCharacter
            {
                Id = "char_easter_chick", Name = "Easter Chick",
                Torso = Hex(0xFFD84A), TorsoShade = Hex(0xE8B830), Legs = Hex(0xF08A24), Boots = Hex(0xD86A10),
                Sleeve = Hex(0xFFD84A), Hand = Hex(0xFFE88A), Outline = Hex(0x4A3008),
            };
            c.Head = new[]
            {
                "..s.s.s.....",
                ".sssssss....",
                ".ssyyyyss...",
                "..yyyyyyyy..",
                ".yyyyyyykyy.",
                ".yyyyyyyyyoo",
                ".yyyyyyyyyo.",
                "..yyyyyyyy..",
                "...yyyyyy...",
            };
            c.Chest = new[]
            {
                "........",
                ".f..f...",
                "........",
                "...f..f.",
                "........",
                ".f..f...",
                "........",
                "........",
            };
            c.Palette = new Dictionary<char, Color32>
            {
                ['s'] = Hex(0xF4F0E4), ['y'] = Hex(0xFFD84A), ['k'] = Hex(0x1A1008), ['o'] = Hex(0xF08A24), ['f'] = Hex(0xFFE88A),
            };
            c.UnlockAchievement = Echo.Core.Achievements.AchievementIds.EggHunt;
            c.UnlockText = "Easter: Egg Hunt";
            return c;
        }
    }
}
