using System.Linq;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Balance addendum, fair challenges: a challenge carries the challenger's whole haunted course, raced on score;
    /// Lite challenges (old links, old runs) keep the challenger's run alone, raced on distance.
    /// </summary>
    public class FairChallengeTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static (Course course, Reel reel) RecordedChallenge(uint seed, int ghostCount, Twist twist = Twist.None)
        {
            Course course = new Course(TestCourses.Content(), seed);
            GhostSet ghosts = ParityRuns.Ghosts(course, ghostCount, seed, Cfg);
            foreach (uint s in ParityRuns.Seeds(60, seed))
            {
                ParityRuns.Played p = ParityRuns.Play(course, ghosts, s, Cfg, "cv");
                if (p.Reel == null || p.Reel.Options.Twist != twist && twist != Twist.None) continue;
                if (twist == Twist.None && p.Reel.Options.Twist != Twist.None) continue;
                return (course, p.Reel);
            }
            Assert.Fail("no run recorded");
            return default;
        }

        [Test]
        public void AFullChallenge_FacesTheChallengersGhosts_PlusTheirRun_UnderTheirTwistAndBounty()
        {
            var (course, reel) = RecordedChallenge(41, 5);
            Challenge c = ChallengeRuns.FromReel(reel);
            Assert.IsFalse(c.Lite);
            Assert.AreEqual(reel.Score, c.Score);

            var friend = new Progress { Seed = 999, NextRunIndex = 77 };
            RunSimulation run = RunRules.StartChallenge(friend, course, Cfg, c);
            Assert.AreEqual(reel.Ghosts.Count + 1, run.GhostsAtStart, "their ghosts and their run");
            Assert.AreEqual(reel.Run.RunIndex, run.RunIndex, "their run index, so the same bounty ghost");
            Assert.AreEqual(reel.Options.Twist, run.Options.Twist);
            Assert.AreEqual(reel.Options.Bounties, run.Options.Bounties);
            Assert.IsTrue(run.Options.Challenge);
            Assert.IsFalse(RunRules.CanContinue(run) || RunRules.CanBanishTen(run, friend, Cfg));
        }

        [Test]
        public void TheSameBountyGhost_AsTheChallengerHad()
        {
            for (uint seed = 50; seed < 70; seed++)
            {
                var (course, reel) = RecordedChallenge(seed, 4);
                if (!reel.Options.Bounties) continue;
                var original = new RunSimulation(course, Set(reel.Ghosts), reel.RunIndex, reel.SpeedPct, Cfg, reel.Options);
                RunSimulation friend = RunRules.StartChallenge(new Progress(), course, Cfg, ChallengeRuns.FromReel(reel));
                int bountyRun = original.BountyIndex >= 0 ? original.Ghosts.RecordAt(original.BountyIndex).RunIndex : -1;
                int friendBountyRun = friend.BountyIndex >= 0 ? friend.Ghosts.RecordAt(friend.BountyIndex).RunIndex : -1;
                Assert.AreEqual(bountyRun, friendBountyRun);
                return;
            }
            Assert.Inconclusive("no bounty run among the seeds");
        }

        static GhostSet Set(System.Collections.Generic.List<GhostRecord> records)
        {
            var set = new GhostSet();
            foreach (GhostRecord g in records) set.Add(g.Clone());
            return set;
        }

        [Test]
        public void Attempts_NeverChangeTheChallenge()
        {
            var (course, reel) = RecordedChallenge(43, 6);
            int[] kills = reel.Ghosts.Select(g => g.Kills).ToArray();
            Challenge c = ChallengeRuns.FromReel(reel);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                RunSimulation run = RunRules.StartChallenge(new Progress(), course, Cfg, c);
                TestRuns.StepTo(run, 5000);
            }
            CollectionAssert.AreEqual(kills, reel.Ghosts.Select(g => g.Kills).ToArray(), "kills never carry between attempts");
        }

        [Test]
        public void AFullChallenge_IsWonOnScore_ALiteOne_OnDistance()
        {
            var (course, reel) = RecordedChallenge(44, 3);
            Challenge full = ChallengeRuns.FromReel(reel);
            RunSimulation run = RunRules.StartChallenge(new Progress(), course, Cfg, full);
            TestRuns.StepTo(run, 20000);
            ChallengeOutcome o = RunRules.CommitChallenge(run, full);
            Assert.IsTrue(o.ByScore);
            Assert.AreEqual(run.Score > full.Score, o.Won);
            Assert.AreEqual(System.Math.Abs(run.Score - full.Score), o.Margin);

            Challenge lite = ChallengeRuns.FromRun(new RunSimulation(course, new GhostSet(), 1, 100, Cfg));
            lite.Distance = 5;
            RunSimulation liteRun = RunRules.StartChallenge(new Progress(), course, Cfg, lite);
            Assert.AreEqual(1, liteRun.GhostsAtStart, "Lite: their run alone");
            TestRuns.StepTo(liteRun, 20000);
            ChallengeOutcome lo = RunRules.CommitChallenge(liteRun, lite);
            Assert.IsFalse(lo.ByScore);
            Assert.AreEqual(liteRun.Distance > 5, lo.Won);
        }

        [Test]
        public void ANewBest_KeepsItsGhostSnapshot_ForChallengesFromStats()
        {
            var p = TestRuns.ProgressOnFlat(TestRuns.Walker(1), TestRuns.Walker(2));
            p.NextRunIndex = 3;
            var run = RunRules.StartRun(p, TestCourses.Flat(), TestRuns.Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.IsTrue(RunRules.Commit(p, run, 9, TestRuns.Cfg).NewBest);
            Assert.IsNotNull(p.BestReel);
            Assert.AreEqual(2, p.BestReel.Ghosts.Count, "the ghosts it beat, though the best then cleared them");
            Assert.AreEqual(1u, p.BestReel.Seed);
        }

        [TestCase("https://echo.uprisestudios.co.uk/c/AbC123xyz/q7Fz_2-k", "AbC123xyz", "q7Fz_2-k")]
        [TestCase("Jamie's Ghost challenges you! https://echo.uprisestudios.co.uk/c/p1/c2 No Echo yet?", "p1", "c2")]
        [TestCase("http://echo.uprisestudios.co.uk/c/p1/c2", "p1", "c2")]
        [TestCase("echoghost://c/OWNER9/ID9", "OWNER9", "ID9")]
        public void ServerLinks_AreFound_InWhateverArrives(string text, string owner, string id)
        {
            Assert.IsTrue(ChallengeLinks.TryParse(text, out string o, out string i));
            Assert.AreEqual((owner, id), (o, i));
            Assert.AreEqual($"https://echo.uprisestudios.co.uk/c/{owner}/{id}", ChallengeLinks.Link(owner, id));
        }

        [TestCase("https://echo.uprisestudios.co.uk/c/onlyowner")]
        [TestCase("https://echo.uprisestudios.co.uk/c//id")]
        [TestCase("https://evil.example.com/c/p1/c2")]
        [TestCase("echoghost://challenge/AAAA")]
        [TestCase("")]
        public void NotServerLinks_AreRefused(string text)
        {
            Assert.IsFalse(ChallengeLinks.TryParse(text, out _, out _));
        }

        [Test]
        public void OverlongIds_AreRefused()
        {
            string id = new string('a', ChallengeLinks.MaxIdLength + 1);
            Assert.IsFalse(ChallengeLinks.TryParse(ChallengeLinks.WebPrefix + "p/" + id, out _, out _));
        }
    }
}
