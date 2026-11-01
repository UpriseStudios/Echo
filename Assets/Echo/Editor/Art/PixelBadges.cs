using System.Collections.Generic;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Badge icons (a user revision: badges as little icons beside names, not text labels): 12 px icons in 16 px cells
    /// along one strip, named for the TMP sprite asset that draws them inline (BadgeSprites). In colour, outlined, so
    /// they read on the dark boards and the gold cards alike.
    /// </summary>
    public static class PixelBadges
    {
        public const int Cell = 16;

        static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            ['k'] = new Color32(16, 18, 24, 255),      // outline
            ['y'] = new Color32(255, 214, 92, 255),    // halo gold
            ['Y'] = new Color32(255, 246, 196, 255),
            ['c'] = new Color32(52, 50, 58, 255),      // coal
            ['C'] = new Color32(92, 88, 100, 255),
            ['e'] = new Color32(255, 120, 60, 255),    // coal's ember
            ['r'] = new Color32(232, 64, 72, 255),     // heart
            ['R'] = new Color32(255, 150, 150, 255),
            ['g'] = new Color32(150, 160, 170, 255),   // headstone
            ['G'] = new Color32(200, 208, 214, 255),
            ['m'] = new Color32(96, 168, 80, 255),     // moss
            ['s'] = new Color32(200, 60, 70, 255),     // medal ribbon
            ['S'] = new Color32(70, 110, 200, 255),
            ['w'] = new Color32(115, 242, 255, 255),   // wraith
            ['W'] = new Color32(220, 252, 255, 255),
        };

        // Medal discs by Ghost Duels tier: (face, shine).
        static readonly Dictionary<string, (Color32, Color32)> Tiers = new Dictionary<string, (Color32, Color32)>
        {
            ["bronze"] = (new Color32(196, 120, 70, 255), new Color32(236, 170, 116, 255)),
            ["silver"] = (new Color32(176, 186, 196, 255), new Color32(236, 242, 248, 255)),
            ["gold"] = (new Color32(240, 186, 52, 255), new Color32(255, 236, 140, 255)),
            ["phantom"] = (new Color32(150, 110, 240, 255), new Color32(214, 196, 255, 255)),
        };

        static readonly string[] Halo =
        {
            "............",
            "............",
            "...kkkkkk...",
            ".kkyYYYYykk.",
            "kyy......yyk",
            "kYk......kYk",
            "kyy......yyk",
            ".kkyyyyyykk.",
            "...kkkkkk...",
            "............",
            "............",
            "............",
        };

        static readonly string[] Coal =
        {
            "............",
            "............",
            "....kkkk....",
            "..kkCCCckk..",
            ".kCCcccccck.",
            ".kCcccecccck",
            "kCccceecccck",
            "kccccccccck.",
            ".kccccccck..",
            "..kkkkkkk...",
            "............",
            "............",
        };

        static readonly string[] Heart =
        {
            "............",
            ".kkk...kkk..",
            "kRRrk.krrrk.",
            "kRrrrkrrrrrk",
            "krrrrrrrrrrk",
            "krrrrrrrrrrk",
            ".krrrrrrrrk.",
            "..krrrrrrk..",
            "...krrrrk...",
            "....krrk....",
            ".....kk.....",
            "............",
        };

        static readonly string[] Stone =
        {
            "....kkkk....",
            "..kkGGGGkk..",
            ".kGGGggGGgk.",
            ".kGGgkkgGgk.",
            ".kGGGkkGGgk.",
            ".kGkkkkkkgk.",
            ".kGGGkkGGgk.",
            ".kGGGkkGGgk.",
            ".kGGGGGGGgk.",
            ".kmmGGGGmmk.",
            "kkkkkkkkkkkk",
            "............",
        };

        static readonly string[] Medal =
        {
            "..kkk..kkk..",
            "..kSskksSk..",
            "...kSsSsk...",
            "....kkkk....",
            "...kFFFFk...",
            "..kFHHffFk..",
            "..kFHfffFk..",
            "..kFfffffk..",
            "..kFffffFk..",
            "...kFFFFk...",
            "....kkkk....",
            "............",
        };

        static readonly string[] Wraith =
        {
            "....kkkk....",
            "..kkwwwwkk..",
            ".kwwWWwwwwk.",
            ".kwkkwwkkwk.",
            "kwwkkwwkkwwk",
            "kwwwwwwwwwwk",
            "kwwwwkkwwwwk",
            "kwwwwwwwwwwk",
            "kwwwwwwwwwwk",
            "kwkwwkkwwkwk",
            "kk.kk..kk.kk",
            "............",
        };

        /// <summary>The strip and each cell's sprite name, in order (BadgeNames uses the same names).</summary>
        public static PixelCanvas Atlas(out List<string> names)
        {
            var icons = new List<(string Name, string[] Rows, Dictionary<char, Color32> Colours)>
            {
                ("nice", Halo, Palette),
                ("naughty", Coal, Palette),
                ("onelife", Heart, Palette),
                ("gravekeeper", Stone, Palette),
            };
            foreach (var (tier, (face, shine)) in Tiers)
            {
                var colours = new Dictionary<char, Color32>(Palette) { ['F'] = Darker(face), ['f'] = face, ['H'] = shine };
                icons.Add(("duel_" + tier, Medal, colours));
            }
            icons.Add(("duel_wraith", Wraith, Palette));

            var canvas = new PixelCanvas(Cell * icons.Count, Cell);
            names = new List<string>();
            for (int i = 0; i < icons.Count; i++)
            {
                // Blit draws rows top-down from its origin; 2 px of margin each side inside the cell.
                canvas.Blit(icons[i].Rows, icons[i].Colours, i * Cell + 2, 2);
                names.Add(icons[i].Name);
            }
            return canvas;
        }

        static Color32 Darker(Color32 c) => new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);
    }
}
