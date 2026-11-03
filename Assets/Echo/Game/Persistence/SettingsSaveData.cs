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

        /// <summary>
        /// World Time Run doc, phase 3: Full effects (bloom, the Far Future's lights) follows Glow once the player has
        /// chosen; until then the game decides: reduced on weaker phones, or once the Far Future ran slow (EffectsReduced).
        /// </summary>
        [JsonProperty("effectsChosen")] public bool EffectsChosen;
        [JsonProperty("effectsReduced")] public bool EffectsReduced;
    }
}
