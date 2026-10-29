using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>A badge the server granted, showing until a time (seconds since 1970 UTC).</summary>
    public sealed class BadgeSaveData
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("until")] public long Until;
    }
}
