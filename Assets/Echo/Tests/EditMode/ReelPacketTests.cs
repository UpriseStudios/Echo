using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Version 2.0: a run travels to the server as a ReelPacket and replays there to exactly what the game saw.
    /// The server's own parity test runs 500 of these through the Cloud Code module's code path.
    /// </summary>
    public class ReelPacketTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;
        const string Version = "cv-test";

        static Course ContentCourse(uint seed) => new Course(TestCourses.Content(), seed);

        [Test]
        public void APacket_RoundTripsEveryField()
        {
            Course course = ContentCourse(77);
            GhostSet ghosts = ParityRuns.Ghosts(course, 5, 3, Cfg);
            ParityRuns.Played played = null;
            foreach (uint s in ParityRuns.Seeds(40, 9))
            {
                played = ParityRuns.Play(course, ghosts, s, Cfg, Version);
                if (played.Reel != null && played.Reel.ContinueTick >= 0 && played.Reel.ShieldTick >= 0) break;
            }
            Reel a = played.Reel;
            Assert.IsNotNull(a);

            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(a), out Reel b));
            Assert.AreEqual((a.ContentVersion, a.Seed, a.SpeedPct, a.RunIndex), (b.ContentVersion, b.Seed, b.SpeedPct, b.RunIndex));
            Assert.AreEqual((a.Options.Bounties, a.Options.Twist, a.Options.TwistWeek, a.Options.Challenge), (b.Options.Bounties, b.Options.Twist, b.Options.TwistWeek, b.Options.Challenge));
            Assert.AreEqual((a.ShieldTick, a.PhaseTick, a.ContinueTick, a.ExtraLife), (b.ShieldTick, b.PhaseTick, b.ContinueTick, b.ExtraLife));
            Assert.AreEqual((a.Score, a.Distance, a.NearMisses, a.GhostsAtStart), (b.Score, b.Distance, b.NearMisses, b.GhostsAtStart));
            AssertSame(a.Run, b.Run);
            Assert.AreEqual(a.Ghosts.Count, b.Ghosts.Count);
            for (int i = 0; i < a.Ghosts.Count; i++) AssertSame(a.Ghosts[i], b.Ghosts[i]);
        }

        static void AssertSame(GhostRecord a, GhostRecord b)
        {
            Assert.AreEqual((a.RunIndex, a.SkinId, a.SpeedPct, a.DeathTick, a.DeathX, a.Cause, a.KillerRunIndex, a.Kills, a.LastKillRun),
                (b.RunIndex, b.SkinId, b.SpeedPct, b.DeathTick, b.DeathX, b.Cause, b.KillerRunIndex, b.Kills, b.LastKillRun));
            CollectionAssert.AreEqual(a.TapTicks, b.TapTicks);
            CollectionAssert.AreEqual(a.ReleaseTicks, b.ReleaseTicks);
            CollectionAssert.AreEqual(a.ReviveTicks, b.ReviveTicks);
        }

        [Test]
        public void RandomRuns_ReplayFromTheirPackets_ToWhatTheGameSaw()
        {
            int checkedRuns = 0;
            foreach (uint s in ParityRuns.Seeds(50, 21))
            {
                Course course = ContentCourse(s % 1000 + 1);
                GhostSet ghosts = ParityRuns.Ghosts(course, (int)(s % 6), s, Cfg);
                ParityRuns.Played played = ParityRuns.Play(course, ghosts, s, Cfg, Version);
                if (played.Reel == null) continue;
                Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(played.Reel), out Reel back));
                ReplayResult r = RunReplay.Replay(back, ContentCourse(back.Seed), Cfg);
                Assert.IsTrue(r.Finished);
                Assert.AreEqual(played.Run.Distance, r.Distance, $"seed {s}");
                Assert.AreEqual(played.Run.Score, r.Score, $"seed {s}");
                Assert.AreEqual(played.Run.DeathTick, r.DeathTick, $"seed {s}");
                Assert.AreEqual(played.Run.Cause, r.Cause, $"seed {s}");
                checkedRuns++;
            }
            Assert.Greater(checkedRuns, 40);
        }

        [Test]
        public void ARunThatNeverEnds_StopsAtTheLimit_Unfinished()
        {
            // A forged run on an endless flat floor, with no ghosts: it would run forever.
            var reel = new Reel { Seed = 1, SpeedPct = 100, RunIndex = 1, Run = new GhostRecord { RunIndex = 1, DeathTick = 10 } };
            ReplayResult r = RunReplay.Replay(reel, TestCourses.Flat(), Cfg);
            Assert.IsFalse(r.Finished);
        }

        [Test]
        public void MangledOrHostilePayloads_AreRefused()
        {
            Course course = ContentCourse(5);
            Reel reel = ParityRuns.Seeds(30, 4).Select(s => ParityRuns.Play(course, new GhostSet(), s, Cfg, Version).Reel).First(x => x != null);
            string good = ReelPacket.Encode(reel);
            Assert.IsTrue(ReelPacket.TryDecode(good, out _));

            Assert.IsFalse(ReelPacket.TryDecode(null, out _));
            Assert.IsFalse(ReelPacket.TryDecode("", out _));
            Assert.IsFalse(ReelPacket.TryDecode("not a packet!", out _));
            Assert.IsFalse(ReelPacket.TryDecode(good.Substring(0, good.Length / 2), out _), "cut off");
            char flip = good[good.Length / 2] == 'A' ? 'B' : 'A';
            Assert.IsFalse(ReelPacket.TryDecode(good.Substring(0, good.Length / 2) + flip + good.Substring(good.Length / 2 + 1), out _), "one character changed");
            Assert.IsFalse(ReelPacket.TryDecode(new string('A', ReelPacket.MaxChars + 4), out _), "oversized, refused before decoding");

            var tooMany = new Reel { Seed = 1, SpeedPct = 100, Run = new GhostRecord { RunIndex = 99 } };
            for (int i = 1; i <= ReelPacket.MaxGhosts + 1; i++) tooMany.Ghosts.Add(new GhostRecord { RunIndex = i, DeathTick = 5 });
            Assert.IsFalse(ReelPacket.TryDecode(ReelPacket.Encode(tooMany), out _), "too many ghosts");

            var unsorted = new Reel { Seed = 1, SpeedPct = 100, Run = new GhostRecord { RunIndex = 9 } };
            unsorted.Ghosts.Add(new GhostRecord { RunIndex = 4 });
            unsorted.Ghosts.Add(new GhostRecord { RunIndex = 2 });
            Assert.IsFalse(ReelPacket.TryDecode(ReelPacket.Encode(unsorted), out _), "ghosts out of order");

            var longRun = new Reel { Seed = 1, SpeedPct = 100, Run = new GhostRecord { RunIndex = 1, DeathTick = ReelPacket.MaxTicks + 1 } };
            Assert.IsFalse(ReelPacket.TryDecode(ReelPacket.Encode(longRun), out _), "death past the tick limit");
        }
    }
}
