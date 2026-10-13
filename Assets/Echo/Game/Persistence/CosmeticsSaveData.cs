using System.Collections.Generic;
using Newtonsoft.Json;

namespace Echo.Game.Persistence
{
    public sealed class CosmeticsSaveData
    {
        public const string DefaultCharacter = "char_hooded";

        [JsonProperty("owned")] public List<string> Owned = new List<string> { "runner_default" };
        [JsonProperty("equipped")] public EquippedSaveData Equipped = new EquippedSaveData();

        /// <summary>Rewarded ads watched in full, of any placement; unlocks the ad characters.</summary>
        [JsonProperty("adsWatched")] public int AdsWatched;

        /// <summary>The Elementals trial (a user revision): ads towards the next trial, and trial runs left.</summary>
        [JsonProperty("trialAds")] public int TrialAds;
        [JsonProperty("trialRunsLeft")] public int TrialRunsLeft;
    }
}
