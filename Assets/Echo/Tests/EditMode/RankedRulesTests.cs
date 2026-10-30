using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Ghost Duels' Core rules (modes doc): Elo with K 40 for the first 10 duels then 24, the opponent at half size, the
    /// tiers, the soft reset; the rival races alongside and can never kill; the new packet bit round-trips.
    /// </summary>
    public class RankedRulesTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        [Test]
        public void Elo_EvenRatings_SplitTheStakes()
        {
            Assert.AreEqual(500, RankedRules.ExpectedScore(1000, 1000));
            Assert.AreEqual(20, RankedRules.Change(1000, 1000, 0, 1000), "a new player's win at even odds: K 40 x 0.5");
            Assert.AreEqual(-20, RankedRules.Change(1000, 1000, 0, 0));
            Assert.AreEqual(0, RankedRules.Change(1000, 1000, 0, 500), "a draw at even odds moves nothing");
            Assert.AreEqual(12, RankedRules.Change(1000, 1000, 10, 1000), "after 10 duels K is 24");
        }

        [Test]
        public void Elo_Upsets_PayMore_AndTheOpponentMovesAtHalfSize()
        {
            int underdog = RankedRules.Change(1000, 1200, 20, 1000);
            int favourite = RankedRules.Change(1200, 1000, 20, 1000);
            Assert.Greater(underdog, favourite, "beating a stronger player is worth more");
            Assert.AreEqual(18, underdog, "24 x (1 - 0.24)");
            Assert.AreEqual(-9, RankedRules.OpponentChange(1200, 1000, 20, 0), "the favourite who wasn't there loses half");
            Assert.AreEqual(1000 - RankedRules.ExpectedScore(1200, 1000), RankedRules.ExpectedScore(1000, 1200), "expectations sum to one");
            Assert.AreEqual(10, RankedRules.ExpectedScore(0, 5000), "clamped far apart");
        }

        [Test]
        public void Tiers_AndTheSoftReset()
        {
            Assert.AreEqual(RankedRules.Bronze, RankedRules.Tier(999, 0));
            Assert.AreEqual(RankedRules.Silver, RankedRules.Tier(1000, 0));
            Assert.AreEqual(RankedRules.Gold, RankedRules.Tier(1250, 0));
            Assert.AreEqual(RankedRules.Phantom, RankedRules.Tier(1400, 0));
            Assert.AreEqual(RankedRules.Wraith, RankedRules.Tier(900, 100), "the top 100 whatever their rating");
            Assert.AreEqual(RankedRules.Phantom, RankedRules.Tier(1500, 101));
            Assert.AreEqual(1200, RankedRules.SoftReset(1400));
            Assert.AreEqual(900, RankedRules.SoftReset(800));
        }

        [Test]
        public void TheRival_RacesAlongside_AndNeverKills()
        {
            // A flat course: the rival runs right on top of the player the whole way, and the player lives.
            Course course = TestCourses.Flat();
            var rival = new GhostRecord { RunIndex = 9, DeathTick = 600, TapTicks = new int[0], SpeedPct = 100 };
            RunSimulation run = RankedRules.Start(course, rival, Cfg);
            Assert.IsTrue(run.Options.Ranked && run.Options.Daily);
            Assert.AreEqual(1, run.GhostsAtStart);
            for (int t = 0; t < 40; t++) run.Step(false, false);
            Assert.IsTrue(run.Ghosts.IsActive(0), "the rival starts with you, not at its grave");
            for (int t = 0; t < 400; t++) run.Step(false, false);
            Assert.IsFalse(run.IsDead, "overlapping the rival is harmless");
        }

        [Test]
        public void ARankedRun_KeepsTheRules_AndItsPacketBit()
        {
            var course = new Course(TestCourses.Content(), 77);
            var rival = new GhostRecord { RunIndex = 1, DeathTick = 300, TapTicks = new int[0], SpeedPct = 100 };
            RunSimulation run = RankedRules.Start(course, rival, Cfg);
            while (!run.IsDead && run.Tick < 6000) run.Step(false, false);
            Reel reel = ReelRules.From(run, "cv", "", false);
            Assert.IsNull(RankedRules.Check(reel));
            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel back));
            Assert.IsTrue(back.Options.Ranked, "bit 128 round-trips");
            Assert.AreEqual(RankedRules.PlayerRunIndex, back.RunIndex);
            reel.Ghosts.Clear();
            Assert.AreEqual("ghosts", RankedRules.Check(reel), "exactly one rival");
        }
    }
}
