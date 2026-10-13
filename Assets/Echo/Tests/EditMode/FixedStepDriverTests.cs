using Echo.Game.Flow;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class FixedStepDriverTests
    {
        const double Tick = 1.0 / 60.0;

        [Test]
        public void OneFrameAt60Fps_IsOneTick()
        {
            var d = new FixedStepDriver();
            Assert.AreEqual(1, d.Advance(Tick));
            Assert.AreEqual(0f, d.Alpha, 1e-4f);
        }

        [Test]
        public void HighFrameRate_AccumulatesUntilATickIsDue()
        {
            var d = new FixedStepDriver();
            Assert.AreEqual(0, d.Advance(Tick * 0.6));
            Assert.AreEqual(0.6f, d.Alpha, 1e-4f);
            Assert.AreEqual(1, d.Advance(Tick * 0.6));
        }

        [Test]
        public void SlowFrame_IsCappedAtFiveTicks_AndTheRestIsDropped()
        {
            var d = new FixedStepDriver();
            Assert.AreEqual(FixedStepDriver.MaxTicksPerFrame, d.Advance(1.0));
            Assert.AreEqual(0f, d.Alpha);
            Assert.AreEqual(0, d.Advance(0));
        }

        [Test]
        public void OneSecondOfFrames_IsSixtyTicks()
        {
            var d = new FixedStepDriver();
            int ticks = 0;
            for (int i = 0; i < 144; i++) ticks += d.Advance(1.0 / 144);
            Assert.That(ticks, Is.InRange(59, 60));
        }

        [Test]
        public void Reset_DropsAccumulatedTime()
        {
            var d = new FixedStepDriver();
            d.Advance(Tick * 0.9);
            d.Reset();
            Assert.AreEqual(0, d.Advance(Tick * 0.5));
        }
    }
}
