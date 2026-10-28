using Newtonsoft.Json;

namespace Echo.Tests.PlayMode
{
    /// <summary>One clip in Marketing/clip-list.json: the scenario the bot plays and how the Vertical clip is cut.</summary>
    public sealed class MarketingClipSpec
    {
        /// <summary>The file name: Marketing/Clips/&lt;id&gt;.mp4.</summary>
        [JsonProperty("id")] public string Id = "";

        /// <summary>Course seed; also seeds the bot, so a clip comes out the same every time.</summary>
        [JsonProperty("seed")] public uint Seed = 1;

        /// <summary>Ghosts on the course before the run: real failed runs, each wearing the next of <see cref="GhostSkins"/>.</summary>
        [JsonProperty("ghosts")] public int Ghosts;
        [JsonProperty("ghostSkins")] public string[] GhostSkins = { "char_hooded" };

        /// <summary>The runner's character.</summary>
        [JsonProperty("skin")] public string Skin = "char_hooded";

        /// <summary>The newest ghost's kills; 5 or more makes it the Nemesis (with horns and a name).</summary>
        [JsonProperty("nemesisKills")] public int NemesisKills;

        /// <summary>How the run must end: "any", "nemesis" (the Nemesis kills you) or "bounty" (a bounty ghost is skimmed).</summary>
        [JsonProperty("ending")] public string Ending = "any";

        /// <summary>How long the bot keeps playing well before it stops jumping.</summary>
        [JsonProperty("surviveSeconds")] public int SurviveSeconds = 20;

        /// <summary>Clip length (9-20 s), ending just after the death.</summary>
        [JsonProperty("seconds")] public int Seconds = 15;

        /// <summary>Hook text; {distance} and {ghosts} are filled in from the run.</summary>
        [JsonProperty("hook")] public string Hook = "";
        [JsonProperty("endCard")] public bool EndCard = true;
    }
}
