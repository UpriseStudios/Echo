using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;

namespace EchoCloud
{
    /// <summary>
    /// The Daily Haunt on the server (Daily Haunt doc). One course a day from a secret seed; today's top 10 runs haunt
    /// it as leader ghosts, kept in a versioned cache rebuilt at most every 5 minutes (12 versions, an hour, so a run
    /// started against a slightly old set still replays); every submitted run is checked against the daily rules,
    /// the exact ghosts it was given and the day, then replayed for its real distance. A failed attempt becomes one
    /// of the player's daily ghosts (20 at most); a new daily best clears them, goes on the leaderboard, and refreshes
    /// the leaders if it made the top 10.
    /// </summary>
    public sealed class DailyService
    {
        public static readonly TimeSpan LeaderRefresh = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(2);
        public const int LeaderVersionsKept = 12;
        static readonly DateTime DayZero = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        readonly GameContent _content;
        readonly IDailyStore _store;
        readonly Func<DateTime> _clock;
        readonly string _salt;

        public DailyService(GameContent content, IDailyStore store, Func<DateTime> utcNow, string salt)
        {
            if (string.IsNullOrEmpty(salt)) throw new ArgumentException("the daily seed salt is missing (Secret Manager DAILY_SEED_SALT)");
            _content = content;
            _store = store;
            _clock = utcNow;
            _salt = salt;
        }

        sealed class PlayerDay
        {
            public int BestDistance { get; set; }

            /// <summary>The best run (as a ReelPacket's Run) and the daily ghosts (its Ghosts).</summary>
            public string Packet { get; set; } = "";
            public int NextRunIndex { get; set; } = 1;
        }

        sealed class PlayerMeta
        {
            public DateTime LastSubmitUtc { get; set; }
        }

        sealed class LeaderEntry
        {
            public string PlayerId { get; set; } = "";
            public string Name { get; set; } = "";
            public int Rank { get; set; }
            public int Distance { get; set; }

            /// <summary>The leader ghost, as a ReelPacket's Run.</summary>
            public string Packet { get; set; } = "";
        }

        sealed class LeaderSet
        {
            /// <summary>A hash of the day and the set's contents (VersionOf): two different sets never share one.</summary>
            public string Version { get; set; } = "";
            public DateTime BuiltUtc { get; set; }
            public List<LeaderEntry> Leaders { get; set; } = new List<LeaderEntry>();
        }

        sealed class LeaderCache
        {
            public List<LeaderSet> Versions { get; set; } = new List<LeaderSet>();
        }

        /// <summary>The day's seed: from the date and the secret salt, so nobody can work out tomorrow's course.</summary>
        public static uint SeedFor(string date, string salt)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("echo-daily:" + date));
            uint seed = BitConverter.ToUInt32(hash, 0);
            return seed == 0 ? 1u : seed;
        }

        public static string DateKey(DateTime utc) => utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static int DayNumber(DateTime utc) => (int)(utc.Date - DayZero).TotalDays;

        // Development only: the module passes a date (yyyy-MM-dd, keeping the time of day) or a full UTC time.
        public DateTime Now(string dateOverride)
        {
            DateTime now = _clock();
            if (string.IsNullOrEmpty(dateOverride)) return now;
            if (!DateTime.TryParse(dateOverride, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at))
                return now;
            return dateOverride.Length <= 10 ? at.Date + now.TimeOfDay : at;
        }

        public async Task<GetDailyResponse> GetDailyAsync(string playerId, string dateOverride = null)
        {
            DateTime now = Now(dateOverride);
            string date = DateKey(now);
            uint seed = SeedFor(date, _salt);
            LeaderSet leaders = await CurrentLeaders(date, now, force: false);
            PlayerDay day = await ReadDay(playerId, date);
            List<GhostRecord> own = OwnGhosts(day);

            var ghosts = new List<GhostRecord>();
            foreach (LeaderEntry e in leaders.Leaders)
                if (e.PlayerId != playerId) ghosts.Add(Decode(e.Packet));   // never your own leader ghost
            ghosts.AddRange(own);

            var response = new GetDailyResponse
            {
                Ok = true,
                Date = date,
                Day = DayNumber(now),
                Seed = seed,
                ContentVersion = _content.ContentVersion,
                SecondsLeft = (int)(now.Date.AddDays(1) - now).TotalSeconds,
                LeaderVersion = leaders.Version,
                GhostsPacket = Container(seed, ghosts, null),
                BestDistance = day.BestDistance,
                NextRunIndex = day.NextRunIndex,
            };
            foreach (LeaderboardRow row in await _store.TopAsync(100))
                response.Board.Add(new DailyBoardEntry { Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.PlayerId == playerId });
            response.Rank = (await _store.PlayerAsync(playerId))?.Rank ?? 0;
            try
            {
                LeaderboardRow winner = await _store.YesterdaysWinnerAsync();
                if (winner != null)
                {
                    response.YesterdayWinner = winner.Name;
                    response.YesterdayWinnerDistance = winner.Distance;
                }
                response.YesterdayRank = (await _store.YesterdaysPlaceAsync(playerId))?.Rank ?? 0;
            }
            catch (Exception)
            {
                // No archive yet (the first day): the lobby simply has no yesterday.
            }
            return response;
        }

        public async Task<SubmitDailyResponse> SubmitAsync(string playerId, string date, string leaderVersion, string packet, string dateOverride = null)
        {
            var response = new SubmitDailyResponse();
            DateTime now = Now(dateOverride);
            PlayerMeta meta = Read<PlayerMeta>(await _store.GetPlayerAsync(playerId, "daily_meta")) ?? new PlayerMeta();
            if (now - meta.LastSubmitUtc < RateLimit && now >= meta.LastSubmitUtc) return Fail(response, "rate-limit");
            meta.LastSubmitUtc = now;
            await _store.SetPlayerAsync(playerId, "daily_meta", JsonSerializer.Serialize(meta));

            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Fail(response, "malformed");
            if (reel.ContentVersion != _content.ContentVersion) return Fail(response, "content-version");
            string broken = DailyRules.Check(reel);
            if (broken != null) return Fail(response, broken);

            // Today, or yesterday within the grace period after midnight for a run started before it.
            string today = DateKey(now);
            bool lateFromYesterday = date == DateKey(now.Date.AddDays(-1)) && now - now.Date < Grace;
            if (date != today && !lateFromYesterday) return Fail(response, "wrong-day");
            if (reel.Seed != SeedFor(date, _salt)) return Fail(response, "seed");

            LeaderCache cache = Read<LeaderCache>(await _store.GetGameAsync(CacheKey(date))) ?? new LeaderCache();
            LeaderSet set = cache.Versions.FirstOrDefault(v => v.Version == leaderVersion);
            if (set == null && !(cache.Versions.Count == 0 && leaderVersion == VersionOf(date, new List<LeaderEntry>())))
                return Fail(response, "leader-version");
            var leaders = (set?.Leaders ?? new List<LeaderEntry>()).Where(e => e.PlayerId != playerId).ToList();

            PlayerDay day = await ReadDay(playerId, date);
            List<GhostRecord> own = OwnGhosts(day);
            var expected = leaders.Select(e => Decode(e.Packet)).Concat(own).ToList();
            if (!DailyRules.SameGhosts(reel.Ghosts, expected)) return Fail(response, "ghosts");
            if (own.Any(g => g.RunIndex == reel.RunIndex)) return Fail(response, "run-index");

            ReplayResult result = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            if (!result.Finished) return Fail(response, "unfinished");

            int distance = result.Distance;
            response.Corrected = distance != reel.Distance;
            response.BoardClosed = lateFromYesterday;
            if (!lateFromYesterday) response.PreviousRank = (await _store.PlayerAsync(playerId))?.Rank ?? 0;
            GhostRecord run = reel.Run.Clone();
            run.RunIndex = reel.RunIndex;
            if (distance > day.BestDistance)
            {
                // A new daily best clears the player's daily ghosts (Daily Haunt doc).
                day.BestDistance = distance;
                own.Clear();
                day.Packet = Container(reel.Seed, own, run);
                if (!lateFromYesterday) await _store.SubmitAsync(playerId, distance);   // yesterday's board is closed
                response.NewBest = true;
            }
            else
            {
                DailyRules.AddOwnGhost(own, run);
                day.Packet = Container(reel.Seed, own, Best(day));
            }
            day.NextRunIndex = Math.Max(day.NextRunIndex, reel.RunIndex + 1);
            await _store.SetPlayerAsync(playerId, DayKey(date), JsonSerializer.Serialize(day));

            response.Ok = true;
            response.Distance = distance;
            response.BestDistance = day.BestDistance;
            response.NextRunIndex = day.NextRunIndex;
            if (!lateFromYesterday) response.Rank = (await _store.PlayerAsync(playerId))?.Rank ?? 0;
            if (response.NewBest && !lateFromYesterday && response.Rank >= 1 && response.Rank <= DailyRules.Leaders)
                await CurrentLeaders(date, now, force: true);   // you're now one of the ghosts others meet
            response.LeadersPassed = DailyRules.Passed(leaders.Select(e => e.Distance), distance);
            int next = DailyRules.NextToBeat(leaders.Select(e => e.Distance), distance);
            if (next >= 0)
            {
                LeaderEntry ahead = leaders.Where(e => e.Distance == next).OrderBy(e => e.Rank).Last();
                response.NextLeaderName = ahead.Name;
                response.NextLeaderDistance = ahead.Distance;
            }
            return response;
        }

        // The leader set for a day: the cached one if it is under 5 minutes old, otherwise rebuilt from the top 10
        // and each of those players' best daily run, as a new version.
        async Task<LeaderSet> CurrentLeaders(string date, DateTime now, bool force)
        {
            LeaderCache cache = Read<LeaderCache>(await _store.GetGameAsync(CacheKey(date))) ?? new LeaderCache();
            LeaderSet latest = cache.Versions.LastOrDefault();
            if (!force && latest != null && now - latest.BuiltUtc < LeaderRefresh && now >= latest.BuiltUtc) return latest;

            var set = new LeaderSet { BuiltUtc = now };
            int rank = 0;
            foreach (LeaderboardRow row in await _store.TopAsync(DailyRules.Leaders))
            {
                PlayerDay theirs = await ReadDay(row.PlayerId, date);
                GhostRecord best = Best(theirs);
                if (best == null) continue;
                rank++;
                GhostRecord ghost = DailyRules.AsLeader(best, rank, row.Name);
                set.Leaders.Add(new LeaderEntry
                {
                    PlayerId = row.PlayerId, Name = row.Name, Rank = rank, Distance = theirs.BestDistance,
                    Packet = Container(0, new List<GhostRecord>(), ghost),
                });
            }
            set.Version = VersionOf(date, set.Leaders);
            if (latest != null && latest.Version == set.Version)
                latest.BuiltUtc = now;   // nothing changed: the same set, the same version, fresh for another 5 minutes
            else
                cache.Versions.Add(set);
            while (cache.Versions.Count > LeaderVersionsKept) cache.Versions.RemoveAt(0);
            await _store.SetGameAsync(CacheKey(date), JsonSerializer.Serialize(cache));
            return cache.Versions[cache.Versions.Count - 1];
        }

        /// <summary>
        /// A leader set's version: SHA-256 over the date and every entry in order, each field length-prefixed so no two
        /// different sets can write the same bytes. Equal contents give equal versions; different ones differ.
        /// </summary>
        public static string VersionOf(string date, IReadOnlyList<(string playerId, string name, int rank, int distance, string packet)> leaders)
        {
            var text = new StringBuilder();
            void Field(string value) => text.Append(value.Length).Append(':').Append(value);
            Field(date);
            text.Append(leaders.Count).Append(';');
            foreach (var e in leaders)
            {
                Field(e.playerId);
                Field(e.name);
                Field(e.rank.ToString(CultureInfo.InvariantCulture));
                Field(e.distance.ToString(CultureInfo.InvariantCulture));
                Field(e.packet);
            }
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static string VersionOf(string date, List<LeaderEntry> leaders) =>
            VersionOf(date, leaders.Select(e => (e.PlayerId, e.Name, e.Rank, e.Distance, e.Packet)).ToList());

        static string CacheKey(string date) => "leaders_" + date.Replace("-", "");
        static string DayKey(string date) => "daily_" + date.Replace("-", "");

        async Task<PlayerDay> ReadDay(string playerId, string date) =>
            Read<PlayerDay>(await _store.GetPlayerAsync(playerId, DayKey(date))) ?? new PlayerDay();

        static List<GhostRecord> OwnGhosts(PlayerDay day) =>
            !string.IsNullOrEmpty(day.Packet) && ReelPacket.TryDecode(day.Packet, out Reel r) ? r.Ghosts : new List<GhostRecord>();

        static GhostRecord Best(PlayerDay day) =>
            day.BestDistance > 0 && ReelPacket.TryDecode(day.Packet, out Reel r) && r.Run.TapTicks != null ? r.Run : null;

        static GhostRecord Decode(string container) => ReelPacket.TryDecode(container, out Reel r) ? r.Run : null;

        // Ghost records travel and rest in the same wire format as runs: a reel holding them.
        string Container(uint seed, List<GhostRecord> ghosts, GhostRecord run) =>
            ReelPacket.Encode(new Reel
            {
                ContentVersion = _content.ContentVersion,
                Seed = seed,
                SpeedPct = DailyRules.SpeedPct,
                RunIndex = 1,
                Run = run ?? new GhostRecord { RunIndex = 0 },
                Ghosts = ghosts.OrderBy(g => g.RunIndex).ToList(),
            });

        static SubmitDailyResponse Fail(SubmitDailyResponse r, string error)
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
    }
}
