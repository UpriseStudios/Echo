using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Characters the server grants for the competitive modes (modes doc), never sold or earned on the device: the
    /// Gravekeeper, for four full weeks (all 7 days) of the Community Graveyard. Drawn on the shared rig, facing right.
    /// </summary>
    public static class PixelModeCast
    {
        public static readonly PixelCharacter[] Rewards = { Gravekeeper() };

        static Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

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
