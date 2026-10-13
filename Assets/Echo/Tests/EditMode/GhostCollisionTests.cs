using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    // On flat floor the player and every ghost share x on every tick, so contact depends only on y.
    // A jump on tick 58 puts the player at y = 672 on tick 60 (k = 3), clear of the ghost's 640-high
    // box but inside a full 800-high one, and back down to y = 462 on tick 90 (k = 33).
    public class GhostCollisionTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        [Test]
        public void GhostsAreHarmless_DuringGracePeriod_ThenKill()
        {
            var walker = TestRuns.Walker();
            var run = TestRuns.OnFlat(TestRuns.Set(walker), runIndex: 4);
            TestRuns.StepTo(run, 1000);

            Assert.AreEqual(Cfg.GhostGraceTicks, run.DeathTick);
            Assert.AreEqual(DeathCause.Ghost, run.Cause);
            Assert.AreSame(walker, run.Killer);
            Assert.AreEqual(1, walker.Kills);
            Assert.AreEqual(4, walker.LastKillRun);
        }

        [Test]
        public void GhostHitbox_Is80Percent()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Walker()));
            TestRuns.StepTo(run, 61, 58);

            Assert.IsFalse(run.IsDead, "y = 672 clears the 640-high ghost box");
            var fullSizeGhost = Aabb.FromBottomCentre(run.Player.X, 0, Cfg.PlayerHitboxW, Cfg.PlayerHitboxH);
            Assert.IsTrue(Runner.PlayerBox(run.Player, Cfg).Overlaps(fullSizeGhost), "a 100% box would have killed");

            TestRuns.StepTo(run, 1000, 58);
            Assert.AreEqual(90, run.DeathTick);
        }

        [Test]
        public void FadingGhost_CannotKill()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, deathTick: 70)));
            TestRuns.StepTo(run, 400, 58);
            Assert.IsFalse(run.IsDead);
        }

        [Test]
        public void InvulnerablePlayer_PassesThroughGhosts_ForReviveInvulnTicks()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Walker()));
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(60, run.DeathTick);

            run.Revive();
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(61 + Cfg.ReviveInvulnTicks, run.DeathTick);
            Assert.AreEqual(2, run.Killer.Kills);
        }

        [Test]
        public void WhenSeveralGhostsOverlap_TheOldestGetsTheKill()
        {
            var older = TestRuns.Walker(1);
            var newer = TestRuns.Walker(2);
            var run = TestRuns.OnFlat(TestRuns.Set(newer, older), runIndex: 3);
            TestRuns.StepTo(run, 1000);
            Assert.AreSame(older, run.Killer);
            Assert.AreEqual(0, newer.Kills);
        }
    }
}
