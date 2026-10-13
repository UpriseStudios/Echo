using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>Today's Daily Haunt from the server (mirrors Server/EchoCloud/GetDailyResponse).</summary>
    public sealed class DailyInfo
    {
        public bool Ok;

        /// <summary>"offline" when the server could not be reached, or the server's reason.</summary>
        public string Error = "";
        public string Date = "";
        public int Day;
        public long Seed;
        public string ContentVersion = "";
        public int SecondsLeft;
        public int LeaderVersion;

        /// <summary>The run's ghosts as a Core.Net.ReelPacket: today's leaders and the player's own daily ghosts.</summary>
        public string GhostsPacket = "";
        public List<DailyBoardRow> Board = new List<DailyBoardRow>();
        public int BestDistance;
        public int Rank;
        public int NextRunIndex = 1;
        public string YesterdayWinner = "";
        public int YesterdayWinnerDistance;
        public int YesterdayRank;

        public static DailyInfo Failed(string error) => new DailyInfo { Error = error };
    }
}
