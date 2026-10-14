using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Game.Persistence;
using Echo.Game.UI;
using Echo.Services.Ads;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The Daily Haunt from the game's side (Daily Haunt doc, version 2.0): the title button and its countdown, the
    /// lobby, daily runs against today's leaders and your own daily ghosts, and the daily death screen. Every run is
    /// sent to the server, which replays it for its real distance; a run that can't be sent waits in the save and is
    /// retried until the end of its day. Daily runs never touch the main game's ghosts, best, Nemesis or graveyard.
    /// </summary>
    public sealed partial class EchoGame
    {
        static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(5);

        [SerializeField] DailyLobbyView _dailyLobby;

        DailyInfo _daily;
        readonly List<GhostRecord> _dailyLeaders = new List<GhostRecord>();
        readonly List<GhostRecord> _dailyOwn = new List<GhostRecord>();
        int _dailyNextRun = 1;
        bool _dailyStale = true;         // fetch today's ghosts again before the next run
        bool _dailyEnabled = true;       // Remote Config, once known
        bool _dailyBusy;
        float _dailyFetchedAt;

        // The server's day as last confirmed (date, seconds left, when): a failed fetch leaves it alone, so queued runs
        // are judged by the server's clock, not the device's.
        string _serverDay;
        int _serverDaySecondsLeft;
        float _serverDayAt;
        float _dailyLabelIn;

        /// <summary>Development builds: play another day's course (honoured by the server in development only).</summary>
        public string DailyDateOverride { get; set; }

        /// <summary>The last daily verdict, for tests.</summary>
        public DailySubmitResult LastDailyResult { get; private set; }

        void WireDaily()
        {
            _title.DailyClicked += OpenDailyLobby;
            if (_dailyLobby == null) return;
            _dailyLobby.PlayClicked += PlayDaily;
            _dailyLobby.BackClicked += CloseMenu;
            _dailyLobby.RowClicked += OnDailyRowClicked;
        }

        static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // ---- the title button ----

        void RefreshDailyButton()
        {
            if (!_dailyEnabled)
            {
                _title.SetDaily("Daily Haunt · back soon");
                return;
            }
            TimeSpan left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            bool played = _save.Daily.LastPlayedDate == TodayUtc();
            _title.SetDaily($"Daily Haunt · {(int)left.TotalHours}h {left.Minutes:00}m" + (played ? "" : "  <color=#FFC94A>●</color>"));
        }

        void TickDailyLabels(float dt)
        {
            if ((_dailyLabelIn -= dt) > 0f) return;
            _dailyLabelIn = 1f;
            if (State == GameState.Title) RefreshDailyButton();
            if (_dailyLobby != null && _dailyLobby.IsOpen && _daily != null && _daily.Ok)
            {
                int left = Math.Max(0, _daily.SecondsLeft - (int)(Time.realtimeSinceStartup - _dailyFetchedAt));
                int closing = DailyClock.Closing(left);
                _dailyLobby.SetCountdown(closing > 0
                    ? $"{_daily.Date} · <color=#FF5A5A>{DailyClock.Line(closing)}</color>"
                    : $"{_daily.Date} · new course in {left / 3600}h {left / 60 % 60:00}m {left % 60:00}s");
            }
            bool dailyRun = State == GameState.Running && _run != null && _run.Options.Daily;
            _hud.SetBoardCloses(dailyRun ? DailyClock.Closing(BoardSecondsLeft()) : 0);
        }

        // After Delete online data: the day was fetched for the old online ID.
        void ForgetDailyDay()
        {
            _daily = null;
            _serverDay = null;
            _dailyLeaders.Clear();
            _dailyOwn.Clear();
        }

        // ---- Banish your own ghost (a user decision: own ghosts only, 3 a day, a rewarded ad) ----

        int _dailyBanishesLeft;
        int _dailyBanishCredit = -1;   // the ghost an ad has paid for whose banish hasn't reached the server yet
        bool _dailyBoardClosed;

        /// <summary>Test hook: own-ghost banishes left today.</summary>
        public int DailyBanishesLeft => _dailyBanishesLeft;

        void OfferDailyBanish()
        {
            bool paid = _run != null && _run.Killer != null && _dailyBanishCredit == _run.Killer.RunIndex;
            bool can = !_dailyBoardClosed && DailyRules.CanBanish(_run, _dailyBanishesLeft)
                       && (paid || _services.Ads.IsRewardedReady(AdPlacements.DailyBanish));
            _deathScreen.SetOffers(false, can);
            if (!can) return;
            _deathScreen.SetBanishLabel(paid
                ? "Banish this ghost\n<size=60%>Try again: the ad is already watched</size>"
                : $"Banish this ghost · ad\n<size=60%>Your own ghost · {_dailyBanishesLeft} left today</size>");
        }

        void OnDailyBanish()
        {
            if (State != GameState.DeathScreen || _dailyBoardClosed || !DailyRules.CanBanish(_run, _dailyBanishesLeft)) return;
            int runIndex = _run.Killer.RunIndex;
            if (_dailyBanishCredit == runIndex)
            {
                SendDailyBanish(runIndex);
                return;
            }
            ShowRewardedAd(AdPlacements.DailyBanish, rewarded =>
            {
                if (!rewarded || State != GameState.DeathScreen) return;
                CountAdWatched(equipUnlocked: false);
                _dailyBanishCredit = runIndex;
                SendDailyBanish(runIndex);
            });
        }

        async void SendDailyBanish(int runIndex)
        {
            RunSimulation run = _run;
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetMessage("Banishing your ghost...");
            DailyBanishResult r = await _services.Backend.BanishDailyGhostAsync(_daily.Date, runIndex, DailyDateOverride);
            if (this == null || _run != run) return;
            if (r.Ok)
            {
                _dailyBanishCredit = -1;
                _dailyBanishesLeft = r.BanishesLeft;
                DailyRules.BanishOwn(_dailyOwn, runIndex);
                if (_graves != null) _graves.Crumble(run.Killer);
                _sfx.GhostClear();
                Log(AnalyticsEvents.DailyBanishUsed, ("ghostRunIndex", runIndex), ("left", r.BanishesLeft));
                _deathScreen.SetMessage(r.BanishesLeft == 1 ? "Your ghost is banished · 1 banish left today"
                    : $"Your ghost is banished · {r.BanishesLeft} banishes left today");
                _hud.QueueToast("GHOST BANISHED");
                return;
            }
            if (r.Error == "none-left" || r.Error == "not-yours" || r.Error == "wrong-day")
            {
                _dailyBanishCredit = -1;
                if (r.Error == "none-left") _dailyBanishesLeft = 0;
                _deathScreen.SetMessage(r.Error == "none-left" ? "No banishes left today." : "That ghost can't be banished.");
                return;
            }
            _deathScreen.SetMessage("Couldn't banish it: check your connection and try again.");
            OfferDailyBanish();   // the ad already watched still counts
        }

        // ---- Watching a run on the board (a user decision: a rewarded ad, unless it's your own) ----

        bool _dailyWatchBusy;
        int _dailyWatchCredit;   // the rank an ad has paid for whose run hasn't arrived yet

        void OnDailyRowClicked(int index)
        {
            if (_daily == null || !_daily.Ok || index < 0 || index >= _daily.Board.Count || _dailyWatchBusy || !_dailyLobby.IsOpen) return;
            DailyBoardRow row = _daily.Board[index];
            if (row.IsYou || _dailyWatchCredit == row.Rank)
            {
                WatchDailyRun(row);
                return;
            }
            if (!_services.Ads.IsRewardedReady(AdPlacements.WatchRun))
            {
                _dailyLobby.SetStatus("No ad is ready right now. Try again in a moment.");
                return;
            }
            ShowRewardedAd(AdPlacements.WatchRun, rewarded =>
            {
                if (!rewarded || !_dailyLobby.IsOpen) return;
                CountAdWatched(equipUnlocked: false);
                _dailyWatchCredit = row.Rank;
                WatchDailyRun(row);
            });
        }

        async void WatchDailyRun(DailyBoardRow row)
        {
            _dailyWatchBusy = true;
            _dailyLobby.SetStatus(row.IsYou ? "Fetching your run..." : $"Fetching {row.Name}'s run...");
            DailyRunResult r = await _services.Backend.GetDailyRunAsync(row.Rank, DailyDateOverride);
            _dailyWatchBusy = false;
            if (this == null || !_dailyLobby.IsOpen) return;
            if (!r.Ok || !ReelPacket.TryDecode(r.Packet, out Reel reel) || !reel.PlayableOn(_contentVersion))
            {
                _dailyLobby.SetStatus(r.Error == "offline" ? "Watching a run needs a connection." : "That run can't be watched right now.");
                return;
            }
            _dailyWatchCredit = 0;
            LastDailyRunWatched = r;
            Log(AnalyticsEvents.DailyRunWatched, ("rank", r.Rank), ("own", row.IsYou));
            _dailyLobby.Hide();
            StartExternalReplay(reel, $"#{r.Rank} · {r.Name} · {r.Distance} m", OpenDailyLobby);
        }

        /// <summary>The last run fetched to watch, for tests.</summary>
        public DailyRunResult LastDailyRunWatched { get; private set; }

        // Seconds until the board closes at 00:00 UTC, by the server's clock when it has told us the day.
        int BoardSecondsLeft() =>
            _serverDay != null
                ? _serverDaySecondsLeft - (int)(Time.realtimeSinceStartup - _serverDayAt)
                : (int)(DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow).TotalSeconds;

        // ---- the lobby ----

        void OpenDailyLobby()
        {
            if (_dailyLobby == null) return;
            OpenMenu(() =>
            {
                _dailyLobby.Show();
                _ = LoadDaily();
            });
        }

        async Task LoadDaily()
        {
            if (!_servicesSettled)
            {
                _dailyLobby.SetMessage("Starting up... try again in a moment.", playable: false);
                return;
            }
            BackendFlags flags = await _services.Backend.GetFlagsAsync();
            if (this == null || !_dailyLobby.IsOpen) return;
            _dailyEnabled = flags.DailyEnabled;
            if (!flags.DailyEnabled && _services.Backend.SignedIn)
            {
                _dailyLobby.SetMessage("The Daily Haunt is resting. Back soon.", playable: false);
                return;
            }
            if (!await RefreshDaily())
            {
                _dailyLobby.SetMessage(_daily != null && _daily.Error == "content-version"
                    ? "Update Echo to play today's Daily Haunt."
                    : _daily != null && _daily.Error == BackendFlags.Disabled
                        ? "The Daily Haunt is resting. Back soon."
                        : "The Daily Haunt needs a connection.", playable: false);
                return;
            }
            Log(AnalyticsEvents.DailyOpened, ("date", _daily.Date), ("rank", _daily.Rank), ("best", _daily.BestDistance));
            ShowDailyDay();
        }

        // Fetches today: the seed, the leader set and your own daily ghosts. Unsent runs go first, so the ghosts match.
        async Task<bool> RefreshDaily()
        {
            await FlushDailyQueue();
            DailyInfo info = await _services.Backend.GetDailyAsync(DailyDateOverride);
            if (this == null) return false;
            _daily = info;
            if (!info.Ok) return false;
            _serverDay = info.Date;
            _serverDaySecondsLeft = info.SecondsLeft;
            _serverDayAt = Time.realtimeSinceStartup;
            if (info.ContentVersion != _contentVersion)
            {
                info.Error = "content-version";
                info.Ok = false;
                return false;
            }
            _dailyFetchedAt = Time.realtimeSinceStartup;
            _dailyBanishesLeft = info.BanishesLeft;
            _dailyLeaders.Clear();
            _dailyOwn.Clear();
            if (ReelPacket.TryDecode(info.GhostsPacket, out Reel ghosts))
                foreach (GhostRecord g in ghosts.Ghosts) (g.Leader ? _dailyLeaders : _dailyOwn).Add(g);
            _dailyNextRun = Math.Max(1, info.NextRunIndex);
            _dailyStale = false;

            // Top Ten: yesterday has closed, so its final rank is known (once per day).
            if (info.YesterdayRank > 0 && info.Day - 1 > _save.Daily.ClosedDay)
            {
                _save.Daily.ClosedDay = info.Day - 1;
                foreach (string id in AchievementRules.OnDailyDayClosed(_progress, info.YesterdayRank)) ToastAchievement(id);
                Save();
            }
            return true;
        }

        void ShowDailyDay()
        {
            var board = new List<DailyLobbyView.Line>();
            foreach (DailyBoardRow row in _daily.Board)
            {
                if (board.Count == DailyRules.Leaders) break;
                board.Add(new DailyLobbyView.Line { Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.IsYou });
            }
            string you = _daily.BestDistance > 0
                ? $"Your best today: {_daily.BestDistance} m · #{_daily.Rank}"
                : "You haven't run today's course yet.";
            string yesterday = string.IsNullOrEmpty(_daily.YesterdayWinner) ? "" : $"Yesterday's winner: {_daily.YesterdayWinner} · {_daily.YesterdayWinnerDistance} m";
            string ghosts = _dailyLeaders.Count == 0 ? "No leader ghosts yet: set the pace."
                : $"{_dailyLeaders.Count} leader ghosts and {_dailyOwn.Count} of yours await. Pass a leader to beat them.";
            _dailyLobby.SetDay(you, board, yesterday, ghosts);
            TickDailyLabels(0f);
        }

        // ---- a daily run ----

        public async void PlayDaily()
        {
            if (_dailyBusy) return;
            _dailyBusy = true;
            try
            {
                bool dayOver = _daily != null && Time.realtimeSinceStartup - _dailyFetchedAt > _daily.SecondsLeft;
                if (_dailyStale || _daily == null || !_daily.Ok || _save.Daily.Pending.Count > 0 || dayOver)
                {
                    if (!await RefreshDaily())
                    {
                        string why = _save.Daily.Pending.Count > 0 ? "Saving your last run. Check your connection, then try again."
                            : "The Daily Haunt needs a connection.";
                        if (State == GameState.DeathScreen) _deathScreen.SetMessage(why);
                        else if (_dailyLobby.IsOpen) _dailyLobby.SetMessage(why, playable: true);
                        return;
                    }
                }
                StartDailyRun();
            }
            finally
            {
                _dailyBusy = false;
            }
        }

        void StartDailyRun()
        {
            _dailyLobby.Hide();
            _title.Hide();
            HideDeathScreen();
            _activeChallenge = null;
            _ghostViews.SetNameTag(null);
            _course = new Course(_library, (uint)_daily.Seed);
            _run = DailyRules.Start(_course, DailyRules.Ghosts(_dailyLeaders, _dailyOwn), _dailyNextRun, _cfg);
            ApplyTwist(Twist.None);
            _bountySeen = false;
            _duelNemesis = null;
            SnapshotPrevious();
            _ghostViews.ResetPop();
            _ghostViews.SetLeaderNames(true);
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, null);
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _hud.SetLives(0);
            _hud.QueueToast(_dailyLeaders.Count > 0 ? $"DAILY HAUNT · {_dailyLeaders.Count} LEADERS" : "DAILY HAUNT");
            StartRunning();
        }

        // The daily death screen: distance, your best today, your rank and the next leader to beat. The run is sent at
        // once; until the server answers (or if it can't be reached) it says "Saving your run".
        async void EndDaily()
        {
            RunSimulation run = _run;
            DailyRules.Settle(run);
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _hud.SetBoardCloses(0);
            _ghostViews.SetLeaderNames(false);
            int bestToday = Math.Max(_daily.BestDistance, run.Distance);
            _deathScreen.Show(run.Distance, bestToday, run.Distance > _daily.BestDistance, "", "Saving your run...", "Best today");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _save.Daily.LastPlayedDate = _daily.Date;

            if (!ReelRules.CanSave(run))
            {
                // Nowhere near anything to rank: nothing to send.
                _deathScreen.SetMessage("Tap to try again.");
                Save();
                return;
            }
            Reel reel = ReelRules.From(run, _contentVersion, "", false);
            _save.Daily.Pending.Add(new PendingDailyRun { Date = _daily.Date, LeaderVersion = _daily.LeaderVersion, Packet = ReelPacket.Encode(reel) });
            Save();

            DailySubmitResult r = await FlushDailyQueue();
            if (this == null || _run != run) return;
            int passed = r != null && r.Ok ? r.LeadersPassed : DailyRules.Passed(LeaderDistances(), run.Distance);
            foreach (string id in AchievementRules.OnDailyRunEnded(_progress, _daily.Day, passed)) ToastAchievement(id);
            Save();
            if (r == null || !r.Ok)
            {
                _deathScreen.SetMessage("Saving your run. It'll be sent when you're back online.");
                return;
            }
            LastDailyResult = r;
            // Keep today's ghosts in step with the server's (same DailyRules), so the next run can start at once.
            GhostRecord mine = reel.Run.Clone();
            mine.RunIndex = reel.RunIndex;
            if (r.NewBest) _dailyOwn.Clear();
            else DailyRules.AddOwnGhost(_dailyOwn, mine);
            _dailyNextRun = Math.Max(_dailyNextRun, r.NextRunIndex);
            int previousRank = _daily.Rank;
            _daily.BestDistance = r.BestDistance;
            if (!r.BoardClosed) _daily.Rank = r.Rank;
            Log(AnalyticsEvents.DailyRunEnd, ("distance", r.Distance), ("best", r.BestDistance), ("rank", r.Rank), ("leadersPassed", r.LeadersPassed), ("newBest", r.NewBest));
            if (r.NewBest) Log(AnalyticsEvents.DailyNewBest, ("distance", r.Distance), ("rank", r.Rank));

            _deathScreen.Show(r.Distance, r.BestDistance, r.NewBest, "",
                r.BoardClosed ? "Midnight passed while you ran, so yesterday's board had closed. Today's course is ready." : NextLeaderLine(r),
                "Best today");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner(r.BoardClosed ? "BOARD CLOSED" : RankLine(r.Rank, previousRank, r.NewBest));
            _dailyBoardClosed = r.BoardClosed;
            OfferDailyBanish();
        }

        static string NextLeaderLine(DailySubmitResult r) =>
            !string.IsNullOrEmpty(r.NextLeaderName) ? $"Next: {r.NextLeaderName} · {r.NextLeaderDistance} m"
            : r.LeadersPassed > 0 ? "You passed every leader ghost!" : "Tap to try again.";

        static string RankLine(int rank, int previous, bool newBest)
        {
            if (rank <= 0) return newBest ? "NEW BEST TODAY" : null;
            string change = previous <= 0 ? "" : rank < previous ? $" (▲{previous - rank})" : rank > previous ? $" (▼{rank - previous})" : "";
            return (newBest ? "NEW BEST TODAY · " : "") + $"RANK #{rank}{change}";
        }

        List<int> LeaderDistances()
        {
            var distances = new List<int>();
            foreach (DailyBoardRow row in _daily.Board)
                if (!row.IsYou && distances.Count < DailyRules.Leaders) distances.Add(row.Distance);
            return distances;
        }

        // Sends unsent daily runs in order (Daily Haunt doc: retried until the end of their day). Returns the verdict on
        // the newest one sent, or null when nothing reached the server.
        async Task<DailySubmitResult> FlushDailyQueue()
        {
            DailySubmitResult last = null;
            List<PendingDailyRun> pending = _save.Daily.Pending;
            while (pending.Count > 0)
            {
                PendingDailyRun next = pending[0];
                if (!StillSendable(next.Date))
                {
                    pending.RemoveAt(0);
                    Log(AnalyticsEvents.DailySubmitFailed, ("error", "expired"));
                    continue;
                }
                DailySubmitResult r = await _services.Backend.SubmitDailyRunAsync(next.Date, next.LeaderVersion, next.Packet, DailyDateOverride);
                if (this == null) return null;
                // Switched off on the server: kept, like an offline run, until the end of its day.
                if (!r.Ok && (r.Error == "offline" || r.Error == "rate-limit" || r.Error == BackendFlags.Disabled))
                {
                    Log(AnalyticsEvents.DailySubmitFailed, ("error", r.Error));
                    break;   // try again later
                }
                pending.RemoveAt(0);
                if (!r.Ok) Log(AnalyticsEvents.DailySubmitFailed, ("error", r.Error));
                if (pending.Count > 0) _dailyStale = true;   // an older run reached the server: refetch before playing
                last = r;
            }
            Save();
            return last;
        }

        // Today's runs, or yesterday's for a few minutes after midnight UTC (the server checks the same).
        bool StillSendable(string date)
        {
            // The server's own date for today, while it is still today.
            if (_serverDay != null && date == _serverDay && Time.realtimeSinceStartup - _serverDayAt <= _serverDaySecondsLeft) return true;
            if (!string.IsNullOrEmpty(DailyDateOverride)) return date == DailyDateOverride;
            DateTime now = DateTime.UtcNow;
            string yesterday = now.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return date == TodayUtc() || date == yesterday && now - now.Date < SubmitGrace;
        }

        /// <summary>Development builds: the Settings button steps the Daily Haunt's date a day on, then back to today.</summary>
        string CycleDailyDate()
        {
            DateTime today = DateTime.UtcNow.Date;
            DateTime current = string.IsNullOrEmpty(DailyDateOverride) ? today
                : DateTime.ParseExact(DailyDateOverride, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime next = current.AddDays(1);
            DailyDateOverride = (next - today).TotalDays > 3 ? null : next.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _daily = null;
            _dailyStale = true;
            return DailyDateOverride == null ? "Dev: daily today" : "Dev: daily " + DailyDateOverride;
        }
    }
}
