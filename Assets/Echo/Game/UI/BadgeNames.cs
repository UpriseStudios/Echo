namespace Echo.Game.UI
{
    /// <summary>
    /// Badges the server grants (modes doc; a user decision: beside names on boards, and in Stats' profile). Ids look
    /// like "team_nice_2026".
    /// </summary>
    public static class BadgeNames
    {
        /// <summary>The short tag before a name on a board, or "".</summary>
        public static string Tag(string id) =>
            id.StartsWith("team_nice_") ? "<color=#FFE9A0>[NICE]</color> "
            : id.StartsWith("team_naughty_") ? "<color=#FF7A6A>[NAUGHTY]</color> "
            : id.StartsWith("onelife_") ? "<color=#73F2FF>[ONE LIFE]</color> "
            : "";

        /// <summary>Its name in the profile, e.g. "Team Nice 2026".</summary>
        public static string Name(string id)
        {
            string[] parts = id.Split('_');
            if (parts.Length == 3 && parts[0] == "team")
                return $"Team {(parts[1] == "nice" ? "Nice" : "Naughty")} {parts[2]}";
            if (parts.Length == 2 && parts[0] == "onelife" && parts[1].Length == 8)
                return $"One Life top 100, {parts[1].Substring(6, 2)}/{parts[1].Substring(4, 2)}/{parts[1].Substring(0, 4)}";
            return id;
        }

        public static string Tags(System.Collections.Generic.IEnumerable<string> ids)
        {
            string tags = "";
            if (ids == null) return tags;
            foreach (string id in ids) tags += Tag(id);
            return tags;
        }
    }
}
