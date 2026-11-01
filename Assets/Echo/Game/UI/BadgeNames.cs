namespace Echo.Game.UI
{
    /// <summary>
    /// Badges the server grants (modes doc; a user decision: beside names on boards, and in Stats' profile). Ids look
    /// like "team_nice_2026". A user revision: each shows as a little icon (the Badges sprite asset, Editor/BadgeSprites)
    /// rather than a [LABEL]; Stats' profile names them beside their icons.
    /// </summary>
    public static class BadgeNames
    {
        /// <summary>The icon's sprite name for a badge id, or null for one this version doesn't know.</summary>
        public static string Sprite(string id)
        {
            if (id.StartsWith("team_nice_")) return "nice";
            if (id.StartsWith("team_naughty_")) return "naughty";
            if (id.StartsWith("onelife_")) return "onelife";
            if (id.StartsWith("gravekeeper_")) return "gravekeeper";
            string[] parts = id.Split('_');
            if (parts.Length == 3 && parts[0] == "duel" && parts[1] is "bronze" or "silver" or "gold" or "phantom" or "wraith") return "duel_" + parts[1];
            return null;
        }

        /// <summary>The icon inline in text, or "".</summary>
        public static string Icon(string id)
        {
            string sprite = Sprite(id);
            return sprite == null ? "" : $"<sprite name=\"{sprite}\">";
        }

        /// <summary>The icon before a name on a board, with a space, or "".</summary>
        public static string Tag(string id)
        {
            string icon = Icon(id);
            return icon == "" ? "" : icon + " ";
        }

        /// <summary>Its name in the profile, e.g. "Team Nice 2026".</summary>
        public static string Name(string id)
        {
            string[] parts = id.Split('_');
            if (parts.Length == 3 && parts[0] == "team")
                return $"Team {(parts[1] == "nice" ? "Nice" : "Naughty")} {parts[2]}";
            if (parts.Length == 2 && parts[0] == "onelife" && parts[1].Length == 8)
                return $"One Life top 100, {parts[1].Substring(6, 2)}/{parts[1].Substring(4, 2)}/{parts[1].Substring(0, 4)}";
            if (parts.Length == 3 && parts[0] == "duel")
                return $"Ghost Duels season {parts[2].Substring(1)}: {char.ToUpperInvariant(parts[1][0]) + parts[1].Substring(1)}";
            if (parts.Length == 2 && parts[0] == "gravekeeper" && parts[1].Length == 8)
                return $"Gravekeeper, week of {parts[1].Substring(6, 2)}/{parts[1].Substring(4, 2)}/{parts[1].Substring(0, 4)}";
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
