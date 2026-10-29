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
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;

namespace EchoCloud
{
    /// <summary>
    /// The Community Graveyard (modes doc): one course for the week (Monday 00:00 UTC to Sunday), and every finished run
    /// joins the pool. Each day's ghosts come from the deaths of the days before it (so Monday starts clean), chosen by
    /// GraveyardRules.Choose: the same days' tallies always give the same set, so whoever builds it first (the midnight
    /// trigger, or the first player after) builds the same one, and no lock is needed. A run is checked against the set
    /// version it played and replayed. Boards: the day's best (graveyard_daily) and the week's sum of daily bests
    /// (graveyard_weekly). Once a week closes: its top 10 get the Gravekeeper badge for the next week, and playing all
    /// 7 days counts towards the Graveyard skin (4 such weeks).
    /// Stored: echo_gy_week_MONDAY (each player's week, each day's ghost set), echo_gy_sec_DATE (that day's deaths by
    /// section and its total), echo_gy_runs_DATE (that day's runs, the ghosts to choose from); all kept 21 days.
    /// </summary>
    public sealed class GraveyardService
    {
        public const string WeekPrefix = "echo_gy_week_", SectionPrefix = "echo_gy_sec_", RunPrefix = "echo_gy_runs_";
        public const int KeepDays = 21;
        public const int BadgeTop = 10, FullWeeksForSkin = 4;
        public const string SkinId = "char_gravekeeper";
        const string MetaKey = "graveyard_meta";
        const string TotalKey = "total";

        // SectionTally is a Core type, with fields.
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        readonly GameContent _content;
        readonly IDailyStore _day, _week;
        readonly IDayStore _days;
        readonly IReadOnlyList<ModeWindow> _windows;
        readonly long _now;
        readonly bool _forced;
        readonly string _salt;

        /// <param name="day">The day's board (and the player's private items).</param>
        /// <param name="week">The week's board.</param>
        public GraveyardService(GameContent content, IDailyStore day, IDailyStore week, IDayStore days, IReadOnlyList<ModeWindow> windows,
            long now, bool forced, string salt)
        {
            if (string.IsNullOrEmpty(salt)) throw new ArgumentException("the seed salt is missing");
            _content = content;
            _day = day;
            _week = week;
            _days = days;
            _windows = windows;
            _now = now;
            _forced = forced;
            _salt = salt;
        }

        DateTime Now => DateTimeOffset.FromUnixTimeSeconds(_now).UtcDateTime;
        DateTime Today => Now.Date;
        public bool Open => ModeService.IsOpen(_windows, ModeIds.Graveyard, _now, _forced);

        public static DateTime Monday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        public static string Key(DateTime d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        public static string DateKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        static DateTime Parse(string date) =>
            DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        /// <summary>One course for the whole week, from its Monday: the Daily Haunt's secret salt with its own label.</summary>
        public static uint SeedFor(DateTime monday, string salt)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt));
            uint seed = BitConverter.ToUInt32(hmac.ComputeHash(Encoding.UTF8.GetBytes("echo-graveyard:" + Key(monday))), 0);
            return seed == 0 ? 1u : seed;
        }

        public sealed class PlayerWeek
        {
            /// <summary>Best distance on each day (yyyy-MM-dd).</summary>
            public Dictionary<string, int> Bests { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> NextRun { get; set; } = new Dictionary<string, int>();

            // Ordered by date, so the sum never depends on dictionary order.
            public int Total => Bests.OrderBy(kv => kv.Key, StringComparer.Ordinal).Sum(kv => kv.Value);
        }

        public sealed class DaySet
        {
            public string Version { get; set; } = "";

            /// <summary>The day's ghosts as a ReelPacket.</summary>
            public string Packet { get; set; } = "";

            /// <summary>Deaths in the whole graveyard up to the day before.</summary>
            public int Size { get; set; }
        }

        public sealed class Meta
        {
            public List<string> Weeks { get; set; } = new List<string>();
            public List<string> Settled { get; set; } = new List<string>();
            public int FullWeeks { get; set; }
        }

        // ---- the day's ghosts ----

        /// <summary>The trigger's job at 00:00 UTC, and anyone's after it: the day's ghost set, built once.</summary>
        public async Task<DaySet> SetForAsync(DateTime day)
        {
            day = day.Date;
            DateTime monday = Monday(day);
            string bucket = WeekPrefix + Key(monday), key = "set_" + Key(day);
            DaySet set = Read<DaySet>(await _days.GetAsync(bucket, key));
            if (set != null) return set;

            // Every death from Monday up to the day before (closed days only, so the result never changes).
            var sections = new Dictionary<int, SectionTally>();
            int size = 0;
            for (DateTime d = monday; d < day; d = d.AddDays(1))
                foreach (var kv in (await _days.GetAllAsync(SectionPrefix + Key(d))).OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (kv.Key == TotalKey)
                    {
                        size += int.TryParse(kv.Value, out int n) ? n : 0;
                        continue;
                    }
                    SectionTally part = Read<SectionTally>(kv.Value);
                    if (part == null) continue;
                    if (!sections.TryGetValue(part.Index, out SectionTally all)) sections[part.Index] = all = new SectionTally { Index = part.Index };
                    all.Deaths += part.Deaths;
                    foreach (string run in part.Runs)
                        if (all.Runs.Count < GraveyardRules.CandidatesKept) all.Runs.Add(run);
                }

            // The runs themselves, by key ("yyyyMMdd/player_runIndex"): deleted ones are skipped.
            var runs = new Dictionary<string, GhostRecord>();
            foreach (var group in sections.Values.SelectMany(s => s.Runs).Distinct().GroupBy(r => r.Split('/')[0]))
                foreach (var kv in await _days.GetManyAsync(RunPrefix + group.Key, group.Select(r => r.Split('/')[1]).ToList()))
                    if (ReelPacket.TryDecode(kv.Value, out Reel r)) runs[group.Key + "/" + kv.Key] = r.Run;
            var chosen = GraveyardRules.Choose(sections.Values, runs.ContainsKey);
            var ghosts = chosen.Select((c, slot) => GraveyardRules.AsGhost(runs[c.Run], slot, c.Deaths)).OrderBy(g => g.RunIndex).ToList();

            uint seed = SeedFor(monday, _salt);
            set = new DaySet
            {
                Packet = Container(seed, ghosts),
                Version = Hash(DateKey(day) + "|" + string.Join(",", chosen.Select(c => c.Run))),
                Size = size,
            };
            await _days.SetAsync(bucket, key, JsonSerializer.Serialize(set));
            return set;
        }

        // ---- the lobby ----

        public async Task<GraveyardResponse> GetAsync(string playerId)
        {
            var r = new GraveyardResponse { Open = Open, Date = DateKey(Today) };
            DateTime monday = Monday(Today);
            r.Rewards = await SettleAsync(playerId);
            await Housekeeping.SweepAsync(_days, WeekPrefix, Today, KeepDays);
            await Housekeeping.SweepAsync(_days, SectionPrefix, Today, KeepDays);
            await Housekeeping.SweepAsync(_days, RunPrefix, Today, KeepDays);
            if (!Open)
            {
                r.Error = "closed";
                ModeWindow w = ModeRules.Pick(_windows, ModeIds.Graveyard, _now);
                r.SecondsLeft = w != null ? ModeRules.SecondsLeft(w, _now) : -1;
                return r;
            }
            DaySet set = await SetForAsync(Today);
            r.Ok = true;
            r.Seed = SeedFor(monday, _salt);
            r.ContentVersion = _content.ContentVersion;
            r.Day = (int)(Today - monday).TotalDays;
            r.DaysLeft = 6 - r.Day;
            r.SecondsLeft = (long)(Today.AddDays(1) - Now).TotalSeconds;
            r.SetVersion = set.Version;
            r.GhostsPacket = set.Packet;
            r.Size = set.Size + (int.TryParse(await _days.GetAsync(SectionPrefix + Key(Today), TotalKey), out int today) ? today : 0);

            PlayerWeek mine = Read<PlayerWeek>(await _days.GetAsync(WeekPrefix + Key(monday), playerId)) ?? new PlayerWeek();
            r.BestToday = mine.Bests.TryGetValue(r.Date, out int best) ? best : 0;
            r.WeekTotal = mine.Total;
            r.NextRunIndex = mine.NextRun.TryGetValue(r.Date, out int next) ? next : 1;
            r.DaysPlayed = mine.Bests.Count;
            r.DailyRank = (await _day.PlayerAsync(playerId))?.Rank ?? 0;
            r.WeeklyRank = (await _week.PlayerAsync(playerId))?.Rank ?? 0;
            r.Daily = await Rows(_day, playerId);
            r.Weekly = await Rows(_week, playerId);
            return r;
        }

        async Task<List<DailyBoardEntry>> Rows(IDailyStore board, string playerId)
        {
            List<LeaderboardRow> rows = await board.TopAsync(DailyRules.Leaders);
            var badges = await TeamService.BadgesAsync(_days, rows.Select(x => x.PlayerId).Append(playerId).Distinct().ToList(), _now);
            return rows.Select(row => new DailyBoardEntry
            {
                Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.PlayerId == playerId,
                Badges = badges.TryGetValue(row.PlayerId, out List<string> b) ? b : new List<string>(),
            }).ToList();
        }

        // ---- a run ----

        public async Task<GraveyardSubmitResponse> SubmitAsync(string playerId, string date, string setVersion, string packet)
        {
            var r = new GraveyardSubmitResponse();
            if (!Open) return Fail(r, "closed");
            DateTime day;
            try
            {
                day = Parse(date);
            }
            catch (FormatException)
            {
                return Fail(r, "wrong-day");
            }
            // Today's, or yesterday's set within a few minutes of midnight (a run started before it).
            bool late = day == Today.AddDays(-1) && Now - Today < TimeSpan.FromMinutes(5);
            if (day != Today && !late) return Fail(r, "wrong-day");
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Fail(r, "malformed");
            if (reel.ContentVersion != _content.ContentVersion) return Fail(r, "content-version");
            string broken = GraveyardRules.Check(reel);
            if (broken != null) return Fail(r, broken);
            DateTime monday = Monday(day);
            if (reel.Seed != SeedFor(monday, _salt)) return Fail(r, "seed");
            DaySet set = await SetForAsync(day);
            if (set.Version != setVersion || !ReelPacket.TryDecode(set.Packet, out Reel given) || !DailyRules.SameGhosts(reel.Ghosts, given.Ghosts))
                return Fail(r, "ghosts");

            string weekBucket = WeekPrefix + Key(monday);
            PlayerWeek mine = Read<PlayerWeek>(await _days.GetAsync(weekBucket, playerId)) ?? new PlayerWeek();
            int next = mine.NextRun.TryGetValue(date, out int n) ? n : 1;
            if (reel.RunIndex < next) return Fail(r, "run-index");

            ReplayResult result = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            if (!result.Finished) return Fail(r, "unfinished");
            r.Distance = result.Distance;
            r.NewBest = r.Distance > (mine.Bests.TryGetValue(date, out int had) ? had : 0);
            if (r.NewBest) mine.Bests[date] = r.Distance;
            mine.NextRun[date] = reel.RunIndex + 1;
            await _days.SetAsync(weekBucket, playerId, JsonSerializer.Serialize(mine));
            r.BestToday = mine.Bests[date];
            r.WeekTotal = mine.Total;
            r.BoardClosed = late;
            if (!late)
            {
                if (r.NewBest) await _day.SubmitAsync(playerId, r.Distance);
                await _week.SubmitAsync(playerId, r.WeekTotal);   // the sum only grows, so keep-best holds it
                r.DailyRank = (await _day.PlayerAsync(playerId))?.Rank ?? 0;
                r.WeeklyRank = (await _week.PlayerAsync(playerId))?.Rank ?? 0;
            }
            Meta meta = Read<Meta>(await _day.GetPlayerAsync(playerId, MetaKey)) ?? new Meta();
            if (!meta.Weeks.Contains(Key(monday)))
            {
                meta.Weeks.Add(Key(monday));
                await _day.SetPlayerAsync(playerId, MetaKey, JsonSerializer.Serialize(meta));
            }

            // Every finished run joins the pool, where it ended, in the day it was sent (so today's tally feeds tomorrow).
            GhostRecord run = reel.Run.Clone();
            run.RunIndex = reel.RunIndex;
            run.DeathTick = result.DeathTick;
            run.Leader = false;
            run.Name = "";
            string runKey = playerId + "_" + reel.RunIndex;
            await _days.SetAsync(RunPrefix + Key(Today), runKey, Container(reel.Seed, new List<GhostRecord>(), run));
            int section = GraveyardRules.Section(r.Distance);
            string sectionBucket = SectionPrefix + Key(Today);
            await UpdateAsync<SectionTally>(sectionBucket, "s_" + section, s =>
            {
                s.Index = section;
                s.Deaths++;
                if (s.Runs.Count < GraveyardRules.CandidatesKept) s.Runs.Add(Key(Today) + "/" + runKey);
            });
            await UpdateTotalAsync(sectionBucket);
            r.Ok = true;
            return r;
        }

        // ---- rewards once a week has closed ----

        async Task<List<string>> SettleAsync(string playerId)
        {
            var earned = new List<string>();
            Meta meta = Read<Meta>(await _day.GetPlayerAsync(playerId, MetaKey));
            if (meta == null) return earned;
            string thisWeek = Key(Monday(Today));
            bool changed = false;
            foreach (string week in meta.Weeks.Where(w => string.CompareOrdinal(w, thisWeek) < 0 && !meta.Settled.Contains(w)).ToList())
            {
                meta.Settled.Add(week);
                changed = true;
                var weeks = (await _days.GetAllAsync(WeekPrefix + week))
                    .Where(kv => !kv.Key.StartsWith("set_", StringComparison.Ordinal))
                    .Select(kv => (Id: kv.Key, Total: Read<PlayerWeek>(kv.Value)?.Total ?? 0))
                    .OrderByDescending(x => x.Total).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
                int rank = weeks.FindIndex(x => x.Id == playerId) + 1;
                PlayerWeek mine = Read<PlayerWeek>(await _days.GetAsync(WeekPrefix + week, playerId));
                var cosmetics = new List<string>();
                var badges = new List<BadgeEntry>();
                DateTime monday = DateTime.ParseExact(week, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                if (rank >= 1 && rank <= BadgeTop)
                    badges.Add(new BadgeEntry { Id = "gravekeeper_" + week, Until = new DateTimeOffset(monday.AddDays(14)).ToUnixTimeSeconds() });
                if (mine != null && mine.Bests.Count >= 7 && ++meta.FullWeeks == FullWeeksForSkin) cosmetics.Add(SkinId);
                if (cosmetics.Count + badges.Count > 0 && await RewardStore.GrantAsync(_days, _day, playerId, "graveyard_" + week, cosmetics, badges))
                    earned.AddRange(cosmetics.Concat(badges.Select(b => b.Id)));
            }
            if (changed) await _day.SetPlayerAsync(playerId, MetaKey, JsonSerializer.Serialize(meta));
            return earned;
        }

        /// <summary>Delete online data: the player's weeks, their runs in the pool and their board scores (tallies stay as counts).</summary>
        public static async Task ForgetPlayerAsync(IDayStore days, IDailyStore day, IDailyStore week, string playerId, DateTime today)
        {
            for (int d = 0; d <= KeepDays; d++)
            {
                DateTime date = today.AddDays(-d);
                await days.DeleteAsync(WeekPrefix + Key(Monday(date)), playerId);
                foreach (string key in (await days.GetAllAsync(RunPrefix + Key(date))).Keys.Where(k => k.StartsWith(playerId + "_", StringComparison.Ordinal)))
                    await days.DeleteAsync(RunPrefix + Key(date), key);
            }
            await day.DeleteScoreAsync(playerId);
            await week.DeleteScoreAsync(playerId);
        }

        // ---- helpers ----

        string Container(uint seed, List<GhostRecord> ghosts, GhostRecord run = null) =>
            ReelPacket.Encode(new Reel
            {
                ContentVersion = _content.ContentVersion, Seed = seed, SpeedPct = DailyRules.SpeedPct, RunIndex = run?.RunIndex ?? 1,
                Run = run ?? new GhostRecord(), Ghosts = ghosts,
            });

        static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Substring(0, 16).ToLowerInvariant();
        }

        async Task UpdateAsync<T>(string bucket, string key, Action<T> change) where T : class, new()
        {
            for (int attempt = 0; ; attempt++)
            {
                var (json, writeLock) = await _days.GetLockedAsync(bucket, key);
                T value = Read<T>(json) ?? new T();
                change(value);
                if (await _days.TrySetAsync(bucket, key, JsonSerializer.Serialize(value, Json), writeLock)) return;
                if (attempt >= 8) throw new InvalidOperationException($"{bucket}/{key} kept changing; try again");
                await Task.Delay(20 * (attempt + 1));
            }
        }

        async Task UpdateTotalAsync(string bucket)
        {
            for (int attempt = 0; ; attempt++)
            {
                var (json, writeLock) = await _days.GetLockedAsync(bucket, TotalKey);
                int total = int.TryParse(json, out int n) ? n : 0;
                if (await _days.TrySetAsync(bucket, TotalKey, (total + 1).ToString(CultureInfo.InvariantCulture), writeLock)) return;
                if (attempt >= 8) return;   // the size is only shown; a lost count isn't worth failing a run over
                await Task.Delay(20 * (attempt + 1));
            }
        }

        static GraveyardSubmitResponse Fail(GraveyardSubmitResponse r, string error)
        {
            r.Error = error;
            return r;
        }

        static T Read<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(json, Json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
