using System;
using System.Collections.Generic;
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
    /// One Life (modes doc) from the game's side: its lobby (the Daily Haunt's, retitled), the warning before the one run,
    /// starting it (which uses today's attempt on the server), and sending it. While a run is going its token and, if the
    /// game is put away, its taps so far wait in the save (OneLifeSaveData); a run cut off by closing the game is sent
    /// abandoned the next time the game can, and counts where it stopped. The server decides everything.
    /// </summary>
    public sealed partial class EchoGame
    {
        bool _lobbyOneLife;
        OneLifeInfo _oneLife;
        float _oneLifeFetchedAt;
        bool _oneLifeBusy;

        /// <summary>The last run One Life's server answered for (tests).</summary>
        public OneLifeSubmitResult LastOneLifeResult { get; private set; }

        bool OneLifeForced => ForceModes && DevTools;

        void OpenOneLifeLobby()
        {
            if (_dailyLobby == null) return;
            OpenMenu(() =>
            {
                _lobbyOneLife = true;
                _dailyLobby.Show();
                _dailyLobby.SetHeading("One Life");
                _dailyLobby.SetTally(null, 0.5f);
                _ = LoadOneLife();
            });
        }

        void OnLobbyPlay()
        {
            if (_lobbyOneLife) AskOneLife();
            else PlayDaily();
        }

        async Task LoadOneLife()
        {
            if (!_servicesSettled)
            {
                _dailyLobby.SetMessage("Starting up... try again in a moment.", playable: false);
                return;
            }
            await FlushOneLife();
            OneLifeInfo info = await _services.Backend.GetOneLifeAsync(DailyDateOverride, OneLifeForced);
            if (this == null || !_dailyLobby.IsOpen || !_lobbyOneLife) return;
            _oneLife = info;
            _oneLifeFetchedAt = Time.realtimeSinceStartup;
            if (info.Rewards.Count > 0)
            {
                foreach (string id in info.Rewards) Log(AnalyticsEvents.ModeRewardEarned, ("mode", ModeIds.OneLife), ("reward", id));
                _ = SyncProfile();
            }
            if (!info.Ok && info.Error != "closed")
            {
                _dailyLobby.SetMessage("One Life needs a connection.", playable: false);
                return;
            }
            ShowOneLife();
        }

        void ShowOneLife()
        {
            OneLifeInfo o = _oneLife;
            if (!o.Open)
            {
                _dailyLobby.SetMessage(o.SecondsLeft >= 0 ? $"One Life is back in {ModeNames.Countdown(false, o.SecondsLeft).Substring(9)}." : "One Life is resting.",
                    playable: false);
                return;
            }
            var board = new List<DailyLobbyView.Line>();
            foreach (DailyBoardRow row in o.Board)
                board.Add(new DailyLobbyView.Line { Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.IsYou, Badges = BadgeNames.Tags(row.Badges) });
            string you = !o.Played ? "One run. No retries. You haven't had yours today."
                : o.Finished ? $"Your run today: {o.Distance} m" + (o.Rank > 0 ? $" · #{o.Rank}" : "")
                : "Your run is on its way to the board.";
            _dailyLobby.SetDay(you, board, "", o.Board.Count > 0 ? "Today's top runs are the ghosts you'll meet." : "Nobody has had their run yet: set the pace.");
            _dailyLobby.SetWatchable(false);
            _dailyLobby.SetPlay(!o.Played, "Start your one run");
            TickOneLifeLabels();
        }

        void TickOneLifeLabels()
        {
            if (_oneLife == null || !_oneLife.Ok || !_dailyLobby.IsOpen || !_lobbyOneLife) return;
            long left = Math.Max(0, _oneLife.SecondsLeft - (long)(Time.realtimeSinceStartup - _oneLifeFetchedAt));
            _dailyLobby.SetCountdown($"{_oneLife.Date} · ends in {left / 3600}h {left / 60 % 60:00}m {left % 60:00}s");
        }

        // Modes doc: a clear warning first.
        void AskOneLife() =>
            Ask("ONE LIFE", "One run. No retries. Closing the game ends your run.\nToday's top runs are the ghosts you'll meet, and there are no 1-UPs.",
                "Start", "Not now", PlayOneLife, null);

        async void PlayOneLife()
        {
            if (_oneLifeBusy) return;
            _oneLifeBusy = true;
            try
            {
                OneLifeStart start = await _services.Backend.StartOneLifeAsync(DailyDateOverride, OneLifeForced);
                if (this == null) return;
                if (!start.Ok || start.ContentVersion != _contentVersion || !ReelPacket.TryDecode(start.GhostsPacket, out Reel ghosts))
                {
                    _dailyLobby.SetStatus(start.Error == "used" ? "You've had today's run."
                        : start.Error == "closed" ? "One Life has closed for today."
                        : start.Ok ? "Update Echo to play One Life." : "One Life needs a connection.");
                    if (start.Error == "used") _dailyLobby.SetPlay(false, "");
                    return;
                }
                _save.OneLife.Date = start.Date;
                _save.OneLife.Token = start.Token;
                _save.OneLife.Pending = "";
                Save();
                Log(AnalyticsEvents.OneLifeStarted, ("date", start.Date), ("leaders", ghosts.Ghosts.Count));
                StartOneLifeRun(start.Seed, ghosts.Ghosts);
            }
            finally
            {
                _oneLifeBusy = false;
            }
        }

        void StartOneLifeRun(uint seed, List<GhostRecord> leaders)
        {
            _dailyLobby.Hide();
            _title.Hide();
            HideDeathScreen();
            _activeChallenge = null;
            _ghostViews.SetNameTag(null);
            _course = new Course(_library, seed);
            _run = OneLifeRules.Start(_course, leaders, _cfg);
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
            _hud.QueueToast("ONE LIFE");
            StartRunning();
        }

        // The run as it stands, to send: finished, or (the game put away) abandoned where it is now.
        string OneLifePacket(bool abandoned)
        {
            return ReelPacket.Encode(abandoned ? ReelRules.Abandoned(_run, _contentVersion) : ReelRules.From(_run, _contentVersion, "", false));
        }

        /// <summary>The game is being put away mid-run: keep the run so far, so it counts if the game doesn't come back.</summary>
        void KeepOneLifeRun()
        {
            if (_run == null || !_run.Options.OneLife || _run.IsDead || string.IsNullOrEmpty(_save.OneLife.Token)) return;
            _save.OneLife.Pending = OneLifePacket(abandoned: true);
            Save();
        }

        async void EndOneLife()
        {
            RunSimulation run = _run;
            DailyRules.Settle(run);
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _save.OneLife.Pending = OneLifePacket(abandoned: false);
            Save();
            _deathScreen.Show(run.Distance, run.Distance, false, "", "Sending your run...", "Your run");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner("ONE LIFE");
            OneLifeSubmitResult r = await FlushOneLife();
            if (this == null || _run != run) return;
            _deathScreen.SetMessage(r == null || !r.Ok
                ? "Your run is saved and will be sent when you're back online."
                : $"Your one run today: {r.Distance} m" + (r.Rank > 0 ? $" · #{r.Rank}" : "") + "\nTap to go back.");
        }

        /// <summary>Sends a waiting One Life run, if any (a finished one, or one cut off when the game was closed).</summary>
        async Task<OneLifeSubmitResult> FlushOneLife()
        {
            OneLifeSaveData pending = _save.OneLife;
            if (string.IsNullOrEmpty(pending.Pending) || string.IsNullOrEmpty(pending.Token) || !_servicesSettled) return null;
            OneLifeSubmitResult r = await _services.Backend.SubmitOneLifeAsync(pending.Date, pending.Token, pending.Pending, DailyDateOverride, OneLifeForced);
            if (this == null) return r;
            bool abandoned = ReelPacket.TryDecode(pending.Pending, out Reel sent) && sent.Abandoned;
            // Sent, or refused for good (already in, or broken): either way it never goes again.
            if (r.Ok || r.Error != "offline")
            {
                _save.OneLife.Pending = "";
                _save.OneLife.Token = "";
                Save();
            }
            if (r.Ok)
            {
                LastOneLifeResult = r;
                Log(abandoned ? AnalyticsEvents.OneLifeAbandoned : AnalyticsEvents.OneLifeFinished, ("distance", r.Distance), ("rank", r.Rank));
            }
            return r;
        }

        /// <summary>Tap on One Life's death screen: there's no second run, so back to its lobby.</summary>
        void LeaveOneLife()
        {
            HideDeathScreen();
            EnterTitle();
            ShowTitle();
            OpenOneLifeLobby();
        }
    }
}
