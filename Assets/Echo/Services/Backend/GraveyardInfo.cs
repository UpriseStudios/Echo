using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>The Community Graveyard's lobby (mirrors Server/EchoCloud/GraveyardResponse).</summary>
    public sealed class GraveyardInfo
    {
        public bool Ok;

        /// <summary>"closed" (no Graveyard week running) or "offline".</summary>
        public string Error = "";
        public bool Open;
        public string Date = "";
        public long SecondsLeft;
        public long Seed;
        public string ContentVersion = "";
        public int Day, DaysLeft;

        /// <summary>Today's ghosts (a ReelPacket) and their version, sent back with every run.</summary>
        public string SetVersion = "";
        public string GhostsPacket = "";
        public int Size, BestToday, WeekTotal, DaysPlayed;
        public int NextRunIndex = 1;
        public int DailyRank, WeeklyRank;
        public List<DailyBoardRow> Daily = new List<DailyBoardRow>();
        public List<DailyBoardRow> Weekly = new List<DailyBoardRow>();
        public List<string> Rewards = new List<string>();

        /// <summary>Last week, if you played it; null otherwise.</summary>
        public GraveyardWeekSummary LastWeek;

        public static GraveyardInfo Failed(string error) => new GraveyardInfo { Error = error };
    }
}
