using System.Text;

namespace Echo.Core.Challenges
{
    /// <summary>
    /// The name on a challenge ("Jamie's Ghost"), cleaned the same way by the game and the server: control characters
    /// dropped, trimmed, capped at Challenge.MaxNameLength, and a name on the blocklist replaced by Default. A simple
    /// filter (a user choice): it sees through case, spacing, repeated letters and common digit swaps, and checks the
    /// short words that hide inside innocent names (Cassie, Hitchcock) only as whole words.
    /// </summary>
    public static class ChallengeNames
    {
        public const string Default = "Runner";

        // Blocked wherever they appear in the name.
        static readonly string[] Anywhere =
        {
            "fuck", "shit", "cunt", "nigg", "fag", "bitch", "whore", "slut", "twat", "wanker", "piss", "bastard",
            "retard", "nazi", "hitler", "porn", "dildo", "jizz", "penis", "vagina", "tranny", "kkk", "bollock", "prick",
        };

        // Blocked only as a whole word, because they hide inside ordinary names.
        static readonly string[] WholeWord =
        {
            "ass", "arse", "dick", "cock", "cum", "sex", "tits", "anal", "rape", "coon", "dyke", "spic", "chink", "paki",
            "kike", "nonce", "pussy", "wank", "slag", "homo",
        };

        /// <summary>The name as it will be shown: "" stays "", a blocked name becomes Default.</summary>
        public static string Clean(string raw)
        {
            var b = new StringBuilder();
            foreach (char ch in raw ?? "")
                if (!char.IsControl(ch) && b.Length < Challenge.MaxNameLength) b.Append(ch);
            string name = b.ToString().Trim();
            return name.Length > 0 && IsBlocked(name) ? Default : name;
        }

        public static bool IsBlocked(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // Letters only, digit swaps undone: "F u_c-k", "fuuuck" and "5h1t" all read as words.
            var letters = new StringBuilder();
            var word = new StringBuilder();
            foreach (char raw in name)
            {
                char ch = Unleet(char.ToLowerInvariant(raw));
                if (ch >= 'a' && ch <= 'z')
                {
                    letters.Append(ch);
                    word.Append(ch);
                }
                else if (EndWord(word)) return true;
            }
            if (EndWord(word)) return true;

            string squashed = letters.ToString(), single = Collapse(squashed);
            foreach (string bad in Anywhere)
            {
                if (squashed.Contains(bad)) return true;
                string badSingle = Collapse(bad);
                if (badSingle.Length >= 4 && single.Contains(badSingle)) return true;
            }
            // The whole name run together ("a s s") counts as one word too.
            return IsWholeWord(squashed);
        }

        static bool EndWord(StringBuilder word)
        {
            bool blocked = word.Length > 0 && IsWholeWord(word.ToString());
            word.Clear();
            return blocked;
        }

        static bool IsWholeWord(string word)
        {
            foreach (string bad in WholeWord)
                if (Stretched(word, bad)) return true;
            return false;
        }

        // "asss" is "ass" stretched; "as" is not (each run of a letter must be at least as long as the blocked word's).
        static bool Stretched(string word, string bad)
        {
            int i = 0, j = 0;
            while (i < word.Length && j < bad.Length)
            {
                char ch = bad[j];
                if (word[i] != ch) return false;
                int runBad = 0, runWord = 0;
                while (j < bad.Length && bad[j] == ch) { j++; runBad++; }
                while (i < word.Length && word[i] == ch) { i++; runWord++; }
                if (runWord < runBad) return false;
            }
            return i == word.Length && j == bad.Length;
        }

        // "fuuuck" -> "fuck": runs of one letter become one.
        static string Collapse(string s)
        {
            var b = new StringBuilder(s.Length);
            foreach (char ch in s)
                if (b.Length == 0 || b[b.Length - 1] != ch) b.Append(ch);
            return b.ToString();
        }

        static char Unleet(char ch)
        {
            switch (ch)
            {
                case '0': return 'o';
                case '1': case '!': case '|': return 'i';
                case '3': return 'e';
                case '4': case '@': return 'a';
                case '5': case '$': return 's';
                case '7': return 't';
                case '8': return 'b';
                case '9': return 'g';
                default: return ch;
            }
        }
    }
}
