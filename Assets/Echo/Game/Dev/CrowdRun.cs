using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Game.Dev
{
    /// <summary>
    /// A graveyard of genuine failed runs and a run through it, made by a bot with ordinary jumps (SurvivingSchedule), for
    /// the marketing clips, the screenshot tests and the Time Run benchmark. Development tools and tests only.
    /// </summary>
    public static class CrowdRun
    {
        /// <summary>
        /// <paramref name="count"/> failed runs on the course, each lasting a little longer than the last so the graves
        /// spread out. Planned at <paramref name="speed"/> (sub-units a tick) and replayed at 100%, so a slow plan makes a
        /// crowd: runs cut short, bunched near the start. The newest has <paramref name="nemesisKills"/> kills.
        /// </summary>
        public static List<GhostRecord> Ghosts(ChunkLibrary library, in SimConfig cfg, uint seed, int count, int speed, Random rng,
            string[] skins, int nemesisKills)
        {
            var records = new List<GhostRecord>();
            for (int i = 1; i <= count; i++)
            {
                var course = new Course(library, seed);
                int survive = 90 + i * 1800 / Math.Max(1, count) + rng.Next(60);
                Schedule s = SurvivingSchedule.Build(new Course(library, seed), speed, survive, rng, cfg);
                var run = new RunSimulation(course, new GhostSet(), i, 100, cfg);
                for (int t = 0; t < 20000 && !run.IsDead; t++) run.Step(s.TapAt(t), s.HeldAt(t));
                GhostRecord g = run.ToGhostRecord();
                g.SkinId = skins != null && skins.Length > 0 ? skins[(i - 1) % skins.Length] : "";
                records.Add(g);
            }
            if (nemesisKills > 0 && records.Count > 0)
            {
                GhostRecord nemesis = records[records.Count - 1];
                nemesis.Kills = nemesisKills;
                nemesis.LastKillRun = count;
            }
            return records;
        }

        /// <summary>A run that avoids every lethal ghost and hazard with ordinary jumps for <paramref name="ticks"/>, then stops jumping.</summary>
        public static Schedule PlayerRun(List<GhostRecord> records, ChunkLibrary library, in SimConfig cfg, uint seed, int speed, int ticks, Random rng)
        {
            var course = new Course(library, seed);
            var manager = new GhostManager();
            manager.Begin(records, cfg, course, speed);
            SimConfig c = cfg;   // lambdas can't use an in parameter
            var boxes = new List<Aabb?[]>();
            for (int t = 0; t < ticks + 1; t++)
            {
                manager.Step(course, t, c);
                boxes.Add(Enumerable.Range(0, records.Count).Select(i => manager.IsLethal(i) ? Runner.GhostBox(manager.ReplayAt(i).State, c) : (Aabb?)null).ToArray());
            }
            return SurvivingSchedule.Build(new Course(library, seed), speed, ticks, rng, cfg,
                (tick, p) => tick >= c.GhostGraceTicks && tick < boxes.Count && boxes[tick].Any(b => b.HasValue && Runner.PlayerBox(p, c).Overlaps(b.Value)));
        }
    }
}
