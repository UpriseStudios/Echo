using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>Ghost Duels' lobby (mirrors Server/EchoCloud/RankedProfileResponse).</summary>
    public sealed class RankedProfileInfo
    {
        public bool Ok;

        /// <summary>"closed" (no season running, or switched off) or "offline".</summary>
        public string Error = "";
        public bool Open;
        public int Season;
        public long SecondsLeft;
        public int Rating, Rank, Duels, DuelsLeftToday;
        public string Tier = "";
        public int AwayWins, AwayLosses, AwayChange;
        public List<RankedResultRow> Recent = new List<RankedResultRow>();

        /// <summary>The season's top 10 by rating (Distance holds the rating).</summary>
        public List<DailyBoardRow> Board = new List<DailyBoardRow>();
        public List<string> Rewards = new List<string>();

        public static RankedProfileInfo Failed(string error) => new RankedProfileInfo { Error = error };
    }
}
