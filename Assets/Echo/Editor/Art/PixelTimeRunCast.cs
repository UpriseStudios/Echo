using System.Collections.Generic;
using Echo.Core.Achievements;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World Time Run doc, phase 4: the world's rewards. The Caveman (Time Traveller) and the Chrono Android (Year 3026)
    /// on the standard 16-bit skin template, so they work as ghosts in every world; and the Fossil gravestone (Extinct),
    /// a slab with an ammonite and bones in it, usable in any world.
    /// </summary>
    public static class PixelTimeRunCast
    {
        public static readonly PixelCharacter[] Earned = { Caveman(), ChronoAndroid() };

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

        // A fur tunic with leopard spots, bare arms and legs, wild hair and a great beard.
        static PixelCharacter Caveman()
        {
            PixelCharacter c = Body("char_caveman", "Caveman", 0x9A6A34, 0x744C22, 0xD8A070, 0xB07850, 0xD8A070, 0xD8A070, 0x1A0E06);
            c.Head = new[]
            {
                "..hhhhhhh...",
                ".hhhhhhhhhh.",
                "hhhhhhhhhhh.",
                "hhhssssssh..",
                "hhssssskss..",
                "hhssssssssn.",
                ".hbbbsssbb..",
                ".bbbbbbbbbb.",
                "..bbbbbbbb..",
                "...bbbbbb...",
            };
            c.Chest = new[]
            {
                "ss......",
                "s.ff....",
                "......f.",
                ".f..f...",
                "....f...",
                "f.....f.",
                "..f.f...",
                "f.f.f.f.",
            };
            c.Palette = Pal(('h', Hex(0x2A1A10)), ('s', Hex(0xD8A070)), ('k', Hex(0x140C08)), ('n', Hex(0xC08050)),
                ('b', Hex(0x3A2416)), ('f', Hex(0x4A2A12)));
            return Earn(c, AchievementIds.TimeTraveller, "Time Run: Time Traveller");
        }

        // Brushed silver with a glowing cyan visor, a light core in its chest and a trail of light behind it.
        static PixelCharacter ChronoAndroid()
        {
            PixelCharacter c = Body("char_chrono", "Chrono Android", 0xC8D0DC, 0x8A96A8, 0x5A6478, 0x2A3040, 0xC8D0DC, 0x8A96A8, 0x0E1220);
            c.Head = new[]
            {
                "....aa......",
                "..mmmmmmm...",
                ".mmmmmmmmm..",
                "mmmmmmmmmmm.",
                "mmmvvvvvvvv.",
                "mmmvvvvvvvv.",
                "mmmmmmmmmmm.",
                ".mmmmmmmmm..",
                "..mmgmmgm...",
                "...mmmmm....",
            };
            c.Chest = new[]
            {
                "........",
                "..cccc..",
                ".c.yy.c.",
                ".c.yy.c.",
                "..cccc..",
                "........",
                "cccccccc",
                "........",
            };
            c.Palette = Pal(('a', Hex(0x4AF0FF)), ('m', Hex(0xD8E0EA)), ('v', Hex(0x3AF0FF)), ('g', Hex(0x8A96A8)),
                ('c', Hex(0x3AF0FF)), ('y', Hex(0xFFFFFF)));
            c.ScarfColor = Hex(0x3AF0FF);
            c.ScarfWidth = 0.05f;
            return Earn(c, AchievementIds.Year3026, "Time Run: Year 3026");
        }

        /// <summary>The Fossil gravestone, 12 x 16 like the stone: a sandstone slab with an ammonite and a little skeleton set in it.</summary>
        public static PixelCanvas FossilGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "..ssssssss..",
                ".sSSSSSSSSs.",
                ".sSSaaaaSSd.",
                ".sSaSSSSaSd.",
                ".sSaSaaSaSd.",
                ".sSaSaSSaSd.",
                ".sSSaSSaSSd.",
                ".sSSSaaSSSd.",
                ".sSbSbSbSSd.",
                ".sSbbbbbbSd.",
                ".sSbSbSbSSd.",
                ".sSSSSSSSSd.",
                ".sSSSSSSSSd.",
                ".ssssssssdd.",
                "rrrrrrrrrrrr",
                "rrrrrrrrrrrr",
            }, Pal(('s', Hex(0xB89A6A)), ('S', Hex(0xD8BC88)), ('d', Hex(0x8A7048)), ('a', Hex(0x6A5030)),
                ('b', Hex(0xF2EAD8)), ('r', Hex(0x6A5A44))), 0, 0);
            c.Outline(Hex(0x2A2014));
            return c;
        }
    }
}
