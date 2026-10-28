using Echo.Core.Modes;

namespace Echo.Game.UI
{
    /// <summary>The competitive modes' names and one-line pitches on the Modes screen (modes doc).</summary>
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
