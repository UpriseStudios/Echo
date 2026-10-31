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
        [SerializeField] EggHuntView _eggHunt;

        SeasonSchedule _schedule = new SeasonSchedule();
        string _scheduleJson;
        string _seasonsCachePath;
        SeasonTheme _season;

        // A user revision (worlds): the backdrop (sky, scenery layers, effects, flyer) can come from the world the player
        // wears while the rest of the season (obstacles, graves, ghost hats, the death line) still shows.
        SeasonTheme _backdrop;
        bool _seasonApplied;
        readonly HashSet<string> _stingersPlayed = new HashSet<string>();
        readonly HashSet<string> _seasonToasted = new HashSet<string>();

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

        // A user revision: the Community Graveyard's lobby and runs wear its own look over any season (a forced season
        // still wins, for testing). Set by the Graveyard, cleared by the title, a main run and the other lobbies.
        bool _graveyardLook;

        void ShowGraveyardLook(bool on)
        {
            _graveyardLook = on;
            RefreshSeason();
        }

        void RefreshSeason()
        {
            string id = ForcedSeason ?? ScheduledSeason(SeasonNowUtc());
            SeasonTheme season = ThemeFor(id);
            // The music follows the season even in the Graveyard: a December graveyard still plays the Christmas tune.
            if (_music != null) _music.SetStems(season != null && season.MusicStems.Length > 0 ? season.MusicStems : null);
            SeasonTheme mode = _graveyardLook && ForcedSeason == null ? ThemeFor(SeasonIds.Graveyard) : null;
            // Which look wins (a user decision): a mode's own look in that mode; otherwise the Seasonal looks setting
            // says whether a season shows over the world you wear (a forced season too, so the setting can be tried).
            bool seasonal = _save == null || _save.Settings.SeasonalLooks;
            SeasonTheme details = mode ?? (seasonal ? season : null);
            SeasonTheme backdrop = mode ?? (seasonal && HasBackdrop(season) ? season : WornWorld());
            if (_seasonApplied && details == _season && backdrop == _backdrop) return;
            ApplySeason(details, backdrop);
        }

        static bool HasBackdrop(SeasonTheme t) =>
            t != null && (t.PaletteStops.Length > 0 || t.FarLayer != null || t.NearLayer != null || t.Effect != SeasonEffect.None
                          || t.Fireworks || t.Flyer != null);

        /// <summary>The world the player wears, if it's owned and isn't the normal one.</summary>
        SeasonTheme WornWorld()
        {
            if (_catalog == null || _save == null || _progress == null) return null;
            CosmeticItem world = _catalog.Resolve(_save.Cosmetics.Equipped.World, CosmeticType.World);
            return world != null && world.World != null && CosmeticUnlocks.IsOwned(world, _progress.Best, _save.Cosmetics.Owned) ? world.World : null;
        }

        /// <summary>Whether the backdrop brings its own scenery, and so stands for the whole run.</summary>
        bool OwnScenery => _backdrop != null && (_backdrop.FarLayer != null || _backdrop.NearLayer != null);

        /// <summary>The backdrop showing now (a season's, a world's or a mode's), or null for the normal one.</summary>
        public SeasonTheme ActiveBackdrop => _backdrop;

        SeasonTheme ThemeFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SeasonTheme t in _seasonThemes)
                if (t != null && t.Id == id) return t;
            return null;
        }

        void ApplySeason(SeasonTheme theme, SeasonTheme backdrop)
        {
            _season = theme;
            _backdrop = backdrop;
            _seasonApplied = true;
            if (_background != null)
            {
                _background.SetSeason(backdrop != null ? backdrop.FarLayer : null, backdrop != null ? backdrop.NearLayer : null);
                _background.SetTints(backdrop != null ? backdrop.FarTint : Color.clear, backdrop != null ? backdrop.NearTint : Color.clear);
            }
            _courseView.SetSeason(theme != null ? theme.Spike : null, theme != null ? theme.Block : null, theme != null ? theme.GroundTop : null);
            ApplyGrave();
            _ghostViews.SetSeason(theme != null ? theme.GhostAccessory : null, theme != null ? theme.NemesisAccessory : null);
            _deathScreen.SetHeadstoneSprite(theme != null ? theme.Headstone : null);
            if (_seasonEffects != null) _seasonEffects.SetTheme(backdrop);
            if (_catalog != null && _save != null && _progress != null) ApplyCosmetics();
            if (theme != null && theme.Stinger != null && _stingersPlayed.Add(theme.Id) && _music != null) _music.PlayStinger(theme.Stinger);
            Debug.Log($"[Seasons] {(theme != null ? theme.Id : "no season")}{(ForcedSeason != null ? " (forced)" : "")}");
            LogSeasonActive();
        }

        CosmeticItem _equippedGrave;

        // The gravestone the player chose wins (a user revision: device testing found a season's grave hid every choice);
        // a season's gravestone replaces only the plain stone, during its season. A reel watched back wears the stone, as
        // its run's own grave isn't recorded.
        void ApplyGrave()
        {
            if (_graves == null) return;
            if (_equippedGrave != null && !_equippedGrave.Tintable && _equippedGrave.Sprite != null) _graves.SetSeason(_equippedGrave.Sprite, true);
            else if (_season != null && _season.Grave != null) _graves.SetSeason(_season.Grave, _season.GraveInColour);
            else _graves.SetSeason(null, false);
        }

        // The season's sky, blended over the player's palette.
        Color SeasonDay(Color day, float metres)
        {
            if (_backdrop == null || _backdrop.PaletteStops.Length == 0) return day;
            return Color.Lerp(day, PaletteSampler.Sample(_backdrop.PaletteStops, metres), _backdrop.PaletteBlend);
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
            if (_season == null || _season.ResetShowSeconds <= 0f || _backdrop != _season) return 0f;
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

        // Seasons doc, Easter: two eggs hidden in each main-game run while Easter's achievements count (the Daily Haunt and
        // the modes get the looks but no eggs). Any other run clears them.
        void PlantEggs()
        {
            if (_eggHunt == null) return;
            // A forced Easter (development builds, the recording APK) hides eggs too, to see the hunt; only the real event counts them.
            if (SeasonalOn(UkCalendar.Date(SeasonNowUtc())).Easter != null || ForcedSeason == SeasonIds.Easter) _eggHunt.Plant(_course, _run.RunIndex);
            else _eggHunt.Clear();
        }

        /// <summary>The egg hunt's view (tests).</summary>
        public EggHuntView EggHunt => _eggHunt;

        // A main-game run, on today's date (seasons doc rule 6: the server's when it has told us, the device's otherwise).
        void CountSeasonalRun(GhostRecord nemesisBefore)
        {
            SeasonalContext s = SeasonalOn(UkCalendar.Date(SeasonNowUtc()));
            int eggs = _eggHunt != null ? _eggHunt.Found : 0;
            if (_eggHunt != null) _eggHunt.Clear();
            if (!s.Any) return;
            bool byNemesis = _run.Cause == DeathCause.Ghost && _run.Killer != null && _run.Killer == nemesisBefore;
            foreach (string id in AchievementRules.OnSeasonalRunEnded(_progress, _run, s, byNemesis)) SeasonalEarned(id);
            foreach (string id in AchievementRules.OnEggsFound(_progress, s, eggs)) SeasonalEarned(id);
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
            if (!_sessionLive || _season == null || _seasonLogged == _season.Id || _season.Id == SeasonIds.Graveyard) return;
            _seasonLogged = _season.Id;
            Log(AnalyticsEvents.SeasonActive, ("season", _season.Id), ("forced", ForcedSeason != null));
        }

        // seasonal_skin_equipped: a seasonal character, trail or gravestone put on (the Christmas Pack included).
        void LogSeasonalEquip()
        {
            if (_catalog == null) return;
            var e = _save.Cosmetics.Equipped;
            foreach (var (type, id) in new[] { (CosmeticType.Character, e.Character), (CosmeticType.Trail, e.Trail), (CosmeticType.Grave, e.Grave), (CosmeticType.World, e.World) })
            {
                if (_equipLogged.TryGetValue(type, out string had) && had == id) continue;
                bool first = !_equipLogged.ContainsKey(type);
                _equipLogged[type] = id;
                CosmeticItem item = _catalog.Find(id);
                if (first || item == null) continue;
                // A user revision: which worlds players wear (bought, earned in a mode, or the normal one).
                if (type == CosmeticType.World)
                {
                    Log(AnalyticsEvents.WorldEquipped, ("world", id), ("bought", item.PurchaseOnly));
                    continue;
                }
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
                if (t != null) order.Add(t.Id);   // the Graveyard's look too, to see it outside the mode
            int next = (order.IndexOf(ForcedSeason) + 1) % order.Count;
            ForceSeason(order[next]);
            return SeasonLabel();
        }

        string SeasonLabel() => ForcedSeason == null
            ? $"Dev: season auto ({(_season != null ? _season.Id : NoSeason)})"
            : $"Dev: season {ForcedSeason}";

        /// <summary>The forced-season button shows in development builds and in the recording APK.</summary>
        static bool SeasonDevSetting => DevTools;
    }
}
