using Echo.Game.UI;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>"Board closes in m:ss": only in the last 5 minutes, and the HUD's buffer reads the same as the lobby.</summary>
    public class DailyClockTests
    {
        [Test]
        public void TheWarning_ShowsOnlyInTheLastFiveMinutes()
        {
            Assert.AreEqual(0, DailyClock.Closing(301));
            Assert.AreEqual(300, DailyClock.Closing(300));
            Assert.AreEqual(1, DailyClock.Closing(1));
            Assert.AreEqual(0, DailyClock.Closing(0), "closed");
            Assert.AreEqual(0, DailyClock.Closing(-5));
        }

        [TestCase(300, "Board closes in 5:00")]
        [TestCase(299, "Board closes in 4:59")]
        [TestCase(65, "Board closes in 1:05")]
        [TestCase(9, "Board closes in 0:09")]
        public void TheLine_AndTheHudBuffer_Agree(int seconds, string expected)
        {
            Assert.AreEqual(expected, DailyClock.Line(seconds));
            var buffer = new char[expected.Length];
            DailyClock.Write(seconds, buffer);
            Assert.AreEqual(expected, new string(buffer));
        }
    }
}
