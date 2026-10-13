namespace Echo.Game.Clips
{
    /// <summary>
    /// A 5 x 7 pixel font for the clip's overlay (game spec: score and ghost count on the share clip), drawn straight
    /// into the frame so it stays as sharp as the world around it. Capitals, digits and a little punctuation.
    /// </summary>
    public static class ClipFont
    {
        public const int Width = 5, Height = 7, Advance = 6;

        const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ,.!:#-";

        // One string per character in Chars: 7 rows of 5, top row first.
        static readonly string[] Glyphs =
        {
            " ### #   ##  ### # ###  ##   # ### ", // 0
            "  #   ##    #    #    #    #   ### ", // 1
            " ### #   #    #   #   #   #   #####", // 2
            "#####   #   #     #     ##   # ### ", // 3
            "   #   ##  # # #  # #####   #    # ", // 4
            "######    ####     #    ##   # ### ", // 5
            "  ##  #   #    #### #   ##   # ### ", // 6
            "#####    #   #   #   #    #    #   ", // 7
            " ### #   ##   # ### #   ##   # ### ", // 8
            " ### #   ##   # ####    #   #  ##  ", // 9
            " ### #   ##   #######   ##   ##   #", // A
            "#### #   ##   ##### #   ##   ##### ", // B
            " ### #   ##    #    #    #   # ### ", // C
            "#### #   ##   ##   ##   ##   ##### ", // D
            "######    #    #### #    #    #####", // E
            "######    #    #### #    #    #    ", // F
            " ### #   ##    # ####   ##   # ####", // G
            "#   ##   ##   #######   ##   ##   #", // H
            " ###   #    #    #    #    #   ### ", // I
            "  ###   #    #    #    # #  #  ##  ", // J
            "#   ##  # # #  ##   # #  #  # #   #", // K
            "#    #    #    #    #    #    #####", // L
            "#   ### ### # ## # ##   ##   ##   #", // M
            "#   ##   ###  ## # ##  ###   ##   #", // N
            " ### #   ##   ##   ##   ##   # ### ", // O
            "#### #   ##   ##### #    #    #    ", // P
            " ### #   ##   ##   ## # ##  #  ## #", // Q
            "#### #   ##   ##### # #  #  # #   #", // R
            " #####    #     ###     #    ##### ", // S
            "#####  #    #    #    #    #    #  ", // T
            "#   ##   ##   ##   ##   ##   # ### ", // U
            "#   ##   ##   ##   ##   # # #   #  ", // V
            "#   ##   ##   ## # ## # ## # # # # ", // W
            "#   ##   # # #   #   # # #   ##   #", // X
            "#   ##   # # #   #    #    #    #  ", // Y
            "#####    #   #   #   #   #    #####", // Z
            "                           #   #   ", // ,
            "                                #  ", // .
            "  #    #    #    #    #         #  ", // !
            "       #    #         #    #       ", // :
            " # # ##### # # ##### # #           ", // #
            "               #####               ", // -
        };

        /// <summary>Whether the pixel at (x, y) of a character is set; y = 0 is the top row.</summary>
        public static bool Pixel(char c, int x, int y)
        {
            int i = Chars.IndexOf(char.ToUpperInvariant(c));
            if (i < 0 || x < 0 || x >= Width || y < 0 || y >= Height) return false;
            string g = Glyphs[i];
            int k = y * Width + x;
            return k < g.Length && g[k] == '#';
        }

        public static int Measure(string text, int scale) => text.Length == 0 ? 0 : (text.Length * Advance - 1) * scale;

        /// <summary>Draws text into an RGBA frame (top row first) at (x, y), its top-left, with a one-pixel dark shadow.</summary>
        public static void Draw(byte[] rgba, int width, int height, string text, int x, int y, int scale, uint color, uint shadow)
        {
            DrawPass(rgba, width, height, text, x + 1, y + 1, scale, shadow);
            DrawPass(rgba, width, height, text, x, y, scale, color);
        }

        static void DrawPass(byte[] rgba, int width, int height, string text, int x, int y, int scale, uint color)
        {
            byte r = (byte)(color >> 16), g = (byte)(color >> 8), b = (byte)color;
            for (int n = 0; n < text.Length; n++)
            {
                int left = x + n * Advance * scale;
                for (int gy = 0; gy < Height; gy++)
                    for (int gx = 0; gx < Width; gx++)
                    {
                        if (!Pixel(text[n], gx, gy)) continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int px = left + gx * scale + sx, py = y + gy * scale + sy;
                                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                                int i = (py * width + px) * 4;
                                rgba[i] = r;
                                rgba[i + 1] = g;
                                rgba[i + 2] = b;
                                rgba[i + 3] = 255;
                            }
                    }
            }
        }
    }
}
