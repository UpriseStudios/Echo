using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Characters the server grants for the competitive modes (modes doc), never sold or earned on the device: the
    /// Gravekeeper, for four full weeks (all 7 days) of the Community Graveyard, and the Wraith, for a Ghost Duels season
    /// finished in the top 100. Drawn on the shared rig, facing right.
    /// </summary>
    public static class PixelModeCast
    {
        public static readonly PixelCharacter[] Rewards = { Gravekeeper(), Wraith() };

        static Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        // Ghost Duels' top 100: a hooded spectre with no face but two cold eyes, a tattered robe, and a lantern on its back.
        static PixelCharacter Wraith() => new PixelCharacter
        {
            Id = "char_wraith", Name = "Wraith", ServerReward = true,
            UnlockText = "Ghost Duels: finish a season in the top 100",
            Torso = Hex(0x1E2A3E), TorsoShade = Hex(0x121A28), Legs = Hex(0x1E2A3E), Boots = Hex(0x121A28),
            Sleeve = Hex(0x1E2A3E), Hand = Hex(0xB8E8F0), Outline = Hex(0x05080E),
            Skirt = true, SkirtColor = Hex(0x1E2A3E), SkirtShade = Hex(0x121A28), SkirtBottom = 1,
            Head = new[]
            {
                "...hhhhh....",
                "..hhhhhhh...",
                ".hhhhhhhhh..",
                ".hhhhvvvvvh.",
                ".hhhvvvvvvh.",
                ".hhhvvevvev.",
                ".hhhvvvvvvh.",
                ".hhhhvvvvh..",
                ".hhhhhhhhh..",
                "..hhh.h.hh..",
            },
            Chest = new[]
            {
                "........",
                "..t..t..",
                "........",
                ".t....t.",
                "........",
                "..t..t..",
                "........",
                ".t.tt.t.",
            },
            Back = new[]
            {
                ".l.",
                "lgl",
                "lgl",
                ".l.",
                ".w.", ".w.", ".w.", ".w.", ".w.",
            },
            BackAt = new Vector2Int(2, 14),
            Palette = Pal(('h', Hex(0x24324A)), ('v', Hex(0x05080E)), ('e', Hex(0x73F2FF)), ('t', Hex(0x2E3E58)),
                ('l', Hex(0x8A96A0)), ('g', Hex(0x9CFFF0)), ('w', Hex(0x5A6670))),
        };

        // A grave digger: a wide-brimmed hat, a grey beard, a long coat, and a spade over the shoulder.
        static PixelCharacter Gravekeeper() => new PixelCharacter
        {
            Id = "char_gravekeeper", Name = "Gravekeeper", ServerReward = true,
            UnlockText = "Community Graveyard: play all 7 days, 4 weeks",
            Torso = Hex(0x3E4A3A), TorsoShade = Hex(0x2A3428), Legs = Hex(0x4A3A2A), Boots = Hex(0x1E1610),
            Sleeve = Hex(0x3E4A3A), Hand = Hex(0xD8B08A), Outline = Hex(0x0C0E0A),
            Skirt = true, SkirtColor = Hex(0x3E4A3A), SkirtShade = Hex(0x2A3428), SkirtBottom = 2,
            Head = new[]
            {
                "...hhhhh....",
                "...hhhhhh...",
                "...bbbbbb...",
                "hhhhhhhhhhhh",
                ".sssssssssk.",
                ".ssssssskss.",
                ".sssssssssp.",
                ".gggsssgggg.",
                "..ggggggggg.",
                "...gggggg...",
            },
            Chest = new[]
            {
                "........",
                "........",
                "..cc....",
                "........",
                "llllllll",
                "........",
                "..cc....",
                "........",
            },
            Back = new[]
            {
                ".mmm.....",
                "mmmmm....",
                "mmmmm....",
                ".mMm.....",
                "..w......", "..w......", "..w......", "..w......", "..w......",
                "..w......", "..w......", "..w......", "..w......", "..w......",
                "..w......", "..w......", "..w......", "..w......", "..w......",
                ".www.....",
            },
            BackAt = new Vector2Int(1, 8),
            Palette = Pal(('h', Hex(0x2A2018)), ('b', Hex(0x6A5A40)), ('s', Hex(0xD8B08A)), ('k', Hex(0x14100C)),
                ('p', Hex(0xC08A70)), ('g', Hex(0xA8A8A0)), ('c', Hex(0xB8A060)), ('l', Hex(0x2A1C12)),
                ('m', Hex(0x8A96A0)), ('M', Hex(0x5A646C)), ('w', Hex(0x7A5230))),
        };
    }
}
