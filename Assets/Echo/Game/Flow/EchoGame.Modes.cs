using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Game.UI;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The Modes screen (modes doc, the mode framework): the main game, the Daily Haunt and the live event modes as
    /// cards. Which modes are live comes from the server (GetModes reads the "modes" Remote Config key, the same switch
    /// and dates every mode endpoint checks), so the game and the server never disagree. Opening it signs in, like the
    /// Daily Haunt. Development builds can force every mode open (Settings › Dev: modes).
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] ModesView _modesView;

        const int MainCard = 0, DailyCard = 1;

        ModesResult _modes;
        float _modesFetchedAt, _modesLabelIn;
        readonly List<ModesView.Card> _modeCards = new List<ModesView.Card>();

        /// <summary>Development builds: every mode open, whatever the schedule says (the server honours it in development only).</summary>
        public bool ForceModes { get; set; }

        /// <summary>The modes the server last said were on show (tests).</summary>
        public ModesResult LastModes => _modes;

        bool ModesOpen => _modesView != null && _modesView.IsOpen;

        /// <summary>
        /// Development builds, and the recording APK (a user decision: the build for testing on a phone, on the
        /// development server): the dev settings and overrides (seasons, modes, the Daily Haunt's date).
        /// </summary>
        static bool DevTools =>
#if ECHO_RECORDING
            true;
#else
            Debug.isDebugBuild;
#endif

        void OpenModes()
        {
            if (_modesView == null) return;
            OpenMenu(() =>
            {
                _modesView.Show();
                _modes = null;
                ShowModeCards();
                _ = LoadModes();
            });
        }

        async Task LoadModes()
        {
            if (!_servicesSettled)
            {
                _modesView.SetStatus("Starting up... events will show in a moment.");
                return;
            }
            _modesView.SetStatus("Checking for events...");
            ModesResult result = await _services.Backend.GetModesAsync(DailyDateOverride, ForceModes && DevTools);
            if (this == null || !ModesOpen) return;
            _modes = result;
            _modesFetchedAt = Time.realtimeSinceStartup;
            _modesView.SetStatus(!result.Ok ? "Events need a connection." : result.Modes.Count == 0 ? "No events on right now. Check back soon." : "");
            ShowModeCards();
        }

        // The main game and the Daily Haunt always; then each mode on show, with its countdown by the server's clock.
        void ShowModeCards()
        {
            _modeCards.Clear();
            _modeCards.Add(new ModesView.Card { Title = "Echo", Detail = $"The main game · best {_progress.Best}" });
            TimeSpan left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            _modeCards.Add(new ModesView.Card
            {
                Title = "Daily Haunt",
                Detail = _dailyEnabled ? $"New course in {(int)left.TotalHours}h {left.Minutes:00}m" : "Back soon",
                Dot = _dailyEnabled && _save.Daily.LastPlayedDate != TodayUtc(),
            });
            if (_modes != null && _modes.Ok)
            {
                long elapsed = (long)(Time.realtimeSinceStartup - _modesFetchedAt);
                foreach (ModeState m in _modes.Modes)
                {
                    if (_modeCards.Count == _modesView.Capacity) break;
                    long seconds = m.SecondsLeft < 0 ? -1 : Math.Max(0, m.SecondsLeft - elapsed);
                    string side = m.Id == Echo.Core.Modes.ModeIds.NaughtyNice && !string.IsNullOrEmpty(_save.Modes.Team) ? $" · Team {Side(_save.Modes.Team)}" : "";
                    _modeCards.Add(new ModesView.Card
                    {
                        Title = ModeNames.Name(m.Id),
                        Detail = $"{ModeNames.Pitch(m.Id)}\n{ModeNames.Countdown(m.Open, seconds)}{side}",
                    });
                }
            }
            _modesView.SetCards(_modeCards);
        }

        void TickModeLabels(float dt)
        {
            if (!ModesOpen || (_modesLabelIn -= dt) > 0f) return;
            _modesLabelIn = 1f;
            ShowModeCards();
        }

        void OnModeCard(int index)
        {
            if (!ModesOpen) return;
            if (index == MainCard)
            {
                CloseMenu();
                StartRunning();
                return;
            }
            if (index == DailyCard)
            {
                CloseMenu();
                OpenDailyLobby();
                return;
            }
            int m = index - 2;
            if (_modes == null || m < 0 || m >= _modes.Modes.Count) return;
            ModeState mode = _modes.Modes[m];
            Log(AnalyticsEvents.ModeOpened, ("mode", mode.Id));
            // Naughty vs Nice is the Daily Haunt with a team: its lobby holds the tally and the side picker.
            if (mode.Id == Echo.Core.Modes.ModeIds.NaughtyNice)
            {
                CloseMenu();
                OpenDailyLobby();
                return;
            }
            if (mode.Id == Echo.Core.Modes.ModeIds.OneLife)
            {
                CloseMenu();
                OpenOneLifeLobby();
                return;
            }
            // Modes doc phase 1 is the framework: each mode's own screen comes with its phase.
            _modesView.SetStatus(mode.Open ? $"{ModeNames.Name(mode.Id)} is coming in this update." : $"{ModeNames.Name(mode.Id)}: {ModeNames.Countdown(false, mode.SecondsLeft)}");
        }

        /// <summary>Development builds: the Settings button forcing every mode open, and back.</summary>
        string ToggleForceModes()
        {
            ForceModes = !ForceModes;
            return ForceModes ? "Dev: modes forced" : "Dev: modes live";
        }
    }
}
