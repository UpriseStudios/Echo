using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Echo.Core.Achievements;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Audio;
using Echo.Game.Bootstrap;
using Echo.Game.Cosmetics;
using Echo.Game.Input;
using Echo.Game.Persistence;
using Echo.Game.UI;
using Echo.Game.Views;
using Echo.Game.World;
using Echo.Services;
using Echo.Services.Ads;
using Echo.Services.Analytics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Game flow (tech spec 9): boots content and save, drives the 60 Hz sim from an accumulator,
    /// feeds it queued taps, renders views, plays feedback and runs the death screen and menus.
    /// Sim state changes only via Core.
    /// </summary>
    public sealed partial class EchoGame : MonoBehaviour
    {
        const int DyingFreezeTicks = 5;
        const float DeathScreenTapDelay = 0.25f;
        const float QuickRestartSeconds = 1f;   // "I Can Fix This": restart within a second of dying
        const int HopHintMetres = 350;
        const float ResumeCountdownSeconds = 3f;
        const float PhotoSeconds = 4f;   // Ghost Photo Mode: how long the ghosts stay frozen on a new best
        const string FirstGhostDeathMessage = "Careful. That was you.";
        // Grave reveal (a user revision): after the grave has risen the camera zooms in on it while the headstone grows.
        const float RevealDelay = 0.6f;
        const float RevealSeconds = 0.65f;

        /// <summary>Lets PlayMode tests keep their own save file. Null uses persistentDataPath.</summary>
        public static string SavePathOverride;

        [SerializeField] TextAsset[] _chunkFiles;
        [SerializeField] CosmeticCatalog _catalog;
        [SerializeField] CameraRig _cameraRig;
        [SerializeField] CourseView _courseView;
        [SerializeField] GhostViews _ghostViews;
        [SerializeField] GraveViews _graves;
        [SerializeField] TwistOverlay _twistOverlay;
        [SerializeField] CopycatView _copycatView;
        [SerializeField] AmbientView _ambient;
        [SerializeField] ChallengeView _challenge;
        [SerializeField] PowerUpsView _powerUpsView;
        [SerializeField] ElementsAwakenedView _awakened;
        [SerializeField] FeatureFlags _features;
        [SerializeField] RunnerView _runnerView;
        [SerializeField] ShardBurst _shards;
        [SerializeField] BackgroundView _background;
        [SerializeField] HudView _hud;
        [SerializeField] TitleView _title;
        [SerializeField] DeathScreenView _deathScreen;
        [SerializeField] StatsView _stats;
        [SerializeField] SettingsView _settings;
        [SerializeField] SkinsView _skins;
        [SerializeField] ScreenFlash _flash;
        [SerializeField] MusicPlayer _music;
        [SerializeField] SfxPlayer _sfx;
        [SerializeField] ConsentView _consentView;
        [SerializeField] EchoServiceConfig _serviceConfig;

        readonly FixedStepDriver _driver = new FixedStepDriver();
        readonly TapInput _input = new TapInput();
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);
        readonly System.Random _seedSource = new System.Random(Environment.TickCount);

        SimConfig _cfg;
        ChunkLibrary _library;
        string _contentVersion;
        EchoServices _services;
        SaveSystem _saves;
        SaveData _save;
        Progress _progress;
        Course _course;
        RunSimulation _run;
        PointerEventData _pointer;

        RunnerState _prevPlayer;
        bool _tapQueued;
        bool _graveReveal;
        float _offersCheckIn;

        // Ghost Challenges (value-adds doc): the challenge being raced, one waiting for the title, and the card's actions.
        Challenge _activeChallenge, _pendingChallenge;
        Action _challengeYes, _challengeNo;
        bool _passedTheirGrave;

        // Power-ups (a user revision): the Lantern's use this run, feedback already played, and what to log and
        // save once the run is over (no disk writes or analytics mid-run, tech spec 18).
        bool _lanternUsed, _extraLifeTaken;
        int _shieldBreaksSeen, _powerUpsUsed;
        Biome _announcedBiome;

        /// <summary>Test hook: draws the scenery as it looks at this distance instead of the runner's. View only.</summary>
        public float? PreviewMetres;
        string _graveName = "", _graveLine = "";
        bool _injectedTap;
        bool _awakenPending;   // the Elementals were just earned: ELEMENTS AWAKENED plays at the next quiet moment
        float _stateTime;
        bool _resumeCountdown;

        Color[] _paletteStops = new Color[0];
        float _clearAt = -1f;
        int _livesEarnedSeen, _livesUsedSeen;
        bool _doubleJumpHintShown;
        float _sessionStart;
        int _sessionRuns;
        float _diedAt = -1f;
        bool _photoMode;
        bool _bountySeen, _bountyToLog;
        GhostRecord _duelNemesis;
        bool _consentFromSettings;

        /// <summary>Test hook: when set, decides the tap for each sim tick (tick index in, tap out).</summary>
        public Func<int, bool> ScriptedTaps;

        /// <summary>
        /// Finger down (variable jump). Update sets it from the input each frame; tests that drive TickFrame directly
        /// leave it true (full jumps) or script it per tick with ScriptedHeld.
        /// </summary>
        public bool Held = true;
        public Func<int, bool> ScriptedHeld;

        /// <summary>Tests and tools: swap the feature flags at runtime and go back to the title for a fresh run.</summary>
        /// <summary>Test hook: owns and wears a character, as the Skins screen would.</summary>
        public void PreviewCharacter(string characterId)
        {
            if (!_save.Cosmetics.Owned.Contains(characterId)) _save.Cosmetics.Owned.Add(characterId);
            _save.Cosmetics.Equipped.Character = characterId;
            ApplyCosmetics();
        }

        public void UseFeatures(FeatureFlags flags)
        {
            _features = flags;
            EnterTitle();
        }

        public GameState State { get; private set; } = GameState.Boot;
        public RunSimulation Run => _run;
        public Progress Progress => _progress;

        /// <summary>The live save data (tests read power-up counts and settings from it).</summary>
        public SaveData CurrentSave => _save;
        public EchoServices Services => _services;

        /// <summary>Test hook: behaves like a tap that is not over any UI button.</summary>
        public void InjectTap() => _injectedTap = true;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _cfg = SimConfig.Default;

            var json = new string[_chunkFiles.Length];
            for (int i = 0; i < json.Length; i++) json[i] = _chunkFiles[i].text;
            _library = ChunkLibrary.FromJson(json);
            _contentVersion = ContentVersion.Compute(_library, _cfg);

            _services = EchoServices.CreateMocks();
            WireBackend();
            string savePath = SavePathOverride ?? Path.Combine(Application.persistentDataPath, "echo_save.json");
            _saves = new SaveSystem(savePath);
            InitSeasons(savePath);
            _save = _saves.Load() ?? new SaveData();
            _save.Tutorial ??= new TutorialSaveData();
            _save.Settings ??= new SettingsSaveData();
            _save.Cosmetics ??= new CosmeticsSaveData();
            _save.Consent ??= new ConsentSaveData();
            _progress = SaveMapper.ToProgress(_save, _contentVersion, out bool discarded);
            if (discarded) Debug.Log("[Echo] content changed since the last save; old ghosts were discarded");
            if (_progress.Seed == 0) _progress.Seed = NewSeed();
            if (_catalog != null)
            {
                CosmeticUnlocks.Earn(_catalog, _progress.Best, _save.Cosmetics.Owned);
                CosmeticUnlocks.EarnByAds(_catalog, _save.Cosmetics.AdsWatched, _save.Cosmetics.Owned);
                CosmeticUnlocks.EarnByStats(_catalog, _progress.Stats, _save.Cosmetics.Owned);
                _awakenPending = ElementalTrial.EarnIfComplete(_catalog, AchievementCatalog.SinglePlayerEarned(_progress.Achievements), _save.Cosmetics.Owned).Count > 0;
            }
            foreach (string id in AchievementRules.OnSessionStarted(_progress)) ToastAchievement(id);
            // Skins addendum: Pumpkin is free to anyone who plays during Halloween week, and kept after.
            if (_features != null && _features.HalloweenPumpkin && IsHalloweenWeek(DateTime.UtcNow)
                && !_save.Cosmetics.Owned.Contains(HalloweenCharacter))
            {
                _save.Cosmetics.Owned.Add(HalloweenCharacter);
                _hud.QueueToast("Happy Halloween! Pumpkin unlocked");
            }

            _deathScreen.ContinueClicked += OnContinue;
            _deathScreen.BanishClicked += OnBanish;
            _deathScreen.ShareClicked += OnShare;
            _deathScreen.MenuClicked += OnMenu;
            _deathScreen.HeadstoneLanded += OnHeadstoneLanded;
            _deathScreen.Chiselled += _sfx.Chisel;
            if (_ambient != null) _ambient.Howled += _sfx.Howl;
            _title.SkinsClicked += () => OpenMenu(ShowSkins);
            _title.StatsClicked += () => OpenMenu(() =>
            {
                bool gallery = _features == null || _features.RoguesGallery;
                _stats.Show(_progress, gallery);
                if (gallery) Log(AnalyticsEvents.GalleryViewed, ("entries", _progress.Gallery.Count));
            });
            _title.TwistClicked += OnTwistClicked;
            _title.ExitClicked += AskToQuit;
            WireDaily();
            _title.PowerUpsClicked += OpenPowerUps;
            _deathScreen.PowerUpsClicked += OpenPowerUps;
            _skins.PowerUpsClicked += OpenPowerUps;
            _hud.PowerUpClicked += UsePowerUp;
            if (_powerUpsView != null)
            {
                _powerUpsView.WatchClicked += WatchPowerUpAd;
                _powerUpsView.Closed += () => _powerUpsView.Hide();
            }
            _deathScreen.DuelClicked += OnDuel;
            _title.SettingsClicked += () => OpenMenu(ShowSettings);
            _skins.Closed += CloseMenu;
            _stats.Closed += CloseMenu;
            _settings.Closed += CloseMenu;
            _deathScreen.ChallengeClicked += OnSendRunAsChallenge;
            WireReels();
            WireHowToPlay();
            WireRecording();
            _stats.ChallengeClicked += SendBestChallenge;
            _stats.PasteClicked += OnPasteChallenge;
            _settings.NameClicked += () => AskName(null);
            _settings.StatusRequested += OnSettingsStatusRequested;
            if (_challenge != null)
            {
                _challenge.PrimaryClicked += () => CloseChallengeCard(_challengeYes);
                _challenge.SecondaryClicked += () => CloseChallengeCard(_challengeNo);
            }
            // A challenge link that launched the game, or arrives while it runs (echoghost://challenge/CODE).
            Application.deepLinkActivated += OpenChallengeLink;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OpenChallengeLink(Application.absoluteURL);
            _skins.Changed += () => { ApplyCosmetics(); Save(); };
            _settings.Changed += () => { ApplySettings(); Save(); };
            _settings.PrivacyClicked += () => { _settings.Hide(); _consentFromSettings = true; _consentView.Show(); };
            _settings.DevAdFailureToggled += () => _services.ToggleMockAdFailure();
            _settings.ServerCheckClicked += OnServerCheck;
            _settings.DeleteOnlineClicked += AskDeleteOnlineData;
            _settings.DailyDateClicked += CycleDailyDate;
            _settings.SeasonClicked += CycleSeason;
            _skins.BuyClicked += Buy;
            _skins.WatchAdClicked += WatchUnlockAd;
            _skins.TrialAdClicked += WatchTrialAd;
            _skins.RestoreClicked += () => _services.Iap.Restore(_ => _skins.Refresh());
            _consentView.Chosen += OnConsentChosen;
            _services.Entitled += OnEntitled;
        }

        void Start()
        {
            if (_serviceConfig == null) _serviceConfig = EchoServiceConfig.Load();
            _services.AdoptShare(RealServices.CreateShare(_serviceConfig));
            ApplyCosmetics();
            ApplySettings();
            _sessionStart = Time.realtimeSinceStartup;
            EnterTitle();

            // tech spec 19: no ad or analytics SDK starts before the player has chosen.
            if (_save.Consent.Decided)
            {
                StartRealServices();
                MaybeStartHowToPlay();
            }
            else
            {
                _title.Hide();
                _consentView.Show();
                SetState(GameState.Consent);
            }
        }

        void Update()
        {
            // Android's back button arrives as Escape.
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) OnBack();
            bool tapped = _input.Poll() && !(_input.Position is Vector2 p && IsOverButton(p));
            Held = _input.Held;
            TickFrame(Time.unscaledDeltaTime, tapped);
        }

        /// <summary>One frame of flow, sim and rendering. Update calls it; PlayMode tests call it directly.</summary>
        public void TickFrame(float dt, bool tapped)
        {
            tapped |= _injectedTap;
            _injectedTap = false;
            _stateTime += dt;
            TickDailyLabels(dt);

            if (_awakenPending && (State == GameState.Title || State == GameState.DeathScreen) && !ModalOpen) PlayAwakened();

            switch (State)
            {
                case GameState.Title:
                    if (tapped && !ModalOpen) StartRunning();
                    break;

                case GameState.Running:
                    if (tapped) _tapQueued = true;
                    StepSim(_driver.Advance(dt));
                    break;

                case GameState.Dying:
                    if (_stateTime >= DyingFreezeTicks / (float)FixedStepDriver.TickRate) EnterDeathScreen();
                    break;

                case GameState.DeathScreen:
                    if (_clearAt >= 0f && _stateTime >= _clearAt) PopGhosts();
                    if (_run.Options.Daily)
                    {
                        // Daily death screen: no offers, just tap to try again.
                        if (tapped && _stateTime >= DeathScreenTapDelay && !ModalOpen) Restart();
                        break;
                    }
                    // The next rewarded ad may finish loading after the screen appears: offer it once it does.
                    if ((_offersCheckIn -= dt) <= 0f)
                    {
                        _offersCheckIn = 0.5f;
                        if (!_run.Options.Duel && !_run.Options.Challenge && (!_run.Committed || _offerBanishTen)) RefreshOffers();
                    }
                    if (tapped && _stateTime >= DeathScreenTapDelay && !ModalOpen) Restart();
                    break;

                case GameState.Replay:
                    if (HowToPlaying) TickHowTo(dt, tapped);
                    else TickReplay(dt);
                    break;

                case GameState.Paused:
                    if (_resumeCountdown)
                    {
                        float left = ResumeCountdownSeconds - _stateTime;
                        _hud.SetCountdown(Mathf.CeilToInt(left));
                        if (left <= 0f) ResumeRunning();
                    }
                    break;
            }

            Render();
            if (State == GameState.Replay) CaptureClipFrame();
        }

        // tech spec 13: taps are queued and consumed on the next sim tick; several in one tick count as one.
        void StepSim(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                SnapshotPrevious();
                int nearMissesBefore = _run.NearMisses;
                if (ScriptedTaps != null) _tapQueued = ScriptedTaps(_run.Tick);
                _run.Step(_tapQueued, ScriptedHeld != null ? ScriptedHeld(_run.Tick) : Held);
                _tapQueued = false;
                PlayTickFeedback(_prevPlayer, _run.Player, _run.NearMisses - nearMissesBefore);
                PlayLifeFeedback();
                if (!_bountySeen && _run.BountyClaimed) OnBountyClaimed();
                // Variable jump: the first hop chunks arrive with tier 3 at 400 m, so the hint comes just before.
                if (!_save.Tutorial.SeenHopHint && _run.Distance >= HopHintMetres)
                {
                    _save.Tutorial.SeenHopHint = true;
                    _hud.QueueToast("TAP LIGHTLY FOR A SMALL HOP");
                }
                if (_run.DuelWon)
                {
                    EndDuel(won: true);
                    return;
                }
                if (_run.IsDead)
                {
                    EnterDying();
                    return;
                }
            }
        }

        // 1-UP: a toast when a life is earned; when one is spent the death plays in miniature and the run goes on.
        void PlayLifeFeedback()
        {
            if (_run.LivesEarned != _livesEarnedSeen)
            {
                _livesEarnedSeen = _run.LivesEarned;
                _hud.QueueToast("1-UP!");
                _sfx.GhostClear();
            }
            if (_run.LivesUsed != _livesUsedSeen)
            {
                _livesUsedSeen = _run.LivesUsed;
                float w = SimUnits.ToWorld(_cfg.PlayerHitboxW), h = SimUnits.ToWorld(_cfg.PlayerHitboxH);
                // Clamped so a fall into a pit still bursts where it can be seen.
                Vector2 at = new Vector2(SimUnits.ToWorld(_prevPlayer.X), Mathf.Max(-0.5f, SimUnits.ToWorld(_prevPlayer.Y)));
                _shards.Burst(new Vector2(at.x - w / 2f, at.y), new Vector2(w, h), Color.white, 24);
                _sfx.Death();
                _cameraRig.Shake(0.1f, 0.2f);
                if (_save.Settings.Haptics) Haptics.Pulse();
                _hud.QueueToast("SAVED BY 1-UP");
                SnapshotPrevious(); // no slide from where you fell to where you respawn
            }
            // The heart shows any life the run can still spend, an Extra 1-UP included.
            _hud.SetLives(_run.Lives + (_run.ExtraLifeHeld && !_run.ExtraLifeUsed ? 1 : 0));
            PlayPowerUpFeedback();
        }

        void PlayTickFeedback(in RunnerState before, in RunnerState after, int newNearMisses)
        {
            int jumpVy = _cfg.JumpVelocity - _cfg.Gravity;
            if (after.Alive && after.Vy == jumpVy && before.Vy != jumpVy)
            {
                _runnerView.OnJump();
                _sfx.Jump();
                if (!_save.Tutorial.SeenTapHint)
                {
                    _save.Tutorial.SeenTapHint = true;
                    _hud.SetTapHint(false);
                }
                // Teach the double jump once a session until the player has used it.
                if (!_save.Tutorial.UsedDoubleJump && !_doubleJumpHintShown)
                {
                    _doubleJumpHintShown = true;
                    _hud.QueueToast("TAP AGAIN IN MID-AIR");
                }
            }
            else if (after.Alive && after.AirJumpsLeft < before.AirJumpsLeft)
            {
                // Double jump: the same stretch and sound, plus a puff of dust under the feet.
                _runnerView.OnJump();
                _sfx.Jump();
                Vector2 feet = new Vector2(SimUnits.ToWorld(after.X), SimUnits.ToWorld(after.Y));
                _shards.Burst(feet + new Vector2(-0.25f, -0.06f), new Vector2(0.5f, 0.12f), new Color(1f, 1f, 1f, 0.8f), 8, 2.5f, 0.3f);
                _save.Tutorial.UsedDoubleJump = true;
            }
            else if (!before.Grounded && after.Grounded)
            {
                _runnerView.OnLand();
            }

            if (newNearMisses > 0)
            {
                _sfx.NearMiss();
                _hud.PopNearMiss();
            }
        }

        void Render()
        {
            if (_run == null) return;
            float alpha = State == GameState.Running || State == GameState.Replay ? _driver.Alpha : 1f;
            _runnerView.Render(_prevPlayer, _run.Player, alpha, _cfg);
            _cameraRig.Follow(_runnerView.Position.x);

            // Themed Biomes (value-adds doc): the scenery and the night follow the distance; units are metres.
            float metres = PreviewMetres ?? Mathf.Max(0f, _runnerView.Position.x);
            EnvironmentState env = Biomes.At(metres);
            if (State == GameState.Running && _activeChallenge != null && !_passedTheirGrave && _run.Distance > _activeChallenge.Distance)
            {
                _passedTheirGrave = true;
                _hud.QueueToast($"PAST {WhoUpper(_activeChallenge)}!");
            }
            if (State == GameState.Running && env.Biome > _announcedBiome)
            {
                _announcedBiome = env.Biome;
                _hud.QueueToast(Biomes.Name(env.Biome));
            }
            Color day = SeasonDay(PaletteSampler.Sample(_paletteStops, metres), metres);
            Color background = NightSky(day, env);
            _cameraRig.Background = background;
            float viewW = _cameraRig.Right - _cameraRig.Left;
            if (_background != null) _background.Render(_cameraRig.transform.position.x, viewW, _cameraRig.Bottom, _cameraRig.Height, background, env);
            RenderSeasonEffects();
            // Obstacles keep their contrast at night: against a dark sky they turn pale instead of dark.
            Color obstacles = Color.Lerp(PaletteSampler.Darker(day, 0.5f), Color.Lerp(day, Color.white, 0.3f), env.Night * 0.75f);
            _courseView.Render(_course, _cameraRig.Left, _cameraRig.Right, Mathf.Max(0, _run.Tick - 1),
                background, PaletteSampler.Darker(Color.Lerp(day, background, 0.5f), 0.7f), obstacles);
            if (_ambient != null)
                _ambient.Render(env, _runnerView.Position, _cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height,
                    _background != null ? _background.GraveyardLeft : 0f, _background != null ? _background.NearTileWidth : 0f,
                    State != GameState.Menu);
            // During the grave reveal the epitaph is on the headstone, so the small label under the grave stays hidden.
            if (_graves != null) _graves.Render(_runnerView.Position.x, PaletteSampler.Darker(background, 0.7f), background,
                State == GameState.Running || State == GameState.Dying || State == GameState.Paused || State == GameState.Replay
                || (State == GameState.DeathScreen && !_graveReveal));
            if (_graveReveal && State == GameState.DeathScreen) _deathScreen.TrackGrave(_cameraRig.ToViewport(_graves.FreshRect));

            bool previews = State == GameState.Running || State == GameState.Title || State == GameState.Paused || State == GameState.Menu;
            _ghostViews.SetBounty(_run.BountyIndex, _run.BountyClaimed);
            if (_twistOverlay != null) _twistOverlay.Render(_runnerView.Position);
            _ghostViews.Render(_run.Ghosts, _run.Tick, alpha, _cfg, _course, ShownNemesis,
                _runnerView.Position.x, previews);
            if (_copycatView != null) _copycatView.Render(_run.Copycat, alpha);
            _ghostViews.SetPhased(_run.PhaseActive);
            if (_ambient != null) _ambient.RenderShield(_run.ShieldArmed && (State == GameState.Running || State == GameState.Paused || State == GameState.Replay), _runnerView.Position);

            int ghostsOnScreen = _run.Ghosts.VisibleCount;
            // The Daily Haunt is scored on distance alone (Daily Haunt doc).
            _hud.SetScore(_run.Options.Daily ? _run.Distance : _run.Score);
            _hud.SetGhosts(ghostsOnScreen);
            _music.SetGhostsOnScreen(ghostsOnScreen);
        }

        void SnapshotPrevious()
        {
            _prevPlayer = _run.Player;
        }

        void PrepareRun()
        {
            RefreshSeason();
            ApplyCosmetics(); // an Elementals trial that has run out falls back to the free character here
            if (!CourseIs(_progress.Seed)) _course = new Course(_library, _progress.Seed);
            _run = RunRules.StartRun(_progress, _course, _cfg, RunOptionsNow());
            ApplyTwist(_run.Options.Twist);
            _bountySeen = false;
            _duelNemesis = null;
            SnapshotPrevious();
            _ghostViews.ResetPop();
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, _progress.Ghosts.Nemesis);
            _livesEarnedSeen = 0;
            _livesUsedSeen = 0;
            _hud.SetLives(0);
        }

        // The current course is the seed's own (a how-to-play course with the same seed is not).
        bool CourseIs(uint seed) => _course != null && !_course.Scripted && _course.Seed == seed;

        void SetState(GameState state)
        {
            State = state;
            _stateTime = 0f;
            int sleep = SleepTimeoutFor(state);
            if (Screen.sleepTimeout != sleep) Screen.sleepTimeout = sleep;
        }

        /// <summary>
        /// The screen stays on while something plays without the player touching it (a reel, a clip being made, the
        /// how-to-play clips: all the Replay state) and through a run; everywhere else the phone's own setting applies.
        /// </summary>
        public static int SleepTimeoutFor(GameState state) =>
            state == GameState.Replay || state == GameState.Running || state == GameState.Dying
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;

        void EnterTitle()
        {
            _activeChallenge = null;
            if (_ghostViews != null) _ghostViews.SetNameTag(null);
            PrepareRun();
            SetState(GameState.Title);
            HideDeathScreen();
            _hud.SetVisible(false);
            _hud.SetCountdown(0);
            ShowTitle();
        }

        void StartRunning(bool freshRun = true)
        {
            // Recorded at the start, not when the run is prepared: the title screen's Skins can still change it.
            if (freshRun) _run.SkinId = EquippedCharacterId();
            // An Elementals trial counts down by run; this run keeps the character even if it was the last one.
            if (freshRun) ElementalTrial.UseRun(_save.Cosmetics);
            if (freshRun)
            {
                _announcedBiome = Biome.Graveyard;
                _lanternUsed = false;
                _extraLifeTaken = false;
                _powerUpsUsed = 0;
                _shieldBreaksSeen = 0;
                _ghostViews.SetLantern(false);
                // The Extra 1-UP comes along whenever one is held; it is only used up if the run spends it.
                // No power-ups in the Daily Haunt (a user decision), so the Extra 1-UP stays at home too.
                if (!_run.Options.Daily && PowerUpInventory.Count(_save.PowerUps, PowerUp.ExtraLife) > 0) _run.GrantExtraLife();
                if (_ambient != null) _ambient.ResetRun();
            }
            if (freshRun)
                Log(AnalyticsEvents.RunStart, ("runIndex", _run.RunIndex), ("ghosts", _run.GhostsAtStart),
                    ("seed", (long)_progress.Seed), ("clears", _progress.Clears));
            if (freshRun && _run.Options.Twist != Twist.None)
                Log(AnalyticsEvents.TwistActive, ("twist", WeeklyTwists.Name(_run.Options.Twist)), ("week", _run.Options.TwistWeek));

            // The tap that starts a run is not a jump (game spec: tap to start, then the runner moves).
            _tapQueued = false;
            _clearAt = -1f;
            _driver.Reset();
            _title.Hide();
            HideDeathScreen();
            _hud.SetVisible(!HudHiddenForRecording);
            _hud.SetCountdown(0);
            _hud.SetTapHint(!_save.Tutorial.SeenTapHint);
            SetState(GameState.Running);
            RefreshPowerUpBar();
        }

        void EnterDying()
        {
            LogBountyClaim();
            SettlePowerUps();
            SnapshotPrevious();
            SetState(GameState.Dying);
            _diedAt = Time.unscaledTime;

            // tech spec 11: freeze, then the player shatters; haptic pulse on death only.
            float w = SimUnits.ToWorld(_cfg.PlayerHitboxW), h = SimUnits.ToWorld(_cfg.PlayerHitboxH);
            Vector2 pos = _runnerView.Position;
            _shards.Burst(new Vector2(pos.x - w / 2f, pos.y), new Vector2(w, h), Color.white, 40);
            _sfx.Death();
            _cameraRig.Shake(0.16f, 0.28f);
            // A run that is not a new best becomes a ghost, so it gets a grave where it fell.
            _graveReveal = _graves != null && !_run.Options.Duel && !_run.Options.Challenge && !_run.Options.Daily && (_run.Options.Twist != Twist.None || _run.Score <= _progress.Best);
            if (_graveReveal)
            {
                GhostRecord fallen = _run.ToGhostRecord();
                _graves.PlantFresh(_run.Player.X, _course, _run.RunIndex, Epitaphs.For(fallen));
                _graveName = Epitaphs.Title(fallen).ToUpperInvariant();
                _graveLine = Epitaphs.Line(fallen);
            }
            if (_save.Settings.Haptics) Haptics.Pulse();
            if (_run.Cause == DeathCause.Ghost && _run.Killer == _progress.Ghosts.Nemesis) _sfx.NemesisSting();
            _hud.SetTapHint(false);
        }

        void EnterDeathScreen()
        {
            if (_run.Options.Duel)
            {
                EndDuel(won: false);
                return;
            }
            if (_run.Options.Challenge)
            {
                EndChallenge();
                return;
            }
            if (_run.Options.Daily)
            {
                EndDaily();
                return;
            }
            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _deathScreen.SetScoreLabel("SCORE");
            // A Weekly Twist run is measured against this week's twist best and never clears the ghosts.
            bool twistRun = _run.Options.Twist != Twist.None;
            int best = RunRules.BestFor(_run, _progress);
            bool newBest = _run.Score > best;

            // Game spec first-time experience: one line on the first death to a ghost teaches the rule.
            string message = UsedPowerUpsLine();
            if (_run.Cause == DeathCause.Ghost && !_save.Tutorial.SeenGhostHint)
            {
                _save.Tutorial.SeenGhostHint = true;
                message = FirstGhostDeathMessage;
            }

            message = WithSeasonLine(message, _run, _progress.Ghosts.Nemesis);
            _deathScreen.Show(_run.Score, best, newBest, DeathScreenText.KillerLine(_run, _progress.Ghosts.Nemesis), message,
                twistRun ? "Week best" : "Best");
            OfferSaveReel(newBest && !twistRun);
            if (_graveReveal && _graves.FreshRect.width > 0f)
            {
                // The grave reveal: the camera zooms in on the fresh grave as the headstone grows out of it.
                _deathScreen.ShowHeadstone(_graveName, _graveLine, $"{_run.Distance} m", RevealDelay, RevealSeconds);
                _cameraRig.FocusOn(new Vector2(_graves.FreshRect.center.x, 0f), DeathScreenView.RevealZoom,
                    DeathScreenView.StoneBase, RevealDelay, RevealSeconds);
            }
            else _graveReveal = false;
            var (canContinue, canBanish, canBanishTen) = RefreshOffers();
            if (_features != null && _features.NemesisDuel && RunRules.CanDuel(_run, _progress))
                Log(AnalyticsEvents.DuelOffered, ("nemesisKills", _progress.Ghosts.Nemesis.Kills));
            if (canContinue) Log(AnalyticsEvents.ContinueOffered, ("score", _run.Score), ("best", best), ("runSeconds", RunSeconds));
            if (canBanishTen) Log(AnalyticsEvents.BanishTenOffered, ("ghostCount", _cfg.GhostCap), ("deathsSinceBest", _progress.RunsSinceBest + 1));
            else if (canBanish) Log(AnalyticsEvents.BanishOffered, ("ghostRunIndex", _run.Killer.RunIndex), ("cycleBanishes", _progress.CycleBanishes));

            // tech spec 11: on a new best, "Nemesis defeated" for 1 s if one existed, then every ghost pops.
            _clearAt = -1f;
            _photoMode = false;
            _deathScreen.SetPhoto(false);
            if (newBest && !twistRun && _run.Ghosts.VisibleCount > 0)
            {
                GhostRecord nemesis = _progress.Ghosts.Nemesis;
                // Ghost Photo Mode (skins addendum): every ghost stays frozen for a moment before the clear, and
                // Share becomes PHOTO, the main share moment.
                _deathScreen.SetBanner(nemesis != null ? $"Nemesis defeated: {NemesisNames.For(nemesis.RunIndex)}" : "NEW BEST! SNAP YOUR GHOSTS");
                _clearAt = PhotoSeconds;
                _photoMode = true;
                _deathScreen.SetPhoto(true);
            }
        }

        // Night darkens the sky towards a deep blue of the same palette; Space is darker still.
        static Color NightSky(Color day, in EnvironmentState env)
        {
            Color night = Color.Lerp(PaletteSampler.Darker(day, 0.42f), new Color(0.05f, 0.07f, 0.16f), 0.45f);
            Color sky = Color.Lerp(day, night, env.Night);
            return Color.Lerp(sky, new Color(0.03f, 0.02f, 0.07f), env.Space * 0.8f);
        }

        // ---- Power-ups (a user revision): earned by rewarded ads outside a run, used during one ----

        bool ModalOpen => ChallengeCardOpen || (_powerUpsView != null && _powerUpsView.IsOpen) || AwakenedOpen || ReelsOpen;
        bool AwakenedOpen => _awakened != null && _awakened.IsOpen;

        void OpenPowerUps()
        {
            if (_powerUpsView == null || State == GameState.Running) return;
            _powerUpsView.Show(_save.PowerUps, () => _services.Ads.IsRewardedReady(AdPlacements.PowerUp));
        }

        // Non-negotiable rule 3: rewarded ads only, and never during a run.
        void WatchPowerUpAd(PowerUp p)
        {
            if (State == GameState.Running || !PowerUpInventory.CanAdd(_save.PowerUps, p)) return;
            ShowRewardedAd(AdPlacements.PowerUp, rewarded =>
            {
                if (rewarded && PowerUpInventory.Add(_save.PowerUps, p))
                {
                    Log(AnalyticsEvents.PowerUpEarned, ("powerUp", p.ToString()), ("held", PowerUpInventory.Count(_save.PowerUps, p)));
                    CountAdWatched(equipUnlocked: false); // any rewarded ad counts towards Pumpkin; this also saves
                }
                if (_powerUpsView != null) _powerUpsView.Refresh();
            });
        }

        /// <summary>A floor button was tapped: one of each per run, taken from the held ones.</summary>
        void UsePowerUp(PowerUp p)
        {
            if (State != GameState.Running || _run.Options.Daily || PowerUpInventory.Count(_save.PowerUps, p) <= 0) return;
            bool used = false;
            if (p == PowerUp.Shield) used = _run.UseShield();
            else if (p == PowerUp.Phase) used = _run.UsePhase();
            else if (p == PowerUp.Lantern && !_lanternUsed)
            {
                _lanternUsed = true;
                _ghostViews.SetLantern(true);
                used = true;
            }
            if (!used) return;
            PowerUpInventory.Take(_save.PowerUps, p);
            _powerUpsUsed |= 1 << (int)p;
            _sfx.GhostClear();
            RefreshPowerUpBar();
        }

        void RefreshPowerUpBar()
        {
            PowerUpsSaveData held = _save.PowerUps;
            bool allowed = !_run.Options.Daily;
            _hud.SetPowerUps(allowed && held.Shield > 0 && !_run.ShieldUsed, allowed && held.Phase > 0 && !_run.PhaseUsed,
                allowed && held.Lantern > 0 && !_lanternUsed);
        }

        // The Shield breaking, and an Extra 1-UP being spent (taken from the held ones only then).
        void PlayPowerUpFeedback()
        {
            if (_run.ShieldBreaks > _shieldBreaksSeen)
            {
                _shieldBreaksSeen = _run.ShieldBreaks;
                Vector2 at = _runnerView.Position;
                _shards.Burst(at + new Vector2(-0.6f, 0f), new Vector2(1.2f, 1.2f), new Color(0.45f, 0.95f, 1f), 24, 4f, 0.6f);
                _sfx.GhostClear();
                _cameraRig.Shake(0.06f, 0.15f);
                _hud.QueueToast("SHIELD!");
            }
            if (_run.ExtraLifeUsed && !_extraLifeTaken)
            {
                _extraLifeTaken = true;
                PowerUpInventory.Take(_save.PowerUps, PowerUp.ExtraLife);
                _powerUpsUsed |= 1 << (int)PowerUp.ExtraLife;
            }
        }

        // Once the run is over: log what was used and save the held counts.
        void SettlePowerUps()
        {
            if (_powerUpsUsed == 0) return;
            foreach (PowerUp p in PowerUpRules.All)
                if ((_powerUpsUsed & (1 << (int)p)) != 0) Log(AnalyticsEvents.PowerUpUsed, ("powerUp", p.ToString()), ("distance", _run.Distance));
            Save();
        }

        // "Powered up: Ghost Shield, Phase" on the death screen, then cleared for the next run.
        string UsedPowerUpsLine()
        {
            if (_powerUpsUsed == 0) return "";
            var names = new System.Collections.Generic.List<string>();
            foreach (PowerUp p in PowerUpRules.All)
                if ((_powerUpsUsed & (1 << (int)p)) != 0) names.Add(PowerUpRules.Name(p));
            _powerUpsUsed = 0;
            return "Powered up: " + string.Join(", ", names);
        }

        // ---- Ghost Challenges (value-adds doc) ----

        bool ChallengeCardOpen => _challenge != null && _challenge.IsOpen;

        /// <summary>Asks for the name on challenges ("Jamie's Ghost"), then carries on with <paramref name="then"/>.</summary>
        void AskName(Action then)
        {
            if (_challenge == null) return;
            _challenge.AskName("YOUR NAME", "Friends will race your ghost by this name.", _save.Settings.PlayerName);
            _challengeYes = () =>
            {
                string name = CleanName(_challenge.InputText);
                if (name.Length == 0) return;
                _save.Settings.PlayerName = name;
                Save();
                _settings.RefreshLabels();
                then?.Invoke();
            };
            _challengeNo = null;
        }

        // A name on the blocklist quietly becomes ChallengeNames.Default (a user choice).
        static string CleanName(string raw) => ChallengeNames.Clean(raw);

        void Ask(string title, string body, string yes, string no, Action onYes, Action onNo)
        {
            if (_challenge == null) return;
            _challengeYes = onYes;
            _challengeNo = onNo;
            _challenge.Show(title, body, yes, no);
        }

        void CloseChallengeCard(Action then)
        {
            _challenge.Hide();
            _challengeYes = _challengeNo = null;
            then?.Invoke();
        }

        void OnDestroy() => Application.deepLinkActivated -= OpenChallengeLink;

        void HideDeathScreen()
        {
            _deathScreen.Hide();
            _cameraRig.ClearFocus();
            _graveReveal = false;
        }

        // The headstone lands: a thud, a puff of earth at its foot and a small shake.
        void OnHeadstoneLanded()
        {
            _sfx.GraveThud();
            _cameraRig.Shake(0.03f, 0.2f);
            Rect grave = _graves != null ? _graves.FreshRect : Rect.zero;
            if (grave.width > 0f) _shards.Burst(new Vector2(grave.center.x - 0.6f, 0f), new Vector2(1.2f, 0.1f), new Color(0.45f, 0.36f, 0.28f), 14, 1.6f, 0.5f);
        }

        void PopGhosts()
        {
            _clearAt = -1f;
            _photoMode = false;
            _deathScreen.SetPhoto(false);
            _deathScreen.SetBanner(null);
            if (_ghostViews.PopAll(_shards) > 0)
            {
                if (_graves != null) _graves.CrumbleAll();
                _flash.Flash();
                _sfx.GhostClear();
            }
        }

        // Balance addendum: at most two ad offers, Continue first, then Banish 10 (at a full graveyard) or Banish.
        (bool canContinue, bool canBanish, bool canBanishTen) RefreshOffers()
        {
            var ads = _services.Ads;
            bool canContinue = RunRules.CanContinue(_run) && ads.IsRewardedReady(AdPlacements.Continue);
            bool canBanishTen = RunRules.CanBanishTen(_run, _progress, _cfg) && ads.IsRewardedReady(AdPlacements.BanishTen);
            bool canBanish = !canBanishTen && RunRules.CanBanish(_run, _progress) && ads.IsRewardedReady(AdPlacements.BanishGhost);
            // Nemesis Duel takes the single Banish's place when offered: free, skill-based, and the Nemesis is never
            // banishable. Banish 10 keeps its place: it is the way out of a full graveyard.
            bool canDuel = !canBanishTen && _features != null && _features.NemesisDuel && RunRules.CanDuel(_run, _progress);
            if (canDuel) canBanish = false;
            _offerBanishTen = canBanishTen;
            _deathScreen.SetOffers(canContinue, canBanish || canBanishTen, canBanishTen);
            _deathScreen.SetDuel(canDuel ? NemesisNames.For(_progress.Ghosts.Nemesis.RunIndex) : null);
            _deathScreen.SetChallenge(ChallengeRuns.CanSend(_run) ? "Challenge" : null);
            return (canContinue, canBanish, canBanishTen);
        }

        void Restart()
        {
            if (_activeChallenge != null)
            {
                // Tap to retry races the same challenge again.
                StartChallenge(_activeChallenge);
                return;
            }
            if (_run.Options.Daily)
            {
                PlayDaily();
                return;
            }
            bool quick = _diedAt >= 0f && Time.unscaledTime - _diedAt <= QuickRestartSeconds;
            if (!_run.Committed) CommitAndSave();
            if (quick)
            {
                foreach (string id in AchievementRules.OnQuickRestart(_progress)) ToastAchievement(id);
                Save();
            }
            // The 30th achievement is a payoff, not something to tap straight past: back to the title for it.
            if (_awakenPending && _awakened != null)
            {
                EnterTitle();
                return;
            }
            PrepareRun();
            StartRunning();
        }

        // ELEMENTS AWAKENED (a user revision): the four Elementals, in catalog order, the moment all 30 are earned.
        void PlayAwakened()
        {
            _awakenPending = false;
            if (_awakened == null || _catalog == null) return;
            var figures = new List<Sprite>();
            var colors = new List<Color>();
            foreach (CosmeticItem item in _catalog.Items)
            {
                if (!item.AllAchievements || item.RunFrames.Length == 0) continue;
                figures.Add(item.RunFrames[0]);
                colors.Add(ElementalTrial.ElementColor(item.Id));
            }
            _awakened.Play(figures.ToArray(), colors.ToArray());
            _sfx.GhostClear();
        }

        void ToastAchievement(string id) => _hud.QueueToast("Achievement: " + AchievementCatalog.Find(id).Title);

        // ---- value-adds (each behind its FeatureFlags switch) ----

        const string HalloweenCharacter = "char_pumpkin";
        static bool IsHalloweenWeek(DateTime utc) => utc.Month == 10 && utc.Day >= 25;

        static int WeekNow() => WeeklyTwists.WeekOf((int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalDays);

        // Weekly Twists start after the first runs, so a new player learns the classic game and sets a real best first.
        const int TwistsAfterRuns = 10;

        // This week's twist whether or not the player is playing it (for the title banner).
        Twist ThisWeeksTwist()
        {
            if (_features == null || !_features.WeeklyTwists) return Twist.None;
            if (!_features.OverrideTwist && _progress.Stats.TotalRuns < TwistsAfterRuns) return Twist.None;
            return _features.OverrideTwist ? _features.Twist : WeeklyTwists.ForWeek(WeekNow());
        }

        RunOptions RunOptionsNow() => new RunOptions
        {
            Bounties = _features != null && _features.GhostBounties,
            Twist = _save.Settings.PlayTwist ? ThisWeeksTwist() : Twist.None,
            TwistWeek = WeekNow(),
        };

        void ApplyTwist(Twist twist)
        {
            _ghostViews.SetTwist(twist);
            _cameraRig.Mirror = twist == Twist.Mirror;
            if (_twistOverlay != null) _twistOverlay.SetTwist(twist);
        }

        void ShowTitle()
        {
            RefreshSeason();
            _title.Show(_progress.Best);
            _title.SetExitVisible(CanQuit);
            _title.SetDevServer(!_recordingMode && _serviceConfig != null && _serviceConfig.ShowsDevServerTag);
            _title.SetAdButtons(!_recordingMode);
            RefreshDailyButton();
            if (_pendingChallenge != null) PresentPendingChallenge();
            ProcessPendingLink();
            Twist week = ThisWeeksTwist();
            if (week == Twist.None) _title.SetTwist(null);
            else if (_save.Settings.PlayTwist)
                _title.SetTwist($"THIS WEEK: {WeeklyTwists.Name(week).ToUpperInvariant()}\n<size=60%>{WeeklyTwists.Description(week)} · tap for Classic</size>");
            else
                _title.SetTwist($"CLASSIC\n<size=60%>Tap to play this week's {WeeklyTwists.Name(week)}</size>");
        }

        // Classic toggle (value-adds doc): a twist must never block someone chasing their best.
        void OnTwistClicked()
        {
            if (State != GameState.Title) return;
            _save.Settings.PlayTwist = !_save.Settings.PlayTwist;
            Save();
            PrepareRun();
            ShowTitle();
        }

        // Ghost Bounties: the bounty ghost was near-missed.
        void OnBountyClaimed()
        {
            _bountySeen = true;
            _hud.QueueToast("BOUNTY +50");
            _sfx.GhostClear();
            if (_save.Settings.Haptics) Haptics.Pulse();
            ref readonly GhostReplay g = ref _run.Ghosts.ReplayAt(_run.BountyIndex);
            var at = new Vector2(SimUnits.ToWorld(g.State.X), SimUnits.ToWorld(g.State.Y));
            _shards.Burst(at + new Vector2(-0.3f, 0f), new Vector2(0.6f, 0.8f), new Color(1f, 0.82f, 0.25f), 18, 4f, 0.6f);
            // Logged when the run ends: building an analytics event allocates, and this happens mid-run (tech spec 18).
            _bountyToLog = true;
        }

        void LogBountyClaim()
        {
            if (!_bountyToLog) return;
            _bountyToLog = false;
            Log(AnalyticsEvents.BountyClaimed, ("ghostRunIndex", _run.Ghosts.RecordAt(_run.BountyIndex).RunIndex), ("score", _run.Score));
        }

        // Rogues' Gallery: a defeated Nemesis is hung with its course, so it can be replayed later.
        void AddToGallery(GhostRecord nemesis, uint seed)
        {
            if (_features != null && !_features.RoguesGallery) return;
            string today = DateTime.UtcNow.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            RoguesGallery.Add(_progress.Gallery, RoguesGallery.Entry(nemesis, seed, _contentVersion, today));
        }

        // Nemesis Duel: the run just ended is committed as usual, then the Nemesis alone on the same course.
        void OnDuel()
        {
            if (State != GameState.DeathScreen || _features == null || !_features.NemesisDuel) return;
            if (!RunRules.CanDuel(_run, _progress)) return;
            if (!_run.Committed) CommitAndSave();
            _duelNemesis = _progress.Ghosts.Nemesis;
            if (_duelNemesis == null) return; // that commit set a new best and cleared it
            if (!CourseIs(_progress.Seed)) _course = new Course(_library, _progress.Seed);
            _run = RunRules.StartDuel(_progress, _course, _cfg);
            Save();
            SnapshotPrevious();
            ApplyTwist(Twist.None);
            _ghostViews.ResetPop();
            if (_graves != null) _graves.Plant(_run.Ghosts, _course, _cfg, _duelNemesis);
            _hud.SetLives(0);
            _hud.QueueToast($"DUEL: GET PAST WHERE {NemesisNames.For(_duelNemesis.RunIndex).ToUpperInvariant()} FELL");
            StartRunning(freshRun: false);
        }

        void EndDuel(bool won)
        {
            string name = NemesisNames.For(_duelNemesis.RunIndex);
            RunRules.CommitDuel(_progress, _run, _duelNemesis);
            if (won) AddToGallery(_duelNemesis, _course.Seed);
            Log(AnalyticsEvents.DuelResult, ("won", won), ("nemesisKills", _duelNemesis.Kills));
            Save();

            SetState(GameState.DeathScreen);
            _hud.SetVisible(false);
            _deathScreen.Show(_run.Score, _progress.Best, false, "", won ? $"{name} is banished for good" : $"{name} wins the duel (+1 kill)");
            _deathScreen.SetOffers(false, false);
            _deathScreen.SetDuel(null);
            _deathScreen.SetChallenge(null);
            if (won)
            {
                PopGhosts();
                _deathScreen.SetBanner($"DUEL WON: {name}");
            }
        }

        // tech spec 7 commit order: score, best or ghost, then achievements, then save.
        void CommitAndSave()
        {
            // Challenge and daily runs never touch progress; they settle on their own death screens. This also keeps a
            // backgrounded app from committing one during the death freeze, before that screen has run.
            if (_run.Options.Challenge || _run.Options.Daily) return;
            GhostRecord nemesisBefore = _progress.Ghosts.Nemesis;
            CommitResult result = RunRules.Commit(_progress, _run, NewSeed(), _cfg);
            if (result.DefeatedNemesis != null) AddToGallery(result.DefeatedNemesis, _course.Seed);
            _sessionRuns++;
            Log(AnalyticsEvents.RunEnd, ("score", result.Score), ("distance", _run.Distance), ("nearMisses", _run.NearMisses),
                ("ticks", _run.DeathTick), ("cause", CauseName(_run.Cause)),
                ("killerRunIndex", _run.Killer?.RunIndex ?? 0), ("killerIsNemesis", _run.Killer != null && _run.Killer == nemesisBefore));
            if (result.NewBest)
                Log(AnalyticsEvents.NewBest, ("score", result.Score), ("ghostsCleared", result.GhostsCleared), ("nemesisDefeated", result.NemesisDefeated));

            foreach (string id in AchievementRules.OnRunEnded(_run, result, _progress, _sessionRuns)) ToastAchievement(id);
            if (_catalog != null)
            {
                foreach (CosmeticItem item in CosmeticUnlocks.Earn(_catalog, _progress.Best, _save.Cosmetics.Owned))
                    _hud.QueueToast("Unlocked: " + item.DisplayName);
                foreach (CosmeticItem item in CosmeticUnlocks.EarnByStats(_catalog, _progress.Stats, _save.Cosmetics.Owned))
                    _hud.QueueToast("Unlocked: " + item.DisplayName);
                // Every achievement earned: the Elementals are yours for good, with their own reward moment.
                if (ElementalTrial.EarnIfComplete(_catalog, AchievementCatalog.SinglePlayerEarned(_progress.Achievements), _save.Cosmetics.Owned).Count > 0)
                    _awakenPending = true;
            }
            Save();
        }

        void Save()
        {
            try
            {
                SaveMapper.Write(_progress, _save, _contentVersion);
                _saves.Save(_save);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Echo] save failed: {e}");
            }
        }

        // The character the next run wears, recorded on its ghost (skins addendum).
        string EquippedCharacterId()
        {
            if (_catalog == null) return CosmeticsSaveData.DefaultCharacter;
            CosmeticItem character = _catalog.Resolve(_save.Cosmetics.Equipped.Character, CosmeticType.Character);
            if (character == null || !ElementalTrial.Available(character, _progress.Best, _save.Cosmetics))
                character = _catalog.Resolve(null, CosmeticType.Character);
            return character != null ? character.Id : CosmeticsSaveData.DefaultCharacter;
        }

        /// <param name="characterId">A reel playing back wears the skin its run wore, owned or not.</param>
        void ApplyCosmetics(string characterId = null)
        {
            if (_catalog == null) return;
            _ghostViews.SetSkinLibrary(_catalog);
            var e = _save.Cosmetics.Equipped;
            CosmeticItem runner = _catalog.Resolve(e.Runner, CosmeticType.Runner);
            CosmeticItem character = _catalog.Resolve(characterId ?? e.Character, CosmeticType.Character);
            // A character that is not owned (still locked, an Elemental whose trial ran out, or from an old save) falls
            // back to the free one.
            if (characterId == null && character != null && !ElementalTrial.Available(character, _progress.Best, _save.Cosmetics))
                character = _catalog.Resolve(null, CosmeticType.Character);
            CosmeticItem trail = _catalog.Resolve(e.Trail, CosmeticType.Trail);
            CosmeticItem palette = _catalog.Resolve(e.Palette, CosmeticType.Palette);
            if (character != null)
            {
                Color tint = character.Tintable && runner != null ? runner.Color : Color.white;
                _runnerView.SetCharacter(character.RunFrames, character.JumpFrame, tint,
                    character.ScarfColor, character.ScarfWidth, character.ScarfAnchor);
                _ghostViews.SetFrames(character.GhostFrames, character.GhostJumpFrame);
                if (_copycatView != null) _copycatView.SetCharacter(character.RunFrames, character.JumpFrame);
                if (_ambient != null) _ambient.SetReaction(character.Reaction);
                if (_background != null) _background.SetBanners(character.Reaction == CharacterReaction.RoyalBanners);
                if (characterId == null && _dailyLobby != null) _dailyLobby.SetRunner(character.RunFrames, character.JumpFrame, tint);
            }
            if (trail != null) _ghostViews.SetTrailColor(trail.Color);
            if (palette != null) _paletteStops = palette.PaletteStops;
        }

        void ApplySettings()
        {
            _cameraRig.SetGlow(_save.Settings.Glow);
            _music.SetEnabled(_save.Settings.Music);
            _sfx.SetEnabled(_save.Settings.Sfx);
        }

        void OpenMenu(Action show)
        {
            if (State != GameState.Title) return;
            _title.Hide();
            show();
            SetState(GameState.Menu);
        }

        void CloseMenu()
        {
            _skins.Hide();
            _stats.Hide();
            _settings.Hide();
            if (_dailyLobby != null) _dailyLobby.Hide();
            if (_reels != null) _reels.Hide();
            SetState(GameState.Title);
            ShowTitle();
        }

        // game spec monetisation: ad offers exist only on the death screen, never during a run.
        void OnContinue()
        {
            if (State != GameState.DeathScreen || !RunRules.CanContinue(_run)) return;
            ShowRewardedAd(AdPlacements.Continue, rewarded =>
            {
                if (!rewarded || State != GameState.DeathScreen || !RunRules.CanContinue(_run)) return;
                Log(AnalyticsEvents.ContinueUsed, ("score", _run.Score), ("best", _progress.Best), ("runSeconds", RunSeconds));
                CountAdWatched(equipUnlocked: false);
                _run.Revive();
                SnapshotPrevious();
                _ghostViews.ResetPop();
                if (_graves != null) _graves.ClearFresh();
                StartRunning(freshRun: false);
            });
        }

        bool _offerBanishTen;   // the banish button is showing Banish 10

        int RunSeconds => _run.Tick / FixedStepDriver.TickRate;

        void OnBanish()
        {
            if (_run != null && _run.Options.Daily)
            {
                OnDailyBanish();
                return;
            }
            if (_offerBanishTen)
            {
                OnBanishTen();
                return;
            }
            if (State != GameState.DeathScreen || !RunRules.CanBanish(_run, _progress)) return;
            ShowRewardedAd(AdPlacements.BanishGhost, rewarded =>
            {
                if (!rewarded || State != GameState.DeathScreen || !RunRules.CanBanish(_run, _progress)) return;
                int banished = _run.Killer.RunIndex;
                if (_graves != null) _graves.Crumble(_run.Killer);
                RunRules.Banish(_run, _progress);
                Log(AnalyticsEvents.BanishUsed, ("ghostRunIndex", banished), ("cycleBanishes", _progress.CycleBanishes));
                CountAdWatched(equipUnlocked: false);
                CommitAndSave();
                _deathScreen.SetMessage($"Run #{banished} banished");
                RefreshOffers();
            });
        }

        // Balance addendum: the run is committed first (its ghost added, the cap applied), then the 10 deadliest
        // non-Nemesis ghosts go at once with the ghost-clear effect. A failed ad removes nothing.
        void OnBanishTen()
        {
            if (State != GameState.DeathScreen || !RunRules.CanBanishTen(_run, _progress, _cfg)) return;
            ShowRewardedAd(AdPlacements.BanishTen, rewarded =>
            {
                if (!rewarded || State != GameState.DeathScreen || !RunRules.CanBanishTen(_run, _progress, _cfg)) return;
                if (!_run.Committed) CommitAndSave();
                bool nemesisPresent = _progress.Ghosts.Nemesis != null;
                var removed = RunRules.BanishTen(_run, _progress, _cfg);
                int kills = 0;
                foreach (GhostRecord g in removed)
                {
                    kills += g.Kills;
                    if (_graves != null) _graves.Crumble(g);
                }
                _flash.Flash();
                _sfx.GhostClear();
                _cameraRig.Shake(0.08f, 0.2f);
                Log(AnalyticsEvents.BanishTenUsed, ("killsRemoved", kills), ("nemesisPresent", nemesisPresent));
                CountAdWatched(equipUnlocked: false);
                Save();
                _deathScreen.SetMessage("10 ghosts banished");
                _hud.QueueToast("10 GHOSTS BANISHED");
                RefreshOffers();
            });
        }

        /// <summary>Back to the title from the death screen; the pending run commits exactly as a retry would.</summary>
        void OnMenu()
        {
            if (State != GameState.DeathScreen) return;
            if (!_run.Committed) CommitAndSave();
            EnterTitle();
        }

        void OnShare()
        {
            if (_photoMode)
            {
                StartCoroutine(TakeGhostPhoto());
                return;
            }
            string nemesis = _progress.Ghosts.Nemesis != null
                ? " Nemesis: " + NemesisNames.For(_progress.Ghosts.Nemesis.RunIndex) + "."
                : "";
            _services.Share.ShareScreenshot($"I scored {_run.Score} with {_run.GhostsAtStart} ghosts.{nemesis}");
        }

        // Hides the UI for the frame the share service captures, so the photo is just the frozen ghost crowd,
        // then lets the clear happen.
        IEnumerator TakeGhostPhoto()
        {
            _photoMode = false;
            var canvas = _deathScreen.GetComponentInParent<Canvas>().rootCanvas;
            canvas.enabled = false;
            _services.Share.ShareScreenshot($"New best: {_run.Score}, past {_run.GhostsAtStart} of my own ghosts. #Echo");
            yield return new WaitForEndOfFrame();
            yield return null;
            canvas.enabled = true;
            if (State == GameState.DeathScreen) PopGhosts();
        }

        void ResumeRunning()
        {
            _resumeCountdown = false;
            _hud.SetCountdown(0);
            _driver.Reset();
            SetState(GameState.Running);
        }

        // A rewarded ad is on screen. It takes the app into the background, which must not commit the pending run
        // (device testing: Continue played its ad, then found the run already committed and did nothing).
        bool _adShowing;
        float _adStartedAt;

        // Two minutes at most, in case an SDK never reports back: after that a pause is the player leaving again.
        bool AdOnScreen => _adShowing && Time.realtimeSinceStartup - _adStartedAt < 120f;

        void ShowRewardedAd(string placement, Action<bool> onComplete)
        {
            _adShowing = true;
            _adStartedAt = Time.realtimeSinceStartup;
            _services.Ads.ShowRewarded(placement, rewarded =>
            {
                _adShowing = false;
                onComplete?.Invoke(rewarded);
            });
        }

        void OnApplicationPause(bool paused)
        {
            if (AdOnScreen) return; // the ad's own pause and resume, not the player leaving
            if (paused)
            {
                LogSessionEnd();
                if (State == GameState.Running)
                {
                    _resumeCountdown = false;
                    SetState(GameState.Paused);
                }
                // tech spec 7: backgrounding the app commits a pending run.
                else if ((State == GameState.Dying || State == GameState.DeathScreen) && !_run.Committed)
                {
                    CommitAndSave();
                }
            }
            else
            {
                LogSessionStart();
            }

            if (!paused && State == GameState.Paused)
            {
                _resumeCountdown = true;
                _stateTime = 0f;
            }
        }

        void OnApplicationQuit()
        {
            // A run cut off mid-run is discarded (tech spec 7); a pending one is committed.
            if ((State == GameState.Dying || State == GameState.DeathScreen) && _run != null && !_run.Committed)
                CommitAndSave();
            LogSessionEnd();
        }

        // ---- services, consent, store ----

        void StartRealServices()
        {
            if (RealServices.Enabled(_serviceConfig))
            {
                _services.StartupNote = "Starting real services...";
                _ = StartRealServicesAsync();
            }
            else
            {
                _services.StartupNote = _serviceConfig == null ? "No service config in this build: mock services."
                    : Application.isEditor ? "Editor: mock services (Real Services In Editor is off)."
                    : "Not a phone build: mock services.";
                LogSessionStart();
                ServicesSettled();
            }
        }

        async Task StartRealServicesAsync()
        {
            try
            {
                RealServices real = await RealServices.CreateAsync(_serviceConfig, _save.Consent.PersonalisedAds, _save.Consent.Analytics);
                if (this == null) return;
                _services.Adopt(real);
                _services.StartupNote = real.Note;
                LogSessionStart();
                ServicesSettled();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Echo] real services failed to start, staying on mocks: {e}");
                _services.StartupNote = $"Real services failed to start ({e.Message}): mocks.";
                LogSessionStart();
                ServicesSettled();
            }
        }

        // Server calls (2.0) wait for this: before it, the game is still on mocks, or before the consent choice.
        void ServicesSettled()
        {
            _servicesSettled = true;
            WireBackend();
            ProcessPendingLink();
        }

        void OnConsentChosen(bool personalisedAds, bool analytics)
        {
            _save.Consent.Version = ConsentSaveData.CurrentVersion;
            _save.Consent.PersonalisedAds = personalisedAds;
            _save.Consent.Analytics = analytics;
            Save();
            _consentView.Hide();

            if (_consentFromSettings)
            {
                _consentFromSettings = false;
                _services.ApplyConsent(personalisedAds, analytics);
                ShowSettings();
                return;
            }

            SetState(GameState.Title);
            ShowTitle();
            StartRealServices();
            MaybeStartHowToPlay();
        }

        void ShowSkins() => _skins.Show(_catalog, _save.Cosmetics, _progress.Best,
            id => _services.Iap.LocalizedPrice(id), id => _services.Iap.Owns(id),
            () => _services.Ads.IsRewardedReady(AdPlacements.UnlockCharacter), _progress.Stats, AchievementCatalog.SinglePlayerEarned(_progress.Achievements));

        // Ad-unlocked characters (user revision): a rewarded ad from the Skins screen, never during a run.
        void WatchUnlockAd()
        {
            if (State != GameState.Menu) return;
            ShowRewardedAd(AdPlacements.UnlockCharacter, rewarded =>
            {
                if (rewarded)
                {
                    CountAdWatched(equipUnlocked: true);
                    Log(AnalyticsEvents.UnlockAdWatched, ("adsWatched", _save.Cosmetics.AdsWatched));
                }
                _skins.Refresh();
            });
        }

        // Every rewarded ad watched in full (Continue and Banish too) counts towards the ad-unlocked characters.
        // Unlocked from the Skins screen they are put on at once; otherwise a toast says so.
        // The Elementals trial: 5 rewarded ads lend the pack for 5 runs (not while a trial is running).
        void WatchTrialAd()
        {
            if (State != GameState.Menu || ElementalTrial.Active(_save.Cosmetics)) return;
            ShowRewardedAd(AdPlacements.UnlockCharacter, rewarded =>
            {
                if (rewarded)
                {
                    bool started = ElementalTrial.CountAd(_save.Cosmetics);
                    Log(AnalyticsEvents.UnlockAdWatched, ("elementalTrialAds", _save.Cosmetics.TrialAds), ("trialStarted", started));
                    CountAdWatched(equipUnlocked: false); // also counts towards Pumpkin; saves
                }
                _skins.Refresh();
            });
        }

        void CountAdWatched(bool equipUnlocked)
        {
            _save.Cosmetics.AdsWatched++;
            if (_catalog != null)
                foreach (CosmeticItem item in CosmeticUnlocks.EarnByAds(_catalog, _save.Cosmetics.AdsWatched, _save.Cosmetics.Owned))
                {
                    // Only a character is worn straight away; colour pack items are announced.
                    if (!equipUnlocked || item.Type != CosmeticType.Character)
                    {
                        _hud.QueueToast($"{item.DisplayName.ToUpperInvariant()} UNLOCKED");
                        continue;
                    }
                    _save.Cosmetics.Equipped.Character = item.Id;
                    ApplyCosmetics();
                }
            Save();
        }

        void ShowSettings()
        {
            _settings.Show(_save.Settings, Debug.isDebugBuild && _services.UsingMockAds, Debug.isDebugBuild);
            _settings.SetSeason(SeasonDevSetting, SeasonLabel());
            // Development builds only: what the ad and store SDKs are doing, readable on the phone itself.
            _settings.SetStatus(Debug.isDebugBuild ? _services.StatusReport : null);
            ApplyRecordingMode();
        }

        // Release builds: five taps on the Settings heading show the services status, for testing store builds.
        void OnSettingsStatusRequested()
        {
            _settings.SetStatus(_services.StatusReport);
            _recordingOffered = true;   // release builds: the same five taps offer recording mode
            ApplyRecordingMode();
        }

        void Buy(string productId)
        {
            _services.Iap.Purchase(productId, ok =>
            {
                if (ok) Log(AnalyticsEvents.IapPurchase, ("productId", productId));
                _skins.Refresh();
            });
        }

        /// <summary>A product (bought, restored or deferred) unlocks every cosmetic that names it, bundles included.</summary>
        void OnEntitled(string productId)
        {
            if (_catalog == null) return;
            if (CosmeticUnlocks.Grant(_catalog, productId, _save.Cosmetics.Owned)) Save();
            _skins.Refresh();
        }

        // ---- analytics (tech spec 15) ----

        void Log(string evt, params (string key, object value)[] props)
        {
            var dict = new Dictionary<string, object>(props.Length);
            foreach (var (key, value) in props) dict[key] = value;
            _services.Analytics.Log(evt, dict);
        }

        void LogSessionStart()
        {
            _sessionStart = Time.realtimeSinceStartup;
            _sessionRuns = 0;
            Log(AnalyticsEvents.SessionStart, ("runs", _progress.Stats.TotalRuns), ("duration", 0));
        }

        void LogSessionEnd() =>
            Log(AnalyticsEvents.SessionEnd, ("runs", _sessionRuns), ("duration", Mathf.RoundToInt(Time.realtimeSinceStartup - _sessionStart)));

        static string CauseName(DeathCause cause) =>
            cause == DeathCause.Ghost ? "ghost" : cause == DeathCause.Copycat ? "copycat" : cause == DeathCause.Fall ? "fall" : "hazard";

        uint NewSeed()
        {
            uint seed;
            do seed = (uint)_seedSource.Next() ^ ((uint)_seedSource.Next() << 16);
            while (seed == 0);
            return seed;
        }

        bool IsOverButton(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            _pointer ??= new PointerEventData(eventSystem);
            _pointer.position = screenPosition;
            _uiHits.Clear();
            eventSystem.RaycastAll(_pointer, _uiHits);
            foreach (var hit in _uiHits)
                if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }
    }
}
