using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Modes;

namespace EchoCloud
{
    /// <summary>
    /// Naughty vs Nice (modes doc): a December team event layered on the Daily Haunt. A player picks a side once (the
    /// smaller side is enforced once one passes 55% of members); each new daily best adds its improvement to their
    /// team's total, so a team's total is the sum of its members' daily bests. After the event the result shows, and the
    /// server grants the rewards. Everything shared is written here with Cloud Save write locks, retried on conflict.
    /// Stored per event in echo_team_EVENT: "tally" (members, totals, each day's change, each side's top contributor)
    /// and one record per player. Delete online data removes the player's record; the totals keep their metres as
    /// anonymous sums (a user decision).
    /// </summary>
    public sealed class TeamService
    {
        public const string Naughty = "naughty", Nice = "nice";

        /// <summary>Past this share of members, new players join the smaller side.</summary>
        public const double MaxShare = 0.55;

        /// <summary>Days contributed for the team badge.</summary>
        public const int BadgeDays = 5;

        /// <summary>Rewards (a user decision: the Halo is a runner accessory). Badges are "team_SIDE_YEAR".</summary>
        public const string HaloId = "acc_halo", CoalTrailId = "trail_coal";

        /// <summary>Badges by player, for the boards: one read for a page of names.</summary>
        public const string BadgesBucket = "echo_badges";

        /// <summary>Team data is deleted this long after the result stops showing (privacy policy retention).</summary>
        public const long RetentionSeconds = 31 * ModeRules.Day;

        const string TallyKey = "tally";
        const int Retries = 8;

        readonly IDayStore _days;
        readonly IDailyStore _players;
        readonly ModeWindow _event;
        readonly long _now;

        public TeamService(IDayStore days, IDailyStore players, IReadOnlyList<ModeWindow> windows, long now, bool forced)
        {
            _days = days;
            _players = players;
            _now = now;
            _event = Pick(windows, now, forced);
        }

        // Development can force the event on: the schedule's own window (switched on for the test), so a test date can
        // fall inside it or after it, or failing that a window round today.
        static ModeWindow Pick(IReadOnlyList<ModeWindow> windows, long now, bool forced)
        {
            if (!forced) return ModeRules.Pick(windows, ModeIds.NaughtyNice, now);
            var on = windows.Where(w => w.Id == ModeIds.NaughtyNice)
                .Select(w => new ModeWindow { Id = w.Id, Enabled = true, Start = w.Start, End = w.End, ShowUntil = w.ShowUntil, Weekdays = w.Weekdays })
                .ToList();
            ModeWindow picked = ModeRules.Pick(on, ModeIds.NaughtyNice, now);
            if (picked != null) return picked;
            long today = now - now % ModeRules.Day;
            return new ModeWindow { Id = ModeIds.NaughtyNice, Enabled = true, Start = today, End = today + ModeRules.Day, ShowUntil = today + 2 * ModeRules.Day };
        }

        public bool Open => _event != null && ModeRules.IsOpen(_event, _now);
        public bool Ended => _event != null && !Open && _now >= _event.End && ModeRules.IsShown(_event, _now);
        public string Event => _event == null ? "" : EventKey(_event);

        public static string EventKey(ModeWindow w) => $"{w.Id}_{DateTimeOffset.FromUnixTimeSeconds(w.Start).UtcDateTime.Year}";
        public static string Bucket(string eventKey) => "echo_team_" + eventKey;
        static string DayKey(long now) => DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        public sealed class Tally
        {
            public Dictionary<string, int> Members { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, long> Totals { get; set; } = new Dictionary<string, long>();
            public Dictionary<string, Dictionary<string, long>> Days { get; set; } = new Dictionary<string, Dictionary<string, long>>();
            public Dictionary<string, Top> Tops { get; set; } = new Dictionary<string, Top>();

            public long Get(Dictionary<string, long> d, string team) => d.TryGetValue(team, out long v) ? v : 0;
        }

        public sealed class Top
        {
            public string PlayerId { get; set; } = "";
            public string Name { get; set; } = "";
            public long Metres { get; set; }
        }

        public sealed class Member
        {
            public string Team { get; set; } = "";
            public bool Assigned { get; set; }
            public long Metres { get; set; }
            public int Days { get; set; }
            public string LastDay { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public async Task<TeamEventResponse> GetAsync(string playerId)
        {
            var r = new TeamEventResponse();
            if (_event == null || !ModeRules.IsShown(_event, _now))
            {
                r.Error = "closed";
                return r;
            }
            string bucket = Bucket(Event);
            Tally tally = Read<Tally>(await _days.GetAsync(bucket, TallyKey)) ?? new Tally();
            Member me = Read<Member>(await _days.GetAsync(bucket, playerId));
            Fill(r, tally, me);
            if (r.Ended && me != null) r.Rewards = await SettleAsync(playerId, me, r.Winner);
            return r;
        }

        public async Task<TeamEventResponse> JoinAsync(string playerId, string team)
        {
            var r = new TeamEventResponse();
            if (team != Naughty && team != Nice)
            {
                r.Error = "bad-team";
                return r;
            }
            if (!Open)
            {
                r.Error = "closed";
                return r;
            }
            string bucket = Bucket(Event);
            Member me = Read<Member>(await _days.GetAsync(bucket, playerId));
            if (me == null)
            {
                // The choice is permanent for the event; balancing happens as the player is counted in.
                string side = team;
                bool assigned = false;
                Tally tally = await UpdateAsync<Tally>(bucket, TallyKey, t =>
                {
                    side = Balanced(t, team);
                    assigned = side != team;
                    t.Members[side] = (t.Members.TryGetValue(side, out int n) ? n : 0) + 1;
                });
                me = new Member { Team = side, Assigned = assigned };
                await _days.SetAsync(bucket, playerId, JsonSerializer.Serialize(me));
                Fill(r, tally, me);
                return r;
            }
            Fill(r, Read<Tally>(await _days.GetAsync(bucket, TallyKey)) ?? new Tally(), me);
            return r;
        }

        /// <summary>The side a new player joins: their choice, unless that side already has more than 55% of members.</summary>
        public static string Balanced(Tally t, string choice)
        {
            int naughty = t.Members.TryGetValue(Naughty, out int a) ? a : 0, nice = t.Members.TryGetValue(Nice, out int b) ? b : 0;
            int total = naughty + nice;
            if (total == 0) return choice;
            int mine = choice == Naughty ? naughty : nice;
            return mine / (double)total > MaxShare ? (choice == Naughty ? Nice : Naughty) : choice;
        }

        /// <summary>
        /// A new daily best improved the player's day by <paramref name="improvement"/> metres: their team's total grows
        /// by it (so a team's total is its members' daily bests). Returns the team, or "" if not playing.
        /// </summary>
        public async Task<string> ContributeAsync(string playerId, string name, long improvement)
        {
            if (!Open || improvement <= 0) return "";
            string bucket = Bucket(Event);
            Member me = Read<Member>(await _days.GetAsync(bucket, playerId));
            if (me == null) return "";
            string today = DayKey(_now);
            if (me.LastDay != today) me.Days++;
            me.LastDay = today;
            me.Metres += improvement;
            if (!string.IsNullOrEmpty(name)) me.Name = name;
            await _days.SetAsync(bucket, playerId, JsonSerializer.Serialize(me));
            await UpdateAsync<Tally>(bucket, TallyKey, t =>
            {
                t.Totals[me.Team] = t.Get(t.Totals, me.Team) + improvement;
                if (!t.Days.TryGetValue(today, out var day)) t.Days[today] = day = new Dictionary<string, long>();
                day[me.Team] = t.Get(day, me.Team) + improvement;
                if (!t.Tops.TryGetValue(me.Team, out Top top) || top.PlayerId == playerId || me.Metres > top.Metres)
                    t.Tops[me.Team] = new Top { PlayerId = playerId, Name = me.Name, Metres = me.Metres };
            });
            return me.Team;
        }

        void Fill(TeamEventResponse r, Tally t, Member me)
        {
            r.Ok = true;
            r.Event = Event;
            r.Open = Open;
            r.Ended = Ended;
            r.SecondsLeft = Open ? ModeRules.SecondsLeft(_event, _now) : Ended ? Math.Max(_event.End, _event.ShowUntil) - _now : -1;
            r.Naughty = t.Get(t.Totals, Naughty);
            r.Nice = t.Get(t.Totals, Nice);
            string today = DayKey(_now);
            if (t.Days.TryGetValue(today, out var day))
            {
                r.NaughtyToday = t.Get(day, Naughty);
                r.NiceToday = t.Get(day, Nice);
            }
            r.NaughtyMembers = t.Members.TryGetValue(Naughty, out int a) ? a : 0;
            r.NiceMembers = t.Members.TryGetValue(Nice, out int b) ? b : 0;
            if (me != null)
            {
                r.Team = me.Team;
                r.Assigned = me.Assigned;
                r.YourMetres = me.Metres;
                r.YourDays = me.Days;
            }
            if (!r.Ended) return;
            r.Winner = r.Naughty > r.Nice ? Naughty : r.Nice > r.Naughty ? Nice : "draw";
            if (t.Tops.TryGetValue(Naughty, out Top tn))
            {
                r.TopNaughty = tn.Name;
                r.TopNaughtyMetres = tn.Metres;
            }
            if (t.Tops.TryGetValue(Nice, out Top tc))
            {
                r.TopNice = tc.Name;
                r.TopNiceMetres = tc.Metres;
            }
        }

        // Modes doc rewards: the winning side's contributors (a draw: both sides) get the Halo (Nice) or the coal-black
        // trail (Naughty); anyone who contributed on 5 or more days gets their team's badge. Granted once, kept for good.
        async Task<List<string>> SettleAsync(string playerId, Member me, string winner)
        {
            var earned = new List<string>();
            if (me.Days >= 1 && (winner == me.Team || winner == "draw")) earned.Add(me.Team == Nice ? HaloId : CoalTrailId);
            string badge = $"team_{me.Team}_{DateTimeOffset.FromUnixTimeSeconds(_event.Start).UtcDateTime.Year}";
            if (me.Days >= BadgeDays) earned.Add(badge);

            var badges = new List<BadgeEntry>();
            if (earned.Contains(badge)) badges.Add(new BadgeEntry { Id = badge, Until = Math.Max(_event.End, _event.ShowUntil) + RetentionSeconds });
            await RewardStore.GrantAsync(_days, _players, playerId, Event, earned.Where(id => id != badge), badges);
            return earned;
        }

        /// <summary>The player's rewards and current side.</summary>
        public async Task<ProfileResponse> ProfileAsync(string playerId)
        {
            RewardStore.PlayerRewards rewards = await RewardStore.ReadAsync(_players, playerId);
            var r = new ProfileResponse { Ok = true, Cosmetics = rewards.Cosmetics, Badges = rewards.Badges.Where(b => b.Until > _now).ToList() };
            if (_event != null) r.Team = Read<Member>(await _days.GetAsync(Bucket(Event), playerId))?.Team ?? "";
            return r;
        }

        /// <summary>Badges still showing for each of these players (the boards).</summary>
        public static async Task<Dictionary<string, List<string>>> BadgesAsync(IDayStore days, IReadOnlyCollection<string> playerIds, long now)
        {
            var result = new Dictionary<string, List<string>>();
            if (playerIds.Count == 0) return result;
            foreach (var kv in await days.GetManyAsync(BadgesBucket, playerIds))
            {
                List<BadgeEntry> badges = Read<List<BadgeEntry>>(kv.Value);
                if (badges == null) continue;
                var live = badges.Where(b => b.Until > now).Select(b => b.Id).ToList();
                if (live.Count > 0) result[kv.Key] = live;
            }
            return result;
        }

        /// <summary>Delete online data: the player's record in every event and their badges (their metres stay in totals).</summary>
        public static async Task ForgetPlayerAsync(IDayStore days, IReadOnlyList<ModeWindow> windows, string playerId)
        {
            foreach (string evt in windows.Where(w => w.Id == ModeIds.NaughtyNice).Select(EventKey).Distinct())
                await days.DeleteAsync(Bucket(evt), playerId);
            await days.DeleteAsync(BadgesBucket, playerId);
        }

        /// <summary>Privacy policy retention: an event's team data goes a month after its result stops showing.</summary>
        public static async Task SweepAsync(IDayStore days, IReadOnlyList<ModeWindow> windows, long now)
        {
            foreach (ModeWindow w in windows.Where(w => w.Id == ModeIds.NaughtyNice))
                if (now > Math.Max(w.End, w.ShowUntil) + RetentionSeconds)
                    await days.DeleteBucketAsync(Bucket(EventKey(w)));
        }

        // Read, change, write with the write lock; on a conflict read again and redo the change.
        async Task<T> UpdateAsync<T>(string bucket, string key, Action<T> change) where T : class, new()
        {
            for (int attempt = 0; ; attempt++)
            {
                var (json, writeLock) = await _days.GetLockedAsync(bucket, key);
                T value = Read<T>(json) ?? new T();
                change(value);
                if (await _days.TrySetAsync(bucket, key, JsonSerializer.Serialize(value), writeLock)) return value;
                if (attempt >= Retries) throw new InvalidOperationException($"{bucket}/{key} kept changing; try again");
                await Task.Delay(20 * (attempt + 1));
            }
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
