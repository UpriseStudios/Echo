using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;

namespace EchoCloud
{
    /// <summary>
    /// Server-stored friend challenges (balance addendum and Daily Haunt doc). A challenge is the challenger's run with
    /// the ghosts it started against, checked and replayed before it is kept: at most 30 ghosts, today's content, a
    /// finished run, and the score worked out here, never taken from the client. Each player keeps their last 20;
    /// a challenge expires after 30 days; a player may create 20 a day.
    /// </summary>
    public sealed class ChallengeService
    {
        public const int KeepPerPlayer = 20;
        public const int PerDay = 20;
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
        const string IndexKey = "challenge_index";
        const string KeyPrefix = "challenge_";
        const int IdLength = 10;
        const string IdChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

        readonly GameContent _content;
        readonly IChallengeStore _store;
        readonly Func<DateTime> _now;

        public ChallengeService(GameContent content, IChallengeStore store, Func<DateTime> utcNow)
        {
            _content = content;
            _store = store;
            _now = utcNow;
        }

        sealed class Stored
        {
            public string Packet { get; set; } = "";
            public string Name { get; set; } = "";
            public int Score { get; set; }
            public int Distance { get; set; }
            public int ClaimedScore { get; set; }
            public bool Corrected { get; set; }
            public DateTime CreatedUtc { get; set; }
        }

        sealed class Index
        {
            public string Day { get; set; } = "";
            public int CreatedToday { get; set; }
            public List<string> Ids { get; set; } = new List<string>();
        }

        public async Task<CreateChallengeResponse> CreateAsync(string playerId, string packet, string name)
        {
            var response = new CreateChallengeResponse { OwnerId = playerId };
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Fail(response, "malformed");
            if (reel.ContentVersion != _content.ContentVersion) return Fail(response, "content-version");
            if (reel.Ghosts.Count > Challenge.MaxGhosts) return Fail(response, "too-many-ghosts");
            if (reel.Options.Duel) return Fail(response, "duel");
            ReplayResult replay = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            if (!replay.Finished) return Fail(response, "unfinished");

            DateTime now = _now();
            string today = now.ToString("yyyy-MM-dd");
            Index index = Read<Index>(await _store.GetAsync(playerId, IndexKey)) ?? new Index();
            if (index.Day != today)
            {
                index.Day = today;
                index.CreatedToday = 0;
            }
            if (index.CreatedToday >= PerDay) return Fail(response, "rate-limit");

            // The server's own result is what friends race against, with the reel's claim brought into line.
            int claimedScore = reel.Score;
            bool corrected = replay.Score != reel.Score || replay.Distance != reel.Distance;
            reel.Score = replay.Score;
            reel.Distance = replay.Distance;
            string id = NewId();
            var stored = new Stored
            {
                Packet = ReelPacket.Encode(reel),
                Name = CleanName(name),
                Score = replay.Score,
                Distance = replay.Distance,
                ClaimedScore = claimedScore,
                Corrected = corrected,
                CreatedUtc = now,
            };
            await _store.SetAsync(playerId, KeyPrefix + id, JsonSerializer.Serialize(stored));

            index.CreatedToday++;
            index.Ids.Add(id);
            while (index.Ids.Count > KeepPerPlayer)
            {
                await _store.DeleteAsync(playerId, KeyPrefix + index.Ids[0]);
                index.Ids.RemoveAt(0);
            }
            await _store.SetAsync(playerId, IndexKey, JsonSerializer.Serialize(index));

            response.Ok = true;
            response.ChallengeId = id;
            response.Link = ChallengeLinks.Link(playerId, id);
            response.Score = replay.Score;
            response.Distance = replay.Distance;
            response.Corrected = corrected;
            return response;
        }

        public async Task<GetChallengeResponse> GetAsync(string ownerId, string challengeId)
        {
            var response = new GetChallengeResponse();
            if (!ChallengeLinks.IsId(ownerId) || !ChallengeLinks.IsId(challengeId))
            {
                response.Error = "malformed";
                return response;
            }
            Stored stored = Read<Stored>(await _store.GetAsync(ownerId, KeyPrefix + challengeId));
            if (stored == null)
            {
                response.Error = "not-found";
                return response;
            }
            if (_now() - stored.CreatedUtc > Lifetime)
            {
                response.Error = "expired";
                return response;
            }
            response.Ok = true;
            response.Packet = stored.Packet;
            response.Name = stored.Name;
            response.Score = stored.Score;
            response.Distance = stored.Distance;
            return response;
        }

        static CreateChallengeResponse Fail(CreateChallengeResponse r, string error)
        {
            r.Error = error;
            return r;
        }

        static T Read<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static string CleanName(string raw)
        {
            var b = new System.Text.StringBuilder();
            foreach (char ch in raw ?? "")
                if (!char.IsControl(ch) && b.Length < Challenge.MaxNameLength) b.Append(ch);
            return b.ToString().Trim();
        }

        static string NewId()
        {
            var chars = new char[IdLength];
            for (int i = 0; i < IdLength; i++) chars[i] = IdChars[RandomNumberGenerator.GetInt32(IdChars.Length)];
            return new string(chars);
        }
    }
}
