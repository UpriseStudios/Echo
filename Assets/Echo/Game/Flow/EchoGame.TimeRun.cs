using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Game.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Game.Flow
{
    /// <summary>
    /// World: Time Run (Assets/Docs/World Time Run.md, phase 1, the era system): a world whose theme changes with the run's
    /// distance, one era every 250 m (TimeEras), read from the simulation so replays and challenges see the same era in the
    /// same place. Crossing into a later era plays the time rift behind the gameplay, a whoosh and the era's label in the
    /// corner. All of it is looks: the era themes go through the same ApplySeason as any world.
    /// Phase 3, art styles: each era is drawn in a different era of video games (8-bit, 16-bit, 32-bit), and the Far
    /// Future breaks out of pixel art (ApplyArtStyle): full resolution, 2D lights, a stronger bloom, and glow, rim light
    /// and trails on the pixel sprites. Full or reduced effects (EffectsQuality) decide the bloom and the lights.
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] TimeRiftView _timeRift;
        [SerializeField] FutureLightsView _futureLights;
        [Tooltip("The Far Future's stronger bloom, weighted in with full effects.")]
        [SerializeField] Volume _futureVolume;

        bool _highDefinition;
        bool? _weakDevice;
        readonly FrameWatch _frameWatch = new FrameWatch();

        bool WeakDevice => _weakDevice ??= EffectsQuality.ThisDeviceIsWeak();

        /// <summary>Full effects now: the player's choice, else this phone's (World Time Run doc, phase 3).</summary>
        public bool FullEffects => _save == null || EffectsQuality.Full(_save.Settings, WeakDevice || _benchmarkReduced);

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

        // Each frame: the era by the simulation's distance; a later one arrives through a time rift.
        void UpdateEra()
        {
            if (_timeRun == null || _run == null) return;
            int era = Mathf.Clamp(ForceEra ?? TimeEras.At(_run.Distance, _timeRun.Eras.Length), 0, _timeRun.Eras.Length - 1);
            if (era == _eraShown) return;
            bool later = era > _eraShown;
            _eraShown = era;
            SeasonTheme theme = _timeRun.Eras[era];
            ApplySeason(null, theme);
            if (!later || (State != GameState.Running && State != GameState.Replay)) return;
            if (_timeRift != null) _timeRift.Play();
            _sfx.TimeRift();
            _hud.ShowEra(theme.EraLabel);
        }

        // The backdrop's art style: extra layers for any backdrop that has them, and high definition for the Far Future.
        void ApplyArtStyle(SeasonTheme backdrop)
        {
            _highDefinition = backdrop != null && backdrop.HighDefinition;
            _cameraRig.HighDefinition = _highDefinition;
            SimUnits.SnapPerUnit = _highDefinition ? CameraRig.PixelsPerUnit * CameraRig.HdGrid : CameraRig.PixelsPerUnit;
            if (_background != null)
            {
                _background.SetExtraLayers(backdrop != null ? backdrop.ExtraLayers : null);
                _background.SetHighDefinition(_highDefinition);
            }
            _runnerView.SetFuture(_highDefinition);
            ApplyEffects();
        }

        // Full effects: bloom (and the Far Future's stronger bloom) and the neon lights; reduced: no bloom, one light.
        void ApplyEffects()
        {
            bool full = FullEffects;
            _cameraRig.SetGlow(full);
            if (_futureVolume != null) _futureVolume.weight = _highDefinition && full ? 1f : 0f;
            if (_futureLights != null) _futureLights.Set(_highDefinition, full);
        }

        void RenderFuture()
        {
            if (!_highDefinition) return;
            if (_futureLights != null) _futureLights.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height, Time.time);
            WatchFrameRate();
        }

        // A phone that can't keep the Far Future smooth with full effects switches to reduced on its own, unless the
        // player chose. Remembered with the next save, so later runs start reduced.
        void WatchFrameRate()
        {
            bool watching = _save != null && !_save.Settings.EffectsChosen && FullEffects && _benchmark == null
                            && (State == GameState.Running || State == GameState.Replay);
            if (!watching)
            {
                _frameWatch.Reset();
                return;
            }
            if (!_frameWatch.Feed(Time.unscaledDeltaTime)) return;
            _save.Settings.EffectsReduced = true;
            ApplyEffects();
            Debug.Log("[Effects] the Far Future ran slow: reduced effects from now on");
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
            else if (_forceEra < 3) _forceEra++;
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
