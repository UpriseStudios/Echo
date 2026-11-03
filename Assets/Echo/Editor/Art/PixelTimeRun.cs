using System.Collections.Generic;
using UnityEngine;
using static Echo.Editor.Art.PixelCanvas;

namespace Echo.Editor.Art
{
    /// <summary>
    /// World: Time Run's gameplay layer per era (World Time Run doc, phase 2). Hazards repaint the originals' own pixels
    /// (the spike's outline is exact and stays greyscale, so every era takes the hazard tint the player knows; blocks keep
    /// their size and 4 px slicing border); gaps, graves and coins are drawn in the era's own colours, and so is the
    /// Nemesis's fossil skull, in red, so the Nemesis stays red and horned. Medieval keeps the originals except its
    /// portcullis and moat. The finished art, in the game's pixel style.
    /// </summary>
    public static class PixelTimeRun
    {
        // ---- spikes: the original's pixels repainted ----

        /// <summary>Dino Land: a volcanic rock shard, rough and speckled, with a crack down it.</summary>
        public static PixelCanvas RockSpike() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            int h = (x * 7 + y * 13) % 11;
            if (x == 7 - y / 5 && y > 1 && y < 12) return Grey(0.32f);          // the crack
            if (h == 0 || h == 5) return Shade(p, 0.62f);                       // pits in the rock
            return Shade(p, 0.82f);
        });

        /// <summary>Today: a traffic cone, two bright reflective bands across a darker cone.</summary>
        public static PixelCanvas ConeSpike() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            if (y <= 1) return Grey(0.4f);                                      // its square base
            if (y >= 4 && y <= 5 || y >= 9 && y <= 10) return Grey(x < 8 ? 1f : 0.86f);
            return Shade(p, 0.62f);
        });

        /// <summary>Far Future: an energy spike, a bright core line inside a darker shell.</summary>
        public static PixelCanvas EnergySpike() => PixelSeasons.Repaint(PixelWorld.Spike(), (x, y, p) =>
        {
            if (x == 7 || x == 8) return Grey(1f);                              // the core
            if (x == 6 || x == 9) return Grey(0.82f);
            return (x + y) % 3 == 0 ? Grey(0.62f) : Grey(0.5f);                 // a shimmer in the shell
        });

        // ---- blocks (movers and ceilings): 16 x 16, 4 px slicing border ----

        /// <summary>Dino Land: a rock overhang, uneven stone with stalactite drips along its underside.</summary>
        public static PixelCanvas CliffBlock()
        {
            var c = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int h = (x * 5 + y * 11) % 9;
                    c[x, y] = Grey(h == 0 ? 0.55f : h < 3 ? 0.68f : 0.76f);
                }
            c.Rect(0, 15, 15, 15, Grey(0.9f));
            foreach (int x in new[] { 2, 6, 11, 14 }) { c[x, 0] = Grey(0.5f); c[x, 1] = Grey(0.58f); }   // drips at the bottom
            c.Rect(0, 0, 0, 15, Grey(0.86f));
            c.Rect(15, 0, 15, 15, Grey(0.56f));
            return c;
        }

        /// <summary>Medieval: a castle portcullis, an iron grid with points along the bottom.</summary>
        public static PixelCanvas PortcullisBlock()
        {
            var c = new PixelCanvas(16, 16);
            c.Rect(0, 0, 15, 15, Grey(0.35f));
            for (int x = 1; x < 16; x += 4) c.Rect(x, 0, x + 1, 15, Grey(0.82f));           // uprights
            for (int y = 3; y < 16; y += 4) c.Rect(0, y, 15, y, Grey(0.7f));                // crossbars
            for (int x = 1; x < 16; x += 4) c[x, 0] = c[x + 1, 0] = Grey(1f);               // the points
            c.Rect(0, 15, 15, 15, Grey(0.92f));
            return c;
        }

        /// <summary>Today: a hanging billboard, a frame round a blank lit panel.</summary>
        public static PixelCanvas BillboardBlock()
        {
            var c = new PixelCanvas(16, 16);
            c.Rect(0, 0, 15, 15, Grey(0.5f));
            c.Rect(2, 2, 13, 13, Grey(0.94f));
            c.Rect(2, 13, 13, 13, Grey(1f));
            c.Rect(3, 6, 12, 6, Grey(0.78f));
            c.Rect(3, 9, 10, 9, Grey(0.78f));
            c.Rect(0, 0, 15, 0, Grey(0.38f));
            return c;
        }

        /// <summary>Far Future: a hovering metal platform, plated, with a light strip underneath.</summary>
        public static PixelCanvas HoverBlock()
        {
            var c = new PixelCanvas(16, 16);
            c.Rect(0, 0, 15, 15, Grey(0.72f));
            c.Rect(0, 15, 15, 15, Grey(0.95f));
            c.Rect(0, 7, 15, 7, Grey(0.56f));
            foreach (var (x, y) in new[] { (2, 12), (13, 12), (2, 4), (13, 4) }) c[x, y] = Grey(0.5f);
            c.Rect(1, 1, 14, 1, Grey(1f));                                                   // the light strip
            c.Rect(0, 0, 15, 0, Grey(0.5f));
            return c;
        }

        // ---- gaps, in colour: a 16 px tile repeated down the pit ----

        /// <summary>Dino Land: a lava pit, bright at the top and glowing down into the dark.</summary>
        public static PixelCanvas LavaGap() => Gap((x, y) =>
            y >= 14 ? ((x + y) % 4 == 0 ? Hex(0xFFE070) : Hex(0xFF9A2A)) : y >= 10 ? Hex(0xE0501E) : y >= 6 ? Hex(0x9A2A18) : Hex(0x4A1410));

        /// <summary>Medieval: the moat, dark water with a light ripple on top.</summary>
        public static PixelCanvas MoatGap() => Gap((x, y) =>
            y == 15 ? ((x / 3) % 2 == 0 ? Hex(0x8AB8D8) : Hex(0x4A7AA8)) : y >= 12 ? Hex(0x2A5A8A) : Hex(0x163A5A));

        /// <summary>Today: the drop between rooftops, dark with a few lit windows far below.</summary>
        public static PixelCanvas RooftopGap() => Gap((x, y) =>
            (x % 6 == 2 && y % 5 == 1) ? Hex(0xFFD86A) : y >= 14 ? Hex(0x2A2A3A) : Hex(0x14141E));

        /// <summary>Far Future: a void with a neon grid running down it.</summary>
        public static PixelCanvas VoidGap() => Gap((x, y) =>
            x % 8 == 0 || y % 8 == 0 ? Hex(0x2AE8FF) : Hex(0x06061A));

        static PixelCanvas Gap(System.Func<int, int, Color32> paint)
        {
            var c = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    c[x, y] = paint(x, y);
            return c;
        }

        // ---- graves, 12 x 16 like the stone, the bottom two rows a mound below the ground line ----

        /// <summary>Dino Land: a rough stone slab with claw marks, greyscale like the standard stone.</summary>
        public static PixelCanvas SlabGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Rect(0, 0, 11, 1, Grey(0.66f));
            c.Rect(1, 2, 10, 10, Grey(0.86f));
            c.Rect(2, 11, 9, 11, Grey(0.86f));
            c.Rect(9, 2, 10, 10, Grey(0.68f));
            foreach (int x in new[] { 3, 5, 7 }) for (int y = 4; y <= 8; y++) c[x + (y - 4) / 3, y] = Grey(0.42f);   // three claw marks
            c[1, 10] = PixelCanvas.Clear;
            c.Outline(Grey(0.25f));
            return c;
        }

        /// <summary>Today: a modern polished headstone with flowers at its foot, in colour.</summary>
        public static PixelCanvas ModernGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "..kkkkkkkk..",
                ".kssssssssk.",
                ".ksSSSSSSsk.",
                ".ksSttttSsk.",
                ".ksSSSSSSsk.",
                ".ksSttttSsk.",
                ".ksSSSSSSsk.",
                ".ksSSSSSSsk.",
                ".ksSSSSSSsk.",
                "rkgsSSSSSskr",
                "ygrsSSSSsgyg",
                "ggggssssgggg",
                "kkkkkkkkkkkk",
                "............",
            }, new Dictionary<char, Color32>
            {
                ['k'] = Hex(0x1A1E26), ['s'] = Hex(0x3A4250), ['S'] = Hex(0x56606E), ['t'] = Hex(0x8A96A6),
                ['r'] = Hex(0xE84A5A), ['y'] = Hex(0xFFD24A), ['g'] = Hex(0x4AA04A),
            }, 0, 2);
            c.Rect(0, 0, 11, 1, Hex(0x4A3A2A));
            return c;
        }

        /// <summary>Far Future: a holographic memorial panel, cyan light over a small emitter.</summary>
        public static PixelCanvas HoloGrave()
        {
            var c = new PixelCanvas(12, 16);
            c.Blit(new[]
            {
                "..cccccccc..",
                ".cCCCCCCCCc.",
                ".cCwwwwwwCc.",
                ".cCCCCCCCCc.",
                ".cCwwwwCCCc.",
                ".cCCCCCCCCc.",
                ".cCwwwwwwCc.",
                ".cCCCCCCCCc.",
                "..cccccccc..",
                "....c..c....",
                "...ummmmu...",
                "..mmmmmmmm..",
            }, new Dictionary<char, Color32>
            {
                ['c'] = new Color32(70, 230, 255, 210), ['C'] = new Color32(40, 170, 220, 140), ['w'] = new Color32(210, 250, 255, 230),
                ['m'] = Hex(0x3A4250), ['u'] = Hex(0x2AE8FF),
            }, 0, 2);
            c.Rect(0, 0, 11, 1, Hex(0x2A2A3A));
            return c;
        }

        // ---- the bounty coin, 13 x 13 like the original, in colour ----

        /// <summary>Dino Land: amber with a mosquito inside.</summary>
        public static PixelCanvas AmberCoin()
        {
            var c = new PixelCanvas(13, 13);
            c.Blit(new[]
            {
                "...ooooo...",
                "..oyyYYyoo.",
                ".oyYYyyyyyo",
                "oyyyykyyyyo",
                "oyyykkkyyyo",
                "oyyykkkyyyo",
                "oyykyyykyyo",
                "oyyyyyyyyyo",
                ".oyyyyyyyo.",
                "..oyyyyyo..",
                "...ooooo...",
            }, new Dictionary<char, Color32> { ['o'] = Hex(0xB8641A), ['y'] = Hex(0xF0A030), ['Y'] = Hex(0xFFE0A0), ['k'] = Hex(0x4A2A10) }, 1, 1);
            c.Outline(Hex(0x3A1E08));
            return c;
        }

        /// <summary>Far Future: a data chip, a gold-pinned square with a cyan core.</summary>
        public static PixelCanvas DataChip()
        {
            var c = new PixelCanvas(13, 13);
            c.Blit(new[]
            {
                ".y.y.y.y.y.",
                "yddddddddy.",
                ".dmmmmmmmd.",
                "ydmcccccmdy",
                ".dmcCCCcmd.",
                "ydmcCCCcmdy",
                ".dmcccccmd.",
                "ydmmmmmmmdy",
                ".ddddddddd.",
                ".y.y.y.y.y.",
            }, new Dictionary<char, Color32>
            {
                ['y'] = Hex(0xE0B040), ['d'] = Hex(0x1A2A3A), ['m'] = Hex(0x2A3E54), ['c'] = Hex(0x2AA8D8), ['C'] = Hex(0x9AF4FF),
            }, 1, 1);
            return c;
        }

        // ---- the Nemesis's fossil look: a horned dinosaur skull, in the Nemesis's red ----

        /// <summary>16 x 9, like the horns it replaces; pivot at its base so it sits on the head.</summary>
        public static PixelCanvas DinoSkull()
        {
            var c = new PixelCanvas(16, 9);
            c.Blit(new[]
            {
                "h............h",
                "hh..........hh",
                ".hh.wwwwww.hh.",
                "..hwwwwwwwwh..",
                "...wkwwwwkw...",
                "...wwwwwwww...",
                "....wtwtwt....",
            }, new Dictionary<char, Color32> { ['h'] = Hex(0xFF5A5A), ['w'] = Hex(0xE8A0A0), ['k'] = Hex(0x5A1414), ['t'] = Hex(0xFFE0E0) }, 1, 1);
            c.Outline(Hex(0x3A0A0A));
            return c;
        }
    }
}
