using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    public sealed class GhostSaveData
    {
        [JsonProperty("runIndex")] public int RunIndex;
        [JsonProperty("tapTicks")] public int[] TapTicks = new int[0];
        [JsonProperty("reviveTicks")] public int[] ReviveTicks = new int[0];
        [JsonProperty("releaseTicks")] public int[] ReleaseTicks = new int[0];
        [JsonProperty("deathTick")] public int DeathTick;
        [JsonProperty("speedPct")] public int SpeedPct = 100;
        [JsonProperty("kills")] public int Kills;
        [JsonProperty("lastKillRun")] public int LastKillRun;

        // Schema 2 (skins addendum).
        [JsonProperty("skinId")] public string SkinId = "";
        [JsonProperty("deathX")] public int DeathX = -1;
        [JsonProperty("cause")] public int Cause;
        [JsonProperty("killerRunIndex")] public int KillerRunIndex;
    }
}
