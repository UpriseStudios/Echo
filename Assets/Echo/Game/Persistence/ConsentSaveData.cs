using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>The player's privacy choices (tech spec 19). An addition to the tech spec 10 schema.</summary>
    public sealed class ConsentSaveData
    {
        public const int CurrentVersion = 1;

        [JsonProperty("version")] public int Version;
        [JsonProperty("personalisedAds")] public bool PersonalisedAds;
        [JsonProperty("analytics")] public bool Analytics;

        /// <summary>False until the player answers the current consent screen; a new version asks again.</summary>
        [JsonIgnore] public bool Decided => Version >= CurrentVersion;
    }
}
