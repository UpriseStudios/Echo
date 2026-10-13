using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Tests.EditMode;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>Server challenges (balance addendum): checked, replayed, stored privately, limited and expired.</summary>
    public class ChallengeServiceTests
    {
        static readonly GameContent Content = GameContent.Shared;

        sealed class MemoryStore : IChallengeStore
        {
            public readonly Dictionary<string, string> Items = new Dictionary<string, string>();
            public Task<string> GetAsync(string playerId, string key) =>
                Task.FromResult(Items.TryGetValue(playerId + "/" + key, out string v) ? v : null);
            public Task SetAsync(string playerId, string key, string json) { Items[playerId + "/" + key] = json; return Task.CompletedTask; }
            public Task DeleteAsync(string playerId, string key) { Items.Remove(playerId + "/" + key); return Task.CompletedTask; }
        }

        MemoryStore _store;
        DateTime _now;
        ChallengeService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryStore();
            _now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            _service = new ChallengeService(Content, _store, () => _now);
        }

        static Reel ARun(uint seed, int ghosts)
        {
            Course course = Content.CourseFor(seed);
            GhostSet set = ParityRuns.Ghosts(course, ghosts, seed, Content.Config);
            return ParityRuns.Seeds(80, seed).Select(s => ParityRuns.Play(course, set, s, Content.Config, Content.ContentVersion).Reel).First(r => r != null);
        }

        [Test]
        public async Task ACreatedChallenge_ComesBackWithItsGhosts()
        {
            Reel reel = ARun(5, 4);
            CreateChallengeResponse created = await _service.CreateAsync("playerA", ReelPacket.Encode(reel), "Jamie");
            Assert.IsTrue(created.Ok, created.Error);
            Assert.AreEqual($"https://echo.uprisestudios.co.uk/c/playerA/{created.ChallengeId}", created.Link);
            Assert.AreEqual(reel.Score, created.Score);
            Assert.IsFalse(created.Corrected);

            GetChallengeResponse got = await _service.GetAsync("playerA", created.ChallengeId);
            Assert.IsTrue(got.Ok, got.Error);
            Assert.AreEqual("Jamie", got.Name);
            Assert.AreEqual(reel.Score, got.Score);
            Assert.IsTrue(ReelPacket.TryDecode(got.Packet, out Reel back));
            Assert.AreEqual(reel.Ghosts.Count, back.Ghosts.Count);
            CollectionAssert.AreEqual(reel.Run.TapTicks, back.Run.TapTicks);
        }

        [Test]
        public async Task ABlockedName_IsStoredAsTheDefault()
        {
            Reel reel = ARun(9, 1);
            CreateChallengeResponse created = await _service.CreateAsync("p", ReelPacket.Encode(reel), "Sh1t\n");
            Assert.IsTrue(created.Ok, created.Error);
            Assert.AreEqual(Echo.Core.Challenges.ChallengeNames.Default, (await _service.GetAsync("p", created.ChallengeId)).Name);
        }

        [Test]
        public async Task AFakedScore_IsReplacedByTheReplay()
        {
            Reel reel = ARun(6, 2);
            int real = reel.Score;
            reel.Score += 10_000;
            reel.Distance += 900;
            CreateChallengeResponse created = await _service.CreateAsync("p", ReelPacket.Encode(reel), "Cheat");
            Assert.IsTrue(created.Ok);
            Assert.IsTrue(created.Corrected);
            Assert.AreEqual(real, created.Score);
            GetChallengeResponse got = await _service.GetAsync("p", created.ChallengeId);
            Assert.AreEqual(real, got.Score, "friends race the real score");
            Assert.IsTrue(ReelPacket.TryDecode(got.Packet, out Reel back));
            Assert.AreEqual(real, back.Score);
        }

        [Test]
        public async Task BadChallenges_AreRefused()
        {
            Assert.AreEqual("malformed", (await _service.CreateAsync("p", "garbage", "x")).Error);

            Reel old = ARun(7, 1);
            old.ContentVersion = "older";
            Assert.AreEqual("content-version", (await _service.CreateAsync("p", ReelPacket.Encode(old), "x")).Error);

            Reel crowded = ARun(8, 0);
            for (int i = 1; i <= Echo.Core.Challenges.Challenge.MaxGhosts + 1; i++)
                crowded.Ghosts.Add(new GhostRecord { RunIndex = i, DeathTick = 30, TapTicks = new int[0] });
            crowded.Run.RunIndex = 999;
            Assert.AreEqual("too-many-ghosts", (await _service.CreateAsync("p", ReelPacket.Encode(crowded), "x")).Error);

            Assert.AreEqual(0, _store.Items.Count, "nothing refused is stored");
        }

        [Test]
        public async Task TwentyADay_ThenRefused_UntilTomorrow()
        {
            string packet = ReelPacket.Encode(ARun(9, 1));
            for (int i = 0; i < ChallengeService.PerDay; i++) Assert.IsTrue((await _service.CreateAsync("p", packet, "x")).Ok);
            Assert.AreEqual("rate-limit", (await _service.CreateAsync("p", packet, "x")).Error);
            Assert.IsTrue((await _service.CreateAsync("someone-else", packet, "x")).Ok, "per player");
            _now = _now.AddDays(1);
            Assert.IsTrue((await _service.CreateAsync("p", packet, "x")).Ok);
        }

        [Test]
        public async Task OnlyTheLastTwenty_AreKept()
        {
            string packet = ReelPacket.Encode(ARun(10, 1));
            var ids = new List<string>();
            for (int day = 0; day < 2; day++, _now = _now.AddDays(1))
                for (int i = 0; i < 15; i++) ids.Add((await _service.CreateAsync("p", packet, "x")).ChallengeId);
            Assert.AreEqual("not-found", (await _service.GetAsync("p", ids[0])).Error, "the oldest is gone");
            Assert.IsTrue((await _service.GetAsync("p", ids[29])).Ok);
            Assert.AreEqual(ChallengeService.KeepPerPlayer, _store.Items.Keys.Count(k => k.StartsWith("p/challenge_") && !k.EndsWith("index")));
        }

        [Test]
        public async Task AChallenge_ExpiresAfterThirtyDays()
        {
            CreateChallengeResponse created = await _service.CreateAsync("p", ReelPacket.Encode(ARun(11, 1)), "x");
            _now = _now.AddDays(29);
            Assert.IsTrue((await _service.GetAsync("p", created.ChallengeId)).Ok);
            _now = _now.AddDays(2);
            Assert.AreEqual("expired", (await _service.GetAsync("p", created.ChallengeId)).Error);
        }

        [Test]
        public async Task GetChallenge_RefusesOddIds_AndUnknownOnes()
        {
            Assert.AreEqual("malformed", (await _service.GetAsync("../etc", "x")).Error);
            Assert.AreEqual("malformed", (await _service.GetAsync("p", "")).Error);
            Assert.AreEqual("not-found", (await _service.GetAsync("p", "nope")).Error);
        }
    }
}
