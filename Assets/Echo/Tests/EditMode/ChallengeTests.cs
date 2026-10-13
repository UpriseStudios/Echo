using System;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Ghost Challenges (value-adds doc): the run travels in a link, and a challenge run touches no progress.</summary>
    public class ChallengeTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        // A real finished run on the flat course: some full jumps, a hop, then a hop into a spike-free death by tick.
        static Challenge SampleChallenge()
        {
            var run = new RunSimulation(TestCourses.Ledge(), TestRuns.Set(), 7, 115, Cfg);
            while (!run.IsDead && run.Tick < 5000)
            {
                int t = run.Tick;
                run.Step(t == 2 || t == 24, held: t >= 24 && t < 30);
            }
            Challenge c = ChallengeRuns.FromRun(run);
            c.Name = "Jamie";
            c.ContentVersion = "a1b2c3d4";
            c.Record.SkinId = "char_wizard";
            return c;
        }

        [Test]
        public void ACode_RoundTripsEverything()
        {
            Challenge sent = SampleChallenge();
            string link = ChallengeCode.Link(sent);
            StringAssert.StartsWith("echoghost://challenge/", link);
            Assert.IsTrue(ChallengeCode.TryParse(link, out Challenge got));

            Assert.AreEqual("Jamie", got.Name);
            Assert.AreEqual("Jamie's Ghost", got.GhostName);
            Assert.AreEqual(sent.Seed, got.Seed);
            Assert.AreEqual("a1b2c3d4", got.ContentVersion);
            Assert.AreEqual(115, got.SpeedPct);
            Assert.AreEqual(sent.Distance, got.Distance);
            Assert.AreEqual("char_wizard", got.Record.SkinId);
            Assert.AreEqual(sent.Record.DeathTick, got.Record.DeathTick);
            Assert.AreEqual(sent.Record.DeathX, got.Record.DeathX);
            Assert.AreEqual(sent.Record.Cause, got.Record.Cause);
            CollectionAssert.AreEqual(sent.Record.TapTicks, got.Record.TapTicks);
            CollectionAssert.AreEqual(sent.Record.ReleaseTicks, got.Record.ReleaseTicks);
            CollectionAssert.AreEqual(sent.Record.ReviveTicks, got.Record.ReviveTicks);
        }

        [Test]
        public void TheLink_IsFound_InsideAWholeShareMessage_OrAsABareCode()
        {
            Challenge sent = SampleChallenge();
            string code = ChallengeCode.Encode(sent);
            Assert.IsTrue(ChallengeCode.TryParse($"Beat my ghost in Echo! {ChallengeCode.Link(sent)} (get Echo: https://example.com)", out _));
            Assert.IsTrue(ChallengeCode.TryParse("  " + code + "\n", out _));
        }

        [Test]
        public void AMistypedOrCutOffCode_IsNotAChallenge()
        {
            string code = ChallengeCode.Encode(SampleChallenge());
            char[] typo = code.ToCharArray();
            typo[code.Length / 2] = typo[code.Length / 2] == 'A' ? 'B' : 'A';
            Assert.IsFalse(ChallengeCode.TryDecode(new string(typo), out _));
            Assert.IsFalse(ChallengeCode.TryDecode(code.Substring(0, code.Length - 3), out _));
            Assert.IsFalse(ChallengeCode.TryParse("hello there", out _));
            Assert.IsFalse(ChallengeCode.TryParse("", out _));
            Assert.IsFalse(ChallengeCode.TryDecode("!!!!", out _));
        }

        [Test]
        public void ALongRun_StillMakesAShareableLink()
        {
            var c = SampleChallenge();
            var taps = new int[150];
            for (int i = 0; i < taps.Length; i++) taps[i] = 40 + i * 37;
            c.Record.TapTicks = taps;
            c.Record.ReleaseTicks = Array.ConvertAll(taps, t => t + 3);
            Assert.Less(ChallengeCode.Link(c).Length, 700, "about one long message line for 150 jumps");
        }

        [Test]
        public void TheFriendsGhost_ReplaysTheSentRun_AndDiesWhereItDied()
        {
            Challenge sent = SampleChallenge();
            ChallengeCode.TryParse(ChallengeCode.Link(sent), out Challenge got);
            var ghosts = new GhostManager();
            Course course = TestCourses.Ledge();
            ghosts.Begin(new[] { got.FreshRecord() }, Cfg, course, Cfg.SpeedForPct(got.SpeedPct));
            Assert.AreEqual(sent.Record.DeathX, ghosts.DeathX(0, course, Cfg));
        }

        [Test]
        public void AChallengeRun_HasOnlyTheirGhost_OffersNoContinue_AndTouchesNoProgress()
        {
            Challenge c = SampleChallenge();
            Progress p = TestRuns.ProgressOnFlat(TestRuns.Walker(1), TestRuns.Walker(2));
            p.Best = 500;
            p.NextRunIndex = 3;
            var run = RunRules.StartChallenge(p, TestCourses.Ledge(), Cfg, c);
            Assert.AreEqual(1, run.Ghosts.Count);
            Assert.AreEqual(115, run.SpeedPct, "their speed, not ours");
            while (!run.IsDead) run.Step(false);

            Assert.IsFalse(RunRules.CanContinue(run));
            Assert.IsFalse(RunRules.CanBanish(run, p));
            Assert.Throws<InvalidOperationException>(() => RunRules.Commit(p, run, 1, Cfg));
            ChallengeOutcome outcome = RunRules.CommitChallenge(run, c);
            Assert.AreEqual(run.Distance, outcome.Distance);
            Assert.AreEqual(c.Distance, outcome.TheirDistance);
            Assert.AreEqual(run.Distance > c.Distance, outcome.Won);
            Assert.AreEqual(Math.Abs(run.Distance - c.Distance), outcome.Margin);

            Assert.AreEqual(500, p.Best);
            Assert.AreEqual(2, p.Ghosts.Count);
            Assert.AreEqual(3, p.NextRunIndex);
            Assert.AreEqual(0, p.Stats.TotalRuns);
        }

        [Test]
        public void ANewBest_KeepsItsRun_ReadyToSend()
        {
            Progress p = TestRuns.ProgressOnFlat();
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Cfg);
            while (!run.IsDead) run.Step(run.Tick == 2);
            RunRules.Commit(p, run, 99, Cfg);
            Assert.IsNotNull(p.BestRun);
            Assert.AreEqual(1u, p.BestRun.Seed, "the course it was run on, not the next cycle's");
            Assert.AreEqual(run.Distance, p.BestRun.Distance);
            CollectionAssert.AreEqual(new[] { 2 }, p.BestRun.Record.TapTicks);
            Assert.IsTrue(ChallengeRuns.CanSend(run));
        }
    }
}
