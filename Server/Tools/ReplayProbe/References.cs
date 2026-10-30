using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Server;
using Echo.Tests.EditMode;

namespace Echo.Server.Tools
{
    /// <summary>
    /// `ReplayProbe references` (modes doc, Ghost Duels: "a simple tool to record the reference runs for each arena").
    /// Offline: for every arena of every season in Server/Config/echo.rc's "ranked" key, the look-ahead player (the one
    /// the parity tests use) runs the course, and its run is cut at five distances that stand for five ratings. The cut
    /// is an abandoned run, which replays to exactly where it stopped. Writes Server/EchoCloud/RankedReferences.json,
    /// which the module ships with; redeploy after running it. Re-run it whenever the arenas or the content change.
    /// </summary>
    public static class References
    {
        static readonly (int Metres, int Rating)[] Levels = { (150, 850), (300, 1000), (500, 1150), (800, 1300), (1200, 1450) };

        public static int Run(GameContent content, string serverDir)
        {
            string rcPath = Path.Combine(serverDir, "Config", "echo.rc");
            using JsonDocument rc = JsonDocument.Parse(File.ReadAllText(rcPath));
            var seeds = new List<uint>();
            foreach (JsonElement season in rc.RootElement.GetProperty("entries").GetProperty("ranked").GetProperty("seasons").EnumerateArray())
                foreach (JsonElement a in season.GetProperty("arenas").EnumerateArray())
                    seeds.Add(a.GetUInt32());
            seeds = seeds.Distinct().ToList();

            var references = new List<object>();
            foreach (uint seed in seeds)
            {
                Course course = content.CourseFor(seed);
                foreach (var (metres, rating) in Levels)
                {
                    Reel reel = Reference(course, seed, metres, content);
                    if (reel == null)
                    {
                        Console.WriteLine($"arena {seed}: no run reached {metres} m");
                        continue;
                    }
                    int distance = RunReplay.Replay(reel, course, content.Config).Distance;
                    references.Add(new { Seed = seed, Rating = rating, Distance = distance, Name = "Echo Ghost", Packet = ReelPacket.Encode(reel) });
                    Console.WriteLine($"arena {seed}: {distance} m as {rating}");
                }
            }
            string outPath = Path.Combine(serverDir, "EchoCloud", "RankedReferences.json");
            File.WriteAllText(outPath, JsonSerializer.Serialize(references, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"\n{references.Count} reference runs for {seeds.Count} arenas written to {outPath}. Redeploy to ship them.");
            return references.Count > 0 ? 0 : 1;
        }

        const int HoldTicks = 30;

        // A run on the arena that gets at least `metres` far, cut there as an abandoned run. The player is the parity
        // tests' look-ahead (try no tap, a held jump and a hop; keep whichever survives longest), without their
        // deliberate mistakes, and a few seeds are tried in case one still falls.
        static Reel Reference(Course course, uint arenaSeed, int metres, GameContent content)
        {
            for (uint tries = 1; tries <= 12; tries++)
            {
                var sim = new RunSimulation(course, DailyRules.Ghosts(new GhostRecord[0], new GhostRecord[0]), 1, DailyRules.SpeedPct, content.Config,
                    new RunOptions { Daily = true });
                var rng = new Xorshift32(arenaSeed * 31 + tries * 7919);
                int speed = content.Config.SpeedForPct(DailyRules.SpeedPct);
                var planner = new Planner(course, speed, content.Config);
                int holdUntil = -1;
                while (!sim.IsDead && sim.Distance < metres + 5 && sim.Tick < ReelPacket.MaxTicks - 1)
                {
                    bool tap = false;
                    if (sim.Tick >= holdUntil && planner.TapNow(sim.Player, sim.Tick, rng, out int hold))
                    {
                        tap = true;
                        holdUntil = sim.Tick + hold;
                    }
                    sim.Step(tap, sim.Tick < holdUntil);
                }
                if (sim.Distance < metres) continue;
                Reel full = ReelRules.Abandoned(sim, content.ContentVersion);
                // The earliest tick by which the run has covered the distance.
                int lo = 1, hi = full.Run.DeathTick;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (RunReplay.Replay(Cut(full, mid), course, content.Config).Distance >= metres) hi = mid;
                    else lo = mid + 1;
                }
                return Cut(full, lo);
            }
            return null;
        }

        // A short search over the runner alone (the rival can't kill): is there a way, with at most two more jumps (held
        // or hopped), to stay alive for the next Horizon ticks? It taps now only when the plan it finds starts with a tap.
        sealed class Planner
        {
            const int Horizon = 80, Depth = 2;
            readonly Course _course;
            readonly int _speed;
            readonly SimConfig _cfg;
            readonly int[] _holds;

            public Planner(Course course, int speed, in SimConfig cfg)
            {
                _course = course;
                _speed = speed;
                _cfg = cfg;
                _holds = new[] { HoldTicks, cfg.HopHoldTicks };
            }

            public bool TapNow(RunnerState now, int tick, Xorshift32 rng, out int hold)
            {
                hold = 0;
                if (Coast(now, tick, Horizon) >= Horizon) return false;   // nothing to do yet
                int first = rng.NextInt(2);
                for (int k = 0; k < 2; k++)
                {
                    int h = _holds[(first + k) % 2];
                    if (Alive(Tapped(now, tick, h, out int used), tick + used, Depth - 1, Horizon - used))
                    {
                        hold = h;
                        return true;
                    }
                }
                // No plan starting now: waiting may still work (the search will tap later); if not, tap anyway.
                if (Alive(now, tick, Depth, Horizon)) return false;
                hold = HoldTicks;
                return true;
            }

            // Ticks the runner survives with no input, up to `limit`.
            int Coast(RunnerState s, int tick, int limit)
            {
                for (int i = 0; i < limit; i++)
                {
                    Runner.Step(ref s, false, _course, tick + i, _speed, _cfg, false);
                    if (!s.Alive) return i;
                }
                return limit;
            }

            // The state after a tap now with the finger down for `hold` ticks (or until it dies).
            RunnerState Tapped(RunnerState s, int tick, int hold, out int used)
            {
                used = 0;
                for (; used < hold && s.Alive; used++) Runner.Step(ref s, used == 0, _course, tick + used, _speed, _cfg, true);
                return s;
            }

            bool Alive(RunnerState s, int tick, int depth, int remaining)
            {
                if (!s.Alive) return false;
                if (remaining <= 0) return true;
                int dies = Coast(s, tick, remaining);
                if (dies >= remaining) return true;
                if (depth == 0) return false;
                // Tap at some point before the death, latest first.
                RunnerState wait = s;
                var states = new RunnerState[dies + 1];
                for (int i = 0; i <= dies; i++)
                {
                    states[i] = wait;
                    Runner.Step(ref wait, false, _course, tick + i, _speed, _cfg, false);
                }
                for (int t = dies; t >= 0; t -= 2)
                    foreach (int h in _holds)
                    {
                        RunnerState after = Tapped(states[t], tick + t, h, out int used);
                        if (Alive(after, tick + t + used, depth - 1, remaining - t - used)) return true;
                    }
                return false;
            }
        }

        static Reel Cut(Reel full, int tick)
        {
            GhostRecord run = full.Run.Clone();
            run.DeathTick = tick;
            run.TapTicks = run.TapTicks.Where(t => t < tick).ToArray();
            run.ReleaseTicks = run.ReleaseTicks.Where(t => t < tick).ToArray();
            run.ReviveTicks = run.ReviveTicks.Where(t => t < tick).ToArray();
            return new Reel
            {
                ContentVersion = full.ContentVersion, Seed = full.Seed, SpeedPct = full.SpeedPct, RunIndex = 1, Run = run,
                Ghosts = new List<GhostRecord>(), Options = new RunOptions { Daily = true }, Abandoned = true,
            };
        }
    }
}
