using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Core.Modes;
using Echo.Game.UI;
using Echo.Services.Analytics;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Ghost Town (a user decision; "Modes" in code; the modes doc's mode framework): the online hub, with the Daily Haunt
    /// and the event modes as cards and the community's live numbers. Which modes are live comes from the server (GetModes reads the "modes" Remote Config key, the same switch
    /// and dates every mode endpoint checks), so the game and the server never disagree. Opening it signs in, like the
    /// Daily Haunt. Development builds can force every mode open (Settings › Dev: modes).
    /// </summary>
    public sealed partial class EchoGame
    {
        [SerializeField] ModesView _modesView;

        ModesResult _modes;
        float _modesFetchedAt, _modesLabelIn;
        readonly List<ModesView.Event> _modeEvents = new List<ModesView.Event>();

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

        // Ghost Town: your profile, the Daily Haunt's card (the server's pulse when it has one: runners today, the leader,
        // your place) and a card per event on show, live first, each with the live number of players.
        void ShowModeCards()
        {
            StatsView.Profile p = ProfileNow();
            string badges = p.Icons.Count > 0 ? "  " + string.Join(" ", p.Icons) : "";
            _modesView.SetProfile(string.IsNullOrEmpty(p.Name) && p.Badges.Count == 0 && string.IsNullOrEmpty(p.Team)
                ? "<color=#9FB8C0>Run the Daily Haunt to join the town</color>"
                : (string.IsNullOrEmpty(p.Name) ? "You" : p.Name) + (string.IsNullOrEmpty(p.Team) ? "" : $"  <color=#FFE9A0>{p.Team}</color>") + badges);

            long elapsed = _modes != null ? (long)(Time.realtimeSinceStartup - _modesFetchedAt) : 0;
            DailyPulseInfo pulse = _modes != null && _modes.Ok ? _modes.Daily : null;
            TimeSpan untilMidnight = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            long left = pulse != null ? Math.Max(0, pulse.SecondsLeft - elapsed) : (long)untilMidnight.TotalSeconds;
            bool playedToday = _save.Daily.LastPlayedDate == TodayUtc();
            _modesView.SetHero(
                !_dailyEnabled ? "Back soon" : pulse == null ? "One course for everyone, every day" : ModeNames.Players(ModesView.DailyId, pulse.Runners),
                pulse == null || string.IsNullOrEmpty(pulse.LeaderName) ? (pulse == null ? "" : "No leader yet: set the pace")
                    : $"Leader: {pulse.LeaderName} · {pulse.LeaderDistance:N0} m",
                pulse != null && pulse.Rank > 0 ? $"You: #{pulse.Rank} · {pulse.BestDistance:N0} m"
                    : playedToday ? "You've run today" : "You haven't run today",
                _dailyEnabled ? $"New course in {left / 3600}h {left / 60 % 60:00}m" : "",
                dot: _dailyEnabled && !playedToday, enabled: _dailyEnabled);

            _modeEvents.Clear();
            if (_modes != null && _modes.Ok)
                foreach (ModeState m in _modes.Modes)
                {
                    long seconds = m.SecondsLeft < 0 ? -1 : Math.Max(0, m.SecondsLeft - elapsed);
                    string side = m.Id == ModeIds.NaughtyNice && !string.IsNullOrEmpty(_save.Modes.Team) ? $" · Team {Side(_save.Modes.Team)}" : "";
                    _modeEvents.Add(new ModesView.Event
                    {
                        Id = m.Id,
                        Name = ModeNames.Name(m.Id),
                        Pitch = ModeNames.Pitch(m.Id),
                        Players = ModeNames.Players(m.Id, m.Players) + side,
                        Countdown = ModeNames.Countdown(m.Open, seconds),
                        Live = m.Open,
                        Colour = ModeNames.Colour(m.Id),
                    });
                }
            _modesView.SetEvents(_modeEvents);
        }

        void TickModeLabels(float dt)
        {
            if (!ModesOpen || (_modesLabelIn -= dt) > 0f) return;
            _modesLabelIn = 1f;
            ShowModeCards();
        }

        void OnModeCard(string id)
        {
            if (!ModesOpen) return;
            if (id == ModesView.DailyId)
            {
                CloseMenu();
                OpenDailyLobby();
                return;
            }
            ModeState mode = _modes?.Modes.Find(m => m.Id == id);
            if (mode == null) return;
            Log(AnalyticsEvents.ModeOpened, ("mode", mode.Id));
            if (!mode.Open)
            {
                _modesView.SetStatus($"{ModeNames.Name(mode.Id)}: {ModeNames.Countdown(false, mode.SecondsLeft)}");
                return;
            }
            CloseMenu();
            switch (mode.Id)
            {
                // Naughty vs Nice is the Daily Haunt with a team: its lobby holds the tally and the side picker.
                case ModeIds.NaughtyNice: OpenDailyLobby(); break;
                case ModeIds.OneLife: OpenOneLifeLobby(); break;
                case ModeIds.Graveyard: OpenGraveyardLobby(); break;
                case ModeIds.Ranked: OpenRankedLobby(); break;
            }
        }

        /// <summary>Development builds: the Settings button forcing every mode open, and back.</summary>
        string ToggleForceModes()
        {
            ForceModes = !ForceModes;
            return ForceModes ? "Dev: modes forced" : "Dev: modes live";
        }
    }
}
