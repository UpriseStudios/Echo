using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>Naughty vs Nice: the server's answer to GetTeamEvent and JoinTeam (mirrors Server/EchoCloud/TeamEventResponse).</summary>
    public sealed class TeamEventResult
    {
        public bool Ok;

        /// <summary>"closed", "bad-team" or "offline".</summary>
        public string Error = "";
        public string Event = "";
        public bool Open;
        public bool Ended;
        public long SecondsLeft;

        /// <summary>"naughty", "nice" or "" (not joined).</summary>
        public string Team = "";
        public bool Assigned;
        public long Naughty, Nice, NaughtyToday, NiceToday;
        public int NaughtyMembers, NiceMembers;
        public long YourMetres;
        public int YourDays;

        /// <summary>Ended: "naughty", "nice" or "draw".</summary>
        public string Winner = "";
        public string TopNaughty = "";
        public long TopNaughtyMetres;
        public string TopNice = "";
        public long TopNiceMetres;
        public List<string> Rewards = new List<string>();

        public static TeamEventResult Failed(string error) => new TeamEventResult { Error = error };
    }
}
