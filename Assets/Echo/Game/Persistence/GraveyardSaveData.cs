using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// The Community Graveyard (modes doc, save schema 5): runs waiting to reach the server, the run index to use next,
    /// and the last week whose summary has been shown.
    /// </summary>
    public sealed class GraveyardSaveData
    {
        [JsonProperty("pending")] public List<PendingGraveyardRun> Pending = new List<PendingGraveyardRun>();
        [JsonProperty("date")] public string Date = "";
        [JsonProperty("nextRun")] public int NextRun = 1;
        [JsonProperty("summarySeen")] public string SummarySeen = "";
    }
}
