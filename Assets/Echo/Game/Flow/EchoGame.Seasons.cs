using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Services.Backend;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Seasonal events (seasons doc, version 2.1): the season on today's UK date is chosen from the schedule and its
    /// <see cref="SeasonTheme"/> handed to the views. Looks only: nothing here reaches the run, so every run replays
    /// the same in every season (SeasonReplayTests). The schedule is the built-in copy until Remote Config's "seasons"
    /// has been read, which happens only with the flags, so only once the player has signed in (a user decision); the
    /// last one read is kept beside the save for next time. Checked at startup, at the title and before each run.
    /// </summary>
    public sealed partial class EchoGame
    {
        /// <summary><see cref="ForceSeason"/> with this shows no season at all.</summary>
        public const string NoSeason = "none";

        [SerializeField] TextAsset _seasonSchedule;
        [SerializeField] SeasonTheme[] _seasonThemes = new SeasonTheme[0];
        [SerializeField] SeasonEffectsView _seasonEffects;

        SeasonSchedule _schedule = new SeasonSchedule();
        string _scheduleJson;
        string _seasonsCachePath;
        SeasonTheme _season;
        bool _seasonApplied;
        readonly HashSet<string> _stingersPlayed = new HashSet<string>();

        /// <summary>The season showing now, or null.</summary>
        public SeasonTheme ActiveSeason => _season;

        public IReadOnlyList<SeasonTheme> SeasonThemes => _seasonThemes;

        /// <summary>Development builds (and the recording APK): a season forced on, <see cref="NoSeason"/>, or null to follow the schedule.</summary>
        public string ForcedSeason { get; private set; }

        public void ForceSeason(string id)
        {
            ForcedSeason = id;
            RefreshSeason();
        }

        void InitSeasons(string savePath)
        {
            _seasonsCachePath = savePath + ".seasons.json";
            string cached = null;
            try
            {
                if (File.Exists(_seasonsCachePath)) cached = File.ReadAllText(_seasonsCachePath);
            }
            catch (IOException) { }
            if (!UseSchedule(cached) && _seasonSchedule != null) UseSchedule(_seasonSchedule.text);
        }

        bool UseSchedule(string json)
        {
            if (!SeasonSchedule.TryParse(json, out SeasonSchedule schedule)) return false;
            _schedule = schedule;
            _scheduleJson = json;
            return true;
        }

        /// <summary>The flags, keeping any season schedule Remote Config sent with them.</summary>
        async Task<BackendFlags> Flags()
        {
            BackendFlags flags = await _services.Backend.GetFlagsAsync();
            if (this != null && flags.SeasonsJson != null && flags.SeasonsJson != _scheduleJson && UseSchedule(flags.SeasonsJson))
            {
                try
                {
                    File.WriteAllText(_seasonsCachePath, flags.SeasonsJson);
                }
                catch (IOException e)
                {
                    Debug.LogWarning("[Seasons] couldn't keep the schedule: " + e.Message);
                }
                if (State == GameState.Title || State == GameState.Menu) RefreshSeason();
            }
            return flags;
        }

        // Seasons doc rule 6: the server's date when it has told us (the Daily Haunt), the device's otherwise.
        DateTime SeasonNowUtc()
        {
            if (_serverDay != null && string.IsNullOrEmpty(DailyDateOverride)
                && DateTime.TryParseExact(_serverDay, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime day))
                return day.AddDays(1).AddSeconds(-BoardSecondsLeft());
            return DateTime.UtcNow;
        }

        /// <summary>The scheduled season's id on this UTC moment (UK local date), or null.</summary>
        public string ScheduledSeason(DateTime utc) => _schedule.ActiveOn(UkCalendar.Date(utc))?.Id;

        void RefreshSeason()
        {
            string id = ForcedSeason ?? ScheduledSeason(SeasonNowUtc());
            SeasonTheme theme = ThemeFor(id);
            if (_seasonApplied && theme == _season) return;
            ApplySeason(theme);
        }

        SeasonTheme ThemeFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SeasonTheme t in _seasonThemes)
                if (t != null && t.Id == id) return t;
            return null;
        }

        void ApplySeason(SeasonTheme theme)
        {
            _season = theme;
            _seasonApplied = true;
            if (_background != null) _background.SetSeason(theme != null ? theme.FarLayer : null, theme != null ? theme.NearLayer : null);
            _courseView.SetSeason(theme != null ? theme.Spike : null, theme != null ? theme.Block : null, theme != null ? theme.GroundTop : null);
            if (_graves != null) _graves.SetSeason(theme != null ? theme.Grave : null, theme != null && theme.GraveInColour);
            _ghostViews.SetSeason(theme != null ? theme.GhostAccessory : null, theme != null ? theme.NemesisAccessory : null);
            _deathScreen.SetHeadstoneSprite(theme != null ? theme.Headstone : null);
            if (_seasonEffects != null) _seasonEffects.SetTheme(theme);
            if (theme != null && theme.Stinger != null && _stingersPlayed.Add(theme.Id) && _music != null) _music.PlayStinger(theme.Stinger);
            Debug.Log($"[Seasons] {(theme != null ? theme.Id : "no season")}{(ForcedSeason != null ? " (forced)" : "")}");
        }

        // The season's sky, blended over the player's palette.
        Color SeasonDay(Color day, float metres)
        {
            if (_season == null || _season.PaletteStops.Length == 0) return day;
            return Color.Lerp(day, PaletteSampler.Sample(_season.PaletteStops, metres), _season.PaletteBlend);
        }

        void RenderSeasonEffects()
        {
            if (_seasonEffects != null) _seasonEffects.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height, Time.time);
        }

        /// <summary>The season's death-screen line under the message, when it applies to how this run ended.</summary>
        string WithSeasonLine(string message, RunSimulation run, GhostRecord nemesis)
        {
            if (_season == null || string.IsNullOrEmpty(_season.DeathLine)) return message;
            if (_season.DeathLineNemesisOnly && (run.Cause != DeathCause.Ghost || run.Killer == null || run.Killer != nemesis)) return message;
            return string.IsNullOrEmpty(message) ? _season.DeathLine : message + "\n" + _season.DeathLine;
        }

        /// <summary>The Settings button: the schedule, no season, then each theme in turn.</summary>
        string CycleSeason()
        {
            var order = new List<string> { null, NoSeason };
            foreach (SeasonTheme t in _seasonThemes)
                if (t != null) order.Add(t.Id);
            int next = (order.IndexOf(ForcedSeason) + 1) % order.Count;
            ForceSeason(order[next]);
            return SeasonLabel();
        }

        string SeasonLabel() => ForcedSeason == null
            ? $"Dev: season auto ({(_season != null ? _season.Id : NoSeason)})"
            : $"Dev: season {ForcedSeason}";

        /// <summary>The forced-season button shows in development builds and in the recording APK.</summary>
        static bool SeasonDevSetting =>
#if ECHO_RECORDING
            true;
#else
            Debug.isDebugBuild;
#endif
    }
}
