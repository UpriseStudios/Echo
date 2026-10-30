using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    public sealed class SettingsSaveData
    {
        [JsonProperty("music")] public bool Music = true;
        [JsonProperty("sfx")] public bool Sfx = true;
        [JsonProperty("haptics")] public bool Haptics = true;
        [JsonProperty("glow")] public bool Glow = true;
        /// <summary>Play this week's twist (on) or Classic (value-adds doc: a twist must never block a best).</summary>
        [JsonProperty("playTwist")] public bool PlayTwist = true;

        /// <summary>The name on Ghost Challenges ("Jamie's Ghost"); asked for the first time one is sent.</summary>
        [JsonProperty("playerName")] public string PlayerName = "";

        /// <summary>
        /// A user revision (worlds): a season's look shows during its dates over the world you wear (on), or your world
        /// always shows (off). Seasonal achievements count either way.
        /// </summary>
        [JsonProperty("seasonalLooks")] public bool SeasonalLooks = true;
    }
}
