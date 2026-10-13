using System.Collections.Generic;
using Echo.Services.Iap;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The paid skin packs (user revision: sold at launch): eight packs of four characters, each pack one product,
    /// all of them also unlocked by the Season of Echo bundle. Cosmetic only, same hitbox. Grids are drawn facing
    /// right; the top row is the top of the sprite.
    /// </summary>
    public static class PixelPacks
    {
        public const string Season = ProductIds.SeasonOfEcho;

        public static readonly PixelPack[] All =
        {
            Pack(ProductIds.PackUndead, "Undead", Lich(), Vampire(), Mummy(), Zombie()),
            Pack(ProductIds.PackDungeon, "Dungeon", Barbarian(), Rogue(), Paladin(), Goblin()),
            Pack(ProductIds.PackCritters, "Critters", Fox(), Frog(), Raccoon(), Bunny()),
            Pack(ProductIds.PackSpace, "Space", Alien(), SpaceMarine(), RetroAndroid(), CosmicExplorer()),
            Pack(ProductIds.PackRetro, "Retro", ArcadeHero(), CrtHead(), CartridgeKid(), PixelBlock()),
            Pack(ProductIds.PackRoyal, "Royal", King(), Queen(), Jester(), RoyalGuard()),
            Pack(ProductIds.PackMonsters, "Monsters", Werewolf(), StitchedMonster(), Banshee(), BogBeast()),
            Pack(ProductIds.PackOddballs, "Oddballs", TrafficCone(), Banana(), Hotdog(), Gnome()),
        };

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

        static PixelCharacter Robe(PixelCharacter c, uint color, uint shade, int bottom)
        {
            c.Skirt = true;
            c.SkirtColor = Hex(color);
            c.SkirtShade = Hex(shade);
            c.SkirtBottom = bottom;
            return c;
        }

        static PixelCharacter Scarf(PixelCharacter c, uint color, float width, int x, int y)
        {
            c.ScarfColor = Hex(color);
            c.ScarfWidth = width;
            c.ScarfAnchor = new Vector2Int(x, y);
            return c;
        }

        // ---- Undead ----

        static PixelCharacter Lich()
        {
            PixelCharacter c = Robe(Body("char_lich", "Lich", 0x3B2A5A, 0x271B40, 0x271B40, 0x1A1228, 0x3B2A5A, 0xE8E4D8, 0x0C0814),
                0x3B2A5A, 0x271B40, 2);
            c.Head = new[]
            {
                "..y..y..y...",
                "..yyyyyyyy..",
                "..yryyyryy..",
                ".wwwwwwwwww.",
                ".wwwwwccwcc.",
                ".wwwwwkkwkk.",
                ".wwwwwwwkww.",
                "..wwwwwwww..",
                "...wkwkwkw..",
                "...wwwwww...",
            };
            c.Chest = new[]
            {
                "...yy...",
                "...yy...",
                "...cc...",
                "........",
                "........",
                "........",
                "yyyyyyyy",
                "........",
            };
            c.Palette = Pal(('y', Hex(0xE8B830)), ('r', Hex(0xD8323C)), ('w', Hex(0xE8E4D8)), ('c', Hex(0x5CF6FF)), ('k', Hex(0x14101C)));
            return Scarf(c, 0x5CF6FF, 0.05f, 9, 15);
        }

        static PixelCharacter Vampire()
        {
            PixelCharacter c = Body("char_vampire", "Vampire", 0x1E1A26, 0x121018, 0x1E1A26, 0x0A080E, 0x1E1A26, 0xE8E0F0, 0x06040A);
            c.Head = new[]
            {
                "...kkkkkk...",
                "..kkkkkkkk..",
                ".kkkkkkkkkk.",
                ".kkkkkkpkkp.",
                ".kkkkpppppp.",
                ".kkkkpprppr.",
                ".kkkkpppppp.",
                "..kkkpppwpw.",
                "..RRRppppp..",
                "..RRRRRRRR..",
            };
            c.Chest = new[]
            {
                "..RwwR..",
                "...ww...",
                "..rrrr..",
                "..rrrr..",
                "..rrrr..",
                "..rrrr..",
                "kkkkkkkk",
                "........",
            };
            c.Palette = Pal(('k', Hex(0x1A1424)), ('p', Hex(0xE8E0F0)), ('r', Hex(0xB01828)), ('w', Hex(0xFFFFFF)), ('R', Hex(0x8A1020)));
            return Scarf(c, 0x8A1020, 0.15f, 9, 16);
        }

        static PixelCharacter Mummy()
        {
            PixelCharacter c = Body("char_mummy", "Mummy", 0xE6DCC0, 0xB8AA88, 0xD8CCAE, 0xB8AA88, 0xE6DCC0, 0xD8CCAE, 0x2A2014);
            c.Head = new[]
            {
                "...bbbbbb...",
                "..bBbbbBbb..",
                ".bbbbBbbbbb.",
                ".BbbbbbBbbB.",
                ".bbbbbbkkbb.",
                ".bbBbbbkgbB.",
                ".bbbbbbbbbb.",
                ".bBbbbBbbbb.",
                "..bbbbbbBb..",
                "...BBBBBB...",
            };
            c.Chest = new[]
            {
                "BBbbbbbb",
                "bbbbBBbb",
                "bBBbbbbb",
                "bbbbbBBb",
                "BBbbbbbb",
                "bbbBBbbb",
                "bbbbbbBB",
                "bBBbbbbb",
            };
            c.Palette = Pal(('b', Hex(0xE6DCC0)), ('B', Hex(0xB8AA88)), ('k', Hex(0x1A140C)), ('g', Hex(0x9CFF5A)));
            return Scarf(c, 0xE6DCC0, 0.05f, 10, 15);
        }

        static PixelCharacter Zombie()
        {
            PixelCharacter c = Body("char_zombie", "Zombie", 0x4A6A9A, 0x34507A, 0x3A3A48, 0x2A2A30, 0x4A6A9A, 0x7FB069, 0x10180C);
            c.Head = new[]
            {
                "..h.hh.h....",
                "..hhhhhhh...",
                ".hhhhhhhhh..",
                ".hhhggggggg.",
                ".hhgggkgggk.",
                ".hhggggggrg.",
                ".hggggggGgg.",
                "..gggKKKKgg.",
                "..ggggggggg.",
                "...GGGGGG...",
            };
            c.Chest = new[]
            {
                "........",
                "........",
                "....gg..",
                "...ggg..",
                "....g...",
                "..g.....",
                "kkkkkkkk",
                "..g..g..",
            };
            c.Palette = Pal(('h', Hex(0x3A3020)), ('g', Hex(0x7FB069)), ('G', Hex(0x5A8A4A)), ('k', Hex(0x1A1A12)),
                ('r', Hex(0xD84040)), ('K', Hex(0x3A1A1A)));
            return c;
        }

        // ---- Dungeon ----

        static PixelCharacter Barbarian()
        {
            PixelCharacter c = Body("char_barbarian", "Barbarian", 0xE0A880, 0xC08860, 0x6B4426, 0x3A2414, 0xE0A880, 0xE0A880, 0x1E1008);
            c.Head = new[]
            {
                "W.........W.",
                "WW.......WW.",
                ".WWmmmmmWW..",
                "..mmmmmmmm..",
                "..mMmmmmmMm.",
                "..ssssskssk.",
                "..ssssssssss",
                "..rrssssssr.",
                "..rrrrrrrrr.",
                "...rrrrrrr..",
                "....rrrrr...",
            };
            c.Chest = new[]
            {
                "......bb",
                ".....bb.",
                "....bb..",
                "...bb...",
                "..bb....",
                ".bb.....",
                "bbbbbbbb",
                "...yy...",
            };
            c.Back = new[]
            {
                "..ggg..",
                ".gGGGg.",
                "gGGGGGg",
                ".gGGGg.",
                "..ghg..",
                "...h...",
                "...h...",
                "...h...",
                "...h...",
            };
            c.BackAt = new Vector2Int(2, 14);
            c.Palette = Pal(('W', Hex(0xF2EAD8)), ('m', Hex(0x9AA3B5)), ('M', Hex(0x6B7488)), ('s', Hex(0xE0A880)), ('k', Hex(0x1A1A1A)),
                ('r', Hex(0xC8501E)), ('b', Hex(0x6B4426)), ('y', Hex(0xE8B830)), ('g', Hex(0xC9CED8)), ('G', Hex(0x8A93A6)), ('h', Hex(0x5A3A1E)));
            return Scarf(c, 0xC8501E, 0.07f, 9, 21);
        }

        static PixelCharacter Rogue()
        {
            PixelCharacter c = Body("char_rogue", "Rogue", 0x3E5A34, 0x2C4224, 0x3A3026, 0x241C14, 0x3E5A34, 0x2C2418, 0x0A0E08);
            c.Head = new[]
            {
                "...hhhhh....",
                "..hhhhhhh...",
                ".hhhhhhhhh..",
                "hhhhhhhhhhh.",
                "Hhhhhhhssssh",
                ".hhhhhskssk.",
                ".hhhhhmmmmmh",
                "..hhhhmmmmm.",
                "..Hhhhhhhh..",
                "...HHHHHH...",
            };
            c.Chest = new[]
            {
                "........",
                "b.......",
                ".b......",
                "..b.....",
                "...b....",
                "....b...",
                "bbbbbbbb",
                "....y...",
            };
            c.Back = new[]
            {
                "....w",
                "...w.",
                "..w..",
                ".h...",
                "h....",
            };
            c.BackAt = new Vector2Int(3, 15);
            c.Palette = Pal(('h', Hex(0x3E5A34)), ('H', Hex(0x2C4224)), ('s', Hex(0xE0B090)), ('k', Hex(0x1A1A1A)), ('m', Hex(0x1E2418)),
                ('b', Hex(0x6B4426)), ('y', Hex(0xC9A040)), ('w', Hex(0xD8DDE6)));
            return Scarf(c, 0x2C4224, 0.1f, 9, 16);
        }

        static PixelCharacter Paladin()
        {
            PixelCharacter c = Body("char_paladin", "Paladin", 0x2E5AA8, 0x1E4080, 0xC9D1DE, 0x8792A8, 0xC9D1DE, 0x8792A8, 0x0C1024);
            c.Head = new[]
            {
                "....yyy.....",
                "...mmmmmm...",
                "..mmmmmmmm..",
                ".mmmmmmmmmm.",
                ".mmmmmmkkkm.",
                ".mmmmmmmkmm.",
                ".mmmmmmmkmm.",
                ".mmmmmmkkkm.",
                ".MmmmmmmmmM.",
                "..MMMMMMMM..",
            };
            c.Chest = new[]
            {
                "...yy...",
                "...yy...",
                ".yyyyyy.",
                "...yy...",
                "...yy...",
                "...yy...",
                "yyyyyyyy",
                "........",
            };
            c.Palette = Pal(('y', Hex(0xFFD84A)), ('m', Hex(0xE0E6EE)), ('M', Hex(0x9AA3B5)), ('k', Hex(0x1A1C24)));
            return Scarf(c, 0x2E5AA8, 0.15f, 9, 16);
        }

        static PixelCharacter Goblin()
        {
            PixelCharacter c = Body("char_goblin", "Goblin", 0x7A5A34, 0x5A4024, 0x5A4024, 0x3A2814, 0x7A5A34, 0x6FAE3A, 0x0E1408);
            c.Head = new[]
            {
                "............",
                "............",
                "....gggg....",
                "G..gggggg..G",
                "GgggggggggGg",
                ".ggggggyyggy",
                ".gggggggkggk",
                "..ggggggggn.",
                "..gggggwgwg.",
                "...GGGGGGG..",
            };
            c.Chest = new[]
            {
                "........",
                "........",
                "..bbbb..",
                "........",
                "........",
                "........",
                "kkkkkkkk",
                "........",
            };
            c.Palette = Pal(('g', Hex(0x6FAE3A)), ('G', Hex(0x4E8A2A)), ('y', Hex(0xFFE45C)), ('k', Hex(0x1A1A0A)), ('n', Hex(0x4E8A2A)),
                ('w', Hex(0xF4F0D8)), ('b', Hex(0xC9A040)));
            return c;
        }

        // ---- Critters ----

        static PixelCharacter Fox()
        {
            PixelCharacter c = Body("char_fox", "Fox", 0xE8742A, 0xC0581A, 0x3A2A22, 0x1E1612, 0xE8742A, 0x3A2A22, 0x2A1006);
            c.Head = new[]
            {
                "..o......o..",
                "..oO....Oo..",
                "..ooooooooo.",
                ".ooooooooooo",
                ".oooooookooo",
                ".ooooooowwww",
                ".oooooowwwwk",
                "..oooowwwww.",
                "...ooowww...",
            };
            c.Chest = new[]
            {
                "....ww..",
                "....www.",
                ".....ww.",
                ".....ww.",
                "......w.",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('o', Hex(0xE8742A)), ('O', Hex(0x3A2A22)), ('k', Hex(0x1A1A1A)), ('w', Hex(0xFFF4E6)));
            return Scarf(c, 0xE8742A, 0.14f, 8, 11);
        }

        static PixelCharacter Frog()
        {
            PixelCharacter c = Body("char_frog", "Frog", 0x5AB84A, 0x3E8E34, 0x5AB84A, 0x3E8E34, 0x5AB84A, 0x5AB84A, 0x0E2008);
            c.Head = new[]
            {
                "...www..www.",
                "..wwkww.wkw.",
                "..wwkwwgwkw.",
                ".gggwwgggwg.",
                ".gggggggggg.",
                ".ggggggggggg",
                ".gggggrrrrrg",
                ".gggggggggg.",
                "..GGGGGGGG..",
            };
            c.Chest = new[]
            {
                "........",
                "..yyyy..",
                ".yyyyyy.",
                ".yyyyyy.",
                ".yyyyyy.",
                "..yyyy..",
                "........",
                "........",
            };
            c.Palette = Pal(('w', Hex(0xF4F8E8)), ('k', Hex(0x101010)), ('g', Hex(0x5AB84A)), ('G', Hex(0x3E8E34)), ('r', Hex(0xD85A6A)),
                ('y', Hex(0xD8E890)));
            return c;
        }

        static PixelCharacter Raccoon()
        {
            PixelCharacter c = Body("char_raccoon", "Raccoon", 0x8A8A92, 0x6A6A72, 0x5A5A62, 0x2A2A30, 0x8A8A92, 0x2A2A30, 0x101014);
            c.Head = new[]
            {
                "..G......G..",
                "..GG....GG..",
                "..ggggggggg.",
                ".gggggggggg.",
                ".gggkkkkkkkk",
                ".ggkkwkkwkkk",
                ".gggggwwwwwk",
                "..ggggwwwwn.",
                "...gggwww...",
            };
            c.Chest = new[]
            {
                "....ww..",
                "....www.",
                ".....ww.",
                "........",
                "........",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('g', Hex(0x8A8A92)), ('G', Hex(0x4A4A52)), ('k', Hex(0x1E1E24)), ('w', Hex(0xE8E8EC)), ('n', Hex(0x101010)));
            return Scarf(c, 0x6A6A72, 0.13f, 8, 11);
        }

        static PixelCharacter Bunny()
        {
            PixelCharacter c = Body("char_bunny", "Bunny", 0xF4F2F6, 0xD0CCD8, 0xF4F2F6, 0xD0CCD8, 0xF4F2F6, 0xF4F2F6, 0x2A2030);
            c.Head = new[]
            {
                "....ww.ww...",
                "....wp.wp...",
                "....wp.wp...",
                "....wp.wp...",
                "....ww.ww...",
                "...wwwwwww..",
                "..wwwwwwwww.",
                "..wwwwwkwww.",
                "..wwwwwwwwp.",
                "..wwwwwwwwW.",
                "...wwwwwW...",
            };
            c.Palette = Pal(('w', Hex(0xF4F2F6)), ('W', Hex(0xFFFFFF)), ('p', Hex(0xFF9AB8)), ('k', Hex(0x1A1420)));
            return c;
        }

        // ---- Space ----

        static PixelCharacter Alien()
        {
            PixelCharacter c = Body("char_alien", "Alien", 0x8AE070, 0x62B850, 0x8AE070, 0x62B850, 0x8AE070, 0x8AE070, 0x0C1E08);
            c.Head = new[]
            {
                "...a.....a..",
                "...a....a...",
                "....a..a....",
                "...gggggg...",
                "..gggggggg..",
                ".gggggggggg.",
                ".ggggkkkgkk.",
                ".ggggkkkgkk.",
                "..gggggggg..",
                "...ggggGg...",
                "....GGGG....",
            };
            c.Chest = new[]
            {
                "........",
                "........",
                "...pp...",
                "..pppp..",
                "...pp...",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('a', Hex(0x8AE070)), ('g', Hex(0x8AE070)), ('G', Hex(0x62B850)), ('k', Hex(0x0A0A14)), ('p', Hex(0xC060FF)));
            return c;
        }

        static PixelCharacter SpaceMarine()
        {
            PixelCharacter c = Body("char_marine", "Space Marine", 0x5A6E3A, 0x44542A, 0x4A5A30, 0x2A3418, 0x5A6E3A, 0x44542A, 0x0C1006);
            c.Head = new[]
            {
                "...mmmmmm...",
                "..mmmmmmmm..",
                ".mmmmmmmmmm.",
                ".mmmmmooooo.",
                "mmmmmoOoooom",
                "mmmmmooooooM",
                ".mmmmmmmmmm.",
                ".MmmmmmmmmM.",
                "..MMMMMMMM..",
            };
            c.Chest = new[]
            {
                "........",
                ".yy.....",
                "........",
                "..dddd..",
                "........",
                "..dddd..",
                "........",
                "........",
            };
            c.Back = new[]
            {
                "MMMMM",
                "MmmmM",
                "MmmmM",
                "MmmmM",
                "MMMMM",
            };
            c.BackAt = new Vector2Int(3, 10);
            c.Palette = Pal(('m', Hex(0x5A6E3A)), ('M', Hex(0x3A4824)), ('o', Hex(0xFF9A2A)), ('O', Hex(0xFFE0A0)), ('y', Hex(0xFFD040)),
                ('d', Hex(0x3A4824)));
            return c;
        }

        static PixelCharacter RetroAndroid()
        {
            PixelCharacter c = Body("char_android", "Retro Android", 0xC8CED6, 0x9AA2AE, 0xB0B8C2, 0x6A7280, 0xC8CED6, 0x9AA2AE, 0x14181E);
            c.Head = new[]
            {
                ".....rr.....",
                "......a.....",
                "...ssssss...",
                "..ssssssss..",
                "a.ssssssssa.",
                "aassrrrrrssa",
                "a.ssrRrrRsa.",
                "..ssssssss..",
                "..sgsgsgss..",
                "..ssssssss..",
                "...SSSSSS...",
            };
            c.Chest = new[]
            {
                "........",
                ".rr..bb.",
                ".rr..bb.",
                "........",
                ".gggggg.",
                ".g.gg.g.",
                "........",
                "........",
            };
            c.Palette = Pal(('r', Hex(0xFF3A3A)), ('R', Hex(0xFFC0C0)), ('a', Hex(0x8A929E)), ('s', Hex(0xC8CED6)), ('S', Hex(0x9AA2AE)),
                ('g', Hex(0x4A525E)), ('b', Hex(0x3A9AFF)));
            return c;
        }

        static PixelCharacter CosmicExplorer()
        {
            PixelCharacter c = Body("char_cosmic", "Cosmic Explorer", 0x6A3FC0, 0x4A2898, 0x5A34A8, 0x2A1858, 0x6A3FC0, 0xC0B0FF, 0x0E0624);
            c.Head = new[]
            {
                "...gggggg...",
                "..gGgggggg..",
                ".gGggggggggg",
                ".gggsssssggg",
                "ggggsskssksg",
                "ggggssssssgg",
                "ggggsssppsgg",
                ".gggggggggg.",
                ".gggggggggg.",
                "..cccccccc..",
            };
            c.Chest = new[]
            {
                "........",
                "..y.....",
                ".yyy....",
                "..y..y..",
                ".....y..",
                "...y....",
                "cccccccc",
                "........",
            };
            c.Palette = Pal(('g', Hex(0xB8E0FF)), ('G', Hex(0xFFFFFF)), ('s', Hex(0xF2C29A)), ('k', Hex(0x1A1A1A)), ('p', Hex(0xE8607A)),
                ('c', Hex(0xFFD84A)), ('y', Hex(0xFFF3A0)));
            return Scarf(c, 0x3AE0FF, 0.06f, 9, 16);
        }

        // ---- Retro ----

        static PixelCharacter ArcadeHero()
        {
            PixelCharacter c = Body("char_arcade", "Arcade Hero", 0x2A6AD8, 0x1A4AA8, 0x2A2A3A, 0xD83A3A, 0x2A6AD8, 0xF2C29A, 0x0A0E1E);
            c.Head = new[]
            {
                "..k.k.k.....",
                "..kkkkkkk...",
                ".kkkkkkkkk..",
                ".rrrrrrrrrr.",
                ".kkkksssssk.",
                ".kkkssksskk.",
                ".kkksssssss.",
                "..kkssssms..",
                "...sssssss..",
            };
            c.Chest = new[]
            {
                "........",
                "...yy...",
                "..yyyy..",
                "yyyyyyyy",
                ".yyyyyy.",
                "..y..y..",
                "kkkkkkkk",
                "........",
            };
            c.Palette = Pal(('k', Hex(0x1A1A24)), ('r', Hex(0xD83A3A)), ('s', Hex(0xF2C29A)), ('m', Hex(0xB86A5A)), ('y', Hex(0xFFE04A)));
            return Scarf(c, 0xD83A3A, 0.06f, 6, 23);
        }

        static PixelCharacter CrtHead()
        {
            PixelCharacter c = Body("char_crt", "CRT Head", 0x3A3A44, 0x2A2A32, 0x3A3A44, 0x1E1E24, 0x3A3A44, 0xD8D0C0, 0x0A0A0E);
            c.Head = new[]
            {
                "......a.a...",
                ".......a....",
                ".bbbbbbbbbbb",
                ".bdddddddddb",
                ".bdgggggggdb",
                ".bdgGggGggdb",
                ".bdgggggggdb",
                ".bdgGgggGgdb",
                ".bdggGGGggdb",
                ".bdddddddddb",
                ".bbbbbbbrbyb",
                "..BBBBBBBBB.",
            };
            c.Palette = Pal(('a', Hex(0x8A8A92)), ('b', Hex(0xD8D0C0)), ('B', Hex(0xA8A090)), ('d', Hex(0x2A2A30)), ('g', Hex(0x1A5A2A)),
                ('G', Hex(0x6CFF8A)), ('r', Hex(0xFF4040)), ('y', Hex(0x40C040)));
            return c;
        }

        static PixelCharacter CartridgeKid()
        {
            PixelCharacter c = Body("char_cartridge", "Cartridge Kid", 0xE84A6A, 0xC03050, 0x3A3A48, 0x2A2A30, 0xE84A6A, 0xF2C29A, 0x14080C);
            c.Head = new[]
            {
                "..gggggggg..",
                "..gGgGgGgg..",
                "..gggggggg..",
                "..glllllllg.",
                "..glllllllg.",
                "..gllklklg..",
                "..glllllllg.",
                "..gllmmmllg.",
                "..glllllllg.",
                "..gggggggg..",
                "..gGGGGGGg..",
            };
            c.Palette = Pal(('g', Hex(0x8A8A96)), ('G', Hex(0x5A5A66)), ('l', Hex(0xFFE87A)), ('k', Hex(0x1A1A1A)), ('m', Hex(0xD8506A)));
            return c;
        }

        static PixelCharacter PixelBlock()
        {
            PixelCharacter c = Body("char_block", "Pixel Block", 0x3AC8E8, 0x2A98B8, 0x2A98B8, 0x1A6A88, 0x3AC8E8, 0xFF5AC8, 0x081820);
            c.Head = new[]
            {
                ".pppppppppp.",
                ".pPPPPPPPPp.",
                ".pPppppppPp.",
                ".pPppkpkpPp.",
                ".pPppppppPp.",
                ".pPpkpppkPp.",
                ".pPppkkkpPp.",
                ".pPppppppPp.",
                ".pPPPPPPPPp.",
                ".pppppppppp.",
            };
            c.Chest = new[]
            {
                "c.c.c.c.",
                ".c.c.c.c",
                "c.c.c.c.",
                ".c.c.c.c",
                "c.c.c.c.",
                ".c.c.c.c",
                "c.c.c.c.",
                ".c.c.c.c",
            };
            c.Palette = Pal(('p', Hex(0xFF5AC8)), ('P', Hex(0xFFA8E4)), ('k', Hex(0x2A0A20)), ('c', Hex(0x2A98B8)));
            return c;
        }

        // ---- Royal ----

        static PixelCharacter King()
        {
            PixelCharacter c = Robe(Body("char_king", "King", 0xB82838, 0x8A1828, 0x6A2030, 0x3A1418, 0xB82838, 0xF2C29A, 0x1A0608),
                0xB82838, 0x8A1828, 2);
            c.Head = new[]
            {
                "..y.y.y.y...",
                "..yyyyyyy...",
                "..yrybyry...",
                "..yyyyyyy...",
                ".sssssssss..",
                ".ssssskssk..",
                ".sssssssss..",
                ".wwwssssww..",
                ".wwwwwwwww..",
                "..wwwwwwww..",
                "...wwwwww...",
                "....www.....",
            };
            c.Chest = new[]
            {
                "wwwwwwww",
                "w.k..k.w",
                "........",
                "........",
                "........",
                "........",
                "yyyyyyyy",
                "........",
            };
            c.Palette = Pal(('y', Hex(0xFFD040)), ('r', Hex(0xE8303A)), ('b', Hex(0x3A7BFF)), ('s', Hex(0xF2C29A)), ('k', Hex(0x1A1A1A)),
                ('w', Hex(0xF4F4F4)));
            return Scarf(c, 0xB82838, 0.15f, 9, 16);
        }

        static PixelCharacter Queen()
        {
            PixelCharacter c = Robe(Body("char_queen", "Queen", 0x7A3AB8, 0x5A2490, 0xF2C29A, 0x5A2490, 0x7A3AB8, 0xF2C29A, 0x14061E),
                0x7A3AB8, 0x5A2490, 1);
            c.Head = new[]
            {
                "...y.y.y....",
                "...yyyyy....",
                "...ypygy....",
                "..hhhhhhh...",
                ".hhhhhhhhh..",
                ".hhhhhssssh.",
                ".hhhhhskssk.",
                ".hhhhhssssh.",
                ".hhhhhssrs..",
                ".hhhhhhhh...",
                ".hhhhhh.....",
                "hhhhh.......",
            };
            c.Chest = new[]
            {
                "..wwww..",
                "...pp...",
                "........",
                "........",
                "........",
                "........",
                "yyyyyyyy",
                "........",
            };
            c.Palette = Pal(('y', Hex(0xFFD040)), ('p', Hex(0xE8303A)), ('g', Hex(0x3AD87A)), ('h', Hex(0x5A3020)), ('s', Hex(0xF2C29A)),
                ('k', Hex(0x1A1A1A)), ('r', Hex(0xD84060)), ('w', Hex(0xFFFFFF)));
            return Scarf(c, 0x5A3020, 0.1f, 6, 21);
        }

        static PixelCharacter Jester()
        {
            PixelCharacter c = Body("char_jester", "Jester", 0xD83A3A, 0xA82A2A, 0xFFD040, 0x3A2A6A, 0xFFD040, 0xFFFFFF, 0x1A0A0A);
            c.Head = new[]
            {
                "b.........b.",
                "rr.......yy.",
                ".rr.....yy..",
                "..rrr.yyy...",
                "...rrryyy...",
                "..wwwwwwww..",
                "..wwwwkwwk..",
                "..wwwwwwww..",
                "..wwwwrrrw..",
                "...wwwwww...",
            };
            c.Chest = new[]
            {
                "yyyy....",
                "yyy..r..",
                "yy..rrr.",
                "y..rrrrr",
                "y...rrr.",
                "yy...r..",
                "yyy.....",
                "yyyy....",
            };
            c.Palette = Pal(('b', Hex(0xFFE87A)), ('r', Hex(0xD83A3A)), ('y', Hex(0xFFD040)), ('w', Hex(0xF4F4F4)), ('k', Hex(0x1A1A1A)));
            return c;
        }

        static PixelCharacter RoyalGuard()
        {
            PixelCharacter c = Body("char_guard", "Royal Guard", 0xD82A2A, 0xA81A1A, 0x1A1A24, 0x0A0A10, 0xD82A2A, 0xF4F4F4, 0x0A0406);
            c.Head = new[]
            {
                "...kkkkkk...",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkkkkkk..",
                "..kkkssssk..",
                "..kkssksskk.",
                "...kssssss..",
                "...ysssssy..",
                "....yssss...",
            };
            c.Chest = new[]
            {
                "...yy...",
                "........",
                "...yy...",
                "........",
                "...yy...",
                "........",
                "wwwwwwww",
                "........",
            };
            c.Palette = Pal(('k', Hex(0x14141A)), ('s', Hex(0xF2C29A)), ('y', Hex(0xFFD040)), ('w', Hex(0xF4F4F4)));
            return c;
        }

        // ---- Monsters ----

        static PixelCharacter Werewolf()
        {
            PixelCharacter c = Body("char_werewolf", "Werewolf", 0x5A6A8A, 0x44506A, 0x5A4A3A, 0x6A5A48, 0x6A5A48, 0x6A5A48, 0x100C08);
            c.Head = new[]
            {
                "..f......f..",
                "..ff....ff..",
                "..ffffffff..",
                ".ffffffffff.",
                ".fffffffyff.",
                ".fffffffffff",
                ".ffffffFFFFk",
                ".ffffffwFwF.",
                "..fffffFFF..",
                "...FFFFFF...",
            };
            c.Chest = new[]
            {
                "....ff..",
                "...fff..",
                "....f...",
                "........",
                ".f......",
                "........",
                "........",
                "........",
            };
            c.Palette = Pal(('f', Hex(0x6A5A48)), ('F', Hex(0x8A7A66)), ('y', Hex(0xFFD040)), ('k', Hex(0x1A1410)), ('w', Hex(0xF4F0E0)));
            return Scarf(c, 0x6A5A48, 0.1f, 8, 11);
        }

        static PixelCharacter StitchedMonster()
        {
            PixelCharacter c = Body("char_stitched", "Stitched Monster", 0x3A3A44, 0x2A2A32, 0x2A2A32, 0x1A1A1E, 0x3A3A44, 0x8AC07A, 0x080C06);
            c.Head = new[]
            {
                ".kkkkkkkkk..",
                ".kkkkkkkkkk.",
                ".kkkkkkkkkk.",
                ".gggggggggg.",
                ".gkgkgkgkgg.",
                ".gggggggggg.",
                ".ggggggkggk.",
                ".gggggggggg.",
                ".ggggggKKKg.",
                "bggggggggggb",
                "..GGGGGGGG..",
            };
            c.Palette = Pal(('k', Hex(0x14141A)), ('g', Hex(0x8AC07A)), ('G', Hex(0x6A9A5A)), ('K', Hex(0x2A3A24)), ('b', Hex(0xA8AEB8)));
            return c;
        }

        static PixelCharacter Banshee()
        {
            PixelCharacter c = Robe(Body("char_banshee", "Banshee", 0xC8D8E8, 0x98AEC4, 0xC8D8E8, 0xC8D8E8, 0xC8D8E8, 0xE8F0F8, 0x182030),
                0xC8D8E8, 0x98AEC4, 0);
            c.Head = new[]
            {
                "...hhhhhh...",
                "..hhhhhhhh..",
                ".hhhhhhhhhh.",
                ".hhhhhppppp.",
                "hhhhhpkpppk.",
                "hhhhhpkpppk.",
                "hhhhhppppp..",
                "hhhhhpppkp..",
                "hhhhhppkk...",
                "hhhhhhh.....",
                "hhhhh.......",
                "hhh.........",
            };
            c.Palette = Pal(('h', Hex(0xF4F8FF)), ('p', Hex(0xD8E4F0)), ('k', Hex(0x1A2438)));
            return Scarf(c, 0xF4F8FF, 0.12f, 6, 22);
        }

        static PixelCharacter BogBeast()
        {
            PixelCharacter c = Body("char_bog", "Bog Beast", 0x3A6A3A, 0x2A502A, 0x3A6A3A, 0x2A502A, 0x3A6A3A, 0x4A8A3A, 0x081408);
            c.Head = new[]
            {
                "..m.mm..m...",
                "..mmmmmmm...",
                ".mmMmmmmMm..",
                ".mmmmmmmmmm.",
                ".mmMmmmyymy.",
                ".mmmmmmmmmm.",
                ".mMmmmmmmmm.",
                ".m.mmmKKKm..",
                ".m.mmmmmmm..",
                "...m.m.m.m..",
            };
            c.Chest = new[]
            {
                "..M.....",
                ".....M..",
                "...M....",
                "M.....M.",
                "....M...",
                ".M......",
                "....M..M",
                "..M.....",
            };
            c.Palette = Pal(('m', Hex(0x3A6A3A)), ('M', Hex(0x6A9A3A)), ('y', Hex(0xE8FF5A)), ('K', Hex(0x14200E)));
            return Scarf(c, 0x6A9A3A, 0.05f, 9, 15);
        }

        // ---- Oddballs ----

        static PixelCharacter TrafficCone()
        {
            // A high-vis vest under the cone, so the whole figure reads as roadworks.
            PixelCharacter c = Body("char_cone", "Traffic Cone", 0xFF7A1A, 0xC85A10, 0x2A2A30, 0x1A1A20, 0xFF7A1A, 0xF2C29A, 0x140804);
            c.Head = new[]
            {
                ".....oo.....",
                ".....oo.....",
                "....oooo....",
                "....wwww....",
                "....wwww....",
                "...oooooo...",
                "...ookook...",
                "...oooooo...",
                "..wwwwwwww..",
                "..wwwwwwww..",
                "..oooooooo..",
                ".oooooooooo.",
                "OOOOOOOOOOOO",
            };
            c.Chest = new[]
            {
                "........",
                "........",
                "wwwwwwww",
                "........",
                "........",
                "wwwwwwww",
                "........",
                "........",
            };
            c.Palette = Pal(('o', Hex(0xFF7A1A)), ('O', Hex(0xC85A10)), ('w', Hex(0xF4F4F4)), ('k', Hex(0x1A1A1A)));
            return c;
        }

        static PixelCharacter Banana()
        {
            PixelCharacter c = Body("char_banana", "Banana", 0xFFE04A, 0xE0B830, 0xFFE04A, 0x6B4426, 0xFFE04A, 0xFFE04A, 0x2A2006);
            c.Head = new[]
            {
                ".......bb...",
                "......yb....",
                ".....yy.....",
                "....yyy.....",
                "...yyyyy....",
                "..yyyyyyy...",
                "..yyyykyyk..",
                ".yyyyyyyyy..",
                ".yyyyyrrry..",
                ".Yyyyyyyy...",
                "..YYYYYY....",
            };
            c.Chest = new[]
            {
                "...Y....",
                "...Y....",
                "...Y....",
                "...Y....",
                "...Y....",
                "...Y....",
                "........",
                "........",
            };
            c.Palette = Pal(('b', Hex(0x6B4426)), ('y', Hex(0xFFE04A)), ('Y', Hex(0xE0B830)), ('k', Hex(0x1A1A1A)), ('r', Hex(0xC8503A)));
            return c;
        }

        static PixelCharacter Hotdog()
        {
            PixelCharacter c = Body("char_hotdog", "Hotdog", 0xE8B86A, 0xC8984A, 0xC0402A, 0x8A2A1A, 0xE8B86A, 0xC0402A, 0x2A1006);
            c.Head = new[]
            {
                "....ssss....",
                "...ssssss...",
                "..ssyysyys..",
                "..ssssssss..",
                ".bbssssssbb.",
                ".bbbbbbbbbb.",
                ".bbbbbkbbkb.",
                ".bbbbbbbbbb.",
                ".bbbbbbrrbb.",
                "..BBBBBBBB..",
            };
            c.Palette = Pal(('s', Hex(0xC0402A)), ('y', Hex(0xFFE040)), ('b', Hex(0xE8B86A)), ('B', Hex(0xC8984A)), ('k', Hex(0x1A1A1A)),
                ('r', Hex(0xC0402A)));
            return c;
        }

        static PixelCharacter Gnome()
        {
            PixelCharacter c = Body("char_gnome", "Gnome", 0x3A6AC8, 0x2A50A0, 0x3A6AC8, 0x6B4426, 0x3A6AC8, 0xF2C29A, 0x0A0E1A);
            c.Head = new[]
            {
                ".....r......",
                ".....rr.....",
                "....rrr.....",
                "....rrrr....",
                "...rrrrrr...",
                "...rrrrrrr..",
                "..rrrrrrrrr.",
                "..RRRRRRRRRR",
                "...sssssss..",
                "...sssksksp.",
                "..wwwssssppw",
                "..wwwwwwwww.",
                "...wwwwwwww.",
                "....wwwwww..",
                ".....www....",
            };
            c.Chest = new[]
            {
                "...ww...",
                "........",
                "........",
                "........",
                "........",
                "........",
                "bbbbbbbb",
                "........",
            };
            c.Palette = Pal(('r', Hex(0xD8323C)), ('R', Hex(0xA82430)), ('s', Hex(0xF2C29A)), ('k', Hex(0x1A1A1A)), ('p', Hex(0xE89080)),
                ('w', Hex(0xF4F4F4)), ('b', Hex(0x6B4426)));
            return c;
        }
    }
}
