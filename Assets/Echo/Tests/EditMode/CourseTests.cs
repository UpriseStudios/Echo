using System.Linq;
using Echo.Core.Courses;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class CourseTests
    {
        static string[] Ids(Course c, int count) =>
            Enumerable.Range(0, count).Select(i => c.ChunkAt(i).Id).ToArray();

        [Test]
        public void SameSeed_GivesSameChunks()
        {
            var lib = TestCourses.Content();
            CollectionAssert.AreEqual(Ids(new Course(lib, 42), 200), Ids(new Course(lib, 42), 200));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentChunks()
        {
            var lib = TestCourses.Content();
            CollectionAssert.AreNotEqual(Ids(new Course(lib, 42), 200), Ids(new Course(lib, 43), 200));
        }

        [Test]
        public void QueryOrder_DoesNotChangeTheCourse()
        {
            var lib = TestCourses.Content();
            var jumpAhead = new Course(lib, 7);
            jumpAhead.HasFloorAt(1_500_000);
            var stepwise = new Course(lib, 7);
            for (int x = 0; x <= 1_500_000; x += 110) stepwise.HasFloorAt(x);

            CollectionAssert.AreEqual(Ids(stepwise, 150), Ids(jumpAhead, 150));
        }

        [Test]
        public void TutorialChunks_ComeFirst_InIdOrder()
        {
            var lib = TestCourses.Content();
            Assert.AreEqual(3, lib.Tutorial.Length);
            for (uint seed = 1; seed < 20; seed++)
                CollectionAssert.AreEqual(
                    new[] { "t0_01_first_spike", "t0_02_taller_spike", "t0_03_two_spikes" },
                    Ids(new Course(lib, seed), 3));
        }

        [Test]
        public void TiersUnlockAtTheirDistances()
        {
            var lib = TestCourses.Content();
            int[] maxSeen = new int[TierSchedule.MaxTier + 1];
            for (uint seed = 1; seed <= 50; seed++)
            {
                var c = new Course(lib, seed);
                for (int i = 3; i < 120; i++)
                {
                    int startX = c.ChunkStartX(i);
                    int allowed = TierSchedule.MaxTierAt(startX);
                    Chunk chunk = c.ChunkAt(i);
                    Assert.LessOrEqual(chunk.Tier, allowed, $"seed {seed} chunk {i} at x={startX}");
                    Assert.GreaterOrEqual(chunk.Tier, 1, "tutorial chunks never repeat");
                    if (chunk.Tier > maxSeen[allowed]) maxSeen[allowed] = chunk.Tier;
                }
            }
            for (int t = 1; t <= TierSchedule.MaxTier; t++)
                Assert.AreEqual(t, maxSeen[t], $"tier {t} never appeared once unlocked");
        }

        [Test]
        public void TierSchedule_MatchesSpec()
        {
            Assert.AreEqual(1, TierSchedule.MaxTierAt(0));
            Assert.AreEqual(1, TierSchedule.MaxTierAt(199_999));
            Assert.AreEqual(2, TierSchedule.MaxTierAt(200_000));
            Assert.AreEqual(3, TierSchedule.MaxTierAt(400_000));
            Assert.AreEqual(4, TierSchedule.MaxTierAt(700_000));
            Assert.AreEqual(4, TierSchedule.MaxTierAt(int.MaxValue));
        }

        [Test]
        public void Queries_KeepThreeChunksGeneratedAhead()
        {
            var c = new Course(TestCourses.Content(), 3);
            Assert.AreEqual(0, c.GeneratedChunkCount);
            c.HasFloorAt(25_000);
            Assert.AreEqual(2 + 1 + Course.LookaheadChunks, c.GeneratedChunkCount);
        }

        [Test]
        public void Floor_IsMissingOnlyInsideGaps()
        {
            var c = TestCourses.Ledge(); // gap [3000, 8000) in chunk 0
            Assert.IsTrue(c.HasFloorAt(-500));
            Assert.IsTrue(c.HasFloorAt(2999));
            Assert.IsFalse(c.HasFloorAt(3000));
            Assert.IsFalse(c.HasFloorAt(7999));
            Assert.IsTrue(c.HasFloorAt(8000));
        }

        [Test]
        public void HazardsInLaterChunks_UseCourseCoordinates()
        {
            var lib = TestCourses.Library(
                @"{ ""id"": ""a"", ""tier"": 0, ""length"": 10000 }",
                @"{ ""id"": ""b"", ""tier"": 1, ""length"": 10000, ""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 600 }] }");
            var c = new Course(lib, 1);
            Assert.IsFalse(c.OverlapsHazard(new Echo.Core.Sim.Aabb(5000, 0, 5600, 600), 0), "chunk a has no spike");
            Assert.IsTrue(c.OverlapsHazard(new Echo.Core.Sim.Aabb(15000, 0, 15600, 600), 0));
            Assert.IsFalse(c.OverlapsHazard(new Echo.Core.Sim.Aabb(15600, 0, 16200, 600), 0), "touching edges");
            Assert.IsFalse(c.OverlapsHazard(new Echo.Core.Sim.Aabb(15000, 600, 15600, 1400), 0), "above the spike");
        }
    }
}
