using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The Elementals (a user revision), the prestige pack: permanently unlocked by earning all 30 achievements, or
    /// tried for 5 runs after 5 rewarded ads. Each has a subtle flourish (CharacterReactions). Never sold. Grids are
    /// drawn facing right; the top row is the top of the sprite.
    /// </summary>
    public static class PixelElementals
    {
        public static readonly PixelCharacter[] All = { Ember(), Frost(), Storm(), Tide() };

        static Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        static PixelCharacter Body(string id, string name, uint torso, uint shade, uint legs, uint boots, uint hand, uint outline,
                                   uint scarf, float scarfWidth, int scarfX, int scarfY) => new PixelCharacter
        {
            Id = id, Name = name, AllAchievements = true, UnlockText = "Earn all 30 achievements",
            Torso = Hex(torso), TorsoShade = Hex(shade), Legs = Hex(legs), Boots = Hex(boots),
            Sleeve = Hex(torso), Hand = Hex(hand), Outline = Hex(outline),
            ScarfColor = Hex(scarf), ScarfWidth = scarfWidth, ScarfAnchor = new Vector2Int(scarfX, scarfY),
        };

        // Flame hair over a charcoal face with glowing eyes, cracks of fire on the chest, a flame for a scarf.
        static PixelCharacter Ember()
        {
            PixelCharacter c = Body("char_ember", "Ember", 0x3A2A2A, 0x241818, 0x2E2020, 0x1A1010, 0x3A2A2A, 0x140808, 0xFF8A2A, 0.08f, 9, 16);
            c.Head = new[]
            {
                "....y..y....",
                "...yoy.oy...",
                "..yoooyoo...",
                "..ooOoooOo..",
                ".ooOcccccOo.",
                ".occcccccco.",
                ".ocyccycccc.",
                ".occcccccco.",
                "..cccccccc..",
                "...CCCCCC...",
            };
            c.Chest = new[]
            {
                "...o....",
                "...oo...",
                "....o...",
                "...oo..o",
                "..o...o.",
                "..o..o..",
                "CCCCCCCC",
                "........",
            };
            c.Palette = Pal(('y', Hex(0xFFE45C)), ('o', Hex(0xFF8A2A)), ('O', Hex(0xD8501A)), ('c', Hex(0x3A2A2A)), ('C', Hex(0x241818)));
            return c;
        }

        // A crown of ice crystals, pale blue skin, frost-white clothes, a thin frosty scarf.
        static PixelCharacter Frost()
        {
            PixelCharacter c = Body("char_frost", "Frost", 0xE8F6FF, 0xA8D8EE, 0xBFE6F7, 0x8FC8E0, 0xCFF4FF, 0x0E2A3A, 0xE8FAFF, 0.06f, 9, 16);
            c.Head = new[]
            {
                "..i...i...i.",
                "..ii.iii.ii.",
                "...iiiiiii..",
                "..iIIIIIIIi.",
                ".iIIIIIIIIi.",
                ".iIIIIIkIIk.",
                ".iIIIIIIIIi.",
                "..iIIIIIIi..",
                "...iiiiii...",
            };
            c.Chest = new[]
            {
                "...ii...",
                "..i..i..",
                "...ii...",
                "........",
                "........",
                "........",
                "IIIIIIII",
                "........",
            };
            c.Palette = Pal(('i', Hex(0xCFF4FF)), ('I', Hex(0x8FD8F0)), ('k', Hex(0x1A4A6A)));
            return c;
        }

        // A storm cloud for a head, a lightning bolt on a midnight-blue tunic.
        static PixelCharacter Storm()
        {
            PixelCharacter c = Body("char_storm", "Storm", 0x2A3A6A, 0x1E2A50, 0x1E2A50, 0x121A34, 0xB8C0CC, 0x0A0E1C, 0xB8C0CC, 0.1f, 9, 16);
            c.Head = new[]
            {
                "...ggg.gg...",
                "..ggggggggg.",
                ".gggGgggGggg",
                ".gGggggggggg",
                ".gggggwkgwk.",
                ".ggggggggggg",
                "..gGgggggGg.",
                "...gggggg...",
            };
            c.Chest = new[]
            {
                "....yy..",
                "...yy...",
                "..yyyy..",
                "....yy..",
                "...yy...",
                "..y.....",
                "bbbbbbbb",
                "........",
            };
            c.Palette = Pal(('g', Hex(0xB8C0CC)), ('G', Hex(0x8A94A4)), ('w', Hex(0xFFFFFF)), ('k', Hex(0x1A2030)), ('y', Hex(0xFFE45C)),
                ('b', Hex(0x121A34)));
            return c;
        }

        // A breaking wave for a head with a crest of foam, sea-green clothes, a ribbon of water for a scarf.
        static PixelCharacter Tide()
        {
            PixelCharacter c = Body("char_tide", "Tide", 0x3AC8D8, 0x2A98B0, 0x1A7898, 0x0E5070, 0x7AE8F0, 0x06202E, 0x7AE8F0, 0.07f, 9, 16);
            c.Head = new[]
            {
                "......wwww..",
                "....wwttttw.",
                "...wttttttt.",
                "..tttTtttttt",
                ".tttTttttttt",
                ".ttTttttkttk",
                ".tTtttttttt.",
                ".Ttttttttt..",
                "..TTTTTTTT..",
            };
            c.Chest = new[]
            {
                "........",
                "..w..w..",
                ".w.ww.w.",
                "........",
                "..w..w..",
                ".w.ww.w.",
                "TTTTTTTT",
                "........",
            };
            c.Palette = Pal(('t', Hex(0x2AA8C8)), ('T', Hex(0x1A7898)), ('w', Hex(0xE8FAFF)), ('k', Hex(0x08283A)));
            return c;
        }
    }
}
