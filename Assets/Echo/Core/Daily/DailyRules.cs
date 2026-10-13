using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.Daily
{
    /// <summary>
    /// The Daily Haunt's rules (version 2.0, Daily Haunt doc): one course a day for everyone, scored on distance, haunted
    /// by today's top 10 runs (leaders, which claim their meeting points first) and the player's own failed attempts,
    /// at most 20 (a user decision), cleared by a new daily best. No Continue, banish, duel, bounty, twist or
    /// power-ups (a user decision); lives earned by playing work as in the main game. The game and the server both
    /// use these, so a run is judged by the same rules it was played under.
    /// </summary>
    public static class DailyRules
    {
        public const int Leaders = 10;
        public const int OwnGhostCap = 20;
        public const int SpeedPct = 100;

        /// <summary>
        /// Leader ghosts take run indices above any player's own (rank 1 highest), so they never collide with the
        /// player's daily attempts and the best rank claims its meeting point first.
        /// </summary>
        public const int LeaderRunIndexBase = 1_000_000;

        /// <summary>A leader's best run, as the ghost others meet: rank 1 to 10, with its owner's name on its tag.</summary>
        public static GhostRecord AsLeader(GhostRecord run, int rank, string name)
        {
            if (rank < 1 || rank > Leaders) throw new ArgumentOutOfRangeException(nameof(rank));
            GhostRecord g = run.Clone();
            g.RunIndex = LeaderRunIndexBase + Leaders + 1 - rank;
            g.Leader = true;
            g.Name = name ?? "";
            g.Kills = 0;
            g.LastKillRun = 0;
            return g;
        }

        /// <summary>The ghosts of a daily run: today's leaders and the player's own attempts.</summary>
        public static GhostSet Ghosts(IEnumerable<GhostRecord> leaders, IEnumerable<GhostRecord> own)
        {
            var set = new GhostSet();
            foreach (GhostRecord g in leaders) set.Add(g.Clone());
            foreach (GhostRecord g in own) set.Add(g.Clone());
            return set;
        }

        public static RunSimulation Start(Course course, GhostSet ghosts, int runIndex, in SimConfig cfg)
        {
            if (runIndex <= 0 || runIndex >= LeaderRunIndexBase) throw new ArgumentOutOfRangeException(nameof(runIndex));
            return new RunSimulation(course, ghosts, runIndex, SpeedPct, cfg, new RunOptions { Daily = true });
        }

        /// <summary>
        /// Why a submitted run breaks the daily rules, or null when it keeps them. The server checks this before
        /// replaying, so a run played under other rules never reaches the leaderboard.
        /// </summary>
        public static string Check(Reel reel)
        {
            RunOptions o = reel.Options;
            if (!o.Daily || o.Duel || o.Challenge || o.Bounties || o.Twist != Twist.None) return "options";
            if (reel.ContinueTick >= 0 || reel.ShieldTick >= 0 || reel.PhaseTick >= 0 || reel.ExtraLife) return "power-ups";
            if (reel.SpeedPct != SpeedPct) return "speed";
            if (reel.RunIndex <= 0 || reel.RunIndex >= LeaderRunIndexBase) return "run-index";
            return null;
        }

        /// <summary>
        /// The ghosts a run faced are exactly the ones the server gave it (by run index, inputs, speed and leader flag),
        /// so nobody can submit a run against an easier crowd.
        /// </summary>
        public static bool SameGhosts(IReadOnlyList<GhostRecord> faced, IReadOnlyList<GhostRecord> expected)
        {
            if (faced.Count != expected.Count) return false;
            var byIndex = new Dictionary<int, GhostRecord>();
            foreach (GhostRecord g in expected) byIndex[g.RunIndex] = g;
            foreach (GhostRecord g in faced)
            {
                if (!byIndex.TryGetValue(g.RunIndex, out GhostRecord e)) return false;
                if (g.Leader != e.Leader || g.SpeedPct != e.SpeedPct || g.DeathTick != e.DeathTick || g.Kills != e.Kills) return false;
                if (!Same(g.TapTicks, e.TapTicks) || !Same(g.ReleaseTicks, e.ReleaseTicks) || !Same(g.ReviveTicks, e.ReviveTicks)) return false;
            }
            return true;
        }

        static bool Same(int[] a, int[] b)
        {
            a = a ?? Array.Empty<int>();
            b = b ?? Array.Empty<int>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>A finished daily run is settled by the server, never committed to the player's progress.</summary>
        public static void Settle(RunSimulation run)
        {
            if (!run.Options.Daily) throw new InvalidOperationException("not a daily run");
            run.Committed = true;
        }

        /// <summary>A failed attempt joins the player's daily ghosts, the oldest dropping past the cap.</summary>
        public static void AddOwnGhost(List<GhostRecord> own, GhostRecord run)
        {
            own.Add(run);
            own.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            while (own.Count > OwnGhostCap) own.RemoveAt(0);
        }

        /// <summary>Leaders passed: "passing" one means going further than its run did (a user decision).</summary>
        public static int Passed(IEnumerable<int> leaderDistances, int distance)
        {
            int n = 0;
            foreach (int d in leaderDistances) if (distance > d) n++;
            return n;
        }

        /// <summary>The nearest leader distance still ahead of <paramref name="distance"/>, or -1 when all are passed.</summary>
        public static int NextToBeat(IEnumerable<int> leaderDistances, int distance)
        {
            int next = -1;
            foreach (int d in leaderDistances)
                if (d >= distance && (next < 0 || d < next)) next = d;
            return next;
        }

        /// <summary>A day was played: counts different days and the run of days in a row.</summary>
        public static void NotePlayed(DailyProgress p, int day)
        {
            if (day == p.LastDay) return;
            p.DaysPlayed++;
            p.Streak = p.LastDay >= 0 && day == p.LastDay + 1 ? p.Streak + 1 : 1;
            p.LastDay = day;
        }
    }
}
