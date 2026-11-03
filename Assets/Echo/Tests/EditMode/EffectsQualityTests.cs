using Echo.Game.Dev;
using Echo.Game.Persistence;
using Echo.Game.World;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// World Time Run doc, phase 3: the reduced-effects version switches on by itself on weaker phones and after the Far
    /// Future ran slow, and is selectable in Settings (the player's choice wins); the benchmark's frame statistics.
    /// </summary>
    public class EffectsQualityTests
    {
        [Test]
        public void FullEffects_UntilChosen_FollowThePhone()
        {
            var s = new SettingsSaveData();
            Assert.IsTrue(EffectsQuality.Full(s, weakDevice: false), "a capable phone gets full effects");
            Assert.IsFalse(EffectsQuality.Full(s, weakDevice: true), "a weaker phone gets reduced effects by itself");
            s.EffectsReduced = true;
            Assert.IsFalse(EffectsQuality.Full(s, weakDevice: false), "once the Far Future ran slow, reduced from then on");
        }

        [Test]
        public void ThePlayersChoice_Wins()
        {
            var s = new SettingsSaveData { EffectsChosen = true, Glow = true, EffectsReduced = true };
            Assert.IsTrue(EffectsQuality.Full(s, weakDevice: true), "chosen full effects stay on, even on a weaker phone");
            s.Glow = false;
            Assert.IsFalse(EffectsQuality.Full(s, weakDevice: false), "chosen reduced effects stay reduced");
        }

        [Test]
        public void WeakerPhones_ByMemoryOrCores()
        {
            Assert.IsTrue(EffectsQuality.IsWeak(3000, 8), "3 GB");
            Assert.IsTrue(EffectsQuality.IsWeak(6000, 4), "four cores");
            Assert.IsFalse(EffectsQuality.IsWeak(6000, 8), "a mid-range phone");
            Assert.IsFalse(EffectsQuality.IsWeak(0, 0), "unknown is not weak");
        }

        [Test]
        public void FrameWatch_NoticesASustainedSlowdown_NotAHitch()
        {
            var watch = new FrameWatch();
            for (int i = 0; i < 600; i++) Assert.IsFalse(watch.Feed(1f / 60f), "60 fps is fine");
            for (int i = 0; i < 10; i++) Assert.IsFalse(watch.Feed(0.1f), "a hitch is not a slow phone");
            for (int i = 0; i < 120; i++) watch.Feed(1f / 60f);
            bool noticed = false;
            for (int i = 0; i < 30 * 6 && !noticed; i++) noticed = watch.Feed(1f / 30f);
            Assert.IsTrue(noticed, "30 fps for several seconds");
        }

        [Test]
        public void FrameStats_AverageAndOnePercentLow()
        {
            var stats = new FrameStats();
            for (int i = 0; i < 990; i++) stats.Add(1f / 60f, 3);
            for (int i = 0; i < 10; i++) stats.Add(0.05f, 12);
            Assert.AreEqual(1000, stats.Frames);
            Assert.AreEqual(12, stats.MostGhostsOnScreen);
            Assert.That(stats.AverageFps, Is.InRange(50f, 60f));
            Assert.That(stats.LowFps(), Is.InRange(19f, 21f), "the slowest 1% ran at 20 fps");
        }
    }
}
