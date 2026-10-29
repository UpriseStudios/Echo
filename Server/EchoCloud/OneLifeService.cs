using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;

namespace EchoCloud
{
    /// <summary>
    /// One Life (modes doc): one attempt a day on its own course, on scheduled days only (the "modes" key: Christmas Day,
    /// then Sundays). StartOneLife uses the attempt the moment the run starts and hands out a single-use token;
    /// SubmitOneLife needs it, replays the run and ranks it on the "onelife" board. Today's top 10 runs are the leader
    /// ghosts. A run closed mid-way comes in when the game next opens and counts where it stopped (after its day: kept,
    /// not ranked). Records live by day in echo_onelife_yyyyMMdd (kept 14 days, so rewards can be settled when a player
    /// comes back); rewards are worked out once a day has closed: Christmas Day finishers get its gravestone (the top 3 a
    /// gold one), each Sunday's top 100 a badge for the week.
    /// </summary>
    public sealed class OneLifeService
    {
        public const string Prefix = "echo_onelife_";
        public const int KeepDays = 14;
        public const int GoldTop = 3, BadgeTop = 100;
        const string MetaKey = "onelife_meta";

        readonly GameContent _content;
        readonly IDailyStore _board;
        readonly IDayStore _days;
        readonly IReadOnlyList<ModeWindow> _windows;
        readonly long _now;
        readonly bool _forced;
        readonly string _salt;

        public OneLifeService(GameContent content, IDailyStore board, IDayStore days, IReadOnlyList<ModeWindow> windows, long now, bool forced, string salt)
        {
            if (string.IsNullOrEmpty(salt)) throw new ArgumentException("the seed salt is missing");
            _content = content;
            _board = board;
            _days = days;
            _windows = windows;
            _now = now;
            _forced = forced;
            _salt = salt;
        }

        DateTime Today => DateTimeOffset.FromUnixTimeSeconds(_now).UtcDateTime.Date;
        public bool Open => ModeService.IsOpen(_windows, ModeIds.OneLife, _now, _forced);

        public static string DateKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static string Bucket(string date) => Prefix + date.Replace("-", "");

        /// <summary>Its own course each day: the Daily Haunt's secret salt with its own label, so the two never match.</summary>
        public static uint SeedFor(string date, string salt)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt));
            uint seed = BitConverter.ToUInt32(hmac.ComputeHash(Encoding.UTF8.GetBytes("echo-onelife:" + date)), 0);
            return seed == 0 ? 1u : seed;
        }

        public sealed class Record
        {
            public string Token { get; set; } = "";
            public long Started { get; set; }
            public bool Finished { get; set; }
            public bool Abandoned { get; set; }
            public int Distance { get; set; }
            public string Name { get; set; } = "";

            /// <summary>The run, as a ReelPacket (a leader ghost for others once it ranks).</summary>
            public string Packet { get; set; } = "";

            /// <summary>The leader ghosts it was given, as a ReelPacket: it must face exactly these.</summary>
            public string Leaders { get; set; } = "";
        }

        public sealed class Meta
        {
            public List<string> Days { get; set; } = new List<string>();
            public List<string> Settled { get; set; } = new List<string>();
        }

        public async Task<OneLifeResponse> GetAsync(string playerId)
        {
            var r = new OneLifeResponse { Ok = true, Open = Open, Date = DateKey(Today) };
            ModeWindow w = ModeRules.Pick(_windows, ModeIds.OneLife, _now);
            r.SecondsLeft = Open ? (long)(Today.AddDays(1) - DateTimeOffset.FromUnixTimeSeconds(_now).UtcDateTime).TotalSeconds
                : w != null ? ModeRules.SecondsLeft(w, _now) : -1;
            r.Rewards = await SettleAsync(playerId);
            await Housekeeping.SweepAsync(_days, Prefix, Today, KeepDays);
            if (!Open)
            {
                r.Error = "closed";
                return r;
            }
            Record mine = Read<Record>(await _days.GetAsync(Bucket(r.Date), playerId));
            if (mine != null)
            {
                r.Played = true;
                r.Finished = mine.Finished;
                r.Distance = mine.Distance;
            }
            List<LeaderboardRow> rows = await _board.TopAsync(Echo.Core.Daily.DailyRules.Leaders);
            var badges = await TeamService.BadgesAsync(_days, rows.Select(x => x.PlayerId).Append(playerId).Distinct().ToList(), _now);
            foreach (LeaderboardRow row in rows)
                r.Board.Add(new DailyBoardEntry
                {
                    Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.PlayerId == playerId,
                    Badges = badges.TryGetValue(row.PlayerId, out List<string> b) ? b : new List<string>(),
                });
            if (r.Finished) r.Rank = (await _board.PlayerAsync(playerId))?.Rank ?? 0;
            return r;
        }

        public async Task<OneLifeStartResponse> StartAsync(string playerId)
        {
            var r = new OneLifeStartResponse { Date = DateKey(Today), ContentVersion = _content.ContentVersion };
            if (!Open)
            {
                r.Error = "closed";
                return r;
            }
            string bucket = Bucket(r.Date);
            if (await _days.GetAsync(bucket, playerId) != null)
            {
                r.Error = "used";
                return r;
            }
            r.Seed = SeedFor(r.Date, _salt);
            List<GhostRecord> leaders = await LeadersAsync(bucket, playerId);
            r.GhostsPacket = ReelPacket.Encode(new Reel
            {
                ContentVersion = _content.ContentVersion, Seed = r.Seed, SpeedPct = Echo.Core.Daily.DailyRules.SpeedPct,
                RunIndex = OneLifeRules.RunIndex, Run = new GhostRecord(), Ghosts = leaders,
            });
            // The attempt is used the moment the run starts (modes doc).
            r.Token = Guid.NewGuid().ToString("N");
            await _days.SetAsync(bucket, playerId, JsonSerializer.Serialize(new Record { Token = r.Token, Started = _now, Leaders = r.GhostsPacket }));
            Meta meta = Read<Meta>(await _board.GetPlayerAsync(playerId, MetaKey)) ?? new Meta();
            if (!meta.Days.Contains(r.Date)) meta.Days.Add(r.Date);
            await _board.SetPlayerAsync(playerId, MetaKey, JsonSerializer.Serialize(meta));
            r.Ok = true;
            return r;
        }

        // Today's top 10 runs, as named leader ghosts (never the player's own).
        async Task<List<GhostRecord>> LeadersAsync(string bucket, string playerId)
        {
            var leaders = new List<GhostRecord>();
            List<LeaderboardRow> top = await _board.TopAsync(Echo.Core.Daily.DailyRules.Leaders);
            var records = await _days.GetManyAsync(bucket, top.Select(t => t.PlayerId).ToList());
            foreach (LeaderboardRow row in top)
            {
                if (row.PlayerId == playerId || !records.TryGetValue(row.PlayerId, out string json)) continue;
                Record rec = Read<Record>(json);
                if (rec == null || !ReelPacket.TryDecode(rec.Packet, out Reel run)) continue;
                leaders.Add(Echo.Core.Daily.DailyRules.AsLeader(run.Run, row.Rank, row.Name));
            }
            leaders.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            return leaders;
        }

        public async Task<OneLifeSubmitResponse> SubmitAsync(string playerId, string date, string token, string packet)
        {
            var r = new OneLifeSubmitResponse();
            Record rec = date == null ? null : Read<Record>(await _days.GetAsync(Bucket(date), playerId));
            if (rec == null || rec.Finished || string.IsNullOrEmpty(token) || rec.Token != token) return Fail(r, "token");
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Fail(r, "malformed");
            if (reel.ContentVersion != _content.ContentVersion) return Fail(r, "content-version");
            string broken = OneLifeRules.Check(reel);
            if (broken != null) return Fail(r, broken);
            if (reel.Seed != SeedFor(date, _salt)) return Fail(r, "seed");
            if (!ReelPacket.TryDecode(rec.Leaders, out Reel given) || !Echo.Core.Daily.DailyRules.SameGhosts(reel.Ghosts, given.Ghosts)) return Fail(r, "ghosts");

            ReplayResult result = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            if (!result.Finished) return Fail(r, "unfinished");
            r.Distance = result.Distance;
            r.Abandoned = reel.Abandoned;
            r.BoardClosed = date != DateKey(Today);
            if (!r.BoardClosed) await _board.SubmitAsync(playerId, r.Distance);
            LeaderboardRow mine = r.BoardClosed ? null : await _board.PlayerAsync(playerId);
            r.Rank = mine?.Rank ?? 0;

            reel.Distance = r.Distance;
            reel.Score = result.Score;
            rec.Finished = true;
            rec.Abandoned = reel.Abandoned;
            rec.Distance = r.Distance;
            rec.Name = mine?.Name ?? "";
            rec.Packet = ReelPacket.Encode(reel);
            rec.Token = "";   // single use
            await _days.SetAsync(Bucket(date), playerId, JsonSerializer.Serialize(rec));
            r.Ok = true;
            return r;
        }

        // Days the player played that have now closed: rank them once and grant the rewards.
        async Task<List<string>> SettleAsync(string playerId)
        {
            var earned = new List<string>();
            Meta meta = Read<Meta>(await _board.GetPlayerAsync(playerId, MetaKey));
            if (meta == null) return earned;
            string today = DateKey(Today);
            bool changed = false;
            foreach (string date in meta.Days.Where(d => string.CompareOrdinal(d, today) < 0 && !meta.Settled.Contains(d)).ToList())
            {
                meta.Settled.Add(date);
                changed = true;
                var finished = (await _days.GetAllAsync(Bucket(date)))
                    .Select(kv => (Id: kv.Key, Rec: Read<Record>(kv.Value)))
                    .Where(x => x.Rec != null && x.Rec.Finished)
                    .OrderByDescending(x => x.Rec.Distance).ThenBy(x => x.Id, StringComparer.Ordinal)
                    .ToList();
                int rank = finished.FindIndex(x => x.Id == playerId) + 1;
                if (rank == 0) continue;
                DateTime day = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                var cosmetics = new List<string>();
                var badges = new List<BadgeEntry>();
                if (day.Month == 12 && day.Day == 25)
                {
                    cosmetics.Add($"grave_xmasday_{day.Year}");
                    if (rank <= GoldTop) cosmetics.Add($"grave_xmasday_{day.Year}_gold");
                }
                else if (day.DayOfWeek == DayOfWeek.Sunday && rank <= BadgeTop)
                {
                    badges.Add(new BadgeEntry { Id = $"onelife_{day:yyyyMMdd}", Until = new DateTimeOffset(day.AddDays(8)).ToUnixTimeSeconds() });
                }
                if (cosmetics.Count + badges.Count == 0) continue;
                if (await RewardStore.GrantAsync(_days, _board, playerId, "onelife_" + date, cosmetics, badges))
                    earned.AddRange(cosmetics.Concat(badges.Select(b => b.Id)));
            }
            if (changed) await _board.SetPlayerAsync(playerId, MetaKey, JsonSerializer.Serialize(meta));
            return earned;
        }

        /// <summary>Delete online data: the player's One Life records for the days still kept, and today's board score.</summary>
        public static async Task ForgetPlayerAsync(IDayStore days, IDailyStore board, string playerId, DateTime today)
        {
            for (int d = 0; d <= KeepDays; d++)
                await days.DeleteAsync(Bucket(DateKey(today.AddDays(-d))), playerId);
            await board.DeleteScoreAsync(playerId);
        }

        static OneLifeSubmitResponse Fail(OneLifeSubmitResponse r, string error)
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
