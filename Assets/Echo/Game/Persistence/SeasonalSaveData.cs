using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>Seasons doc (version 2.1, schema 5): which event each seasonal achievement's progress belongs to.</summary>
    public sealed class SeasonalSaveData
    {
        [JsonProperty("event")] public Dictionary<string, string> Event = new Dictionary<string, string>();
        [JsonProperty("lastDay")] public Dictionary<string, int> LastDay = new Dictionary<string, int>();
    }
}
