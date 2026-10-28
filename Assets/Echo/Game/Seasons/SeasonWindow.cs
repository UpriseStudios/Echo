using Newtonsoft.Json;

namespace Echo.Game.Seasons
{
    /// <summary>
    /// One season in the schedule (seasons doc, "Season schedule"): its id and UK local dates, inclusive. A date is
    /// "MM-dd" (every year; an end before the start runs over New Year) or "yyyy-MM-dd" (that year only, for Easter).
    /// </summary>
    public sealed class SeasonWindow
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("start")] public string Start = "";
        [JsonProperty("end")] public string End = "";

        /// <summary>Off: the season's art ships but stays switched off (seasons doc: dormant until next year).</summary>
        [JsonProperty("enabled")] public bool Enabled = true;

        /// <summary>Whether the season's achievements can be earned while it is on.</summary>
        [JsonProperty("achievements")] public bool Achievements;
    }
}
