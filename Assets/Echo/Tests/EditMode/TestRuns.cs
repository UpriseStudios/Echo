using System;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Tests.EditMode
{
    internal static class TestRuns
    {
        /// <summary>Ghosts locked to the player: the rule tests (grace, hitbox, kills, near-miss) are written for it.</summary>
        public static readonly SimConfig Cfg = SimConfig.Default.WithLockstepGhosts();

        /// <summary>Endless flat floor, seed 1. Nothing kills the player except ghosts.</summary>
        public static RunSimulation OnFlat(GhostSet ghosts, int runIndex = 1) =>
            new RunSimulation(TestCourses.Flat(), ghosts, runIndex, 100, Cfg);

        public static GhostRecord Ghost(int runIndex, int deathTick, params int[] taps) =>
            new GhostRecord { RunIndex = runIndex, DeathTick = deathTick, TapTicks = taps, SpeedPct = 100 };

        /// <summary>A ghost that runs along the flat floor without jumping for longer than any test lasts.</summary>
        public static GhostRecord Walker(int runIndex = 1) => Ghost(runIndex, 100_000);

        public static GhostSet Set(params GhostRecord[] ghosts)
        {
            var set = new GhostSet();
            foreach (var g in ghosts) set.Add(g);
            return set;
        }

        /// <summary>Steps until <paramref name="tick"/> or death, tapping on the listed ticks.</summary>
        public static void StepTo(RunSimulation run, int tick, params int[] taps)
        {
            while (run.Tick < tick && !run.IsDead) run.Step(Array.IndexOf(taps, run.Tick) >= 0);
        }

        public static Progress ProgressOnFlat(params GhostRecord[] ghosts)
        {
            var p = new Progress { Seed = 1 };
            foreach (var g in ghosts) p.Ghosts.Add(g);
            return p;
        }
    }
}
