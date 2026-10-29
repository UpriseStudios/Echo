using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>The Community Graveyard's lobby (modes doc): the week's course, today's ghosts, both boards and your standing.</summary>
    public sealed class GraveyardResponse
    {
        public bool Ok { get; set; }

        /// <summary>"closed" (no Graveyard week running).</summary>
        public string Error { get; set; } = "";
        public bool Open { get; set; }
        public string Date { get; set; } = "";

        /// <summary>Open: seconds until today's ghosts change; closed: until the Graveyard next opens (-1 never).</summary>
        public long SecondsLeft { get; set; }
        public uint Seed { get; set; }
        public string ContentVersion { get; set; } = "";

        /// <summary>0 Monday .. 6 Sunday, and the days after today.</summary>
        public int Day { get; set; }
        public int DaysLeft { get; set; }

        /// <summary>Today's ghosts (a ReelPacket) and their version, sent back with every run.</summary>
        public string SetVersion { get; set; } = "";
        public string GhostsPacket { get; set; } = "";

        /// <summary>Runners the week's graveyard has claimed so far.</summary>
        public int Size { get; set; }
        public int BestToday { get; set; }
        public int WeekTotal { get; set; }
        public int DaysPlayed { get; set; }
        public int NextRunIndex { get; set; } = 1;
        public int DailyRank { get; set; }
        public int WeeklyRank { get; set; }
        public List<DailyBoardEntry> Daily { get; set; } = new List<DailyBoardEntry>();
        public List<DailyBoardEntry> Weekly { get; set; } = new List<DailyBoardEntry>();

        /// <summary>Rewards granted from weeks that have closed since you last looked (cosmetic and badge ids).</summary>
        public List<string> Rewards { get; set; } = new List<string>();
    }
}
