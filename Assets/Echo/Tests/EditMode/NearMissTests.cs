using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class NearMissTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        [Test]
        public void PassingJustAboveAGhost_CountsOnce()
        {
            // Jump on 58: y = 672 on tick 60 is a 32 sub gap over the ghost; the ghost is gone before the landing.
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, deathTick: 75)));
            TestRuns.StepTo(run, 400, 58);

            Assert.IsFalse(run.IsDead);
            Assert.AreEqual(1, run.NearMisses);
        }

        [Test]
        public void NoNearMiss_DuringGracePeriod()
        {
            // Jump on 40: the only close passes are before tick 60. From tick 60 (y = 2058) until the
            // ghost dies on 65 the gap is far wider than 300, and it is gone before the landing.
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, deathTick: 65)));
            TestRuns.StepTo(run, 400, 40);
            Assert.AreEqual(0, run.NearMisses);
        }

        [Test]
        public void NoNearMiss_WhenTheGapIsWiderThanNearMissDistance()
        {
            // Jump on 50: y is 1848-2002 on ticks 60-62, a gap of 1208+ over the ghost, which then fades.
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, deathTick: 62)));
            TestRuns.StepTo(run, 400, 50);
            Assert.AreEqual(0, run.NearMisses);
        }

        [Test]
        public void Touching_IsNotANearMiss()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Walker()));
            TestRuns.StepTo(run, 400);
            Assert.IsTrue(run.IsDead);
            Assert.AreEqual(0, run.NearMisses);
        }

        [Test]
        public void EachGhost_CountsSeparately()
        {
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, 75), TestRuns.Ghost(2, 75), TestRuns.Ghost(3, 75)));
            TestRuns.StepTo(run, 400, 58);
            Assert.AreEqual(3, run.NearMisses);
        }

        [Test]
        public void AabbGap_IsTheLargestAxisGap()
        {
            var a = new Aabb(0, 0, 100, 100);
            Assert.AreEqual(0, a.GapTo(new Aabb(50, 50, 150, 150)), "overlapping");
            Assert.AreEqual(0, a.GapTo(new Aabb(100, 0, 200, 100)), "touching");
            Assert.AreEqual(300, a.GapTo(new Aabb(0, 400, 100, 500)));
            Assert.AreEqual(301, a.GapTo(new Aabb(-401, 250, -301, 350)));
        }
    }
}
