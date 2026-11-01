using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    public sealed class StatsSaveData
    {
        [JsonProperty("totalRuns")] public int TotalRuns;
        [JsonProperty("nemesesDefeated")] public int NemesesDefeated;
        [JsonProperty("worstNemesisKills")] public int WorstNemesisKills;
        [JsonProperty("worstNemesisName")] public string WorstNemesisName = "";

        // Schema 2 (skins addendum unlocks).
        [JsonProperty("deaths")] public int Deaths;
        [JsonProperty("maxDistance")] public int MaxDistance;
        [JsonProperty("maxGhostsAtStart")] public int MaxGhostsAtStart;
        [JsonProperty("maxGhostsCleared")] public int MaxGhostsCleared;
        // Schema 5 (2.1, unreleased): power-ups used, for Performance Enhanced. Older saves read 0.
        [JsonProperty("powerUpsUsed")] public int PowerUpsUsed;
    }
}
