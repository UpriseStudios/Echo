using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    /// <summary>The save file document (tech spec 10).</summary>
    public sealed class SaveData
    {
        // 2: ghosts carry skin, death x, cause and killer; stats gain the unlock counters (skins addendum).
        // 3 (version 2.0): the best run keeps its ghost snapshot (bestReel), for full challenges from Stats.
        public const int CurrentSchema = 3;

        [JsonProperty("schema")] public int Schema = CurrentSchema;
        [JsonProperty("contentVersion")] public string ContentVersion = "";
        [JsonProperty("best")] public int Best;
        [JsonProperty("clears")] public int Clears;
        [JsonProperty("seed")] public long Seed;
        [JsonProperty("nextRunIndex")] public int NextRunIndex = 1;
        [JsonProperty("cycleBanishes")] public int CycleBanishes;
        [JsonProperty("ghosts")] public List<GhostSaveData> Ghosts = new List<GhostSaveData>();
        [JsonProperty("stats")] public StatsSaveData Stats = new StatsSaveData();
        [JsonProperty("cosmetics")] public CosmeticsSaveData Cosmetics = new CosmeticsSaveData();
        [JsonProperty("achievements")] public List<string> Achievements = new List<string>();
        [JsonProperty("achievementProgress")] public Dictionary<string, int> AchievementProgress = new Dictionary<string, int>();
        [JsonProperty("twistWeek")] public int TwistWeek;
        [JsonProperty("twistBest")] public int TwistBest;
        [JsonProperty("duelUsed")] public bool DuelUsed;

        // Balance addendum: Banish 10 once per high-score cycle, and classic runs since the last best.
        [JsonProperty("banishTenUsed")] public bool BanishTenUsed;
        [JsonProperty("runsSinceBest")] public int RunsSinceBest;
        [JsonProperty("gallery")] public List<GallerySaveData> Gallery = new List<GallerySaveData>();

        /// <summary>The highlights reel (a user revision): up to five saved runs.</summary>
        [JsonProperty("reels")] public List<ReelSaveData> Reels = new List<ReelSaveData>();

        /// <summary>Ghost Challenges: the run behind the current best, as its challenge code (it carries its own content version).</summary>
        [JsonProperty("bestRun")] public string BestRun = "";

        /// <summary>Schema 3: the best run with the ghosts it started against. Empty before a best set since 2.0.</summary>
        [JsonProperty("bestReel")] public ReelSaveData BestReel;

        /// <summary>Power-ups held, up to PowerUpRules.MaxHeld of each (a user revision).</summary>
        [JsonProperty("powerUps")] public PowerUpsSaveData PowerUps = new PowerUpsSaveData();
        [JsonProperty("settings")] public SettingsSaveData Settings = new SettingsSaveData();
        [JsonProperty("tutorial")] public TutorialSaveData Tutorial = new TutorialSaveData();
        [JsonProperty("consent")] public ConsentSaveData Consent = new ConsentSaveData();
    }
}
