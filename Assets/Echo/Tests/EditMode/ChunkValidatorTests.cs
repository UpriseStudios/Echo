using System.Collections.Generic;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class ChunkValidatorTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static IEnumerable<string> ContentIds() => TestCourses.Content().All.Select(c => c.Id);

        // tech spec 17: every chunk clearable at every speed and every mover phase, joining any neighbour.
        // Checked with the single jump: the double jump is there to recover from mistakes, never required.
        [TestCaseSource(nameof(ContentIds))]
        public void ContentChunk_IsClearable(string id)
        {
            Chunk chunk = TestCourses.Content().All.Single(c => c.Id == id);
            ChunkValidationResult result = ChunkValidator.Validate(chunk, Cfg.WithAirJumps(0));
            Assert.IsTrue(result.Clearable, $"{id}: {result}");
        }

        [Test]
        public void Library_Has33Chunks_ThreeTutorial_AtLeastSixPerTier()
        {
            var lib = TestCourses.Content();
            Assert.AreEqual(33, lib.All.Length, "30 launch chunks and 3 hop chunks");
            Assert.AreEqual(3, lib.Tutorial.Length);
            for (int tier = 1; tier <= TierSchedule.MaxTier; tier++)
                Assert.GreaterOrEqual(lib.All.Count(c => c.Tier == tier), 6, $"tier {tier}");
        }

        [Test]
        public void Tier4Chunks_AllHaveMovers_AndOnlyTier4HasThem()
        {
            foreach (Chunk c in TestCourses.Content().All)
                Assert.AreEqual(c.Tier == 4, c.Movers.Length > 0, c.Id);
        }

        [Test]
        public void ContentGaps_AreSortedAndApart()
        {
            // CourseView draws floor segments between gaps in array order.
            foreach (Chunk c in TestCourses.Content().All)
                for (int i = 1; i < c.Gaps.Length; i++)
                    Assert.Greater(c.Gaps[i].X, c.Gaps[i - 1].X + c.Gaps[i - 1].W, c.Id);
        }

        // Variable jump (user revision): the hop chunks are the ones that need a short hop. Without one (release
        // gravity equal to gravity, so every jump is full height) the runner hits the ceiling.
        [TestCase("t3_hop_under_ceiling")]
        [TestCase("t3_hop_then_jump")]
        [TestCase("t3_hop_double")]
        public void HopChunks_NeedAShortHop(string id)
        {
            Chunk chunk = TestCourses.Content().All.Single(c => c.Id == id);
            SimConfig single = Cfg.WithAirJumps(0);
            Assert.IsTrue(ChunkValidator.Validate(chunk, single).Clearable, "with hops");
            Assert.IsFalse(ChunkValidator.Validate(chunk, single.WithReleaseGravity(single.Gravity)).Clearable, "full jumps only");
        }

        [Test]
        public void ImpossibleChunk_Fails()
        {
            // A 4-unit-tall spike cannot be jumped (apex 2142).
            var wall = ChunkParser.Parse(@"{ ""id"": ""wall"", ""tier"": 1, ""length"": 10000, ""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 4000 }] }");
            ChunkValidationResult result = ChunkValidator.Validate(wall, Cfg.WithAirJumps(0));
            Assert.IsFalse(result.Clearable);
            Assert.AreEqual(Cfg.RunSpeedBase, result.FailedSpeed);
        }

        [Test]
        public void TwoSeparateJumpsInTenUnits_FailAboveBaseSpeed()
        {
            // Clearable at base speed, but at speed a jump spans 5+ units: after the first landing the
            // second jump can only come down beyond the next chunk's clear floor. This is the case that
            // made the v1 content 12 units long.
            var pair = ChunkParser.Parse(@"{ ""id"": ""pair"", ""tier"": 1, ""length"": 10000,
                ""spikes"": [{ ""x"": 2500, ""w"": 600, ""h"": 600 }, { ""x"": 7000, ""w"": 600, ""h"": 600 }] }");
            // With full jumps only (no air jump, no hop). A hop, lower and shorter, fits between the spikes,
            // which is the skill the variable jump adds.
            SimConfig fullJumpsOnly = Cfg.WithAirJumps(0).WithReleaseGravity(Cfg.Gravity);
            ChunkValidationResult result = ChunkValidator.Validate(pair, fullJumpsOnly);
            Assert.IsFalse(result.Clearable);
            Assert.Greater(result.FailedSpeed, Cfg.RunSpeedBase);
            Assert.IsTrue(ChunkValidator.Validate(pair, Cfg.WithAirJumps(0)).Clearable, "a hop clears it");
        }

        [Test]
        public void IntermediateSpeeds_AreChecked()
        {
            CollectionAssert.AreEqual(new[] { 110, 115, 121, 126, 132, 137, 143, 148, 154, 159, 165 }, ChunkValidator.ReachableSpeeds(Cfg));
        }

        [Test]
        public void EveryEntryPosition_IsChecked_WithoutMovers()
        {
            CollectionAssert.AreEqual(Enumerable.Range(0, 110), ChunkValidator.RunInLines(110, sampleOnly: false));
            Assert.AreEqual(5, ChunkValidator.RunInLines(110, sampleOnly: true).Count);
            Assert.AreEqual(700, ChunkValidator.BoundaryBand(Cfg));
        }

        [Test]
        public void MoverThatAlwaysBlocks_Fails()
        {
            // A mover that never rises above the runner's head and is too tall to jump.
            var c = ChunkParser.Parse(@"{ ""id"": ""m"", ""tier"": 4, ""length"": 10000,
                ""movers"": [{ ""x"": 5000, ""w"": 800, ""h"": 3000, ""yMin"": 0, ""yMax"": 400, ""period"": 120, ""phase"": 0 }] }");
            Assert.IsFalse(ChunkValidator.Validate(c, Cfg.WithAirJumps(0)).Clearable, "one jump peaks at 2.27 units");
            // (A jump plus an air jump peaks at about 4 units and clears it; DoubleJumpTests cover the air jump.)
        }

        [Test]
        public void PhaseCount_IsTheLcmOfMoverPeriods()
        {
            Assert.AreEqual(1, ChunkValidator.PhaseCount(ChunkParser.Parse(TestCourses.FlatTier1)));
            var two = new Chunk("x", 4, 10000, null, null, null,
                new[] { new Mover(2000, 500, 500, 0, 2000, 80, 0), new Mover(6000, 500, 500, 0, 2000, 120, 0) });
            Assert.AreEqual(240, ChunkValidator.PhaseCount(two));
        }
    }
}
