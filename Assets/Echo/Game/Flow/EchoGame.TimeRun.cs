using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Game.World;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// World: Time Run (Assets/Docs/World Time Run.md, phase 1, the era system): a world whose theme changes with the run's
    /// distance, one era every 250 m (TimeEras), read from the simulation so replays and challenges see the same era in the
    /// same place. Crossing into a later era plays the time rift behind the gameplay, a whoosh and the era's label in the
    /// corner. All of it is looks: the era themes go through the same ApplySeason as any world.
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] TimeRiftView _timeRift;

        SeasonTheme _timeRun;
        int _eraShown = -1;

        int? _forceEra;
        bool _previewTimeRun;

        /// <summary>Development builds and tests: show this era whatever the distance (null: by distance).</summary>
        public int? ForceEra
        {
            get => _forceEra;
            set => _forceEra = value;
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

        // A backdrop with eras stands in for the era at the run's distance, as details too: Time Run's eras bring their own
        // obstacles, graves and ghosts (later phases), so a season's never mix in.
        SeasonTheme EraOf(SeasonTheme backdrop)
        {
            _timeRun = backdrop != null && backdrop.Eras.Length > 0 ? backdrop : null;
            if (_timeRun == null) return backdrop;
            _eraShown = ForceEra ?? TimeEras.At(_run != null ? _run.Distance : 0, _timeRun.Eras.Length);
            return _timeRun.Eras[Mathf.Clamp(_eraShown, 0, _timeRun.Eras.Length - 1)];
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
            ApplySeason(theme, theme);
            if (!later || (State != GameState.Running && State != GameState.Replay)) return;
            if (_timeRift != null) _timeRift.Play();
            _sfx.TimeRift();
            _hud.ShowEra(theme.EraLabel);
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
