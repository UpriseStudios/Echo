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
    /// a player may create 20 a day. Challenges are kept by the day they were made (the id's first 3 characters say
    /// which), so a day is deleted whole 30 days on (privacy policy), whether or not anyone opens them again.
    /// </summary>
    public sealed class ChallengeService
    {
        public const int KeepPerPlayer = 20;
        public const int PerDay = 20;
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
        const string IndexKey = "challenge_index";
        public const string DayPrefix = "echo_ch_";
        const int DayChars = 3;
        static readonly DateTime DayZero = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const int IdLength = 10;
        const string IdChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

        readonly GameContent _content;
        readonly IChallengeStore _store;
        readonly IDayStore _days;
        readonly Func<DateTime> _now;

        public ChallengeService(GameContent content, IChallengeStore store, IDayStore days, Func<DateTime> utcNow)
        {
            _content = content;
            _store = store;
            _days = days;
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

            /// <summary>When each of Ids was made, so expired ones can be deleted (privacy policy: 30 days).</summary>
            public List<DateTime> CreatedUtc { get; set; } = new List<DateTime>();
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
            await Housekeeping.SweepAsync(_days, DayPrefix, now, (int)Lifetime.TotalDays);
            string id = NewId(now);
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
            await _days.SetAsync(Bucket(id), Key(playerId, id), JsonSerializer.Serialize(stored));

            index.CreatedToday++;
            while (index.CreatedUtc.Count < index.Ids.Count) index.CreatedUtc.Insert(0, now);   // indexes from before the dates
            index.Ids.Add(id);
            index.CreatedUtc.Add(now);
            while (index.Ids.Count > KeepPerPlayer || index.Ids.Count > 0 && now - index.CreatedUtc[0] > Lifetime)
            {
                await _days.DeleteAsync(Bucket(index.Ids[0]), Key(playerId, index.Ids[0]));
                index.Ids.RemoveAt(0);
                index.CreatedUtc.RemoveAt(0);
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
            await Housekeeping.SweepAsync(_days, DayPrefix, _now(), (int)Lifetime.TotalDays);
            // The id says when it was made: one from a day already swept away is expired, not unknown.
            if (DayOf(challengeId) is DateTime madeOn && _now().Date - madeOn > Lifetime)
            {
                response.Error = "expired";
                return response;
            }
            Stored stored = DayOf(challengeId) is DateTime made && made <= _now()
                ? Read<Stored>(await _days.GetAsync(Bucket(challengeId), Key(ownerId, challengeId)))
                : null;
            if (stored == null)
            {
                response.Error = "not-found";
                return response;
            }
            if (_now() - stored.CreatedUtc > Lifetime)
            {
                await _days.DeleteAsync(Bucket(challengeId), Key(ownerId, challengeId));   // expired: deleted, not just refused
                response.Error = "expired";
                return response;
            }
            response.Ok = true;
            response.Packet = stored.Packet;
            response.Name = CleanName(stored.Name);
            response.Score = stored.Score;
            response.Distance = stored.Distance;
            return response;
        }

        /// <summary>Delete online data (Settings): every challenge the player sent. Their index goes with their private data.</summary>
        public async Task ForgetPlayerAsync(string playerId)
        {
            Index index = Read<Index>(await _store.GetAsync(playerId, IndexKey));
            if (index == null) return;
            foreach (string id in index.Ids) await _days.DeleteAsync(Bucket(id), Key(playerId, id));
            await _store.DeleteAsync(playerId, IndexKey);
        }

        static string Key(string ownerId, string id) => ownerId + "_" + id;

        // The day a challenge was made, from the first characters of its id; null for an id that isn't one of ours.
        static DateTime? DayOf(string id)
        {
            if (id.Length < DayChars) return null;
            int n = 0;
            for (int i = 0; i < DayChars; i++)
            {
                int digit = IdChars.IndexOf(id[i]);
                if (digit < 0) return null;
                n = n * IdChars.Length + digit;
            }
            return DayZero.AddDays(n);
        }

        static string Bucket(string id) => DayOf(id) is DateTime day ? DayPrefix + Housekeeping.Day(day) : DayPrefix + "none";

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

        // The same filter as the game: a name on the blocklist is stored as ChallengeNames.Default.
        static string CleanName(string raw) => ChallengeNames.Clean(raw);

        // The day (since 2026-01-01) in the first 3 characters, then 7 random ones.
        static string NewId(DateTime now)
        {
            var chars = new char[IdLength];
            int day = (int)(now.Date - DayZero).TotalDays;
            for (int i = DayChars - 1; i >= 0; i--, day /= IdChars.Length) chars[i] = IdChars[day % IdChars.Length];
            for (int i = DayChars; i < IdLength; i++) chars[i] = IdChars[RandomNumberGenerator.GetInt32(IdChars.Length)];
            return new string(chars);
        }
    }
}
