using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Echo.Core.Achievements;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Services.Analytics;
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
            ApplyGrave();
            _ghostViews.SetSeason(theme != null ? theme.GhostAccessory : null, theme != null ? theme.NemesisAccessory : null);
            _deathScreen.SetHeadstoneSprite(theme != null ? theme.Headstone : null);
            if (_seasonEffects != null) _seasonEffects.SetTheme(theme);
            if (theme != null && theme.Stinger != null && _stingersPlayed.Add(theme.Id) && _music != null) _music.PlayStinger(theme.Stinger);
            Debug.Log($"[Seasons] {(theme != null ? theme.Id : "no season")}{(ForcedSeason != null ? " (forced)" : "")}");
            LogSeasonActive();
        }

        CosmeticItem _equippedGrave;

        // A season's gravestone shows during its season; otherwise the one the player wears (the stone by default). A
        // reel watched back wears the stone, as its run's own grave isn't recorded.
        void ApplyGrave()
        {
            if (_graves == null) return;
            if (_season != null && _season.Grave != null) _graves.SetSeason(_season.Grave, _season.GraveInColour);
            else if (_equippedGrave != null && !_equippedGrave.Tintable && _equippedGrave.Sprite != null) _graves.SetSeason(_equippedGrave.Sprite, true);
            else _graves.SetSeason(null, false);
        }

        // The season's sky, blended over the player's palette.
        Color SeasonDay(Color day, float metres)
        {
            if (_season == null || _season.PaletteStops.Length == 0) return day;
            return Color.Lerp(day, PaletteSampler.Sample(_season.PaletteStops, metres), _season.PaletteBlend);
        }

        void RenderSeasonEffects()
        {
            if (_seasonEffects != null)
                _seasonEffects.Render(_cameraRig.Left, _cameraRig.Right, _cameraRig.Bottom, _cameraRig.Height, Time.time, ResetShow());
        }

        /// <summary>Test and screenshot hook: the New Year show as if the board had just reset.</summary>
        public bool PreviewResetShow { get; set; }

        float _showCheckAt = -1f, _show;
        string _showToastedFor;

        // Seasons doc (New Year): fireworks when the Daily Haunt board resets at 00:00 UTC, by the server's clock when
        // known. Checked once a second, so the date sums never run every frame.
        float ResetShow()
        {
            if (_season == null || _season.ResetShowSeconds <= 0f) return 0f;
            if (PreviewResetShow) return 1f;
            if (Time.realtimeSinceStartup < _showCheckAt) return _show;
            _showCheckAt = Time.realtimeSinceStartup + 1f;
            DateTime now = SeasonNowUtc();
            _show = (now - now.Date).TotalSeconds < _season.ResetShowSeconds ? 1f : 0f;
            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_show > 0f && _showToastedFor != day && !string.IsNullOrEmpty(_season.ResetShowToast))
            {
                _showToastedFor = day;
                _hud.QueueToast(_season.ResetShowToast);
            }
            return _show;
        }

        /// <summary>The season's death-screen line under the message, when it applies to how this run ended.</summary>
        string WithSeasonLine(string message, RunSimulation run, GhostRecord nemesis)
        {
            if (_season == null || string.IsNullOrEmpty(_season.DeathLine)) return message;
            if (_season.DeathLineNemesisOnly && (run.Cause != DeathCause.Ghost || run.Killer == null || run.Killer != nemesis)) return message;
            return string.IsNullOrEmpty(message) ? _season.DeathLine : message + "\n" + _season.DeathLine;
        }

        // ---- seasonal achievements (seasons doc) ----

        /// <summary>The events counting towards seasonal achievements on this UK date.</summary>
        public SeasonalContext SeasonalOn(DateTime ukDate) => SeasonalEvents.On(_schedule, ukDate);

        // A main-game run, on today's date (seasons doc rule 6: the server's when it has told us, the device's otherwise).
        void CountSeasonalRun(GhostRecord nemesisBefore)
        {
            SeasonalContext s = SeasonalOn(UkCalendar.Date(SeasonNowUtc()));
            if (!s.Any) return;
            bool byNemesis = _run.Cause == DeathCause.Ghost && _run.Killer != null && _run.Killer == nemesisBefore;
            foreach (string id in AchievementRules.OnSeasonalRunEnded(_progress, _run, s, byNemesis)) SeasonalEarned(id);
            GrantAchievementRewards();
        }

        // A Daily Haunt run, on its course's date (the server's).
        void CountSeasonalDailyRun(string date, int day, int leadersPassed)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime ukDate)) return;
            SeasonalContext s = SeasonalOn(ukDate);
            if (!s.Any) return;
            foreach (string id in AchievementRules.OnSeasonalDailyRun(_progress, day, leadersPassed, s)) SeasonalEarned(id);
            GrantAchievementRewards();
        }

        void SeasonalEarned(string id)
        {
            ToastAchievement(id);
            Log(AnalyticsEvents.SeasonalAchievement, ("id", id));
        }

        // ---- analytics (seasons doc) ----

        bool _sessionLive;
        string _seasonLogged;
        readonly Dictionary<CosmeticType, string> _equipLogged = new Dictionary<CosmeticType, string>();

        // season_active: once a session for each season shown, after the session has started (so after consent).
        void LogSeasonActive()
        {
            if (!_sessionLive || _season == null || _seasonLogged == _season.Id) return;
            _seasonLogged = _season.Id;
            Log(AnalyticsEvents.SeasonActive, ("season", _season.Id), ("forced", ForcedSeason != null));
        }

        // seasonal_skin_equipped: a seasonal character, trail or gravestone put on (the Christmas Pack included).
        void LogSeasonalEquip()
        {
            if (_catalog == null) return;
            var e = _save.Cosmetics.Equipped;
            foreach (var (type, id) in new[] { (CosmeticType.Character, e.Character), (CosmeticType.Trail, e.Trail), (CosmeticType.Grave, e.Grave) })
            {
                if (_equipLogged.TryGetValue(type, out string had) && had == id) continue;
                bool first = !_equipLogged.ContainsKey(type);
                _equipLogged[type] = id;
                CosmeticItem item = _catalog.Find(id);
                if (first || item == null) continue;
                if (!string.IsNullOrEmpty(item.UnlockAchievement) || item.ProductId == Echo.Services.Iap.ProductIds.PackChristmas)
                    Log(AnalyticsEvents.SeasonalSkinEquipped, ("skin", id));
            }
        }

        // Seasons doc: a seasonal reward is kept for good once its achievement is earned.
        void GrantAchievementRewards()
        {
            if (_catalog == null) return;
            foreach (CosmeticItem item in CosmeticUnlocks.EarnByAchievements(_catalog, _progress.Achievements, _save.Cosmetics.Owned))
                _hud.QueueToast("Unlocked: " + item.DisplayName);
        }

        // Whether the schedule runs Halloween this year (2.1 ships it switched off, so 2.0's free week carries on).
        bool HalloweenScheduled()
        {
            foreach (SeasonWindow w in _schedule.Seasons)
                if (w.Id == SeasonIds.Halloween && w.Enabled) return true;
            return false;
        }

        /// <summary>Seasons doc: the Christmas Pack is on sale during Christmas and New Year (by the schedule, or forced).</summary>
        bool ChristmasPackOnSale()
        {
            string id = ForcedSeason ?? ScheduledSeason(SeasonNowUtc());
            return id == SeasonIds.Christmas || id == SeasonIds.NewYear;
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
