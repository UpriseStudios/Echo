using System.Collections.Generic;

namespace Echo.Core.Achievements
{
    /// <summary>
    /// Which event each seasonal achievement's progress belongs to (seasons doc: progress only counts during the event
    /// and starts again next time if unfinished). An event is a season id and the year it started, e.g. "christmas-2026".
    /// </summary>
    public sealed class SeasonalProgress
    {
        /// <summary>Achievement id to the event its progress was made in.</summary>
        public readonly Dictionary<string, string> Event = new Dictionary<string, string>();

        /// <summary>Achievement id to the last whole UTC day it counted (days played, counted once each).</summary>
        public readonly Dictionary<string, int> LastDay = new Dictionary<string, int>();
    }
}
