using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>The Daily Haunt on the device (version 2.0, schema 3): its achievement history and unsent runs.</summary>
    public sealed class DailySaveData
    {
        [JsonProperty("daysPlayed")] public int DaysPlayed;
        [JsonProperty("lastDay")] public int LastDay = -1;
        [JsonProperty("streak")] public int Streak;

        /// <summary>The last date played (yyyy-MM-dd), for the title's "not played today" dot.</summary>
        [JsonProperty("lastPlayedDate")] public string LastPlayedDate = "";

        /// <summary>The last day whose final rank was checked for Top Ten.</summary>
        [JsonProperty("closedDay")] public int ClosedDay = -1;

        /// <summary>Runs the server hasn't received yet, retried until the end of their day (Daily Haunt doc).</summary>
        [JsonProperty("pending")] public List<PendingDailyRun> Pending = new List<PendingDailyRun>();
    }
}
