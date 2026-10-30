using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>A Community Graveyard run waiting to reach the server: what SubmitGraveyardRun needs to replay it.</summary>
    public sealed class PendingGraveyardRun
    {
        [JsonProperty("date")] public string Date = "";
        [JsonProperty("setVersion")] public string SetVersion = "";
        [JsonProperty("packet")] public string Packet = "";
    }
}
