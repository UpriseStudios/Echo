using System.Collections.Generic;
using System.Linq;
using Echo.Game.Cosmetics;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The cast (skins addendum): the hooded runner (greyscale, so colour skins tint it) and the 8 base skins
    /// are free; Pumpkin unlocks after 5 rewarded ads (a user revision); Shadow Self, RGB Glitch, Negative,
    /// Golden Echo and Grim Reaper are earned, never bought. All adds the 32 paid pack characters (PixelPacks).
    /// Grids are drawn facing right; the top row is the top of the sprite.
    /// </summary>
    public static class PixelCharacters
    {
        public const string Hooded = "char_hooded";

        // Declared before All: static fields initialise in textual order, and the hood variants need it.
        static readonly string[] HoodGrid =
        {
            "...hhhhh....",
            "..hhhhhhh...",
            ".hhhhhhhhh..",
            "hhhhhhhhhhh.",
            "Hhhhhhhkkkkh",
            ".hhhhhkekkek",
            ".hhhhhkkkkkh",
            "..hhhhhhhhh.",
            "..Hhhhhhhh..",
            "...HHHHHH...",
        };

        /// <summary>The free, ad and earned cast.</summary>
        public static readonly PixelCharacter[] Base =
        {
            HoodedRunner(), Ninja(), Knight(), Wizard(), Princess(), Cat(), Skeleton(), Astronaut(), Robot(),
            Pumpkin(), ShadowSelf(), RgbGlitch(), Negative(), GoldenEcho(), Reaper(),
        };

        /// <summary>Everyone: the base cast, the pack characters in pack order, the Elementals, then the seasonal cast.</summary>
        public static readonly PixelCharacter[] All =
            Base.Concat(PixelPacks.All.SelectMany(p => p.Characters)).Concat(PixelElementals.All)
                .Concat(PixelSeasonalCast.Earned).Concat(PixelSeasonalCast.ChristmasPack.Characters).ToArray();

        static Dictionary<char, Color32> Pal(params (char ch, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (ch, c) in entries) d[ch] = c;
            return d;
        }

        // The hooded figure in a given palette: the starter runner and its earned variants share one drawing.
        static PixelCharacter Hood(string id, string name, Color32 body, Color32 shade, Color32 legs, Color32 boots,
                                   Color32 hand, Color32 face, Color32 eyes, Color32 outline, Color32 scarf) => new PixelCharacter
        {
            Id = id, Name = name,
            Torso = body, TorsoShade = shade, Legs = legs, Boots = boots, Sleeve = body, Hand = hand, Outline = outline,
            Head = HoodGrid,
            Palette = Pal(('h', body), ('H', shade), ('k', face), ('e', eyes)),
            ScarfColor = scarf, ScarfWidth = 0.09f, ScarfAnchor = new Vector2Int(11, 17),
        };

        static PixelCharacter HoodedRunner()
        {
            PixelCharacter c = Hood(Hooded, "Hooded", Grey(0.95f), Grey(0.72f), Grey(0.58f), Grey(0.3f),
                Grey(0.98f), Grey(0.13f), Grey(1f), Grey(0.08f), Hex(0xFFC948));
            c.Torso = Grey(0.92f);
            c.TorsoShade = Grey(0.7f);
            c.Sleeve = Grey(0.92f);
            c.Tintable = true;
            return c;
        }

        // ---- earned skins (skins addendum): never sold ----

        static PixelCharacter ShadowSelf()
        {
            PixelCharacter c = Hood("char_shadow", "Shadow Self", Hex(0x24242E), Hex(0x16161E), Hex(0x1C1C24), Hex(0x0E0E14),
                Hex(0x2C2C36), Hex(0x050508), Hex(0xBFF6FF), Hex(0x000000), Hex(0x3A3A4A));
            return Earned(c, CosmeticUnlockStat.GhostsAtStart, 15, "Have 15 ghosts active");
        }

        static PixelCharacter RgbGlitch()
        {
            PixelCharacter c = Hood("char_glitch", "RGB Glitch", Grey(0.95f), Grey(0.72f), Grey(0.58f), Grey(0.3f),
                Grey(0.98f), Grey(0.13f), Grey(1f), Grey(0.08f), Hex(0xFF3FD0));
            c.Effect = PixelEffect.Glitch;
            return Earned(c, CosmeticUnlockStat.Distance, 1000, "Reach 1,000 m");
        }

        static PixelCharacter Negative()
        {
            PixelCharacter c = Hood("char_negative", "Negative", Grey(0.95f), Grey(0.72f), Grey(0.58f), Grey(0.3f),
                Grey(0.98f), Grey(0.13f), Grey(1f), Grey(0.08f), Hex(0x0036B7));
            c.Effect = PixelEffect.Negative;
            return Earned(c, CosmeticUnlockStat.GhostsCleared, 15, "Clear 15+ ghosts with a new best");
        }

        static PixelCharacter GoldenEcho()
        {
            PixelCharacter c = Hood("char_golden", "Golden Echo", Hex(0xFFD54A), Hex(0xC9962A), Hex(0xE0B030), Hex(0x8A6A10),
                Hex(0xFFE680), Hex(0x5A3A08), Hex(0xFFFFFF), Hex(0x3A2604), Hex(0xFFF3B0));
            return Earned(c, CosmeticUnlockStat.GhostsCleared, 20, "Clear 20+ ghosts at once");
        }

        static PixelCharacter Earned(PixelCharacter c, CosmeticUnlockStat stat, int target, string text)
        {
            c.UnlockStat = stat;
            c.UnlockTarget = target;
            c.UnlockText = text;
            return c;
        }

        // Unlocked after 5 rewarded ads: a jack-o'-lantern with a glowing carved face and a vine for a scarf.
        static PixelCharacter Pumpkin() => new PixelCharacter
        {
            Id = "char_pumpkin", Name = "Pumpkin", AdsToUnlock = 5,
            Torso = Hex(0x3A2E5A), TorsoShade = Hex(0x2A2042), Legs = Hex(0x2A2042), Boots = Hex(0x1A1424),
            Sleeve = Hex(0x3A2E5A), Hand = Hex(0x3FA34D), Outline = Hex(0x1A0E08),
            Head = new[]
            {
                ".....gg.....",
                "......g.....",
                "..ooOoOoo...",
                ".ooOooOoOoo.",
                "ooOoooOoyoyo",
                "ooOoooOoooOo",
                "ooOoooOyyyyo",
                "ooOoooOyoyoo",
                ".ooOoooOooo.",
                "..ooOoOooo..",
                "....oooo....",
            },
            Chest = new[]
            {
                "..o..o..",
                "..oooo..",
                "........",
                "...g....",
                "........",
                "...g....",
                "gggggggg",
                "........",
            },
            Palette = Pal(('o', Hex(0xF08A24)), ('O', Hex(0xC0601A)), ('g', Hex(0x3FA34D)), ('y', Hex(0xFFE45C))),
            ScarfColor = Hex(0x3FA34D), ScarfWidth = 0.05f, ScarfAnchor = new Vector2Int(11, 17),
        };

        // Earned after 250 deaths (skins addendum): a hooded skull with red eyes, bony feet and a scythe on its back.
        static PixelCharacter Reaper() => new PixelCharacter
        {
            Id = "char_reaper", Name = "Grim Reaper",
            UnlockStat = CosmeticUnlockStat.Deaths, UnlockTarget = 250, UnlockText = "Die 250 times",
            Torso = Hex(0x2A2238), TorsoShade = Hex(0x1A1524), Legs = Hex(0x2A2238), Boots = Hex(0xE8E4D8),
            Sleeve = Hex(0x2A2238), Hand = Hex(0xE8E4D8), Outline = Hex(0x07050C),
            Skirt = true, SkirtColor = Hex(0x2A2238), SkirtShade = Hex(0x1A1524), SkirtBottom = 2,
            Head = new[]
            {
                "...rrrrr....",
                "..rrrrrrr...",
                ".rrrrrrrrr..",
                ".rrrrrsssr..",
                ".rrrrsssssr.",
                ".rrrrsesses.",
                ".rrrrsssssr.",
                ".rrrrssksr..",
                ".rrrrrsssr..",
                "..rrrrrrr...",
                "...RRRRR....",
            },
            Chest = new[]
            {
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "cccccccc",
                "........",
            },
            Back = new[]
            {
                "...bbbbh.",
                ".bbbbbBh.",
                "bbBB...h.",
                "bB.....h.",
                "B......h.",
                ".......h.", ".......h.", ".......h.", ".......h.", ".......h.",
                ".......h.", ".......h.", ".......h.", ".......h.", ".......h.",
                ".......h.", ".......h.", ".......h.", ".......h.", ".......h.",
                ".......h.", ".......h.", ".......h.", ".......h.", ".......h.",
                ".......h.", ".......h.", ".......h.", ".......h.", ".......h.",
            },
            BackAt = new Vector2Int(0, 2),
            Palette = Pal(('r', Hex(0x2A2238)), ('R', Hex(0x1A1524)), ('s', Hex(0xE8E4D8)), ('e', Hex(0xFF3B3B)),
                ('k', Hex(0x0A0A0F)), ('c', Hex(0x8A8396)), ('b', Hex(0xC9CED8)), ('B', Hex(0x6E7686)), ('h', Hex(0x6B4426))),
            ScarfColor = Hex(0x2A2238), ScarfWidth = 0.1f, ScarfAnchor = new Vector2Int(9, 15),
        };

        static PixelCharacter Knight() => new PixelCharacter
        {
            Id = "char_knight", Name = "Knight",
            Torso = Hex(0xB7C0CF), TorsoShade = Hex(0x8792A8), Legs = Hex(0x9AA3B5), Boots = Hex(0x5B6375),
            Sleeve = Hex(0xB7C0CF), Hand = Hex(0x8792A8), Outline = Hex(0x12141C),
            Head = new[]
            {
                ".rrr........",
                "rrrrr.......",
                "..rrrr......",
                "...mmmmm....",
                "..mmmmmmm...",
                ".mmmmmmmmmm.",
                ".mmmmmkkkkm.",
                ".mmmmmmmmmm.",
                ".mmmmmmkmkm.",
                ".Mmmmmmmmmm.",
                "..MMMMMMMM..",
            },
            Chest = new[]
            {
                "........",
                "...rr...",
                "..rrrr..",
                "...rr...",
                "...rr...",
                "........",
                "bbbbbbbb",
                "........",
            },
            Palette = Pal(('r', Hex(0xD8323C)), ('m', Hex(0xC9D1DE)), ('M', Hex(0x8792A8)), ('k', Hex(0x1A1C24)), ('b', Hex(0x6B4426))),
            ScarfColor = Hex(0xB8283A), ScarfWidth = 0.13f, ScarfAnchor = new Vector2Int(10, 16),
        };

        static PixelCharacter Ninja() => new PixelCharacter
        {
            Id = "char_ninja", Name = "Ninja",
            Torso = Hex(0x2B3050), TorsoShade = Hex(0x1C2036), Legs = Hex(0x2B3050), Boots = Hex(0x14172A),
            Sleeve = Hex(0x2B3050), Hand = Hex(0x1C2036), Outline = Hex(0x07080E),
            Head = new[]
            {
                "...nnnnn....",
                "..nnnnnnn...",
                ".nnnnnnnnn..",
                "RRRRRRRRRRR.",
                ".nnnnnssssn.",
                ".nnnnnskssk.",
                ".nnnnnnnnnn.",
                ".nnnnnnnnn..",
                "..NNNNNNN...",
            },
            Chest = new[]
            {
                "........",
                "......RR",
                ".....RR.",
                "....RR..",
                "...RR...",
                "..RR....",
                "RRRRRRRR",
                "........",
            },
            Palette = Pal(('n', Hex(0x2B3050)), ('N', Hex(0x1C2036)), ('R', Hex(0xD8323C)), ('s', Hex(0xF2C29A)), ('k', Hex(0x111111))),
            ScarfColor = Hex(0xD8323C), ScarfWidth = 0.06f, ScarfAnchor = new Vector2Int(6, 22),
        };

        static PixelCharacter Robot() => new PixelCharacter
        {
            Id = "char_robot", Name = "Robot",
            Torso = Hex(0x8FA3B8), TorsoShade = Hex(0x627489), Legs = Hex(0x7A8DA2), Boots = Hex(0x4B5A6B),
            Sleeve = Hex(0x8FA3B8), Hand = Hex(0x627489), Outline = Hex(0x10141C),
            Head = new[]
            {
                ".....r......",
                ".....a......",
                "..bbbbbbbb..",
                ".bbbbbbbbbb.",
                ".bbbvvvvvvb.",
                ".bbbvVvvVvb.",
                ".bbbvvvvvvb.",
                ".bbbbbbbbbb.",
                ".BbbbbbbbbB.",
                "..BBBBBBBB..",
            },
            Chest = new[]
            {
                "........",
                ".gg..yy.",
                ".gg..yy.",
                "........",
                ".dddddd.",
                "........",
                ".dddddd.",
                "........",
            },
            Palette = Pal(('r', Hex(0xFF4040)), ('a', Hex(0xA0A8B0)), ('b', Hex(0x8FA3B8)), ('B', Hex(0x627489)),
                ('v', Hex(0x4BE3FF)), ('V', Hex(0xD8FBFF)), ('g', Hex(0x50E070)), ('y', Hex(0xFFD040)), ('d', Hex(0x627489))),
        };

        static PixelCharacter Astronaut() => new PixelCharacter
        {
            Id = "char_astronaut", Name = "Astronaut",
            Torso = Hex(0xF2F2F2), TorsoShade = Hex(0xBFC4CC), Legs = Hex(0xE2E4E8), Boots = Hex(0x8A9099),
            Sleeve = Hex(0xF2F2F2), Hand = Hex(0xBFC4CC), Outline = Hex(0x1A1E26),
            Head = new[]
            {
                "...wwwwww...",
                "..wwwwwwww..",
                ".wwwwwwwwww.",
                ".wwwwgggggw.",
                "wwwwgGgggggw",
                "wwwwgggggggw",
                "wwwwgggggggw",
                ".wwwwgggggw.",
                ".Wwwwwwwwww.",
                "..WWWWWWWW..",
            },
            Chest = new[]
            {
                "........",
                "........",
                ".oooooo.",
                "........",
                "..b..r..",
                "........",
                "........",
                "........",
            },
            Back = new[]
            {
                "WWWW",
                "WwwW",
                "WwwW",
                "WwwW",
                "WwwW",
                "WooW",
                "WWWW",
            },
            BackAt = new Vector2Int(4, 9),
            Palette = Pal(('w', Hex(0xF2F2F2)), ('W', Hex(0xBFC4CC)), ('g', Hex(0x22407A)), ('G', Hex(0x9CC8FF)),
                ('o', Hex(0xFF8A2A)), ('b', Hex(0x3A7BFF)), ('r', Hex(0xFF4040))),
        };

        static PixelCharacter Cat() => new PixelCharacter
        {
            Id = "char_cat", Name = "Cat",
            Torso = Hex(0xF0A040), TorsoShade = Hex(0xD08030), Legs = Hex(0xF0A040), Boots = Hex(0xFFF4E6),
            Sleeve = Hex(0xF0A040), Hand = Hex(0xFFF4E6), Outline = Hex(0x2A1408),
            Head = new[]
            {
                "..O.....O...",
                "..OO...OO...",
                "..ooooooooo.",
                ".ooOooooOoo.",
                ".oooooooooo.",
                ".oooookoook.",
                ".ooooooooWo.",
                ".oooooooWpW.",
                "..oooooWWW..",
                "...ooooooo..",
            },
            Chest = new[]
            {
                "........",
                "OO......",
                "........",
                "OO....WW",
                "......WW",
                "OO....WW",
                "......WW",
                "........",
            },
            Palette = Pal(('o', Hex(0xF0A040)), ('O', Hex(0xC46A1E)), ('k', Hex(0x1A1A1A)), ('W', Hex(0xFFF4E6)), ('p', Hex(0xFF8FA8))),
            ScarfColor = Hex(0xF0A040), ScarfWidth = 0.08f, ScarfAnchor = new Vector2Int(8, 11),
        };

        static PixelCharacter Skeleton() => new PixelCharacter
        {
            Id = "char_skeleton", Name = "Skeleton",
            Torso = Hex(0x2E2E38), TorsoShade = Hex(0x22222A), Legs = Hex(0xEDE8D8), Boots = Hex(0xD4CEBC),
            Sleeve = Hex(0xEDE8D8), Hand = Hex(0xEDE8D8), Outline = Hex(0x0E0E14),
            Head = new[]
            {
                "...wwwwww...",
                "..wwwwwwww..",
                ".wwwwwwwwww.",
                ".wwwwkkwkkw.",
                ".wwwwkkwkkw.",
                ".wwwwwwkwww.",
                "..wwwwwwwww.",
                "...wkwkwkw..",
                "...wwwwww...",
            },
            Chest = new[]
            {
                "...ww...",
                ".wwwwww.",
                "...ww...",
                ".wwwwww.",
                "...ww...",
                ".wwwwww.",
                "...ww...",
                "..wwww..",
            },
            Palette = Pal(('w', Hex(0xEDE8D8)), ('k', Hex(0x1A1A22))),
        };

        static PixelCharacter Wizard() => new PixelCharacter
        {
            Id = "char_wizard", Name = "Wizard",
            Torso = Hex(0x6A3FA0), TorsoShade = Hex(0x4A2878), Legs = Hex(0x3A2458), Boots = Hex(0x6B4426),
            Sleeve = Hex(0x6A3FA0), Hand = Hex(0xF2C29A), Outline = Hex(0x160A24),
            Skirt = true, SkirtColor = Hex(0x6A3FA0), SkirtShade = Hex(0x4A2878), SkirtBottom = 3,
            Head = new[]
            {
                ".........pp...",
                "........ppp...",
                ".......pppp...",
                "......ppppp...",
                ".....ppppppp..",
                ".....ppyppppp.",
                "....ppppppppp.",
                "pPPPPPPPPPPPPP",
                "...sssssss....",
                "...sssskssk...",
                "...wwwwssss...",
                "...wwwwwwwww..",
                "....wwwwwwww..",
                ".....wwwwww...",
            },
            Chest = new[]
            {
                "..wwww..",
                "...ww...",
                "........",
                "..y.....",
                ".yyy....",
                "..y.....",
                "bbbbbbbb",
                "........",
            },
            Palette = Pal(('p', Hex(0x6A3FA0)), ('P', Hex(0x4A2878)), ('y', Hex(0xFFD84A)), ('s', Hex(0xF2C29A)),
                ('k', Hex(0x1A1A1A)), ('w', Hex(0xF4F4F4)), ('b', Hex(0xC9A040))),
        };

        static PixelCharacter Princess() => new PixelCharacter
        {
            Id = "char_princess", Name = "Princess",
            Torso = Hex(0xF07AB0), TorsoShade = Hex(0xC85A90), Legs = Hex(0xF7CDAA), Boots = Hex(0xC0306A),
            Sleeve = Hex(0xF49AC4), Hand = Hex(0xF7CDAA), Outline = Hex(0x2A0E1C),
            Skirt = true, SkirtColor = Hex(0xF07AB0), SkirtShade = Hex(0xC85A90), SkirtBottom = 1,
            Head = new[]
            {
                "...y.y.y....",
                "...yyyyy....",
                "..hhhhhhh...",
                ".hhhhhhhhh..",
                ".hhhhhssssh.",
                ".hhhhhskssk.",
                ".hhhhhssssh.",
                ".hhhhhsspsh.",
                ".hhhhhhhh...",
                "hhhhhhh.....",
                "hhhhh.......",
            },
            Chest = new[]
            {
                "..wwww..",
                "...ww...",
                "........",
                "........",
                "........",
                "........",
                "yyyyyyyy",
                "........",
            },
            Palette = Pal(('y', Hex(0xFFD040)), ('h', Hex(0xF8CF60)), ('s', Hex(0xF7CDAA)), ('k', Hex(0x2A1A1A)),
                ('p', Hex(0xE8607A)), ('w', Hex(0xFFFFFF))),
            ScarfColor = Hex(0xF8CF60), ScarfWidth = 0.1f, ScarfAnchor = new Vector2Int(6, 21),
        };
    }
}
