using System.Collections.Generic;

namespace Echo.Services.Backend
{
    /// <summary>One Life's lobby (mirrors Server/EchoCloud/OneLifeResponse).</summary>
    public sealed class OneLifeInfo
    {
        public bool Ok;

        /// <summary>"closed" (not a One Life day) or "offline".</summary>
        public string Error = "";
        public bool Open;
        public string Date = "";
        public long SecondsLeft;
        public bool Played, Finished;
        public int Distance, Rank;
        public List<DailyBoardRow> Board = new List<DailyBoardRow>();
        public List<string> Rewards = new List<string>();

        public static OneLifeInfo Failed(string error) => new OneLifeInfo { Error = error };
    }
}
