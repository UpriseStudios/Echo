using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.HowToPlay;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.UI;
using Echo.Game.Views;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// How to play (a user revision, asked for by testers): short clips of real play with captions, on jumping and jump
    /// height, the double jump, ghosts and power-ups. Shown once on first launch (and once to players updating to 2.0),
    /// skippable, and again from Settings › How to play. The clips are scripted runs (Core/HowToPlay) built off the main
    /// thread and played through the replay path, so they wear the player's character and today's art. They never touch
    /// progress, the save's power-ups or the reels.
    /// </summary>
    public sealed partial class EchoGame
    {
        const float HowToSlowMo = 0.35f;

        /// <summary>Test hook: false keeps the first-launch tutorial from starting on its own.</summary>
        public static bool AutoHowToPlay = true;

        [SerializeField] HowToPlayView _howTo;

        Task<List<HowToPlayClip>> _howToBuild;
        List<HowToPlayClip> _howToClips;
        int _howToIndex = -1;
        int _howToHeld;          // ticks shown since the clip's run ended
        bool _howToFromSettings;
        bool _howToHintBefore;

        /// <summary>The tutorial is on screen (loading or playing). Test hooks below.</summary>
        public bool HowToPlaying => _howToBuild != null || _howToClips != null;
        public int HowToClipIndex => _howToIndex;
        public IReadOnlyList<HowToPlayClip> HowToClips => _howToClips;
        public string HowToCaption => _howTo != null ? _howTo.Caption : null;

        void WireHowToPlay()
        {
            if (_howTo == null) return;
            _howTo.SkipClicked += EndHowToPlay;
            _settings.HowToPlayClicked += () =>
            {
                CloseMenu();
                StartHowToPlay(fromSettings: true);
            };
        }

        // First launch (or the first launch of 2.0): once, straight after the title appears.
        void MaybeStartHowToPlay()
        {
            if (AutoHowToPlay && !_save.Tutorial.SeenHowToPlay && State == GameState.Title) StartHowToPlay(fromSettings: false);
        }

        public void StartHowToPlay(bool fromSettings)
        {
            if (_howTo == null || HowToPlaying) return;
            _howToFromSettings = fromSettings;
            _howToHintBefore = _doubleJumpHintShown;
            _doubleJumpHintShown = true;   // no in-run hints over the clips
            _title.Hide();
            _hud.SetVisible(false);
            _howTo.ShowLoading();
            ChunkLibrary library = _library;
            SimConfig cfg = _cfg;
            string skin = EquippedCharacterId();
            _howToBuild = Task.Run(() => HowToPlayDirector.Build(library, cfg, skin));
            SetState(GameState.Replay);
        }

        void TickHowTo(float dt, bool tapped)
        {
            if (_howToBuild != null)
            {
                if (!_howToBuild.IsCompleted) return;
                _howToClips = _howToBuild.Status == TaskStatus.RanToCompletion ? _howToBuild.Result : new List<HowToPlayClip>();
                if (_howToBuild.Exception != null) Debug.LogException(_howToBuild.Exception);
                _howToBuild = null;
                if (_howToClips.Count == 0)
                {
                    EndHowToPlay();
                    return;
                }
                PlayHowToClip(0);
                return;
            }
            if (_reelPlayer == null) return;
            if (tapped)
            {
                NextHowToClip();
                return;
            }

            HowToPlayClip clip = _howToClips[_howToIndex];
            HowToPlayBeat beat = clip.BeatAt(_run.Tick + _howToHeld);
            _howTo.SetCaption(beat?.Text);
            int ticks = _driver.Advance(dt * (beat != null && beat.SlowMo ? HowToSlowMo : 1f));
            for (int i = 0; i < ticks; i++)
            {
                if (_run.Tick + _howToHeld >= clip.EndTick)
                {
                    NextHowToClip();
                    return;
                }
                if (_reelPlayer.Finished)
                {
                    if (_howToHeld == 0) HowToDied();
                    _howToHeld++;
                    continue;
                }
                SnapshotPrevious();
                int nearMissesBefore = _run.NearMisses;
                _reelPlayer.Step();
                PlayTickFeedback(_prevPlayer, _run.Player, _run.NearMisses - nearMissesBefore);
                PlayLifeFeedback();
            }
        }

        void PlayHowToClip(int index)
        {
            _howToIndex = index;
            _howToHeld = 0;
            HowToPlayClip clip = _howToClips[index];
            _course = clip.Course;
            _replayNemesis = null;
            RestartReelPlayer(clip.Reel);
            ApplyCosmetics(clip.Reel.Run.SkinId);
            ApplyTwist(Twist.None);
            _ghostViews.SetLantern(clip.Lantern);
            _ghostViews.SetNameTag(null);
            _ghostViews.SetLeaderNames(false);
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, null);
            _cameraRig.ClearFocus();
            _extraLifeTaken = true;   // a clip's Extra 1-UP never comes from the save

            _hud.SetVisible(true);
            _hud.SetPowerUps(clip.PowerButtons, clip.PowerButtons, clip.PowerButtons);
            _hud.SetCountdown(0);
            _hud.SetTapHint(false);
            _hud.SetLives(0);
            _hud.SetBoardCloses(0);
            _howTo.SetTopic(clip.Topic, HowToPlayDirector.Topics.Length, clip.TopicName, index == _howToClips.Count - 1);
            _howTo.SetCaption(null);
            SetState(GameState.Replay);
        }

        void HowToDied()
        {
            SnapshotPrevious();
            float w = SimUnits.ToWorld(_cfg.PlayerHitboxW), h = SimUnits.ToWorld(_cfg.PlayerHitboxH);
            Vector2 pos = _runnerView.Position;
            _shards.Burst(new Vector2(pos.x - w / 2f, pos.y), new Vector2(w, h), Color.white, 40);
            _sfx.Death();
            _cameraRig.Shake(0.16f, 0.28f);
        }

        void NextHowToClip()
        {
            if (_howToClips != null && _howToIndex + 1 < _howToClips.Count) PlayHowToClip(_howToIndex + 1);
            else EndHowToPlay();
        }

        public void EndHowToPlay()
        {
            if (!HowToPlaying) return;
            _howToBuild = null;
            _howToClips = null;
            _howToIndex = -1;
            _reelPlayer = null;
            _howTo.Hide();
            _hud.SetPowerUps(false, false, false);
            _hud.SetVisible(false);
            _ghostViews.SetLantern(false);
            _doubleJumpHintShown = _howToHintBefore;
            _save.Tutorial.SeenHowToPlay = true;
            Save();
            ApplySettings();
            ApplyCosmetics();
            EnterTitle();
            if (_howToFromSettings) OpenMenu(ShowSettings);
        }
    }
}
