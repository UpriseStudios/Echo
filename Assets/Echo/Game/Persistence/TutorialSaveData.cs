using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>First-time experience flags (game spec). An addition to the tech spec 10 schema.</summary>
    public sealed class TutorialSaveData
    {
        [JsonProperty("seenTapHint")] public bool SeenTapHint;
        [JsonProperty("seenGhostHint")] public bool SeenGhostHint;
        [JsonProperty("usedDoubleJump")] public bool UsedDoubleJump;
        [JsonProperty("seenHopHint")] public bool SeenHopHint;

        /// <summary>How to play (schema 4): the tutorial clips have been shown (or skipped) once.</summary>
        [JsonProperty("seenHowToPlay")] public bool SeenHowToPlay;
    }
}
