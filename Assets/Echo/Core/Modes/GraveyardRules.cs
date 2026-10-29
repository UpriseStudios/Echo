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
    /// The Community Graveyard (modes doc): one course for the week, and each day up to 30 ghosts chosen from everyone's
    /// deaths so far: the course is split into 50 m sections, ranked by deaths, and up to 3 runs taken from each of the
    /// deadliest in turn, so the ghosts spread along the course. Played under the Daily Haunt's rules (distance, 100%
    /// speed, no Continue, banish, power-ups, bounty or twist; earned 1-UPs work). The ghosts are the leader kind, each
    /// tagged with how deadly its spot is instead of a name.
    /// </summary>
    public static class GraveyardRules
    {
        public const int SectionMetres = 50;
        public const int PerSection = 3;
        public const int MaxGhosts = 30;

        /// <summary>The runs each section keeps as candidates (more than it can use, so deleted ones can be skipped).</summary>
        public const int CandidatesKept = 6;

        /// <summary>The section a run ended in, from its distance in metres.</summary>
        public static int Section(int distanceMetres) => distanceMetres < 0 ? 0 : distanceMetres / SectionMetres;

        public static string ClaimLine(int deaths) => deaths == 1 ? "This spot has claimed 1 runner" : "This spot has claimed " + deaths.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " runners";

        /// <summary>
        /// The day's ghosts, in order (deadliest section first): sections by deaths (most first, then nearest the start),
        /// up to 3 runs from each, 30 in all. <paramref name="usable"/> says whether a run can still be had (deleted ones
        /// are skipped). The same tallies always give the same choice.
        /// </summary>
        public static List<(string Run, int Deaths)> Choose(IEnumerable<SectionTally> sections, System.Func<string, bool> usable = null)
        {
            var ordered = new List<SectionTally>(sections);
            ordered.Sort((a, b) => a.Deaths != b.Deaths ? b.Deaths.CompareTo(a.Deaths) : a.Index.CompareTo(b.Index));
            var chosen = new List<(string, int)>();
            foreach (SectionTally s in ordered)
            {
                int taken = 0;
                foreach (string run in s.Runs)
                {
                    if (chosen.Count == MaxGhosts) return chosen;
                    if (taken == PerSection) break;
                    if (usable != null && !usable(run)) continue;
                    chosen.Add((run, s.Deaths));
                    taken++;
                }
            }
            return chosen;
        }

        /// <summary>A chosen run as the ghost players meet: slot 0 the deadliest, tagged with its spot's toll.</summary>
        public static GhostRecord AsGhost(GhostRecord run, int slot, int deaths)
        {
            GhostRecord g = run.Clone();
            g.RunIndex = DailyRules.LeaderRunIndexBase + MaxGhosts - slot;
            g.Leader = true;
            g.Name = ClaimLine(deaths);
            g.Kills = 0;
            g.LastKillRun = 0;
            return g;
        }

        public static RunSimulation Start(Course course, IEnumerable<GhostRecord> ghosts, int runIndex, in SimConfig cfg)
        {
            if (runIndex <= 0 || runIndex >= DailyRules.LeaderRunIndexBase) throw new System.ArgumentOutOfRangeException(nameof(runIndex));
            GhostSet set = DailyRules.Ghosts(ghosts, new GhostRecord[0]);
            return new RunSimulation(course, set, runIndex, DailyRules.SpeedPct, cfg, new RunOptions { Daily = true, Graveyard = true });
        }

        /// <summary>Why a submitted run breaks the Graveyard's rules, or null.</summary>
        public static string Check(Reel reel)
        {
            string daily = DailyRules.Check(reel);
            if (daily != null) return daily;
            if (!reel.Options.Graveyard || reel.Options.OneLife) return "options";
            foreach (GhostRecord g in reel.Ghosts)
                if (!g.Leader) return "ghosts";
            return null;
        }
    }
}
