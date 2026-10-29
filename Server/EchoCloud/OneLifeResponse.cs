using System.Collections.Generic;

namespace EchoCloud
{
    /// <summary>One Life's lobby (modes doc): today's board, your run if you've had it, and rewards settled since.</summary>
    public sealed class OneLifeResponse
    {
        public bool Ok { get; set; }

        /// <summary>"closed" (not a One Life day).</summary>
        public string Error { get; set; } = "";
        public bool Open { get; set; }
        public string Date { get; set; } = "";

        /// <summary>Open: seconds until today's One Life closes; closed: until it next opens (-1 never).</summary>
        public long SecondsLeft { get; set; }

        /// <summary>Today's attempt has been used (started), and whether its run has come in.</summary>
        public bool Played { get; set; }
        public bool Finished { get; set; }
        public int Distance { get; set; }
        public int Rank { get; set; }
        public List<DailyBoardEntry> Board { get; set; } = new List<DailyBoardEntry>();

        /// <summary>Rewards granted from days that have closed since you last looked (cosmetic and badge ids).</summary>
        public List<string> Rewards { get; set; } = new List<string>();
    }
}
