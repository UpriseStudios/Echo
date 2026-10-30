using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>GetDuelProfile (modes doc, Ghost Duels): your rating, tier and duels left, the season, recent results, the board.</summary>
    public sealed class RankedProfileResponse : IFailable
    {
        public bool Ok { get; set; }

        /// <summary>"closed" (no season running, or switched off).</summary>
        public string Error { get; set; } = "";
        public bool Open { get; set; }
        public int Season { get; set; }

        /// <summary>Open: seconds until the season ends; closed: until the next one starts (-1 none planned).</summary>
        public long SecondsLeft { get; set; }
        public int Rating { get; set; }
        public string Tier { get; set; } = "";
        public int Rank { get; set; }
        public int Duels { get; set; }
        public int DuelsLeftToday { get; set; }

        /// <summary>While you were away: duels your ghost won and lost against challengers, and the rating it moved.</summary>
        public int AwayWins { get; set; }
        public int AwayLosses { get; set; }
        public int AwayChange { get; set; }
        public List<RankedResultEntry> Recent { get; set; } = new List<RankedResultEntry>();

        /// <summary>The season's top 10 by rating (Distance holds the rating).</summary>
        public List<DailyBoardEntry> Board { get; set; } = new List<DailyBoardEntry>();

        /// <summary>Season rewards granted since you last looked (cosmetic and badge ids).</summary>
        public List<string> Rewards { get; set; } = new List<string>();
    }
}
