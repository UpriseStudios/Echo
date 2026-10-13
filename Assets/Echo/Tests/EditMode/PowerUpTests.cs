using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Power-ups (a user revision). On flat 10-unit chunks at 110 sub/tick a walker that fell in chunk 2 is met on
    /// tick 182 and, if the player never jumps, kills them a few ticks before (EchoGhostTests).
    /// </summary>
    public class PowerUpTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static GhostRecord Walker(int runIndex, int graveChunk = 2) => TestRuns.Ghost(runIndex, (graveChunk * 10_000 + 2000) / 110);

        static RunSimulation OnFlat(params GhostRecord[] ghosts) =>
            new RunSimulation(TestCourses.Flat(), TestRuns.Set(ghosts), ghosts.Length + 1, 100, Cfg.WithLives(1_000_000, 1));

        [Test]
        public void TheShield_TakesOneGhostTouch_ThenTheNextOneKills()
        {
            var run = OnFlat(Walker(1, 2), Walker(2, 6));
            Assert.IsTrue(run.UseShield());
            Assert.IsFalse(run.UseShield(), "once per run");
            TestRuns.StepTo(run, 400);
            Assert.IsFalse(run.IsDead, "the first ghost broke the shield instead");
            Assert.IsFalse(run.ShieldArmed);
            Assert.AreEqual(1, run.ShieldBreaks);
            Assert.AreEqual(0, run.Ghosts.RecordAt(0).Kills, "a blocked touch is not a kill");

            TestRuns.StepTo(run, 1000);
            Assert.IsTrue(run.IsDead);
            Assert.AreEqual(DeathCause.Ghost, run.Cause);
            Assert.AreSame(run.Ghosts.RecordAt(1), run.Killer);
        }

        [Test]
        public void Phase_LetsYouRunThroughGhosts_For100Metres()
        {
            var phased = OnFlat(Walker(1, 2));
            Assert.IsTrue(phased.UsePhase());
            Assert.IsFalse(phased.UsePhase(), "once per run");
            Assert.AreEqual(Cfg.RunSpeedBase * 0 + PowerUpRules.PhaseMetres * 1000, phased.PhaseUntilX);
            TestRuns.StepTo(phased, 600);
            Assert.IsFalse(phased.IsDead);
            Assert.IsTrue(phased.PhaseActive, "still within 100 m at 66 m");

            // A ghost met after the Phase has worn off kills as usual.
            var late = OnFlat(Walker(1, 12));
            late.UsePhase();
            TestRuns.StepTo(late, 2000);
            Assert.IsTrue(late.IsDead);
            Assert.Greater(late.Player.X, PowerUpRules.PhaseMetres * 1000);
        }

        [Test]
        public void AnExtraLife_SavesYouOnce_AfterAnyLifeEarnedInTheRun()
        {
            var run = OnFlat(Walker(1, 2), Walker(2, 6));
            run.GrantExtraLife();
            TestRuns.StepTo(run, 400);
            Assert.IsFalse(run.IsDead, "the extra life revived the run");
            Assert.IsTrue(run.ExtraLifeUsed);
            Assert.AreEqual(1, run.LivesUsed);
            Assert.AreEqual(1, run.ToGhostRecord().ReviveTicks.Length, "recorded like any revive, so ghosts replay it");
            TestRuns.StepTo(run, 2000);
            Assert.IsTrue(run.IsDead, "only one");
        }

        [Test]
        public void AnUnusedExtraLife_IsNotSpent()
        {
            var run = new RunSimulation(TestCourses.Flat(), TestRuns.Set(), 1, 100, Cfg);
            run.GrantExtraLife();
            TestRuns.StepTo(run, 300);
            Assert.IsFalse(run.ExtraLifeUsed);
        }

        [Test]
        public void PowerUps_NeverChangeTheRunItself_SoItReplaysAsAGhost()
        {
            // Same taps with and without a Shield: the runner's states match tick for tick until the plain run dies.
            var plain = OnFlat(Walker(1, 4));
            var shielded = OnFlat(Walker(1, 4));
            shielded.UseShield();
            while (!plain.IsDead && plain.Tick < 2000)
            {
                bool tap = plain.Tick % 97 == 0;
                plain.Step(tap);
                shielded.Step(tap);
                if (!plain.IsDead) Assert.AreEqual(plain.Player.Hash(), shielded.Player.Hash(), $"tick {plain.Tick}");
            }
        }
    }
}
