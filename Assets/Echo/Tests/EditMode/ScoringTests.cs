using Echo.Core.Ghosts;
using Echo.Core.Scoring;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class ScoringTests
    {
        [TestCase(0, 0)]
        [TestCase(999, 0)]
        [TestCase(1000, 1)]
        [TestCase(123_456, 123)]
        public void Distance_IsWholeMetres(int maxX, int metres)
        {
            Assert.AreEqual(metres, ScoreRules.Distance(maxX));
        }

        [TestCase(100, 0, 0, 100)]
        [TestCase(100, 2, 0, 110)]
        [TestCase(100, 0, 1, 105)]
        [TestCase(100, 0, 30, 250)]
        [TestCase(7, 1, 3, 13)] // (7 + 5) * 115 / 100 = 13.8, truncated
        public void Score_Formula(int distance, int nearMisses, int ghosts, int expected)
        {
            Assert.AreEqual(expected, ScoreRules.Score(distance, nearMisses, ghosts));
        }

        [Test]
        public void Run_ScoresItsNearMisses_WithTheStartingGhostMultiplier()
        {
            // Near-miss over the 75-tick ghost (see NearMissTests), then 400 ticks: x = 44000 -> 44 m.
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Ghost(1, 75)));
            TestRuns.StepTo(run, 400, 58);
            Assert.AreEqual(44, run.Distance);
            Assert.AreEqual(1, run.NearMisses);
            Assert.AreEqual(0, run.Ghosts.VisibleCount, "multiplier uses ghosts at tick 0, not ghosts left");
            Assert.AreEqual((44 + 5) * 105 / 100, run.Score);
        }
    }
}
