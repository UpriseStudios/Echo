using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Runs played the way the game plays them (version 2.0 replay parity): a tap and a finger state every tick,
    /// sometimes a Shield, Phase, Extra 1-UP or Continue, against ghosts that are themselves such runs. The player
    /// looks ahead with the runner's own rules (not the ghosts), so runs go far enough to meet ghosts, and makes the
    /// odd mistake so they end. Shared by the EditMode tests and the server's parity test, which compile this file.
    /// </summary>
    public static class ParityRuns
    {
        const int LookAhead = 50;
        const int HoldTicks = 30;

        public sealed class Played
        {
            public RunSimulation Run;
            public Reel Reel;
        }

        public static Played Play(Course course, GhostSet ghosts, uint seed, in SimConfig cfg, string contentVersion, int maxTicks = 6000)
        {
            var rng = new Xorshift32(seed | 1u);
            bool powerUps = rng.NextInt(2) == 0;
            var options = new RunOptions { Bounties = rng.NextInt(2) == 0, Twist = (Twist)rng.NextInt(7), TwistWeek = 2900 };
            int speedPct = 100 + 5 * rng.NextInt(6);
            var run = new RunSimulation(course, ghosts, ghosts.Count + 1, speedPct, cfg, options);
            if (powerUps) run.GrantExtraLife();
            int shieldAt = powerUps ? 20 + rng.NextInt(600) : -1, phaseAt = powerUps ? 20 + rng.NextInt(1200) : -1;
            bool continues = rng.NextInt(2) == 0;
            int mistakeOdds = 300 + rng.NextInt(900);
            int speed = cfg.SpeedForPct(speedPct);
            int holdUntil = -1;
            while (run.Tick < maxTicks)
            {
                if (run.IsDead)
                {
                    if (continues && !run.ContinueUsed) run.Revive();
                    else break;
                }
                if (run.Tick == shieldAt) run.UseShield();
                if (run.Tick == phaseAt) run.UsePhase();

                bool tap = false;
                if (run.Tick >= holdUntil && rng.NextInt(mistakeOdds) != 0)
                {
                    RunnerState now = run.Player;
                    int none = Survives(now, course, run.Tick, speed, cfg, -1, 0);
                    if (none < LookAhead)
                    {
                        int hold = Survives(now, course, run.Tick, speed, cfg, 0, HoldTicks);
                        int hop = Survives(now, course, run.Tick, speed, cfg, 0, cfg.HopHoldTicks);
                        if (hold > none || hop > none)
                        {
                            tap = true;
                            int length = hold > hop || (hold == hop && rng.NextInt(2) == 0) ? HoldTicks : cfg.HopHoldTicks;
                            holdUntil = run.Tick + length;
                        }
                    }
                }
                run.Step(tap, run.Tick < holdUntil);
            }
            Reel reel = run.IsDead ? ReelRules.From(run, contentVersion, "", false) : null;
            return new Played { Run = run, Reel = reel };
        }

        // How many ticks the runner alone stays alive (up to LookAhead) if it taps at tapAt (-1: never) and keeps
        // the finger down for `hold` ticks. The finger rule matches RunSimulation.Step.
        static int Survives(RunnerState s, Course course, int tick, int speed, in SimConfig cfg, int tapAt, int hold)
        {
            bool down = false;
            for (int i = 0; i < LookAhead; i++)
            {
                bool tap = i == tapAt;
                down = (tap || down) && i < tapAt + hold;
                Runner.Step(ref s, tap, course, tick + i, speed, cfg, down);
                if (!s.Alive) return i;
            }
            return LookAhead;
        }

        /// <summary>A graveyard of earlier runs, oldest first, a few of them with kills.</summary>
        public static GhostSet Ghosts(Course course, int count, uint seed, in SimConfig cfg)
        {
            var set = new GhostSet();
            var rng = new Xorshift32(seed | 1u);
            for (int i = 1; i <= count; i++)
            {
                // Only finished runs become ghosts, as in the game.
                Played p;
                do p = Play(course, new GhostSet(), rng.NextUInt(), cfg, "", 4000);
                while (!p.Run.IsDead);
                GhostRecord g = p.Run.ToGhostRecord();
                g.RunIndex = i;
                g.Kills = rng.NextInt(4) == 0 ? rng.NextInt(5) : 0;
                g.LastKillRun = g.Kills > 0 ? i : 0;
                set.Add(g);
            }
            return set;
        }

        public static IEnumerable<uint> Seeds(int count, uint start)
        {
            var rng = new Xorshift32(start | 1u);
            for (int i = 0; i < count; i++) yield return rng.NextUInt();
        }
    }
}
