using System.Collections.Generic;
using System.Linq;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Cosmetics;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>Skins addendum: what a ghost record carries, the lifetime stats behind unlocks, and stat unlocks.</summary>
    public class SkinsAddendumTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        [Test]
        public void GhostRecord_CarriesSkin_DeathX_Cause_AndKiller()
        {
            var walker = TestRuns.Walker(3);
            var p = TestRuns.ProgressOnFlat(walker);
            p.NextRunIndex = 4;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            run.SkinId = "char_cat";
            TestRuns.StepTo(run, 1000);
            Assert.IsTrue(run.IsDead);

            GhostRecord record = run.ToGhostRecord();
            Assert.AreEqual("char_cat", record.SkinId);
            Assert.AreEqual(run.Player.X, record.DeathX);
            Assert.AreEqual(DeathCause.Ghost, record.Cause);
            Assert.AreEqual(3, record.KillerRunIndex);
        }

        [Test]
        public void Commit_CountsEveryDeath_IncludingContinueAndLives()
        {
            // The ledge: a life at 2 m catches the first fall, a Continue the second, the third ends the run.
            var p = new Progress { Best = 0 };
            var run = new RunSimulation(TestCourses.Ledge(), p.Ghosts, 1, 100, SimConfig.Default.WithLives(2, 1));
            while (!run.IsDead) run.Step(false);
            Assert.AreEqual(1, run.LivesUsed);
            run.Revive();
            while (!run.IsDead) run.Step(false);

            RunRules.Commit(p, run, 7, SimConfig.Default);
            Assert.AreEqual(3, p.Stats.Deaths);
            Assert.AreEqual(1, p.Stats.TotalRuns);
            Assert.AreEqual(run.Distance, p.Stats.MaxDistance);
        }

        [Test]
        public void Commit_KeepsTheMostGhostsAtStart_AndTheMostCleared()
        {
            var ghosts = Enumerable.Range(1, 6).Select(i => TestRuns.Ghost(i, 2)).ToArray();
            var p = TestRuns.ProgressOnFlat(ghosts);
            p.Best = 0;
            p.NextRunIndex = 7;
            // The six ghosts end on tick 2; the player falls off the ledge at 3 m, a new best over 0.
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Cfg);
            while (!run.IsDead) run.Step(false);
            CommitResult r = RunRules.Commit(p, run, 9, Cfg);

            Assert.IsTrue(r.NewBest);
            Assert.AreEqual(6, p.Stats.MaxGhostsAtStart);
            Assert.AreEqual(6, p.Stats.MaxGhostsCleared);
        }

        [Test]
        public void StatUnlocks_ArriveWhenTheirStatReachesTheTarget()
        {
            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            catalog.Items = new[]
            {
                new CosmeticItem { Id = "char_reaper", Type = CosmeticType.Character, UnlockStat = CosmeticUnlockStat.Deaths, UnlockTarget = 250 },
                new CosmeticItem { Id = "char_glitch", Type = CosmeticType.Character, UnlockStat = CosmeticUnlockStat.Distance, UnlockTarget = 1000 },
                new CosmeticItem { Id = "trail_starter", Type = CosmeticType.Trail, UnlockStat = CosmeticUnlockStat.GhostsAtStart, UnlockTarget = 1 },
            };
            var owned = new List<string>();
            var stats = new LifetimeStats { Deaths = 249, MaxDistance = 1000, MaxGhostsAtStart = 0 };

            Assert.IsEmpty(CosmeticUnlocks.Earn(catalog, 1_000_000, owned), "never by score");
            CollectionAssert.AreEqual(new[] { "char_glitch" }, CosmeticUnlocks.EarnByStats(catalog, stats, owned).Select(i => i.Id));
            stats.Deaths = 250;
            stats.MaxGhostsAtStart = 1;
            CollectionAssert.AreEquivalent(new[] { "char_reaper", "trail_starter" }, CosmeticUnlocks.EarnByStats(catalog, stats, owned).Select(i => i.Id));
            Assert.IsEmpty(CosmeticUnlocks.EarnByStats(catalog, stats, owned), "already owned");
            Object.DestroyImmediate(catalog);
        }
    }
}
