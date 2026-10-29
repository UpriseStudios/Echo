using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// Competitive modes on the device (version 2.1, schema 5): the Naughty vs Nice side and event, the results already
    /// revealed, and the profile the server last sent (online name and badges), shown at the top of Stats.
    /// </summary>
    public sealed class ModesSaveData
    {
        [JsonProperty("teamEvent")] public string TeamEvent = "";
        [JsonProperty("team")] public string Team = "";
        [JsonProperty("revealed")] public List<string> Revealed = new List<string>();
        [JsonProperty("playerName")] public string PlayerName = "";
        [JsonProperty("badges")] public List<BadgeSaveData> Badges = new List<BadgeSaveData>();
    }
}
