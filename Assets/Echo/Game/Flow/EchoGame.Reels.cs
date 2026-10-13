using System;
using System.Collections.Generic;
using System.Globalization;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.UI;
using Echo.Game.Views;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The highlights reel (a user revision): Save reel on the death screen, the reel cupboard (five slots; a sixth
    /// replaces one the player picks) and watching a reel back exactly through the normal views. Clips are made in
    /// the clip editor (EchoGame.Clips). Replays never touch progress, the save or the power-ups held.
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] ReelsView _reels;
        [SerializeField] ReplayView _replay;

        Reel _pendingReel;      // waiting for a slot while the cupboard is full
        bool _reelSaved;        // this death screen's run is in the cupboard
        ReelPlayer _reelPlayer;
        int _reelIndex;
        GhostRecord _replayNemesis;
        float _replaySpeed = 1f;
        bool _replayEnded;
        float _replayEndedFor;
        bool _hintBeforeReplay;

        /// <summary>A reel is playing back (or in the clip editor). Test hook.</summary>
        public ReelPlayer CurrentReplay => _reelPlayer;

        public bool ReplayEnded => _replayEnded;

        // The Nemesis the ghost views mark: the reel's own while one plays.
        GhostRecord ShownNemesis => _reelPlayer != null ? _replayNemesis : _activeChallenge != null ? _challengeNemesis : _progress.Ghosts.Nemesis;

        bool ReelsOpen => _reels != null && _reels.IsOpen;

        void WireReels()
        {
            if (_reels != null)
            {
                _reels.WatchClicked += StartReplay;
                _reels.ClipClicked += OpenClipEditor;
                _reels.ChallengeClicked += SendReelChallenge;
                _reels.DeleteClicked += AskDeleteReel;
                _reels.ReplaceClicked += ReplaceReel;
                _reels.CloseClicked += CloseReels;
                _title.ReelsClicked += () => OpenMenu(ShowReels);
            }
            if (_replay != null)
            {
                _replay.ExitClicked += EndReplay;
                _replay.SpeedClicked += () =>
                {
                    _replaySpeed = _replaySpeed > 1f ? 1f : 2f;
                    _replay.SetSpeed(_replaySpeed);
                };
                _replay.AgainClicked += () => StartReplay(_reelIndex);
                _replay.ClipClicked += () => OpenClipEditor(_reelIndex);
            }
            _deathScreen.SaveReelClicked += OnSaveReel;
            WireClipEditor();
        }

        // ---- saving from the death screen ----

        void OfferSaveReel(bool newBest)
        {
            _reelSaved = false;
            _pendingReel = null;
            _deathScreen.SetSaveReel(ReelRules.CanSave(_run) ? "Save reel" : null, newBest, true);
        }

        void OnSaveReel()
        {
            if (State != GameState.DeathScreen || _reelSaved || !ReelRules.CanSave(_run)) return;
            bool newBest = _run.Options.Twist == Twist.None && !_run.Options.Challenge && _run.Score > _progress.Best;
            Reel reel = ReelRules.From(_run, _contentVersion, DateTime.Now.ToString("d MMM yyyy", CultureInfo.InvariantCulture), newBest);
            if (ReelRules.Add(_progress.Reels, reel))
            {
                ReelSaved();
                return;
            }
            // The cupboard is full: the player picks which one this run replaces.
            _pendingReel = reel;
            _reels.Show(Cards(), replaceMode: true);
        }

        void ReplaceReel(int index)
        {
            if (_pendingReel == null || index < 0 || index >= _progress.Reels.Count) return;
            ReelRules.Replace(_progress.Reels, index, _pendingReel);
            _pendingReel = null;
            _reels.Hide();
            ReelSaved();
        }

        void ReelSaved()
        {
            _reelSaved = true;
            Save();
            _deathScreen.SetSaveReel("Saved", false, false);
            _hud.QueueToast("SAVED TO YOUR REELS");
        }

        // ---- the cupboard ----

        void ShowReels() => _reels.Show(Cards(), replaceMode: false);

        void CloseReels()
        {
            if (_reels.ReplaceMode)
            {
                _pendingReel = null;
                _reels.Hide();
                return;
            }
            CloseMenu();
        }

        void AskDeleteReel(int index)
        {
            if (index < 0 || index >= _progress.Reels.Count) return;
            Reel reel = _progress.Reels[index];
            Ask("DELETE REEL?", $"{reel.Score:N0} on {reel.SavedOn} will be gone for good.", "Delete", "Keep", () =>
            {
                _progress.Reels.Remove(reel);
                Save();
                if (ReelsOpen) ShowReels();
            }, null);
        }

        List<ReelsView.Card> Cards()
        {
            var cards = new List<ReelsView.Card>();
            foreach (Reel reel in _progress.Reels)
            {
                CosmeticItem skin = _catalog != null ? _catalog.Resolve(reel.Run.SkinId, CosmeticType.Character) : null;
                bool playable = reel.PlayableOn(_contentVersion);
                string kind = reel.Options.Challenge ? " · Challenge" : reel.Options.Twist != Twist.None ? " · " + WeeklyTwists.Name(reel.Options.Twist) : "";
                cards.Add(new ReelsView.Card
                {
                    Preview = skin != null && skin.RunFrames.Length > 0 ? skin.RunFrames[0] : null,
                    Title = $"{reel.Score:N0}{(reel.NewBest ? "  BEST" : "")}",
                    Detail = playable
                        ? $"{reel.Distance} m · {Plural(reel.GhostsAtStart, "ghost")} · {reel.SavedOn}{kind}"
                        : "From an older version of Echo: it can't play back",
                    Playable = playable,
                });
            }
            return cards;
        }

        static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

        // ---- playing a reel back ----

        void StartReplay(int index)
        {
            if (!SetUpReplay(index)) return;
            _hud.SetVisible(true);
            _replay.ShowPlaying($"REPLAY · RUN #{_reelPlayer.Reel.RunIndex}", _replaySpeed);
            SetState(GameState.Replay);
        }

        // The reel from its start on its own course, wearing its own skin, with its ghosts and graves.
        bool SetUpReplay(int index)
        {
            if (index < 0 || index >= _progress.Reels.Count) return false;
            if (State != GameState.Menu && State != GameState.Replay) return false;
            Reel reel = _progress.Reels[index];
            if (!reel.PlayableOn(_contentVersion)) return false;

            CloseClipEditor();
            if (_reelPlayer == null) _hintBeforeReplay = _doubleJumpHintShown;
            _doubleJumpHintShown = true;  // no tutorial hints over a replay
            _reelIndex = index;
            _reels.Hide();
            _title.Hide();
            if (_course == null || _course.Seed != reel.Seed) _course = new Course(_library, reel.Seed);
            _replayNemesis = NemesisRules.Select(reel.Ghosts);
            RestartReelPlayer(reel);

            ApplyCosmetics(reel.Run.SkinId);
            ApplyTwist(reel.Options.Twist);
            _ghostViews.SetLantern(false);
            _ghostViews.SetNameTag(null);
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, _replayNemesis);
            _cameraRig.ClearFocus();
            _extraLifeTaken = true;  // a replayed Extra 1-UP must never be taken from the save
            _replaySpeed = 1f;

            _hud.SetPowerUps(false, false, false);
            _hud.SetCountdown(0);
            _hud.SetTapHint(false);
            _hud.SetLives(0);
            return true;
        }

        void RestartReelPlayer(Reel reel)
        {
            _reelPlayer = new ReelPlayer(reel, _course, _cfg);
            _run = _reelPlayer.Run;
            _replayEnded = false;
            _replayEndedFor = 0f;
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _shieldBreaksSeen = 0;
            _ghostViews.ResetPop();
            if (_ambient != null) _ambient.ResetRun();
            _driver.Reset();
            SnapshotPrevious();
        }

        void TickReplay(float dt)
        {
            if (_reelPlayer == null) return;
            if (_clipStage == ClipStage.Editing || _clipStage == ClipStage.Saving)
            {
                TickClipEditing();
                return;
            }
            if (_replayEnded)
            {
                _replayEndedFor += dt;
                return;
            }
            // Clips are previewed and recorded in real time; watching can run at double speed.
            int ticks = _driver.Advance(dt * (_clipStage != ClipStage.None ? 1f : _replaySpeed));
            for (int i = 0; i < ticks; i++)
            {
                if (_reelPlayer.ContinueDue)
                {
                    _reelPlayer.Continue();
                    SnapshotPrevious();
                    _ghostViews.ResetPop();
                    _hud.QueueToast("CONTINUE");
                }
                SnapshotPrevious();
                int nearMissesBefore = _run.NearMisses;
                _reelPlayer.Step();
                PlayTickFeedback(_prevPlayer, _run.Player, _run.NearMisses - nearMissesBefore);
                PlayLifeFeedback();
                if (_reelPlayer.Finished)
                {
                    ReplayDied();
                    return;
                }
            }
        }

        void ReplayDied()
        {
            _replayEnded = true;
            _replayEndedFor = 0f;
            SnapshotPrevious();
            float w = SimUnits.ToWorld(_cfg.PlayerHitboxW), h = SimUnits.ToWorld(_cfg.PlayerHitboxH);
            Vector2 pos = _runnerView.Position;
            _shards.Burst(new Vector2(pos.x - w / 2f, pos.y), new Vector2(w, h), Color.white, 40);
            _sfx.Death();
            _cameraRig.Shake(0.16f, 0.28f);
            if (_clipStage == ClipStage.None) _replay.ShowEnded();
        }

        void EndReplay()
        {
            if (_reelPlayer == null) return;
            CloseClipEditor();
            _reelPlayer = null;
            _replayNemesis = null;
            _doubleJumpHintShown = _hintBeforeReplay;
            _replay.Hide();
            _hud.SetVisible(false);
            ApplySettings();
            ApplyCosmetics();
            EnterTitle();
            OpenMenu(ShowReels);
        }
    }
}
