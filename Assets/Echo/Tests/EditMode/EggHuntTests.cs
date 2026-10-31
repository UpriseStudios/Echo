using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Views;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The Easter egg hunt (seasons doc): two eggs per run at chunk boundaries, above the standing runner; a hop through
    /// one collects it, running underneath doesn't, and each is found once.
    /// </summary>
    public class EggHuntTests
    {
        [Test]
        public void AHopCollects_RunningUnderneathDoesnt_AndEachEggOnce()
        {
            var go = new GameObject("EggHunt");
            try
            {
                EggHuntView hunt = go.AddComponent<EggHuntView>();
                var course = new Course(TestCourses.Content(), 4242);
                hunt.Plant(course, 7);
                SimConfig cfg = SimConfig.Default;
                int eggX = Mathf.RoundToInt(hunt.EggAt(0).x * 1000f);

                RunnerState state = RunnerState.AtStart();
                state.X = eggX;
                state.Y = 0;
                Assert.AreEqual(0, hunt.Collect(state, cfg), "running underneath");

                state.Y = 600;   // part way up a hop
                Assert.AreEqual(1, hunt.Collect(state, cfg));
                Assert.AreEqual(0, hunt.Collect(state, cfg), "found once");
                Assert.AreEqual(1, hunt.Found);

                hunt.Plant(course, 8);
                Assert.AreEqual(0, hunt.Found, "a new run starts its own count");
                bool moved = false;
                for (int run = 8; run < 14 && !moved; run++)
                {
                    hunt.Plant(course, run);
                    moved = Mathf.RoundToInt(hunt.EggAt(0).x * 1000f) != eggX;
                }
                Assert.IsTrue(moved, "runs hide their eggs in different places");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
