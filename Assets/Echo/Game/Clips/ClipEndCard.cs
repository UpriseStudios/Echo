namespace Echo.Game.Clips
{
    /// <summary>
    /// The Vertical clip's 1-second end card (marketing content plan): the ECHO wordmark and where to get it, drawn in
    /// the clip font. A placeholder for a real logo image.
    /// </summary>
    public static class ClipEndCard
    {
        const uint Background = 0x0F2930, Cyan = 0x73F2FF, White = 0xFFFFFF, Shadow = 0x05080C;

        public static void Draw(byte[] rgba, int width, int height, string storeLine)
        {
            byte r = (byte)((Background >> 16) & 0xFF), g = (byte)((Background >> 8) & 0xFF), b = (byte)(Background & 0xFF);
            for (int i = 0; i < width * height; i++)
            {
                rgba[i * 4] = r;
                rgba[i * 4 + 1] = g;
                rgba[i * 4 + 2] = b;
                rgba[i * 4 + 3] = 255;
            }
            const string word = "ECHO";
            int big = 8;
            while (ClipFont.Measure(word, big) > width - 40 && big > 2) big--;
            int y = height / 2 - ClipFont.Height * big;
            ClipFont.DrawOutlined(rgba, width, height, word, (width - ClipFont.Measure(word, big)) / 2, y, big, Cyan, Shadow);
            string line = (storeLine ?? "").ToUpperInvariant();
            int small = 2;
            while (ClipFont.Measure(line, small) > width - 16 && small > 1) small--;
            ClipFont.Draw(rgba, width, height, line, (width - ClipFont.Measure(line, small)) / 2, y + ClipFont.Height * big + 24, small, White, Shadow);
        }
    }
}
