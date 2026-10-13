using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class ContinueTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        static RunSimulation KilledOnTick60()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Walker()), runIndex: 2);
            TestRuns.StepTo(run, 1000);
            return run;
        }

        [Test]
        public void OnEveryRun_WhateverTheScore()
        {
            // Balance addendum: the old "over half your best" condition is gone.
            var run = KilledOnTick60();
            Assert.AreEqual(6, run.Score);
            Assert.IsTrue(RunRules.CanContinue(run));
        }

        [Test]
        public void NotInDuelsOrFriendChallenges()
        {
            foreach (var options in new[] { new RunOptions { Duel = true }, new RunOptions { Challenge = true } })
            {
                var run = new RunSimulation(TestCourses.Flat(), TestRuns.Set(TestRuns.Walker()), 2, 100, Cfg, options);
                TestRuns.StepTo(run, 1000);
                Assert.IsTrue(run.IsDead);
                Assert.IsFalse(RunRules.CanContinue(run));
            }
        }

        [Test]
        public void OncePerRun()
        {
            var run = KilledOnTick60();
            run.Revive();
            Assert.IsFalse(run.IsDead);
            TestRuns.StepTo(run, 1000);
            Assert.IsTrue(run.IsDead);
            Assert.IsFalse(RunRules.CanContinue(run));
            Assert.Throws<System.InvalidOperationException>(() => run.Revive());
        }

        [Test]
        public void NotWhileAlive()
        {
            var run = TestRuns.OnFlat(new GhostSet());
            run.Step(false);
            Assert.IsFalse(RunRules.CanContinue(run));
            Assert.Throws<System.InvalidOperationException>(() => run.Revive());
        }

        [Test]
        public void Revive_PlacesAtLastSafeX_Grounded_Invulnerable_AndKeepsTheTick()
        {
            var run = new RunSimulation(TestCourses.Ledge(), new GhostSet(), 1, 100, Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(DeathCause.Fall, run.Cause);
            int tick = run.Tick, lastSafeX = run.Player.LastSafeX, maxX = run.MaxX;
            Assert.Less(lastSafeX, 3000, "last grounded x is before the gap");

            run.Revive();
            Assert.AreEqual(lastSafeX, run.Player.X);
            Assert.AreEqual(0, run.Player.Y);
            Assert.AreEqual(0, run.Player.Vy);
            Assert.IsTrue(run.Player.Grounded);
            Assert.AreEqual(Cfg.ReviveInvulnTicks, run.Player.Invuln);
            Assert.AreEqual(tick, run.Tick);
            Assert.AreEqual(DeathCause.None, run.Cause);
            Assert.AreEqual(maxX, run.MaxX, "distance never goes backwards");
        }

        [Test]
        public void Invulnerability_ProtectsFromHazards()
        {
            // Grounded into a 600-wide spike: the revive puts the runner on the spike's edge,
            // and 60 invulnerable ticks carry it 6600 sub, well past the spike.
            var lib = TestCourses.Library(
                @"{ ""id"": ""a"", ""tier"": 0, ""length"": 10000, ""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 600 }] }",
                TestCourses.FlatTier1);
            var run = new RunSimulation(new Course(lib, 1), new GhostSet(), 1, 100, Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(DeathCause.Hazard, run.Cause);

            run.Revive();
            TestRuns.StepTo(run, 1000);
            Assert.IsFalse(run.IsDead);
        }
    }
}
