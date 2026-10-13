using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;
using Echo.Tests.EditMode;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>
    /// The most important test of version 2.0: runs played the way the game plays them (the client code path)
    /// replay through the Cloud Code module's code path (packet, decode, ReplayService) to exactly the same result.
    /// </summary>
    public class ReplayParityTests
    {
        static readonly GameContent Content = GameContent.Shared;

        static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Echo"))) dir = dir.Parent;
                Assert.IsNotNull(dir, "repo root not found");
                return dir.FullName;
            }
        }

        [Test]
        public void TheServer_BuildsTheSameContentVersion_AsTheGame()
        {
            string chunks = Path.Combine(RepoRoot, "Assets", "Echo", "Content", "Chunks");
            var library = ChunkLibrary.FromJson(Directory.GetFiles(chunks, "*.json").Select(File.ReadAllText));
            Assert.AreEqual(ContentVersion.Compute(library, Content.Config), Content.ContentVersion);
        }

        [Test]
        public void FiveHundredRandomRuns_ReplayIdentically_OnTheServer()
        {
            var service = new ReplayService(Content);
            int matched = 0, played = 0, continued = 0, powered = 0, withGhosts = 0;
            long totalTicks = 0;
            int longest = 0;
            uint courseSeed = 0;
            GhostSet ghosts = null;
            Course course = null;
            foreach (uint s in ParityRuns.Seeds(2000, 2026))
            {
                // A new course and graveyard every 10 runs: different chunks, speeds and ghost crowds.
                if (played % 10 == 0 || course == null)
                {
                    courseSeed = s % 100_000 + 1;
                    course = Content.CourseFor(courseSeed);
                    ghosts = ParityRuns.Ghosts(course, (int)(s % 9), s, Content.Config);
                }
                ParityRuns.Played run = ParityRuns.Play(course, ghosts, s, Content.Config, Content.ContentVersion);
                played++;
                if (run.Reel == null) continue;

                ReplayCheckResponse server = service.Check(ReelPacket.Encode(run.Reel));
                Assert.IsTrue(server.Accepted, $"seed {s}: {server.Error}");
                Assert.IsTrue(server.Finished, $"seed {s}");
                Assert.AreEqual(run.Run.Distance, server.Distance, $"distance, seed {s}");
                Assert.AreEqual(run.Run.Score, server.Score, $"score, seed {s}");
                Assert.AreEqual(run.Run.DeathTick, server.DeathTick, $"death tick, seed {s}");
                Assert.IsTrue(server.Matches);
                totalTicks += run.Run.DeathTick;
                longest = System.Math.Max(longest, run.Run.Distance);
                if (run.Reel.ContinueTick >= 0) continued++;
                if (run.Reel.ShieldTick >= 0 || run.Reel.PhaseTick >= 0) powered++;
                if (run.Reel.Ghosts.Count > 0) withGhosts++;
                if (++matched == 500) break;
            }
            Assert.AreEqual(500, matched);
            TestContext.Progress.WriteLine($"500 runs: mean {totalTicks / 500 / 60.0:0.0} s, longest {longest} m, {continued} continued, {powered} with Shield/Phase, {withGhosts} against ghosts");
        }

        [Test]
        public void ARunMadeForOtherContent_IsRefused()
        {
            Course course = Content.CourseFor(3);
            Reel reel = ParityRuns.Seeds(40, 5).Select(s => ParityRuns.Play(course, new GhostSet(), s, Content.Config, "old-version").Reel).First(r => r != null);
            ReplayCheckResponse server = new ReplayService(Content).Check(ReelPacket.Encode(reel));
            Assert.IsFalse(server.Accepted);
            Assert.AreEqual("content-version", server.Error);
        }

        [Test]
        public void AMangledPacket_IsRefused()
        {
            ReplayCheckResponse server = new ReplayService(Content).Check("definitely-not-a-run");
            Assert.IsFalse(server.Accepted);
            Assert.AreEqual("malformed", server.Error);
        }

        [Test]
        public void AFakedDistance_IsReplacedByTheServersOwn()
        {
            Course course = Content.CourseFor(8);
            Reel reel = ParityRuns.Seeds(40, 6).Select(s => ParityRuns.Play(course, new GhostSet(), s, Content.Config, Content.ContentVersion).Reel).First(r => r != null);
            int real = reel.Distance;
            reel.Distance = real + 5000;
            ReplayCheckResponse server = new ReplayService(Content).Check(ReelPacket.Encode(reel));
            Assert.IsTrue(server.Accepted);
            Assert.AreEqual(real, server.Distance);
            Assert.IsFalse(server.Matches, "the claim does not match");
        }
    }
}
