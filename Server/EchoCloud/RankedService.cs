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
    /// Ghost Duels (modes doc; "Ranked" in code): race a stranger's run on one of the season's 10 arenas, one attempt, and
    /// climb the tiers. StartDuel picks the arena and an opponent near your rating (within 150, wider when nobody's
    /// there, then a reference run), never your own run nor the last opponent again, and hands out a single-use token;
    /// SubmitDuel replays the run against exactly that opponent, settles the result, moves both ratings (the opponent's at
    /// half size, with a note for them) and pools the run for others. 10 ranked duels a day; practice is unlimited and
    /// changes nothing. A ranked duel left unsent counts as a loss after <see cref="ForfeitSeconds"/>, so closing the game
    /// never dodges a defeat. Ratings soft-reset between seasons; rewards go by final tier once a season ends.
    /// Stored: echo_rk_sN (each player's season), echo_rk_pool_sN (runs by arena and rating band), the duel_season_N board.
    /// </summary>
    public sealed class RankedService
    {
        public const string SeasonPrefix = "echo_rk_s", PoolPrefix = "echo_rk_pool_s";
        public const int ForfeitSeconds = 15 * 60;
        public const int RecentKept = 10;

        /// <summary>Seasons' data is deleted this long after they end (rewards settle when a player returns within it).</summary>
        public const int KeepDaysAfterSeason = 60;

        static readonly int[] Widen = { RankedRules.MatchWindow, 300, 600, int.MaxValue };

        readonly GameContent _content;
        readonly IDayStore _days;
        readonly IReadOnlyList<ModeWindow> _windows;
        readonly IReadOnlyList<RankedSeason> _seasons;
        readonly Func<RankedSeason, IDailyStore> _board;
        readonly IDailyStore _players;
        readonly IReadOnlyList<RankedReference> _references;
        readonly long _now;
        readonly bool _forced;

        /// <param name="players">The players' private items (where server rewards are kept).</param>
        public RankedService(GameContent content, IDayStore days, IDailyStore players, IReadOnlyList<ModeWindow> windows,
            IReadOnlyList<RankedSeason> seasons, Func<RankedSeason, IDailyStore> board, IReadOnlyList<RankedReference> references, long now, bool forced)
        {
            _players = players;
            _content = content;
            _days = days;
            _windows = windows;
            _seasons = seasons;
            _board = board;
            _references = references;
            _now = now;
            _forced = forced;
        }

        string Today => DateTimeOffset.FromUnixTimeSeconds(_now).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>The season to play now: the one running, or in development (forced) the latest one configured.</summary>
        RankedSeason Season => RankedSchedule.Current(_seasons, _now) ?? (_forced ? _seasons.LastOrDefault() : null);

        public bool Open => Season != null && ModeService.IsOpen(_windows, ModeIds.Ranked, _now, _forced);

        // ---- records ----

        public sealed class Pending
        {
            public string Token { get; set; } = "";
            public bool Ranked { get; set; }
            public int Arena { get; set; }
            public long Started { get; set; }

            /// <summary>"p:PLAYER" for a player's run, "r:N" for a reference run.</summary>
            public string Opponent { get; set; } = "";
            public int OpponentRating { get; set; }
            public int OpponentDistance { get; set; }
            public string OpponentName { get; set; } = "";

            /// <summary>The opponent's run as a ReelPacket, so the submitted duel is checked against exactly it.</summary>
            public string OpponentPacket { get; set; } = "";
        }

        public sealed class Result
        {
            public string Opponent { get; set; } = "";
            public string Outcome { get; set; } = "";
            public int Change { get; set; }
            public int Distance { get; set; }
            public int OpponentDistance { get; set; }
            public long At { get; set; }
        }

        public sealed class PlayerSeason
        {
            public int Rating { get; set; } = RankedRules.StartRating;
            public int Duels { get; set; }
            public string Day { get; set; } = "";
            public int Today { get; set; }
            public string LastOpponent { get; set; } = "";
            public Pending Duel { get; set; }
            public List<Result> Recent { get; set; } = new List<Result>();

            /// <summary>While you were away: duels your ghost won and lost, and how your rating moved.</summary>
            public int AwayWins { get; set; }
            public int AwayLosses { get; set; }
            public int AwayChange { get; set; }
            public string Name { get; set; } = "";
            public bool Settled { get; set; }
        }

        public sealed class PoolEntry
        {
            public string Player { get; set; } = "";
            public int Rating { get; set; }
            public int Distance { get; set; }
            public string Name { get; set; } = "";
            public string Packet { get; set; } = "";
            public long At { get; set; }
        }

        static string SeasonBucket(RankedSeason s) => SeasonPrefix + s.Number;
        static string PoolBucket(RankedSeason s) => PoolPrefix + s.Number;
        static string PoolKey(int arena, int band) => "a" + arena + "_b" + band;

        async Task<PlayerSeason> ReadPlayer(RankedSeason season, string playerId)
        {
            PlayerSeason p = Read<PlayerSeason>(await _days.GetAsync(SeasonBucket(season), playerId));
            if (p != null) return p;
            // A new season: the soft reset from the season before, if they played it.
            p = new PlayerSeason();
            RankedSeason before = _seasons.Where(x => x.Number < season.Number).OrderBy(x => x.Number).LastOrDefault();
            if (before != null && Read<PlayerSeason>(await _days.GetAsync(SeasonBucket(before), playerId)) is PlayerSeason old)
                p.Rating = RankedRules.SoftReset(old.Rating);
            return p;
        }

        Task WritePlayer(RankedSeason season, string playerId, PlayerSeason p) =>
            _days.SetAsync(SeasonBucket(season), playerId, JsonSerializer.Serialize(p));

        void RollDay(PlayerSeason p)
        {
            if (p.Day == Today) return;
            p.Day = Today;
            p.Today = 0;
        }

        // ---- profile ----

        public async Task<RankedProfileResponse> ProfileAsync(string playerId)
        {
            var r = new RankedProfileResponse { Open = Open };
            r.Rewards = await SettleAsync(playerId);
            await SweepAsync();
            RankedSeason season = Season;
            if (!r.Open || season == null)
            {
                r.Error = "closed";
                RankedSeason next = _seasons.Where(x => x.Start > _now).OrderBy(x => x.Start).FirstOrDefault();
                r.SecondsLeft = next != null ? next.Start - _now : -1;
                return r;
            }
            PlayerSeason p = await ReadPlayer(season, playerId);
            await ForfeitIfStale(season, playerId, p);
            RollDay(p);
            IDailyStore board = _board(season);
            r.Ok = true;
            r.Season = season.Number;
            r.SecondsLeft = season.End - _now;
            r.Rating = p.Rating;
            r.Duels = p.Duels;
            r.DuelsLeftToday = Math.Max(0, RankedRules.RankedPerDay - p.Today);
            r.Rank = (await board.PlayerAsync(playerId))?.Rank ?? 0;
            r.Tier = RankedRules.Tier(p.Rating, r.Rank);
            r.AwayWins = p.AwayWins;
            r.AwayLosses = p.AwayLosses;
            r.AwayChange = p.AwayChange;
            r.Recent = p.Recent.Select(x => new RankedResultEntry
            {
                Opponent = x.Opponent, Outcome = x.Outcome, Change = x.Change, Distance = x.Distance, OpponentDistance = x.OpponentDistance,
            }).ToList();
            List<LeaderboardRow> top = await board.TopAsync(DailyRules.Leaders);
            var badges = await TeamService.BadgesAsync(_days, top.Select(x => x.PlayerId).Append(playerId).Distinct().ToList(), _now);
            r.Board = top.Select(x => new DailyBoardEntry
            {
                Rank = x.Rank, Name = x.Name, Distance = x.Distance, IsYou = x.PlayerId == playerId,
                Badges = badges.TryGetValue(x.PlayerId, out List<string> b) ? b : new List<string>(),
            }).ToList();
            // The "while you were away" note is shown once.
            p.AwayWins = p.AwayLosses = p.AwayChange = 0;
            await WritePlayer(season, playerId, p);
            return r;
        }

        // ---- a duel ----

        public async Task<RankedStartResponse> StartAsync(string playerId, bool ranked)
        {
            var r = new RankedStartResponse { Ranked = ranked, ContentVersion = _content.ContentVersion };
            RankedSeason season = Season;
            if (!Open || season == null) return Fail(r, "closed");
            PlayerSeason p = await ReadPlayer(season, playerId);
            // Starting again abandons a ranked duel still open: it counts as a loss, so restarting never rerolls a bad draw.
            if (p.Duel != null && p.Duel.Ranked) await Forfeit(season, playerId, p);
            RollDay(p);
            if (ranked && p.Today >= RankedRules.RankedPerDay) return Fail(r, "limit");

            string token = NewToken();
            var rng = new Random((int)Seed32(token + "/pick"));
            // A player's run is found first, from any arena, and played on its own arena; only a reference run (nobody near
            // enough) takes an arena at random. Picking the arena first would rarely let players meet.
            (PoolEntry opponent, int arena) = await PickPlayer(season, playerId, p, ranked, rng);
            RankedReference reference = null;
            if (opponent == null)
            {
                arena = (int)(Seed32(token) % (uint)season.Arenas.Count);
                reference = PickReference(season, arena, p, ranked, rng);
            }
            if (opponent == null && reference == null) return Fail(r, "no-opponent");

            p.Duel = new Pending
            {
                Token = token, Ranked = ranked, Arena = arena, Started = _now,
                Opponent = opponent != null ? "p:" + opponent.Player : "r:" + reference.Index,
                OpponentRating = opponent?.Rating ?? reference.Rating,
                OpponentDistance = opponent?.Distance ?? reference.Distance,
                OpponentName = opponent?.Name ?? reference.Name,
                OpponentPacket = opponent?.Packet ?? reference.Packet,
            };
            if (ranked) p.Today++;
            await WritePlayer(season, playerId, p);

            ReelPacket.TryDecode(p.Duel.OpponentPacket, out Reel rival);
            r.Ok = true;
            r.Token = token;
            r.Season = season.Number;
            r.Arena = arena + 1;
            r.Seed = season.Arenas[arena];
            r.OpponentPacket = p.Duel.OpponentPacket;
            r.OpponentName = p.Duel.OpponentName;
            r.OpponentSkin = rival?.Run.SkinId ?? "";
            r.OpponentRating = p.Duel.OpponentRating;
            r.OpponentTier = RankedRules.Tier(p.Duel.OpponentRating, 0);
            r.DuelsLeftToday = Math.Max(0, RankedRules.RankedPerDay - p.Today);
            return r;
        }

        // Within 150 of your rating, then wider: never your own run, nor (ranked) the opponent you just had. Practice takes
        // anyone's run.
        async Task<(PoolEntry, int)> PickPlayer(RankedSeason season, string playerId, PlayerSeason p, bool ranked, Random rng)
        {
            var entries = new List<(PoolEntry Entry, int Arena)>();
            foreach (var kv in (await _days.GetAllAsync(PoolBucket(season))).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                int under = kv.Key.IndexOf('_');
                if (under < 2 || !int.TryParse(kv.Key.Substring(1, under - 1), out int arena) || arena < 0 || arena >= season.Arenas.Count) continue;
                foreach (PoolEntry e in Read<List<PoolEntry>>(kv.Value) ?? new List<PoolEntry>())
                    if (e.Player != playerId && (!ranked || "p:" + e.Player != p.LastOpponent)) entries.Add((e, arena));
            }
            foreach (int window in ranked ? Widen : new[] { int.MaxValue })
            {
                var near = entries.Where(e => Math.Abs((long)e.Entry.Rating - p.Rating) <= window).ToList();
                if (near.Count > 0) return near[rng.Next(near.Count)];
            }
            return (null, 0);
        }

        // The arena's reference run nearest your rating (any of them for practice), not the one you just had if there's another.
        RankedReference PickReference(RankedSeason season, int arena, PlayerSeason p, bool ranked, Random rng)
        {
            uint seed = season.Arenas[arena];
            var references = _references.Where(x => x.Seed == seed && (!ranked || "r:" + x.Index != p.LastOpponent)).ToList();
            if (references.Count == 0) references = _references.Where(x => x.Seed == seed).ToList();
            if (references.Count == 0) return null;
            return ranked ? references.OrderBy(x => Math.Abs(x.Rating - p.Rating)).ThenBy(x => x.Index).First() : references[rng.Next(references.Count)];
        }

        public async Task<RankedSubmitResponse> SubmitAsync(string playerId, string token, string packet)
        {
            var r = new RankedSubmitResponse();
            RankedSeason season = _seasons.FirstOrDefault(s => s.Holds(_now)) ?? (_forced ? _seasons.LastOrDefault() : null);
            if (season == null) return Fail(r, "closed");
            PlayerSeason p = await ReadPlayer(season, playerId);
            Pending duel = p.Duel;
            if (duel == null || string.IsNullOrEmpty(token) || duel.Token != token) return Fail(r, "token");
            if (duel.Ranked && _now - duel.Started > ForfeitSeconds)
            {
                await Forfeit(season, playerId, p);
                return Fail(r, "token");
            }
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Fail(r, "malformed");
            if (reel.ContentVersion != _content.ContentVersion) return Fail(r, "content-version");
            string broken = RankedRules.Check(reel);
            if (broken != null) return Fail(r, broken);
            if (reel.Seed != season.Arenas[duel.Arena]) return Fail(r, "seed");
            if (!ReelPacket.TryDecode(duel.OpponentPacket, out Reel rival) || !DailyRules.SameGhosts(reel.Ghosts, RivalOf(rival))) return Fail(r, "ghosts");
            ReplayResult result = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            if (!result.Finished) return Fail(r, "unfinished");

            p.Duel = null;
            int score = RankedRules.Score(result.Distance, duel.OpponentDistance);
            r.Ok = true;
            r.Ranked = duel.Ranked;
            r.Distance = result.Distance;
            r.OpponentDistance = duel.OpponentDistance;
            r.Outcome = score == 1000 ? "won" : score == 500 ? "draw" : "lost";
            r.RatingBefore = p.Rating;
            if (duel.Ranked)
            {
                IDailyStore board = _board(season);
                string tierBefore = RankedRules.Tier(p.Rating, (await board.PlayerAsync(playerId))?.Rank ?? 0);
                r.Change = RankedRules.Change(p.Rating, duel.OpponentRating, p.Duels, score);
                await Settle(season, playerId, p, duel, score, r.Change, result.Distance);
                await AddToPool(season, duel.Arena, playerId, r.RatingBefore, result.Distance, reel, p.Name);
                r.Rank = (await board.PlayerAsync(playerId))?.Rank ?? 0;
                r.Tier = RankedRules.Tier(p.Rating, r.Rank);
                r.TierUp = RankedRules.TierOrder(r.Tier) > RankedRules.TierOrder(tierBefore);
            }
            else
            {
                r.Tier = RankedRules.Tier(p.Rating, 0);
                await WritePlayer(season, playerId, p);
            }
            r.RatingAfter = p.Rating;
            r.DuelsLeftToday = Math.Max(0, RankedRules.RankedPerDay - (p.Day == Today ? p.Today : 0));
            return r;
        }

        static List<GhostRecord> RivalOf(Reel rival)
        {
            GhostRecord g = rival.Run.Clone();
            g.RunIndex = RankedRules.RivalRunIndex;
            g.Leader = false;
            g.Kills = 0;
            g.LastKillRun = 0;
            return new List<GhostRecord> { g };
        }

        // Your result, both ratings, the board and the opponent's note.
        async Task Settle(RankedSeason season, string playerId, PlayerSeason p, Pending duel, int score, int change, int distance)
        {
            p.Rating += change;
            p.Duels++;
            p.LastOpponent = duel.Opponent;
            p.Recent.Insert(0, new Result
            {
                Opponent = duel.OpponentName, Outcome = score == 1000 ? "won" : score == 500 ? "draw" : "lost", Change = change,
                Distance = distance, OpponentDistance = duel.OpponentDistance, At = _now,
            });
            if (p.Recent.Count > RecentKept) p.Recent.RemoveRange(RecentKept, p.Recent.Count - RecentKept);
            IDailyStore board = _board(season);
            await board.SubmitAsync(playerId, p.Rating);
            p.Name = (await board.PlayerAsync(playerId))?.Name ?? p.Name;
            await WritePlayer(season, playerId, p);

            if (!duel.Opponent.StartsWith("p:", StringComparison.Ordinal)) return;   // a reference run has no rating to move
            string opponentId = duel.Opponent.Substring(2);
            int theirChange = 0;
            await UpdateAsync(SeasonBucket(season), opponentId, (PlayerSeason o) =>
            {
                theirChange = RankedRules.OpponentChange(o.Rating, p.Rating - change, o.Duels, 1000 - score);
                o.Rating += theirChange;
                if (score == 0) o.AwayWins++;          // your loss was their ghost's win
                else if (score == 1000) o.AwayLosses++;
                o.AwayChange += theirChange;
            });
            if (theirChange != 0)
            {
                PlayerSeason after = Read<PlayerSeason>(await _days.GetAsync(SeasonBucket(season), opponentId));
                if (after != null) await board.SubmitAsync(opponentId, after.Rating);
            }
        }

        async Task AddToPool(RankedSeason season, int arena, string playerId, int rating, int distance, Reel reel, string name)
        {
            GhostRecord run = reel.Run.Clone();
            run.RunIndex = 1;
            var container = new Reel
            {
                ContentVersion = _content.ContentVersion, Seed = reel.Seed, SpeedPct = DailyRules.SpeedPct, RunIndex = 1, Run = run,
                Ghosts = new List<GhostRecord>(), Abandoned = reel.Abandoned,
            };
            var entry = new PoolEntry
            {
                Player = playerId, Rating = rating, Distance = distance, Name = string.IsNullOrEmpty(name) ? "Runner" : name,
                Packet = ReelPacket.Encode(container), At = _now,
            };
            await UpdateAsync(PoolBucket(season), PoolKey(arena, RankedRules.Band(rating)), (List<PoolEntry> list) =>
            {
                list.RemoveAll(e => e.Player == playerId);   // one run per player per band: their latest
                list.Add(entry);
                if (list.Count > RankedRules.PoolPerBand) list.RemoveRange(0, list.Count - RankedRules.PoolPerBand);
            });
        }

        // A ranked duel never sent counts as a loss against its opponent.
        async Task Forfeit(RankedSeason season, string playerId, PlayerSeason p)
        {
            Pending duel = p.Duel;
            p.Duel = null;
            if (duel == null || !duel.Ranked)
            {
                await WritePlayer(season, playerId, p);
                return;
            }
            int change = RankedRules.Change(p.Rating, duel.OpponentRating, p.Duels, 0);
            await Settle(season, playerId, p, duel, 0, change, 0);
        }

        async Task ForfeitIfStale(RankedSeason season, string playerId, PlayerSeason p)
        {
            if (p.Duel != null && p.Duel.Ranked && _now - p.Duel.Started > ForfeitSeconds) await Forfeit(season, playerId, p);
        }

        // ---- the end of a season ----

        // Rewards by final tier (modes doc: final rating, not peak, so throwing duels never pays): a tier badge and trail,
        // an animated trail for Phantom, and the Wraith skin for the top 100.
        async Task<List<string>> SettleAsync(string playerId)
        {
            var earned = new List<string>();
            foreach (RankedSeason ended in _seasons.Where(s => s.End <= _now && _now - s.End < KeepDaysAfterSeason * 86400L))
            {
                PlayerSeason p = Read<PlayerSeason>(await _days.GetAsync(SeasonBucket(ended), playerId));
                if (p == null || p.Settled || p.Duels == 0) continue;
                p.Settled = true;
                await WritePlayer(ended, playerId, p);
                int rank = (await _board(ended).PlayerAsync(playerId))?.Rank ?? 0;
                string tier = RankedRules.Tier(p.Rating, rank);
                var cosmetics = new List<string> { tier == RankedRules.Wraith ? "trail_duel_phantom" : "trail_duel_" + tier };
                if (tier == RankedRules.Wraith) cosmetics.Add("char_wraith");
                var badges = new List<BadgeEntry>
                {
                    new BadgeEntry { Id = "duel_" + tier + "_s" + ended.Number, Until = ended.End + 120L * 86400 },
                };
                if (await RewardStore.GrantAsync(_days, _players, playerId, "ranked_s" + ended.Number, cosmetics, badges))
                    earned.AddRange(cosmetics.Concat(badges.Select(b => b.Id)));
            }
            return earned;
        }

        async Task SweepAsync()
        {
            foreach (RankedSeason old in _seasons.Where(s => _now - s.End >= KeepDaysAfterSeason * 86400L))
            {
                await _days.DeleteBucketAsync(SeasonBucket(old));
                await _days.DeleteBucketAsync(PoolBucket(old));
            }
        }

        /// <summary>Delete online data: the player's seasons, their runs in every pool, and their board entries.</summary>
        public static async Task ForgetPlayerAsync(IDayStore days, IReadOnlyList<RankedSeason> seasons, Func<RankedSeason, IDailyStore> board, string playerId)
        {
            foreach (RankedSeason s in seasons)
            {
                await days.DeleteAsync(SeasonBucket(s), playerId);
                foreach (var kv in await days.GetAllAsync(PoolBucket(s)))
                {
                    List<PoolEntry> list = Read<List<PoolEntry>>(kv.Value);
                    if (list == null || list.All(e => e.Player != playerId)) continue;
                    list.RemoveAll(e => e.Player == playerId);
                    await days.SetAsync(PoolBucket(s), kv.Key, JsonSerializer.Serialize(list));
                }
                await board(s).DeleteScoreAsync(playerId);
            }
        }

        // ---- helpers ----

        static string NewToken()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        static uint Seed32(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToUInt32(sha.ComputeHash(Encoding.UTF8.GetBytes(text)), 0);
        }

        async Task UpdateAsync<T>(string bucket, string key, Action<T> change) where T : class, new()
        {
            for (int attempt = 0; ; attempt++)
            {
                var (json, writeLock) = await _days.GetLockedAsync(bucket, key);
                T value = Read<T>(json) ?? new T();
                change(value);
                if (await _days.TrySetAsync(bucket, key, JsonSerializer.Serialize(value), writeLock)) return;
                if (attempt >= 8) throw new InvalidOperationException($"{bucket}/{key} kept changing; try again");
                await Task.Delay(20 * (attempt + 1));
            }
        }

        static T Fail<T>(T r, string error) where T : IFailable
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
