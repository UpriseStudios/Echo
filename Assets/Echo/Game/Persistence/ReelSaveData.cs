using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>One saved run in the highlights reel (a user revision). It keeps its own content version, like the gallery.</summary>
    public sealed class ReelSaveData
    {
        [JsonProperty("contentVersion")] public string ContentVersion = "";
        [JsonProperty("seed")] public long Seed;
        [JsonProperty("speedPct")] public int SpeedPct = 100;
        [JsonProperty("runIndex")] public int RunIndex;
        [JsonProperty("bounties")] public bool Bounties;
        [JsonProperty("twist")] public int Twist;
        [JsonProperty("twistWeek")] public int TwistWeek;
        [JsonProperty("challenge")] public bool Challenge;
        [JsonProperty("daily")] public bool Daily;
        [JsonProperty("ghosts")] public List<GhostSaveData> Ghosts = new List<GhostSaveData>();
        [JsonProperty("run")] public GhostSaveData Run = new GhostSaveData();
        [JsonProperty("shieldTick")] public int ShieldTick = -1;
        [JsonProperty("phaseTick")] public int PhaseTick = -1;
        [JsonProperty("continueTick")] public int ContinueTick = -1;
        [JsonProperty("extraLife")] public bool ExtraLife;
        [JsonProperty("score")] public int Score;
        [JsonProperty("distance")] public int Distance;
        [JsonProperty("nearMisses")] public int NearMisses;
        [JsonProperty("ghostsAtStart")] public int GhostsAtStart;
        [JsonProperty("newBest")] public bool NewBest;
        [JsonProperty("savedOn")] public string SavedOn = "";
    }
}
