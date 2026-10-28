using System.Collections.Generic;
using System.Text;

namespace Echo.Game.Clips
{
    /// <summary>
    /// The Vertical clip's hook (marketing content plan): a line of up to 8 words typed at export time, drawn in the
    /// clip font with an outline in the top third, below the score, and kept out of the right 15% and bottom 20% of the
    /// frame, where TikTok and Reels put their buttons.
    /// </summary>
    public static class ClipHook
    {
        public const int MaxWords = 8;
        public const int Top = 60;                 // below the ECHO / score / ghosts stamp
        public const int Left = 10;
        const uint Fill = 0xFFFFFF, Outline = 0x080A12;

        /// <summary>The right edge of the hook's area: 85% of the frame.</summary>
        public static int Right(int width) => width * 85 / 100;

        /// <summary>The bottom of the hook's area: the top third of the frame.</summary>
        public static int Bottom(int height) => height / 3;

        /// <summary>Capitals the font can draw, single spaces, at most 8 words.</summary>
        public static string Clean(string raw)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            foreach (char c0 in (raw ?? "").ToUpperInvariant() + " ")
            {
                char c = c0 == '\u2019' ? '\'' : c0;
                if (char.IsWhiteSpace(c))
                {
                    if (word.Length > 0 && words.Count < MaxWords) words.Add(word.ToString());
                    word.Clear();
                }
                else if (ClipFont.Supports(c)) word.Append(c);
            }
            return string.Join(" ", words);
        }

        /// <summary>The hook broken into lines no wider than <paramref name="maxWidth"/> at <paramref name="scale"/>.</summary>
        public static List<string> Lines(string text, int scale, int maxWidth)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string w in text.Split(' '))
            {
                if (w.Length == 0) continue;
                string word = w;
                while (ClipFont.Measure(word, scale) > maxWidth && word.Length > 1) word = word.Substring(0, word.Length - 1);
                string trial = line.Length == 0 ? word : line + " " + word;
                if (ClipFont.Measure(trial, scale) <= maxWidth) line = trial;
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        /// <summary>
        /// Draws the hook into an RGBA frame (top row first): as large as fits the area, each line centred in it.
        /// Returns false when there was nothing to draw.
        /// </summary>
        public static bool Draw(byte[] rgba, int width, int height, string hook)
        {
            string text = Clean(hook);
            if (text.Length == 0) return false;
            int areaW = Right(width) - Left - 2, areaH = Bottom(height) - Top - 2;
            for (int scale = 3; scale >= 1; scale--)
            {
                List<string> lines = Lines(text, scale, areaW);
                int lineH = (ClipFont.Height + 3) * scale;
                if (lines.Count * lineH > areaH && scale > 1) continue;
                int y = Top + 1;
                foreach (string line in lines)
                {
                    if (y + ClipFont.Height * scale > Bottom(height) - 1) break;
                    int x = Left + 1 + (areaW - ClipFont.Measure(line, scale)) / 2;
                    ClipFont.DrawOutlined(rgba, width, height, line, x, y, scale, Fill, Outline);
                    y += lineH;
                }
                return true;
            }
            return false;
        }
    }
}
