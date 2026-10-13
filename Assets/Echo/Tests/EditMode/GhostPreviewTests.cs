using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class GhostPreviewTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;
        const int Ahead = 18; // tech spec 11: 0.3 s

        [Test]
        public void Preview_PredictsTheNextTicks_WithoutTouchingTheGhost()
        {
            var course = new Course(TestCourses.Content(), 9);
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Ghost(1, 500, 30, 75, 120) }, Cfg);
            for (int t = 0; t < 25; t++) ghosts.Step(course, t, Cfg);

            GhostReplay before = ghosts.ReplayAt(0);
            var xs = new int[Ahead];
            var ys = new int[Ahead];
            int n = ghosts.Preview(0, course, Ahead, Cfg, xs, ys);
            Assert.AreEqual(Ahead, n);
            Assert.AreEqual(before.State.Hash(), ghosts.ReplayAt(0).State.Hash(), "preview must not move the ghost");
            Assert.AreEqual(before.TapCursor, ghosts.ReplayAt(0).TapCursor);

            for (int i = 0; i < Ahead; i++)
            {
                ghosts.Step(course, 25 + i, Cfg);
                Assert.AreEqual(ghosts.ReplayAt(0).State.X, xs[i], $"x {i} ahead");
                Assert.AreEqual(ghosts.ReplayAt(0).State.Y, ys[i], $"y {i} ahead");
            }
        }

        [Test]
        public void Preview_StopsAtTheGhostsDeath()
        {
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Ghost(1, 30) }, Cfg);
            var course = TestCourses.Flat();
            for (int t = 0; t < 25; t++) ghosts.Step(course, t, Cfg);
            int n = ghosts.Preview(0, course, Ahead, Cfg, new int[Ahead], new int[Ahead]);
            Assert.AreEqual(5, n, "ticks 25-29; tick 30 is the death");
        }

        [Test]
        public void Preview_OfAFadingGhost_IsEmpty()
        {
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Ghost(1, 5) }, Cfg);
            var course = TestCourses.Flat();
            for (int t = 0; t < 10; t++) ghosts.Step(course, t, Cfg);
            Assert.AreEqual(0, ghosts.Preview(0, course, Ahead, Cfg, new int[Ahead], new int[Ahead]));
        }
    }
}
