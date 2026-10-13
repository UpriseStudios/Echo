using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>How many of each power-up the player holds (a user revision).</summary>
    public sealed class PowerUpsSaveData
    {
        [JsonProperty("shield")] public int Shield;
        [JsonProperty("phase")] public int Phase;
        [JsonProperty("lantern")] public int Lantern;
        [JsonProperty("extraLife")] public int ExtraLife;
    }
}
