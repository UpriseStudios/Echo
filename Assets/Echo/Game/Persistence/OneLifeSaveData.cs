using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>
    /// One Life (version 2.1, schema 5): today's attempt in flight. While a run is going (or was cut off by closing the
    /// game) its token and its taps so far wait here, and are sent when the game next can (modes doc).
    /// </summary>
    public sealed class OneLifeSaveData
    {
        [JsonProperty("date")] public string Date = "";
        [JsonProperty("token")] public string Token = "";

        /// <summary>The run as a ReelPacket, marked abandoned when the game was closed mid-run; "" while none waits.</summary>
        [JsonProperty("pending")] public string Pending = "";
    }
}
