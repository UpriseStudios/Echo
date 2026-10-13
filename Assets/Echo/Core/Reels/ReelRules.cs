using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.Reels
{
    /// <summary>The highlights reel (a user revision): up to five saved runs; a sixth replaces one the player picks.</summary>
    public static class ReelRules
    {
        public const int Capacity = 5;

        /// <summary>Any finished run that went somewhere, except a duel (it ends alive, past the Nemesis's grave).</summary>
        public static bool CanSave(RunSimulation run) =>
            run != null && run.IsDead && !run.Options.Duel && run.Distance > 0;

        public static Reel From(RunSimulation run, string contentVersion, string savedOn, bool newBest)
        {
            if (!CanSave(run)) throw new InvalidOperationException("only a finished run can be saved");
            var reel = new Reel
            {
                ContentVersion = contentVersion ?? "",
                Seed = run.Course.Seed,
                SpeedPct = run.SpeedPct,
                RunIndex = run.RunIndex,
                Options = run.Options,
                Run = run.ToGhostRecord(),
                ShieldTick = run.ShieldTick,
                PhaseTick = run.PhaseTick,
                ContinueTick = run.ContinueTick,
                ExtraLife = run.ExtraLifeHeld,
                Score = run.Score,
                Distance = run.Distance,
                NearMisses = run.NearMisses,
                GhostsAtStart = run.GhostsAtStart,
                NewBest = newBest,
                SavedOn = savedOn ?? "",
            };
            foreach (GhostRecord g in run.StartGhosts) reel.Ghosts.Add(g.Clone());
            return reel;
        }

        public static bool IsFull(IReadOnlyList<Reel> reels) => reels.Count >= Capacity;

        /// <summary>Adds to the reel; false when it is full (the player then picks one to replace).</summary>
        public static bool Add(List<Reel> reels, Reel reel)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            if (IsFull(reels)) return false;
            reels.Add(reel);
            return true;
        }

        public static void Replace(List<Reel> reels, int index, Reel reel)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            if (index < 0 || index >= reels.Count) throw new ArgumentOutOfRangeException(nameof(index));
            reels[index] = reel;
        }

        /// <summary>The run again from its start: the same ghosts (fresh copies), options, speed and Extra 1-UP.</summary>
        public static RunSimulation Restart(Reel reel, Course course, in SimConfig cfg)
        {
            if (course.Seed != reel.Seed) throw new ArgumentException("a reel replays on its own course", nameof(course));
            var ghosts = new GhostSet();
            foreach (GhostRecord g in reel.Ghosts) ghosts.Add(g.Clone());
            var run = new RunSimulation(course, ghosts, reel.RunIndex, reel.SpeedPct, cfg, reel.Options) { SkinId = reel.Run.SkinId ?? "" };
            if (reel.ExtraLife) run.GrantExtraLife();
            return run;
        }
    }
}
