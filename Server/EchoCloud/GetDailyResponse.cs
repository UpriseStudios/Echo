using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>Everything the daily lobby and a daily run need (Daily Haunt doc, GetDaily).</summary>
    public sealed class GetDailyResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        /// <summary>Today in UTC as yyyy-MM-dd, and as a whole day number for the achievements.</summary>
        public string Date { get; set; } = "";
        public int Day { get; set; }
        public long Seed { get; set; }
        public string ContentVersion { get; set; } = "";
        public int SecondsLeft { get; set; }

        /// <summary>The leader set this run will face; SubmitDailyRun takes it back so the run replays against it.</summary>
        public string LeaderVersion { get; set; } = "";

        /// <summary>The run's ghosts, as a ReelPacket whose Ghosts are today's leaders (not the player's own) and their daily ghosts.</summary>
        public string GhostsPacket { get; set; } = "";
        public List<DailyBoardEntry> Board { get; set; } = new List<DailyBoardEntry>();
        public int BestDistance { get; set; }
        public int Rank { get; set; }
        public int NextRunIndex { get; set; } = 1;
        public string YesterdayWinner { get; set; } = "";
        public int YesterdayWinnerDistance { get; set; }

        /// <summary>Where the player finished yesterday (0 if they didn't play), for the Top Ten achievement.</summary>
        public int YesterdayRank { get; set; }
    }
}
