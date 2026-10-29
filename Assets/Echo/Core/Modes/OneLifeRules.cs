using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.Modes
{
    /// <summary>
    /// One Life (modes doc): one attempt a day on its own course, against today's top 10 One Life runs as named leader
    /// ghosts and nothing else. Played under the Daily Haunt's rules (distance, 100% speed, no Continue, banish,
    /// power-ups, bounty or twist) with no 1-UPs. The run is the player's only one, so it is always run index 1.
    /// </summary>
    public static class OneLifeRules
    {
        public const int RunIndex = 1;

        public static RunSimulation Start(Course course, IEnumerable<GhostRecord> leaders, in SimConfig cfg)
        {
            GhostSet ghosts = DailyRules.Ghosts(leaders, new GhostRecord[0]);
            return new RunSimulation(course, ghosts, RunIndex, DailyRules.SpeedPct, cfg, new RunOptions { Daily = true, OneLife = true });
        }

        /// <summary>Why a submitted run breaks One Life's rules, or null.</summary>
        public static string Check(Reel reel)
        {
            string daily = DailyRules.Check(reel);
            if (daily != null) return daily;
            if (!reel.Options.OneLife) return "options";
            if (reel.RunIndex != RunIndex) return "run-index";
            foreach (GhostRecord g in reel.Ghosts)
                if (!g.Leader) return "ghosts";
            if (reel.Run.ReviveTicks != null && reel.Run.ReviveTicks.Length > 0) return "power-ups";
            return null;
        }
    }
}
