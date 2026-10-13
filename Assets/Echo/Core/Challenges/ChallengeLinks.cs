using System;

namespace Echo.Core.Challenges
{
    /// <summary>
    /// Links to server-stored challenges (version 2.0): https://echo.uprisestudios.co.uk/c/OWNER/ID, opened by
    /// Android App Links and iOS Universal Links, and the same path on the game's own scheme
    /// (echoghost://c/OWNER/ID), which the web page's "Open in Echo" button uses. Lite challenges keep their
    /// echoghost://challenge/CODE links (ChallengeCode).
    /// </summary>
    public static class ChallengeLinks
    {
        public const string Host = "echo.uprisestudios.co.uk";
        public const string WebPrefix = "https://" + Host + "/c/";
        public const string AppPrefix = ChallengeCode.Scheme + "://c/";
        public const int MaxIdLength = 64;

        public static string Link(string ownerId, string challengeId) => WebPrefix + ownerId + "/" + challengeId;

        /// <summary>Finds a server challenge link in anything the player opens or pastes.</summary>
        public static bool TryParse(string text, out string ownerId, out string challengeId)
        {
            ownerId = challengeId = null;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string prefix in new[] { WebPrefix, "http://" + Host + "/c/", AppPrefix })
            {
                int at = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                int pos = at + prefix.Length;
                if (!Id(text, ref pos, out ownerId) || pos >= text.Length || text[pos] != '/') return false;
                pos++;
                if (!Id(text, ref pos, out challengeId)) return false;
                return true;
            }
            return false;
        }

        /// <summary>Player and challenge ids: letters, digits, '-' and '_', at most MaxIdLength.</summary>
        public static bool IsId(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > MaxIdLength) return false;
            foreach (char ch in s)
                if (!IsIdChar(ch)) return false;
            return true;
        }

        static bool Id(string text, ref int pos, out string id)
        {
            int start = pos;
            while (pos < text.Length && IsIdChar(text[pos])) pos++;
            id = text.Substring(start, pos - start);
            return IsId(id);
        }

        static bool IsIdChar(char ch) =>
            ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '-' || ch == '_';
    }
}
