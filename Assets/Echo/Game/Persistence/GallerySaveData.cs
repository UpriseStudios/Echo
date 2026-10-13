using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>One Rogues' Gallery entry (value-adds doc), with its ghost run for a replay.</summary>
    public sealed class GallerySaveData
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("skinId")] public string SkinId = "";
        [JsonProperty("kills")] public int Kills;
        [JsonProperty("defeatedOn")] public string DefeatedOn = "";
        [JsonProperty("epitaph")] public string Epitaph = "";
        [JsonProperty("seed")] public long Seed;
        [JsonProperty("contentVersion")] public string ContentVersion = "";
        [JsonProperty("ghost")] public GhostSaveData Ghost = new GhostSaveData();
    }
}
