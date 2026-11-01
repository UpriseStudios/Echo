using Echo.Core.Modes;

namespace Echo.Game.UI
{
    /// <summary>The competitive modes' names, pitches, colours and live numbers in Ghost Town (modes doc).</summary>
    public static class ModeNames
    {
        public static string Name(string id) => id switch
        {
            ModeIds.NaughtyNice => "Naughty vs Nice",
            ModeIds.OneLife => "One Life",
            ModeIds.Graveyard => "Community Graveyard",
            // A user decision: "Ranked" in code, and plainly ranked to players.
            ModeIds.Ranked => "Ranked Ghost Duels",
            _ => id,
        };

        public static string Pitch(string id) => id switch
        {
            ModeIds.NaughtyNice => "Pick a side: your best Daily Haunt counts for your team",
            ModeIds.OneLife => "One run a day. No retries",
            ModeIds.Graveyard => "Everyone's deaths haunt one course all week",
            ModeIds.Ranked => "Ranked: beat a stranger's ghost, climb the tiers",
            _ => "",
        };

        /// <summary>Ghost Town: each event's own colour, on its card's stripe, chip and player count.</summary>
        public static UnityEngine.Color Colour(string id) => id switch
        {
            ModeIds.NaughtyNice => new UnityEngine.Color(1f, 0.42f, 0.5f),
            ModeIds.OneLife => new UnityEngine.Color(1f, 0.33f, 0.33f),
            ModeIds.Graveyard => new UnityEngine.Color(0.55f, 0.95f, 0.6f),
            ModeIds.Ranked => new UnityEngine.Color(0.45f, 0.95f, 1f),
            _ => new UnityEngine.Color(1f, 0.79f, 0.29f),
        };

        /// <summary>Ghost Town's live numbers (a user decision): "312 runners today", by each mode's own period.</summary>
        public static string Players(string id, int players)
        {
            if (players < 0) return "";
            string n = players.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return id switch
            {
                ModeIds.NaughtyNice => $"{n} on the teams",
                ModeIds.Graveyard => $"{n} {(players == 1 ? "runner" : "runners")} this week",
                ModeIds.Ranked => $"{n} {(players == 1 ? "duellist" : "duellists")} this season",
                _ => $"{n} {(players == 1 ? "runner" : "runners")} today",
            };
        }

        /// <summary>"Ends in 3d 4h", "Opens in 5h 10m", or "Results are in" once it has closed for good.</summary>
        public static string Countdown(bool open, long seconds)
        {
            if (seconds < 0) return open ? "Open now" : "Results are in";
            string span = seconds >= 86400 ? $"{seconds / 86400}d {seconds / 3600 % 24}h"
                : seconds >= 3600 ? $"{seconds / 3600}h {seconds / 60 % 60:00}m"
                : $"{seconds / 60}m {seconds % 60:00}s";
            return (open ? "Ends in " : "Opens in ") + span;
        }
    }
}
