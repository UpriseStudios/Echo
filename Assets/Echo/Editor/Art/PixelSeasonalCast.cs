using System.Collections.Generic;
using Echo.Core.Achievements;
using Echo.Services.Iap;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// Seasonal characters (seasons doc, version 2.1). Santa and Rudolph are free, each earned with a Christmas
    /// achievement and kept for good; the Christmas Pack (Elf, Snowman, Gingerbread Runner, Nutcracker) is its own £1.99
    /// product, sold during Christmas and New Year and never part of the Season of Echo bundle. Drawn on the
    /// shared rig, facing right like the rest of the cast.
    /// </summary>
    public static class PixelSeasonalCast
    {
        /// <summary>Earned with a seasonal achievement: Santa and Rudolph.</summary>
        public static readonly PixelCharacter[] Earned = { Santa(), Rudolph() };

        public static readonly PixelPack ChristmasPack = Pack(ProductIds.PackChristmas, "Christmas", Elf(), Snowman(), Gingerbread(), Nutcracker());

        static PixelPack Pack(string productId, string name, params PixelCharacter[] characters)
        {
            foreach (PixelCharacter c in characters) c.ProductId = productId;
            return new PixelPack { ProductId = productId, Name = name, Characters = characters };
        }

        static Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        static PixelCharacter Body(string id, string name, uint torso, uint shade, uint legs, uint boots, uint sleeve, uint hand, uint outline) =>
            new PixelCharacter
            {
                Id = id, Name = name,
                Torso = Hex(torso), TorsoShade = Hex(shade), Legs = Hex(legs), Boots = Hex(boots),
                Sleeve = Hex(sleeve), Hand = Hex(hand), Outline = Hex(outline),
            };

        static PixelCharacter Earn(PixelCharacter c, string achievement, string text)
        {
            c.UnlockAchievement = achievement;
            c.UnlockText = text;
            return c;
        }

        // ---- earned ----

        static PixelCharacter Santa()
        {
            PixelCharacter c = Body("char_santa", "Santa", 0xD8323C, 0xA82028, 0xA82028, 0x1A1418, 0xD8323C, 0xF2F2F2, 0x1A0A0C);
            c.Head = new[]
            {
                "w..rrrr.....",
                "wrrrrrrrr...",
                ".rrrrrrrrrr.",
                "wwwwwwwwwwww",
                ".sssssskssks",
                ".wsssssssps.",
                ".wwwsssswww.",
                ".wwwwwwwwww.",
                "..wwwwwwww..",
                "...wwwwww...",
            };
            c.Chest = new[]
            {
                "..wwww..",
                "...ww...",
                "........",
                "........",
                "kkkyykkk",
                "........",
                "wwwwwwww",
                "........",
            };
            c.Palette = Pal(('r', Hex(0xD8323C)), ('w', Hex(0xF4F4F4)), ('s', Hex(0xF2C8A2)), ('k', Hex(0x161014)),
                ('p', Hex(0xE88A80)), ('y', Hex(0xF2C230)));
            return Earn(c, AchievementIds.TwelveDays, "Christmas: Twelve Days");
        }

        static PixelCharacter Rudolph()
        {
            PixelCharacter c = Body("char_rudolph", "Rudolph", 0x8A5A34, 0x6A4024, 0x6A4024, 0x2A1A10, 0x8A5A34, 0x6A4024, 0x1A0E06);
            c.Head = new[]
            {
                "a..a..a.....",
                "aa.aaa......",
                ".aaa.a......",
                "..bbbbbbb...",
                ".bbbbbbbbb..",
                "bbbbbbbbkbb.",
                ".bbbbbbbbbbb",
                ".bbbbbbcccRR",
                "..bbbbbbccRR",
                "...bbbbbbb..",
            };
            c.Chest = new[]
            {
                "..cccc..",
                ".cccccc.",
                "...yy...",
                ".cccccc.",
                "..cccc..",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('a', Hex(0xC89A5A)), ('b', Hex(0x8A5A34)), ('c', Hex(0xD8B488)), ('k', Hex(0x140C08)),
                ('R', Hex(0xFF3A30)), ('y', Hex(0xF2C230)));
            return Earn(c, AchievementIds.LeadTheWay, "Christmas: Lead the Way");
        }

        // ---- the Christmas Pack ----

        static PixelCharacter Elf()
        {
            PixelCharacter c = Body("char_elf", "Elf", 0x2F9E4A, 0x207434, 0xD8323C, 0x207434, 0x2F9E4A, 0xF2C8A2, 0x0E1A10);
            c.Head = new[]
            {
                "y...........",
                "gg..........",
                ".ggg........",
                "..ggggggg...",
                ".rrrrrrrrr..",
                ".sssssssss..",
                "ssssssskssk.",
                ".ssssssssss.",
                "..sssssspss.",
                "...ssssss...",
            };
            c.Chest = new[]
            {
                "........",
                "...rr...",
                "........",
                "........",
                "kkkkykkk",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('g', Hex(0x2F9E4A)), ('r', Hex(0xD8323C)), ('s', Hex(0xF2C8A2)), ('k', Hex(0x161014)),
                ('p', Hex(0xE88A80)), ('y', Hex(0xF2C230)));
            return c;
        }

        static PixelCharacter Snowman()
        {
            PixelCharacter c = Body("char_snowman", "Snowman", 0xF2F6FA, 0xC8D4E0, 0xDDE6EE, 0x8A949E, 0x6A4A2A, 0x6A4A2A, 0x2A3440);
            c.Head = new[]
            {
                "...kkkkk....",
                "...kkkkk....",
                "...rrrrr....",
                ".kkkkkkkkk..",
                "..wwwwwwww..",
                ".wwwwwkwwkw.",
                ".wwwwwwwwwoo",
                ".wwwwwwwwww.",
                "..wwkwkwkw..",
                "...wwwwww...",
            };
            c.Chest = new[]
            {
                "rrrrrrrr",
                "..rr....",
                "..rr.k..",
                "........",
                ".....k..",
                "........",
                ".....k..",
                "........",
            };
            c.Palette = Pal(('k', Hex(0x1A1C22)), ('r', Hex(0xD8323C)), ('w', Hex(0xF2F6FA)), ('o', Hex(0xF08A24)));
            c.ScarfColor = Hex(0xD8323C);
            c.ScarfWidth = 0.06f;
            c.ScarfAnchor = new Vector2Int(9, 17);
            return c;
        }

        static PixelCharacter Gingerbread()
        {
            PixelCharacter c = Body("char_gingerbread", "Gingerbread Runner", 0xB06A34, 0x8A4E22, 0xB06A34, 0x8A4E22, 0xB06A34, 0xB06A34, 0x3A1E0C);
            c.Head = new[]
            {
                "...gggggg...",
                "..gggggggg..",
                ".gggggggggg.",
                ".ggggggwggw.",
                ".gggggggggg.",
                ".gggggggggg.",
                ".gggggwwwwg.",
                "..gggggggg..",
                "...gggggg...",
                "............",
            };
            c.Chest = new[]
            {
                "........",
                "...r....",
                "........",
                "...y....",
                "........",
                "...r....",
                "w.w.w.w.",
                ".w.w.w.w",
            };
            c.Palette = Pal(('g', Hex(0xB06A34)), ('w', Hex(0xFAF4EA)), ('r', Hex(0xE8323C)), ('y', Hex(0x3FB0E8)));
            return c;
        }

        static PixelCharacter Nutcracker()
        {
            PixelCharacter c = Body("char_nutcracker", "Nutcracker", 0xC8283A, 0x962030, 0x1A1A2A, 0x0E0E14, 0xC8283A, 0xF2F2F2, 0x0E0A0A);
            c.Head = new[]
            {
                "..kkkkkkk...",
                "..kkkykkk...",
                "..kkkkkkk...",
                "..yyyyyyy...",
                ".ssssssskss.",
                ".sssssssssr.",
                ".wwwkkkkkkw.",
                ".wwwwwwwwww.",
                "..wwkkkkww..",
                "...wwwwww...",
            };
            c.Chest = new[]
            {
                "...y.y..",
                "........",
                "...y.y..",
                "........",
                "...y.y..",
                "........",
                "kkkkkkkk",
                "........",
            };
            c.Palette = Pal(('k', Hex(0x14121A)), ('y', Hex(0xF2C230)), ('s', Hex(0xF2C8A2)), ('w', Hex(0xF4F4F4)), ('r', Hex(0xE86A70)));
            return c;
        }
    }
}
