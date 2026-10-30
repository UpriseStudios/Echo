using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// Ghost Duels (modes doc, save schema 5): a ranked duel in flight. Its token, and the run (finished, or cut off when
    /// the game was put away, marked abandoned) waiting to be sent; "" while none waits.
    /// </summary>
    public sealed class RankedSaveData
    {
        [JsonProperty("token")] public string Token = "";
        [JsonProperty("pending")] public string Pending = "";
    }
}
