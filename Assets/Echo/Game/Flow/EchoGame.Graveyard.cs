using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Game.Persistence;
using Echo.Game.UI;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The Community Graveyard (modes doc) from the game's side: its lobby (the Daily Haunt's, retitled, with today's
    /// board and the week's), runs against the day's community ghosts (each tagged with how deadly its spot is), the
    /// death screen, and last week's summary once. Runs are sent with the ghost set version they were played against; a
    /// run that can't be sent waits in the save and is retried while its day lasts. The server decides everything.
    /// </summary>
    public sealed partial class EchoGame
    {
        bool _lobbyGraveyard;
        bool _graveyardWeekly;
        bool _graveyardBusy;
        GraveyardInfo _graveyard;
        readonly List<GhostRecord> _graveyardGhosts = new List<GhostRecord>();
        float _graveyardFetchedAt;

        /// <summary>The last run the Graveyard's server answered for (tests).</summary>
        public GraveyardSubmitResult LastGraveyardResult { get; private set; }

        bool GraveyardForced => ForceModes && DevTools;

        void OpenGraveyardLobby()
        {
            if (_dailyLobby == null) return;
            OpenMenu(() =>
            {
                _lobbyOneLife = false;
                _lobbyRanked = false;
                _lobbyGraveyard = true;
                _graveyardWeekly = false;
                _dailyLobby.Show();
                _dailyLobby.SetHeading("Community Graveyard");
                _dailyLobby.SetTally(null, 0.5f);
                ShowGraveyardLook(true);
                _ = LoadGraveyard();
            });
        }

        void OnGraveyardBoardToggle()
        {
            if (!_lobbyGraveyard || _graveyard == null || !_graveyard.Ok) return;
            _graveyardWeekly = !_graveyardWeekly;
            ShowGraveyard();
        }

        async Task LoadGraveyard()
        {
            if (!_servicesSettled)
            {
                _dailyLobby.SetMessage("Starting up... try again in a moment.", playable: false);
                return;
            }
            if (!await RefreshGraveyard() && _lobbyGraveyard && _dailyLobby.IsOpen)
            {
                _dailyLobby.SetMessage(_graveyard != null && _graveyard.Error == "closed" ? ClosedLine()
                    : _graveyard != null && _graveyard.Error == "content-version" ? "Update Echo to play the Community Graveyard."
                    : "The Community Graveyard needs a connection.", playable: false);
                return;
            }
            if (this == null || !_dailyLobby.IsOpen || !_lobbyGraveyard) return;
            Log(AnalyticsEvents.GraveyardOpened, ("day", _graveyard.Day), ("size", _graveyard.Size), ("rank", _graveyard.WeeklyRank));
            ShowGraveyard();
            ShowGraveyardSummary();
        }

        string ClosedLine() =>
            _graveyard.SecondsLeft >= 0 ? $"The Community Graveyard opens in {ModeNames.Countdown(false, _graveyard.SecondsLeft).Substring(9)}."
                : "The Community Graveyard is resting.";

        // Fetches the week: its course, today's ghosts and your standing. Unsent runs go first.
        async Task<bool> RefreshGraveyard()
        {
            await FlushGraveyard();
            GraveyardInfo info = await _services.Backend.GetGraveyardAsync(DailyDateOverride, GraveyardForced);
            if (this == null) return false;
            _graveyard = info;
            _graveyardFetchedAt = Time.realtimeSinceStartup;
            if (info.Rewards.Count > 0)
            {
                foreach (string id in info.Rewards) Log(AnalyticsEvents.ModeRewardEarned, ("mode", ModeIds.Graveyard), ("reward", id));
                _ = SyncProfile();
            }
            if (!info.Ok) return false;
            if (info.ContentVersion != _contentVersion)
            {
                info.Error = "content-version";
                info.Ok = false;
                return false;
            }
            _graveyardGhosts.Clear();
            if (ReelPacket.TryDecode(info.GhostsPacket, out Reel set)) _graveyardGhosts.AddRange(set.Ghosts);
            // A new day starts its run numbers from the server's; within a day they only rise.
            if (_save.Graveyard.Date != info.Date)
            {
                _save.Graveyard.Date = info.Date;
                _save.Graveyard.NextRun = 1;
            }
            _save.Graveyard.NextRun = Math.Max(_save.Graveyard.NextRun, info.NextRunIndex);
            Save();
            return true;
        }

        void ShowGraveyard()
        {
            GraveyardInfo g = _graveyard;
            var board = new List<DailyLobbyView.Line>();
            foreach (DailyBoardRow row in _graveyardWeekly ? g.Weekly : g.Daily)
                if (board.Count < DailyRules.Leaders)
                    board.Add(new DailyLobbyView.Line { Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.IsYou, Badges = BadgeNames.Tags(row.Badges) });
            string you = (g.BestToday > 0 ? $"Today {g.BestToday} m" + (g.DailyRank > 0 ? $" · #{g.DailyRank}" : "") : "Today: not yet")
                         + (g.WeekTotal > 0 ? $"   Week {g.WeekTotal:N0} m" + (g.WeeklyRank > 0 ? $" · #{g.WeeklyRank}" : "") : "");
            string size = g.Size == 0 ? "No graves yet this week." : $"{g.Size.ToString("N0", CultureInfo.InvariantCulture)} {(g.Size == 1 ? "death" : "deaths")} and counting";
            string days = g.DaysLeft == 0 ? "Last day of the week" : $"{g.DaysLeft} {(g.DaysLeft == 1 ? "day" : "days")} left";
            string ghosts = g.Day == 0 ? "Fresh graves: no community ghosts on day one."
                : _graveyardGhosts.Count == 0 ? "Nobody fell yet, so no ghosts rise today."
                : $"{_graveyardGhosts.Count} ghosts rise from the week's deadliest spots.";
            _dailyLobby.SetDay(you, board, $"{size} · {days}", board.Count > 0 ? ghosts + "\nTap a run to watch it." : ghosts);
            _dailyLobby.SetWatchable(false);   // the board toggle has its place; the status line says it instead
            _dailyLobby.SetBoardToggle(_graveyardWeekly ? "Showing this week · tap for today" : "Showing today · tap for this week");
            _dailyLobby.SetPlay(true, "Play this week's course");
            TickGraveyardLabels();
        }

        void TickGraveyardLabels()
        {
            if (_graveyard == null || !_graveyard.Ok || !_dailyLobby.IsOpen || !_lobbyGraveyard) return;
            long left = Math.Max(0, _graveyard.SecondsLeft - (long)(Time.realtimeSinceStartup - _graveyardFetchedAt));
            _dailyLobby.SetCountdown($"Day {_graveyard.Day + 1} of 7 · new ghosts in {left / 3600}h {left / 60 % 60:00}m {left % 60:00}s");
        }

        // Modes doc: the end of the week, once: the deadliest spot, the champion and how you did.
        void ShowGraveyardSummary()
        {
            GraveyardWeekSummary s = _graveyard.LastWeek;
            if (s == null || _save.Graveyard.SummarySeen == s.Week || ChallengeCardOpen) return;
            _save.Graveyard.SummarySeen = s.Week;
            Save();
            string body = (s.DeadliestDeaths > 0 ? $"The deadliest spot was {s.DeadliestMetres}-{s.DeadliestMetres + GraveyardRules.SectionMetres} m: it claimed {s.DeadliestDeaths:N0} runners.\n" : "")
                          + (string.IsNullOrEmpty(s.Champion) ? "" : $"Champion: {s.Champion} · {s.ChampionTotal:N0} m\n")
                          + $"You: {s.Total:N0} m over {s.DaysPlayed} {(s.DaysPlayed == 1 ? "day" : "days")}" + (s.Rank > 0 ? $" · #{s.Rank}" : "")
                          + (s.Rank > 0 && s.Rank <= 10 ? "\nTop 10: you're a Gravekeeper this week." : "");
            Ask("LAST WEEK'S GRAVEYARD", body, "OK", null, null, null);
        }

        // ---- a run ----

        async void PlayGraveyard()
        {
            if (_graveyardBusy) return;
            _graveyardBusy = true;
            try
            {
                bool dayOver = _graveyard != null && Time.realtimeSinceStartup - _graveyardFetchedAt > _graveyard.SecondsLeft;
                if (_graveyard == null || !_graveyard.Ok || _save.Graveyard.Pending.Count > 0 || dayOver)
                {
                    if (!await RefreshGraveyard())
                    {
                        string why = _save.Graveyard.Pending.Count > 0 ? "Saving your last run. Check your connection, then try again."
                            : _graveyard != null && _graveyard.Error == "closed" ? ClosedLine()
                            : "The Community Graveyard needs a connection.";
                        if (State == GameState.DeathScreen) _deathScreen.SetMessage(why);
                        else if (_dailyLobby.IsOpen) _dailyLobby.SetMessage(why, playable: _graveyard == null || _graveyard.Error != "closed");
                        return;
                    }
                }
                StartGraveyardRun();
            }
            finally
            {
                _graveyardBusy = false;
            }
        }

        void StartGraveyardRun()
        {
            _dailyLobby.Hide();
            _title.Hide();
            HideDeathScreen();
            _activeChallenge = null;
            _ghostViews.SetNameTag(null);
            _course = new Course(_library, (uint)_graveyard.Seed);
            _run = GraveyardRules.Start(_course, _graveyardGhosts, _save.Graveyard.NextRun, _cfg);
            ShowGraveyardLook(true);
            ApplyTwist(Twist.None);
            _bountySeen = false;
            _duelNemesis = null;
            SnapshotPrevious();
            _ghostViews.ResetPop();
            _ghostViews.SetLeaderNames(true);   // the next ghost ahead shows its spot's toll
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, null);
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _hud.SetLives(0);
            _hud.QueueToast(_graveyardGhosts.Count > 0 ? $"COMMUNITY GRAVEYARD · {_graveyardGhosts.Count} GHOSTS" : "COMMUNITY GRAVEYARD · FRESH GRAVES");
            StartRunning();
        }

        async void EndGraveyard()
        {
            RunSimulation run = _run;
            DailyRules.Settle(run);
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _hud.SetBoardCloses(0);
            _ghostViews.SetLeaderNames(false);
            int bestToday = Math.Max(_graveyard.BestToday, run.Distance);
            _deathScreen.Show(run.Distance, bestToday, run.Distance > _graveyard.BestToday, "", "Saving your run...", "Best today");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner("COMMUNITY GRAVEYARD");
            _save.Graveyard.NextRun = run.RunIndex + 1;
            // Modes doc: every finished run joins the pool, so even a short one is sent.
            _save.Graveyard.Pending.Add(new PendingGraveyardRun
            {
                Date = _graveyard.Date, SetVersion = _graveyard.SetVersion, Packet = ReelPacket.Encode(ReelRules.From(run, _contentVersion, "", false)),
            });
            Save();
            GraveyardSubmitResult r = await FlushGraveyard();
            if (this == null || _run != run) return;
            if (r == null || !r.Ok)
            {
                _deathScreen.SetMessage("Saving your run. It'll be sent when you're back online.");
                return;
            }
            _graveyard.BestToday = r.BestToday;
            _graveyard.WeekTotal = r.WeekTotal;
            _graveyard.DaysPlayed = r.DaysPlayed;
            if (!r.BoardClosed)
            {
                _graveyard.DailyRank = r.DailyRank;
                _graveyard.WeeklyRank = r.WeeklyRank;
            }
            _deathScreen.Show(r.Distance, r.BestToday, r.NewBest, "",
                r.BoardClosed ? "Midnight passed while you ran: it counts for your week, but yesterday's board had closed. New ghosts have risen."
                    : $"Your week: {r.WeekTotal:N0} m · #{r.WeeklyRank}\nYour run joins the graveyard tomorrow.",
                "Best today");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner(r.BoardClosed ? "BOARD CLOSED" : (r.NewBest ? "NEW BEST TODAY · " : "") + $"RANK #{r.DailyRank}");
        }

        // The Graveyard's own day, by the server's clock while it lasts; otherwise the Daily Haunt's rule (today, or
        // yesterday for a few minutes after midnight UTC).
        bool GraveyardSendable(string date) =>
            _graveyard != null && _graveyard.Ok && date == _graveyard.Date
                && Time.realtimeSinceStartup - _graveyardFetchedAt <= _graveyard.SecondsLeft + SubmitGrace.TotalSeconds
            || StillSendable(date);

        // Sends unsent runs in order, while their day lasts. Returns the verdict on the newest one sent, or null.
        async Task<GraveyardSubmitResult> FlushGraveyard()
        {
            GraveyardSubmitResult last = null;
            List<PendingGraveyardRun> pending = _save.Graveyard.Pending;
            while (pending.Count > 0 && _servicesSettled)
            {
                PendingGraveyardRun next = pending[0];
                if (!GraveyardSendable(next.Date))
                {
                    pending.RemoveAt(0);
                    continue;
                }
                int daysBefore = _graveyard?.DaysPlayed ?? 0;
                GraveyardSubmitResult r = await _services.Backend.SubmitGraveyardRunAsync(next.Date, next.SetVersion, next.Packet, DailyDateOverride, GraveyardForced);
                if (this == null) return null;
                if (!r.Ok && r.Error == "offline") break;   // try again later
                pending.RemoveAt(0);   // sent, or refused for good
                if (!r.Ok) continue;
                last = r;
                LastGraveyardResult = r;
                Log(AnalyticsEvents.GraveyardRunEnd, ("distance", r.Distance), ("day", _graveyard?.Day ?? 0), ("newBest", r.NewBest));
                if (r.DaysPlayed == 7 && daysBefore < 7) Log(AnalyticsEvents.GraveyardWeekComplete, ("total", r.WeekTotal), ("rank", r.WeeklyRank));
            }
            Save();
            return last;
        }
    }
}
