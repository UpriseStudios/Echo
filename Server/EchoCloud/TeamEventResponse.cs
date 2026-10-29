using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>
    /// Naughty vs Nice (modes doc): the tally, your side and contribution, and after the event the result and your
    /// rewards. Returned by GetTeamEvent and JoinTeam.
    /// </summary>
    public sealed class TeamEventResponse
    {
        public bool Ok { get; set; }

        /// <summary>"closed" (no event on show), "no-team", "bad-team", "offline".</summary>
        public string Error { get; set; } = "";

        /// <summary>The event (e.g. "naughty_nice_2026"); the game remembers its reveal by it.</summary>
        public string Event { get; set; } = "";
        public bool Open { get; set; }

        /// <summary>The event is over and its result is showing (until show_until).</summary>
        public bool Ended { get; set; }

        /// <summary>Open: seconds until it closes. Ended: until the result stops showing.</summary>
        public long SecondsLeft { get; set; }

        /// <summary>"naughty", "nice" or "" (not joined).</summary>
        public string Team { get; set; } = "";

        /// <summary>Joining put you on the smaller side (the elves did it).</summary>
        public bool Assigned { get; set; }

        public long Naughty { get; set; }
        public long Nice { get; set; }
        public long NaughtyToday { get; set; }
        public long NiceToday { get; set; }
        public int NaughtyMembers { get; set; }
        public int NiceMembers { get; set; }

        public long YourMetres { get; set; }
        public int YourDays { get; set; }

        /// <summary>Ended: "naughty", "nice" or "draw".</summary>
        public string Winner { get; set; } = "";
        public string TopNaughty { get; set; } = "";
        public long TopNaughtyMetres { get; set; }
        public string TopNice { get; set; } = "";
        public long TopNiceMetres { get; set; }

        /// <summary>Ended: what you earned (cosmetic and badge ids), kept for good in your profile.</summary>
        public List<string> Rewards { get; set; } = new List<string>();
    }
}
