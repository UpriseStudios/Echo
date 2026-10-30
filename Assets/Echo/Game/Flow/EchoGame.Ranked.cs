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
    /// Ghost Duels (modes doc; "Ranked" in code, "Ranked Ghost Duels" to players, a user decision) from the game's side:
    /// the lobby (the Daily Haunt's, retitled: your rating, tier and duels left, the season's board by rating), a ranked
    /// duel (asked first, as it counts once started) or unrated practice, the opponent shown by name, skin and tier but
    /// never distance, the run with their ghost racing beside you and unable to hurt you, and the result. A duel cut off
    /// by putting the game away is sent where it stopped, as One Life's is. The server decides everything.
    /// </summary>
    public sealed partial class EchoGame
    {
        bool _lobbyRanked;
        RankedProfileInfo _ranked;
        RankedStart _duel;
        float _rankedFetchedAt;
        bool _rankedBusy;

        /// <summary>The last duel the server answered for (tests).</summary>
        public RankedSubmitResult LastDuelResult { get; private set; }

        bool RankedForced => ForceModes && DevTools;

        void OpenRankedLobby()
        {
            if (_dailyLobby == null) return;
            OpenMenu(() =>
            {
                _lobbyOneLife = false;
                _lobbyGraveyard = false;
                _lobbyRanked = true;
                ShowGraveyardLook(false);
                _dailyLobby.Show();
                _dailyLobby.SetHeading("Ranked Ghost Duels");
                _dailyLobby.SetTally(null, 0.5f);
                _ = LoadRanked();
            });
        }

        async Task LoadRanked()
        {
            if (!_servicesSettled)
            {
                _dailyLobby.SetMessage("Starting up... try again in a moment.", playable: false);
                return;
            }
            await FlushDuel();
            RankedProfileInfo info = await _services.Backend.GetDuelProfileAsync(DailyDateOverride, RankedForced);
            if (this == null || !_dailyLobby.IsOpen || !_lobbyRanked) return;
            _ranked = info;
            _rankedFetchedAt = Time.realtimeSinceStartup;
            if (info.Rewards.Count > 0)
            {
                foreach (string id in info.Rewards)
                {
                    Log(AnalyticsEvents.ModeRewardEarned, ("mode", ModeIds.Ranked), ("reward", id));
                    if (id.StartsWith("duel_", StringComparison.Ordinal)) Log(AnalyticsEvents.RankedSeasonReward, ("tier", id.Split('_')[1]), ("season", info.Season));
                }
                _ = SyncProfile();
            }
            if (!info.Ok)
            {
                _dailyLobby.SetMessage(info.Error == "closed"
                    ? info.SecondsLeft >= 0 ? $"The next season starts in {ModeNames.Countdown(false, info.SecondsLeft).Substring(9)}." : "Ranked Ghost Duels are resting."
                    : "Ranked Ghost Duels need a connection.", playable: false);
                return;
            }
            ShowRanked();
            // Modes doc: "Your ghost beat 3 challengers while you were away."
            if (info.AwayWins + info.AwayLosses > 0 && !ChallengeCardOpen)
                Ask("WHILE YOU WERE AWAY",
                    $"Your ghost beat {info.AwayWins} {(info.AwayWins == 1 ? "challenger" : "challengers")}"
                    + (info.AwayLosses > 0 ? $" and lost to {info.AwayLosses}" : "") + $".\nRating {Signed(info.AwayChange)}.", "OK", null, null, null);
        }

        static string Signed(int n) => n > 0 ? "+" + n : n.ToString(CultureInfo.InvariantCulture);

        static string TierName(string tier) => string.IsNullOrEmpty(tier) ? "" : tier.ToUpperInvariant();

        void ShowRanked()
        {
            RankedProfileInfo r = _ranked;
            var board = new List<DailyLobbyView.Line>();
            foreach (DailyBoardRow row in r.Board)
                if (board.Count < DailyRules.Leaders)
                    board.Add(new DailyLobbyView.Line { Rank = row.Rank, Name = row.Name, Distance = row.Distance, IsYou = row.IsYou,
                        Badges = BadgeNames.Tags(row.Badges), Unit = "" });
            string you = $"Rating {r.Rating:N0} · {TierName(r.Tier)}" + (r.Rank > 0 ? $" · #{r.Rank}" : "");
            string last = r.Recent.Count == 0 ? "Your first duel is waiting."
                : $"Last: {r.Recent[0].Outcome} vs {r.Recent[0].Opponent} · {Signed(r.Recent[0].Change)}";
            string status = r.DuelsLeftToday > 0
                ? $"{r.DuelsLeftToday} ranked {(r.DuelsLeftToday == 1 ? "duel" : "duels")} left today. Practice is unlimited."
                : "No ranked duels left today. Practice is unlimited.";
            _dailyLobby.SetDay(you, board, last, status);
            _dailyLobby.SetWatchable(false);
            _dailyLobby.SetPlay(r.DuelsLeftToday > 0, "Ranked duel");
            _dailyLobby.SetBoardToggle("Practice duel · unranked");
            TickRankedLabels();
        }

        void TickRankedLabels()
        {
            if (_ranked == null || !_ranked.Ok || !_dailyLobby.IsOpen || !_lobbyRanked) return;
            long left = Math.Max(0, _ranked.SecondsLeft - (long)(Time.realtimeSinceStartup - _rankedFetchedAt));
            _dailyLobby.SetCountdown($"Season {_ranked.Season} · {ModeNames.Countdown(true, left)}");
        }

        // A ranked duel counts once started (closing the game or starting another counts as a loss), so it's asked first.
        void AskRanked() =>
            Ask("RANKED DUEL", "One attempt against a stranger's ghost near your rating. Go further than their run to win.\n"
                + "Once it starts it counts, even if you leave.", "Start", "Not now", () => _ = BeginDuel(true), null);

        async Task BeginDuel(bool ranked)
        {
            if (_rankedBusy) return;
            _rankedBusy = true;
            try
            {
                await FlushDuel();
                RankedStart start = await _services.Backend.StartDuelAsync(ranked, DailyDateOverride, RankedForced);
                if (this == null) return;
                if (!start.Ok || start.ContentVersion != _contentVersion || !ReelPacket.TryDecode(start.OpponentPacket, out Reel rival))
                {
                    string why = start.Error == "limit" ? "That's today's ranked duels. Practice is unlimited."
                        : start.Error == "closed" ? "Ranked Ghost Duels have closed."
                        : start.Error == "no-opponent" ? "No opponent could be found. Try again in a moment."
                        : start.Ok ? "Update Echo to duel." : "Ranked Ghost Duels need a connection.";
                    if (_dailyLobby.IsOpen) _dailyLobby.SetStatus(why);
                    return;
                }
                _duel = start;
                if (ranked)
                {
                    _save.Ranked.Token = start.Token;
                    _save.Ranked.Pending = "";
                    Save();
                }
                Log(AnalyticsEvents.DuelStarted, ("ranked", ranked), ("arena", start.Arena), ("opponentTier", start.OpponentTier));
                // Modes doc: their name, skin and tier, never their distance.
                GhostRecord run = rival.Run;
                Ask(ranked ? $"RANKED · ARENA {start.Arena}" : $"PRACTICE · ARENA {start.Arena}",
                    $"vs {start.OpponentName}\n{TierName(start.OpponentTier)} · {start.OpponentRating:N0}\n"
                    + "Their ghost runs beside you and can't hurt you.", "Race", null, () => StartDuelRun(start, run), null);
            }
            finally
            {
                _rankedBusy = false;
            }
        }

        void StartDuelRun(RankedStart start, GhostRecord rival)
        {
            _dailyLobby.Hide();
            _title.Hide();
            HideDeathScreen();
            _activeChallenge = null;
            rival = rival.Clone();
            rival.SkinId = string.IsNullOrEmpty(start.OpponentSkin) ? rival.SkinId : start.OpponentSkin;
            _course = new Course(_library, (uint)start.Seed);
            _run = RankedRules.Start(_course, rival, _cfg);
            _ghostViews.SetNameTag($"{start.OpponentName} · {TierName(start.OpponentTier)}", RankedRules.RivalRunIndex);
            ApplyTwist(Twist.None);
            _bountySeen = false;
            _duelNemesis = null;
            SnapshotPrevious();
            _ghostViews.ResetPop();
            _ghostViews.SetLeaderNames(false);
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, null);
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _hud.SetLives(0);
            _hud.QueueToast(start.Ranked ? $"RANKED DUEL · VS {start.OpponentName.ToUpperInvariant()}" : "PRACTICE DUEL");
            StartRunning();
        }

        string DuelPacket(bool abandoned) =>
            ReelPacket.Encode(abandoned ? ReelRules.Abandoned(_run, _contentVersion) : ReelRules.From(_run, _contentVersion, "", false));

        /// <summary>The game is being put away mid-duel: keep it so far, so a ranked duel counts where it stopped.</summary>
        void KeepDuelRun()
        {
            if (_run == null || !_run.Options.Ranked || _run.IsDead || _duel == null || !_duel.Ranked) return;
            _save.Ranked.Token = _duel.Token;
            _save.Ranked.Pending = DuelPacket(abandoned: true);
            Save();
        }

        async void EndDuel()
        {
            RunSimulation run = _run;
            RankedStart duel = _duel;
            DailyRules.Settle(run);
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _hud.SetBoardCloses(0);
            _ghostViews.SetNameTag(null);
            _deathScreen.Show(run.Distance, 0, false, "", "Sending your run...", "Rival");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner(duel != null && duel.Ranked ? "RANKED DUEL" : "PRACTICE DUEL");
            RankedSubmitResult r;
            if (duel != null && duel.Ranked)
            {
                _save.Ranked.Token = duel.Token;
                _save.Ranked.Pending = DuelPacket(abandoned: false);
                Save();
                r = await FlushDuel();
            }
            else r = duel != null ? await _services.Backend.SubmitDuelAsync(duel.Token, DuelPacket(false), DailyDateOverride, RankedForced) : null;
            if (this == null || _run != run) return;
            if (r == null || !r.Ok)
            {
                _deathScreen.SetMessage(r != null && r.Error == "offline" && duel != null && duel.Ranked
                    ? "Saving your duel. It'll be sent when you're back online."
                    : "That duel couldn't be counted. Tap to go back.");
                return;
            }
            LastDuelResult = r;
            Log(AnalyticsEvents.RankedResult, ("outcome", r.Outcome), ("change", r.Change), ("tier", r.Tier), ("ranked", r.Ranked));
            if (r.TierUp)
            {
                Log(AnalyticsEvents.RankedTierUp, ("tier", r.Tier));
                _hud.QueueToast("TIER UP · " + TierName(r.Tier));
            }
            string versus = duel != null ? duel.OpponentName : "your rival";
            string line = r.Outcome == "won" ? $"You beat {versus}: {r.Distance} m to {r.OpponentDistance} m."
                : r.Outcome == "draw" ? $"A draw with {versus}: both {r.Distance} m."
                : $"{versus} got further: {r.OpponentDistance} m to your {r.Distance} m.";
            string rating = r.Ranked ? $"\nRating {r.RatingBefore:N0} → {r.RatingAfter:N0} ({Signed(r.Change)}) · {TierName(r.Tier)}" : "\nPractice: no rating change.";
            _deathScreen.Show(r.Distance, r.OpponentDistance, r.Outcome == "won", "", line + rating + "\nTap to go back.", "Rival");
            _deathScreen.SetScoreLabel("DISTANCE");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            _deathScreen.SetSaveReel(ReelRules.CanSave(run) ? "Save reel" : null, false, true);
            _deathScreen.SetBanner(r.Outcome == "won" ? "VICTORY" : r.Outcome == "draw" ? "DRAW" : "DEFEAT");
            if (_ranked != null && r.Ranked)
            {
                _ranked.Rating = r.RatingAfter;
                _ranked.Tier = r.Tier;
                _ranked.DuelsLeftToday = r.DuelsLeftToday;
            }
        }

        /// <summary>Sends a waiting ranked duel (finished, or cut off when the game was put away), if any.</summary>
        async Task<RankedSubmitResult> FlushDuel()
        {
            RankedSaveData pending = _save.Ranked;
            if (string.IsNullOrEmpty(pending.Pending) || string.IsNullOrEmpty(pending.Token) || !_servicesSettled) return null;
            RankedSubmitResult r = await _services.Backend.SubmitDuelAsync(pending.Token, pending.Pending, DailyDateOverride, RankedForced);
            if (this == null) return r;
            if (r.Ok || r.Error != "offline")
            {
                _save.Ranked.Pending = "";
                _save.Ranked.Token = "";
                Save();
            }
            return r;
        }

        /// <summary>Tap on a duel's result: back to the Ghost Duels lobby (each duel is started there).</summary>
        void LeaveDuel()
        {
            HideDeathScreen();
            EnterTitle();
            ShowTitle();
            OpenRankedLobby();
        }
    }
}
