using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>A daily run waiting to reach the server: what SubmitDailyRun needs to replay it.</summary>
    public sealed class PendingDailyRun
    {
        [JsonProperty("date")] public string Date = "";
        [JsonProperty("leaderVersion")] public string LeaderVersion = "";
        [JsonProperty("packet")] public string Packet = "";
    }
}
