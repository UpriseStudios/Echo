using Echo.Core.Achievements;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Services.Analytics;
using Echo.Game.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Game.Flow
{
    /// <summary>
    /// World: Time Run (Assets/Docs/World Time Run.md, phase 1, the era system): a world whose theme changes with the run's
    /// distance, one era every 300 m (TimeEras; five since the art pass), read from the simulation so replays and challenges see the same era in the
    /// same place. Crossing into a later era plays the time rift behind the gameplay, a whoosh and the era's label in the
    /// corner. All of it is looks: the era themes go through the same ApplySeason as any world.
    /// Phase 3, art styles: each era is drawn in a different era of video games (8-bit, 16-bit, 32-bit), and the Far
    /// Future breaks out of pixel art (ApplyArtStyle): full resolution, 2D lights, a stronger bloom, and glow, rim light
    /// and trails on the pixel sprites. Full or reduced effects (EffectsQuality) decide the bloom and the lights.
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] TimeRiftView _timeRift;
        [SerializeField] EraPreloadView _eraPreload;
        [SerializeField] LowResView _lowRes;
        bool _halfResolution;

        /// <summary>The backdrop and floor are drawn at half resolution now (Dino Land), for tests.</summary>
        public bool HalfResolutionShown => _halfResolution && _lowRes != null && _lowRes.On;
        [SerializeField] FutureLightsView _futureLights;
        [SerializeField] FutureFxView _futureFx;
        [SerializeField] Far3DView _far3D;
        [SerializeField] Material _spriteUnlit;
        [SerializeField] Material _spriteLit;
        bool _scene3D, _gameplayLit;

        /// <summary>The Far Far Future's 3D city is showing (tests).</summary>
        public Far3DView.Mode Scene3DShown => _far3D != null ? _far3D.Showing : Far3DView.Mode.Off;
        [Tooltip("The Far Future's stronger bloom, weighted in with full effects.")]
        [SerializeField] Volume _futureVolume;

        bool _highDefinition;
        bool? _weakDevice;
        readonly FrameWatch _frameWatch = new FrameWatch();

        bool WeakDevice => _weakDevice ??= EffectsQuality.ThisDeviceIsWeak();

        /// <summary>Full effects now: the player's choice, else this phone's (World Time Run doc, phase 3).</summary>
        // The benchmark's reduced passes are reduced whatever the player chose (art fix phase 3: a phone with effects chosen
        // in Settings measured full effects twice and reported it as reduced).
        public bool FullEffects => !_benchmarkReduced && (_save == null || EffectsQuality.Full(_save.Settings, WeakDevice || _sessionReduced));

        // Art fix phase 1: a slow Far Future turns effects down for this session only, judged on the game's own frame time.
        bool _sessionReduced;
        float _frameDt = 1f / 60f;

        /// <summary>The Far Future's high definition is showing (tests).</summary>
        public bool HighDefinitionShown => _highDefinition;

        /// <summary>Tests: count this as a weaker phone (null: ask the device).</summary>
        public bool? WeakDeviceOverride
        {
            set
            {
                _weakDevice = value;
                if (_save != null) ApplyEffects();
            }
        }

        SeasonTheme _timeRun;
        int _eraShown = -1;

        int? _forceEra;
        bool _previewTimeRun;
        readonly SeasonTheme[] _oneEra = new SeasonTheme[1];

        /// <summary>Development builds and tests: show this era whatever the distance (null: by distance), everywhere.</summary>
        public int? ForceEra
        {
            get => _forceEra;
            set
            {
                _forceEra = value;
                if (_seasonThemes != null) ApplyEraViews();
            }
        }

        /// <summary>Development builds and tests: wear Time Run this session, owned or not (applied at once).</summary>
        public bool PreviewTimeRun
        {
            get => _previewTimeRun;
            set
            {
                if (_previewTimeRun == value) return;
                _previewTimeRun = value;
                if (_seasonThemes != null) RefreshSeason();
            }
        }

        /// <summary>The Time Run world being worn, with its eras (null outside it), for tests.</summary>
        public SeasonTheme ActiveWorld => _timeRun;

        /// <summary>The era being preloaded (null: none), for tests.</summary>
        public SeasonTheme EraPreloading => _eraPreload != null ? _eraPreload.Warming : null;

        /// <summary>The era on screen (-1 outside Time Run), for tests.</summary>
        public int EraShown => _timeRun != null ? _eraShown : -1;

        /// <summary>The time rift is playing (tests).</summary>
        public bool TimeRiftPlaying => _timeRift != null && _timeRift.Playing;

        // A backdrop with eras stands in for the era at the run's distance. Its obstacles, graves and ghosts follow each
        // thing's own position instead (ApplyEraViews), and a season's never mix in.
        SeasonTheme EraOf(SeasonTheme backdrop)
        {
            _timeRun = backdrop != null && backdrop.Eras.Length > 0 ? backdrop : null;
            if (_timeRun == null) return backdrop;
            _eraShown = ForceEra ?? TimeEras.At(_run != null ? _run.Distance : 0, _timeRun.Eras.Length);
            return _timeRun.Eras[Mathf.Clamp(_eraShown, 0, _timeRun.Eras.Length - 1)];
        }

        // Phase 2: the gameplay layer takes each thing's era from its own position (a forced era shows everywhere). A
        // gravestone the player chose keeps winning, as it does over a season's.
        void ApplyEraViews()
        {
            SeasonTheme[] eras = _timeRun != null ? _timeRun.Eras : null;
            if (eras != null && _forceEra is int forced)
            {
                _oneEra[0] = eras[Mathf.Clamp(forced, 0, eras.Length - 1)];
                eras = _oneEra;
            }
            _courseView.SetEras(eras);
            _ghostViews.SetEras(eras);
            if (_graves != null) _graves.SetEras(_equippedGrave != null && !_equippedGrave.Tintable && _equippedGrave.Sprite != null ? null : eras);
        }

        // ---- phase 4: the free trial, the achievements and the analytics, for runs that wear the world (not previews) ----

        bool _timeRunWorn;
        int _eraReached, _eraLogged, _dinoGhostsMost;

        /// <summary>The worn world is Time Run itself (not the development preview or a forced era).</summary>
        bool WearingTimeRun => _timeRun != null && !PreviewTimeRun && _forceEra == null;

        CosmeticItem WornWorldItem() => _catalog != null && _save != null ? _catalog.Resolve(_save.Cosmetics.Equipped.World, CosmeticType.World) : null;

        // A fresh run: a free run used if Time Run is worn on trial, and the run's counters reset.
        void StartTimeRun()
        {
            _timeRunWorn = WearingTimeRun;
            _eraReached = _eraLogged = 0;
            _dinoGhostsMost = 0;
            if (!_timeRunWorn) return;
            CosmeticItem world = WornWorldItem();
            if (world == null || world.World != _timeRun || !WorldTrial.OnTrial(world, _progress.Best, _save.Cosmetics)) return;
            int run = WorldTrial.UseRun(_save.Cosmetics);
            Log(AnalyticsEvents.TimeRunTrialStarted, ("run", run), ("left", WorldTrial.Left(world, _save.Cosmetics)));
            Save();
        }

        // Each running frame in Time Run: the furthest era, and the most ghosts on screen in Dino Land (no allocation).
        void TrackTimeRun(int era)
        {
            if (!_timeRunWorn || State != GameState.Running) return;
            if (era > _eraReached) _eraReached = era;
            if (era == 0)
            {
                int seen = _ghostViews.Visible(_cameraRig.Left, _cameraRig.Right);
                if (seen > _dinoGhostsMost) _dinoGhostsMost = seen;
            }
        }

        // A death in Time Run: the eras reached (logged now rather than mid-run, so nothing allocates while running) and
        // the achievements, whose rewards (the Caveman, the Chrono Android, the Fossil gravestone, the Amber trail) unlock.
        void SettleTimeRun()
        {
            if (!_timeRunWorn || _run == null) return;
            // Each era once a run, even if a Continue carries it on.
            for (int e = _eraLogged + 1; e <= _eraReached; e++)
                Log(AnalyticsEvents.EraReached, ("era", _timeRun.Eras[e].Title), ("index", e));
            _eraLogged = _eraReached;
            int fell = TimeEras.At(_run.Player.X / Echo.Core.Scoring.ScoreRules.SubUnitsPerMetre, _timeRun.Eras.Length);
            var death = new TimeRunDeath
            {
                Distance = _run.Distance,
                InDinoLand = fell == 0,
                InFarFuture = fell >= TimeEras.FarFuture,
                ByOwnGhost = _run.Cause == DeathCause.Ghost && _run.Killer != null && !_run.Killer.Leader,
                MostGhostsOnScreenInDinoLand = _dinoGhostsMost,
            };
            bool any = false;
            foreach (string id in AchievementRules.OnTimeRunDeath(_progress, death))
            {
                ToastAchievement(id);
                any = true;
            }
            if (any) GrantAchievementRewards();
            Save();
        }

        // Each frame: the era by the simulation's distance; a later one arrives through a time rift.
        void UpdateEra()
        {
            if (_timeRun == null || _run == null) return;
            int era = Mathf.Clamp(ForceEra ?? TimeEras.At(_run.Distance, _timeRun.Eras.Length), 0, _timeRun.Eras.Length - 1);
            TrackTimeRun(era);
            if (_eraPreload != null)
            {
                int next = ForceEra == null ? TimeEras.PreloadAt(_run.Distance, _timeRun.Eras.Length) : -1;
                _eraPreload.Warm(next >= 0 ? _timeRun.Eras[next] : null);
                _eraPreload.Render(_cameraRig.Left + CameraRig.WidthUnits / 2f, _cameraRig.Bottom + _cameraRig.Height / 2f);
            }
            // Art pass phase 7: the first era's name as a run sets off in it (the later ones come with their rifts).
            bool running = State == GameState.Running || State == GameState.Replay;
            if (running && _titledRun != _run && ForceEra == null && era == 0 && _run.Distance < 5)
            {
                _titledRun = _run;
                _hud.ShowEra(_timeRun.Eras[0].EraLabel, _timeRun.Eras[0].EraTitle);
            }
            if (era == _eraShown) return;
            bool later = era > _eraShown;
            _eraShown = era;
            SeasonTheme theme = _timeRun.Eras[era];
            ApplySeason(null, theme);
            if (!later || !running) return;
            _titledRun = _run;
            // Each rift bigger than the last; the last breaks the screen open into the 3D city.
            if (_timeRift != null) _timeRift.Play(era);
            // The Infinite City: through the rift, the camera rises and the orbital ring climbs into view.
            if (theme.Scene3D && _far3D != null) _far3D.BeginReveal();
            _sfx.TimeRift();
            _hud.ShowEra(theme.EraLabel, theme.EraTitle);
        }

        RunSimulation _titledRun;

        // The backdrop's art style: extra layers for any backdrop that has them, and high definition for the Far Future.
        void ApplyArtStyle(SeasonTheme backdrop)
        {
            _highDefinition = backdrop != null && backdrop.HighDefinition;
            _cameraRig.HighDefinition = _highDefinition;
            SimUnits.SnapPerUnit = _highDefinition ? CameraRig.PixelsPerUnit * CameraRig.HdGrid : CameraRig.PixelsPerUnit;
            if (_background != null)
            {
                _background.SetExtraLayers(backdrop != null ? backdrop.ExtraLayers : null);
                _background.SetDusk(backdrop != null ? backdrop.DuskFrom : 0f, backdrop != null ? backdrop.DuskTo : 0f,
                    backdrop != null ? backdrop.DuskShade : Color.white);
                _background.SetHighDefinition(_highDefinition);
            }
            _runnerView.SetFuture(_highDefinition);
            // Art pass phase 6: the 3D city behind the gameplay, the 2D backdrop aside, the gameplay lit and shadowed to match.
            _scene3D = backdrop != null && backdrop.Scene3D && _far3D != null;
            if (_background != null) _background.SetHidden(_scene3D);
            SetGameplayLit(_scene3D);
            _runnerView.SetShadow(_scene3D);
            _ghostViews.SetShadows(_scene3D);
            ApplyEffects();

            // Art pass phase 2: Dino Land's backdrop and floor at half resolution; everything else back on the default layer.
            _halfResolution = backdrop != null && backdrop.HalfResolution && _lowRes != null && LowResView.Layer >= 0;
            if (_lowRes != null) _lowRes.SetOn(_halfResolution);
            int layer = _halfResolution ? LowResView.Layer : 0;
            if (_background != null) LowResView.SetLayer(_background.transform, layer);
            if (_seasonEffects != null) LowResView.SetLayer(_seasonEffects.transform, layer);
            _courseView.SetLowRes(_halfResolution ? (_forceEra != null ? 0 : _eraShown) : -1, LowResView.Layer);
        }

        // Each time the camera is placed (CameraRig.Followed), so the backdrop always matches the framing.
        void RenderLowRes()
        {
            if (_halfResolution) _lowRes.Render(_cameraRig.transform.position, _cameraRig.Right - _cameraRig.Left, _cameraRig.Height, _cameraRig.Background);
        }

        // Full effects: bloom (and the Far Future's stronger bloom) and the neon lights; reduced: no bloom, one light.
        void ApplyEffects()
        {
            bool full = FullEffects;
            _cameraRig.SetGlow(full);
            if (_futureVolume != null) _futureVolume.weight = _highDefinition && full ? 1f : 0f;
            if (_futureLights != null) _futureLights.Set(_highDefinition, full);
            // Art pass phase 5: the wet floor reflects only with full effects; the splashes and motes stay either way.
            // The glass floor's backing in both future eras; reflections only where there's a 2D city to mirror.
            bool glass = _highDefinition && _backdrop != null && (_backdrop.Reflections || _backdrop.Scene3D);
            if (_background != null)
                _background.SetFloorGlass(glass, full && _backdrop != null && _backdrop.Reflections, _backdrop != null ? _backdrop.FloorBacking : Color.black);
            if (_background != null) _background.SetFullEffects(full);
            // Art fix phase 2: the 2D Far Future's searchlights and mist; the 3D city brings its own atmosphere.
            if (_futureFx != null) _futureFx.SetOn(_highDefinition, _highDefinition && !_scene3D);
            if (_futureLights != null) _futureLights.SetGameplay(_scene3D);
            if (_far3D != null) _far3D.SetMode(!_scene3D ? Far3DView.Mode.Off : full ? Far3DView.Mode.RealTime : Far3DView.Mode.Video);
        }

        // The runner, ghosts, course, graves and the Copycat on the lit sprite shader (the Far Far Future) or back to unlit.
        void SetGameplayLit(bool lit)
        {
            if (lit == _gameplayLit || _spriteUnlit == null || _spriteLit == null) return;
            _gameplayLit = lit;
            Material from = lit ? _spriteUnlit : _spriteLit, to = lit ? _spriteLit : _spriteUnlit;
            SpriteLighting.Swap(_runnerView.transform, from, to);
            SpriteLighting.Swap(_ghostViews.transform, from, to);
            if (_graves != null) SpriteLighting.Swap(_graves.transform, from, to);
            if (_copycatView != null) SpriteLighting.Swap(_copycatView.transform, from, to);
            _courseView.SetPieceMaterial(to);
        }

        /// <summary>The gameplay sprites are lit (tests).</summary>
        public bool GameplayLit => _gameplayLit;

        // Each frame and each time the camera is placed (a screenshot's framing too), so the city always fills the view.
        void RenderFar3D()
        {
            if (_scene3D) _far3D.Render(_cameraRig.transform.position, _cameraRig.Right - _cameraRig.Left, _cameraRig.Height, _runnerView.Position.x, Time.time);
        }

        void RenderFuture()
        {
            if (!_highDefinition) return;
            if (_futureLights != null) _futureLights.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height, Time.time);
            if (_futureFx != null) _futureFx.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height, Time.time);
            RenderFar3D();
            WatchFrameRate();
        }

        // A phone that can't keep the Far Future smooth with full effects switches to reduced on its own, unless the
        // player chose. Remembered with the next save, so later runs start reduced.
        void WatchFrameRate()
        {
            bool watching = _save != null && !_save.Settings.EffectsChosen && FullEffects && _benchmark == null && !_sessionReduced
                            && (State == GameState.Running || State == GameState.Replay);
            if (!watching)
            {
                _frameWatch.Reset();
                return;
            }
            if (!_frameWatch.Feed(_frameDt)) return;
            _sessionReduced = true;
            ApplyEffects();
            Debug.Log("[Effects] the Far Future ran slow: reduced effects for this session");
        }

        void RenderTimeRift()
        {
            if (_timeRift != null) _timeRift.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height);
        }

        /// <summary>Settings › Dev: Time Run cycles off, by distance, then each era.</summary>
        string CycleTimeRun()
        {
            if (!_previewTimeRun)
            {
                _previewTimeRun = true;
                _forceEra = null;
            }
            else if (_forceEra == null) _forceEra = 0;
            else if (_forceEra < (_timeRun != null ? _timeRun.Eras.Length - 1 : 4)) _forceEra++;
            else
            {
                _previewTimeRun = false;
                _forceEra = null;
            }
            RefreshSeason();
            return TimeRunLabel();
        }

        string TimeRunLabel() =>
            !PreviewTimeRun ? "Dev: Time Run off" : ForceEra == null ? "Dev: Time Run by distance" : $"Dev: Time Run era {ForceEra + 1}";

        SeasonTheme PreviewedTimeRun() => PreviewTimeRun ? ThemeFor(SeasonIds.TimeRun) : null;
    }
}
