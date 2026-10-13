using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    public sealed class EquippedSaveData
    {
        [JsonProperty("runner")] public string Runner = "runner_default";
        [JsonProperty("trail")] public string Trail = "trail_default";
        [JsonProperty("palette")] public string Palette = "palette_default";
        [JsonProperty("character")] public string Character = "char_hooded";
    }
}
