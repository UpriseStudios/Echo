using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Echo.Core.Achievements;
using Echo.Game.Bootstrap;
using Echo.Game.Persistence;
using Echo.Services.PlayGames;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Google Play Games (Play's Level Up guidelines; StoreListing/PlayGames/README.md): signs in once the services
    /// settle (after the privacy choice), mirrors achievements, sends Game Stats after each finished run, and keeps the
    /// save in one Saved Game. A cloud save that is further along (CloudSave.CloudIsAhead) is offered on the title,
    /// never mid-run; taking it reloads the game on it, keeping the running services. Where Play Games isn't
    /// available nothing here does anything.
    /// </summary>
    public sealed partial class EchoGame
    {
        const float CloudSaveEverySeconds = 60f;

        /// <summary>Test hook: the Play Games service the next game starts with.</summary>
        public static IPlayGamesService PlayGamesOverride;

        // Services carried over a reload onto a restored cloud save, so the SDKs aren't started twice.
        static EchoServices s_carriedServices;

        bool _servicesCarried;
        bool _cloudChecked;
        float _lastCloudSave = -1000f;
        SaveData _cloudOffer;
        readonly Dictionary<string, int> _playReported = new Dictionary<string, int>();

        IPlayGamesService PlayGames => _services.PlayGames;

        /// <summary>Tests: the cloud save waiting to be offered on the title.</summary>
        public bool CloudSaveOffered => _cloudOffer != null;

        EchoServices CreateServices()
        {
            EchoServices services = s_carriedServices ?? EchoServices.CreateMocks();
            _servicesCarried = s_carriedServices != null;
            s_carriedServices = null;
            if (PlayGamesOverride != null) services.UsePlayGames(PlayGamesOverride);
            return services;
        }

        async Task StartPlayGames(bool interactive)
        {
            if (!PlayGames.Available) return;
            if (!PlayGames.SignedIn && !await PlayGames.SignInAsync(interactive)) return;
            if (this == null) return;
            SyncPlayAchievements();
            if (_progress.Stats.MaxDistance > 0) PlayGames.RecordBestDistance(_progress.Stats.MaxDistance);
            PlayGames.FlushEvents();
            if (_settings != null) RefreshPlayGamesButton();
            if (!_cloudChecked) await CheckCloudSave();
        }

        // Everything earned or counted so far, once a session, then only what has risen.
        void SyncPlayAchievements()
        {
            if (!PlayGames.SignedIn) return;
            foreach (AchievementDef def in AchievementCatalog.All)
            {
                int progress = _progress.Achievements.Contains(def.Id) ? def.Target
                    : _progress.AchievementProgress.TryGetValue(def.Id, out int p) ? p : 0;
                ReportPlayAchievement(def.Id, progress, def.Target);
            }
        }

        void ReportPlayAchievement(string id, int progress, int target)
        {
            if (progress <= 0 || (_playReported.TryGetValue(id, out int sent) && sent >= progress)) return;
            if (!PlayGames.SignedIn) return;
            _playReported[id] = progress;
            PlayGames.ReportAchievement(id, progress, target);
        }

        void OnAchievementEarned(string id)
        {
            AchievementDef def = AchievementCatalog.Find(id);
            if (def != null) ReportPlayAchievement(id, def.Target, def.Target);
        }

        /// <summary>A finished run (main game, Daily Haunt, modes; never a replay): Game Stats, achievement steps, the cloud.</summary>
        void PlayGamesRunEnded()
        {
            if (!PlayGames.SignedIn || _run == null) return;
            PlayGames.RecordRun(_run.Distance, _run.Score, _run.GhostsPassed, _run.NearMisses);
            PlayGames.RecordBestDistance(_progress.Stats.MaxDistance);
            PlayGames.FlushEvents();
            SyncPlayAchievements();
            _ = UploadCloudSave(false);
        }

        async Task UploadCloudSave(bool now)
        {
            // Never before the cloud's save has been looked at: it may be the one to keep.
            if (!PlayGames.SignedIn || !_cloudChecked || _cloudOffer != null) return;
            if (!now && Time.realtimeSinceStartup - _lastCloudSave < CloudSaveEverySeconds) return;
            _lastCloudSave = Time.realtimeSinceStartup;
            SaveMapper.Write(_progress, _save, _contentVersion);
            await PlayGames.WriteCloudSaveAsync(CloudSave.Pack(_save), CloudSave.Describe(_save), _save.Stats?.TotalRuns ?? 0);
        }

        async Task CheckCloudSave()
        {
            SaveData cloud = CloudSave.Unpack(await PlayGames.LoadCloudSaveAsync());
            if (this == null) return;
            _cloudChecked = true;
            SaveMapper.Write(_progress, _save, _contentVersion);
            if (cloud != null && CloudSave.CloudIsAhead(_save, cloud))
            {
                _cloudOffer = cloud;
                OfferCloudSave();
            }
            else _ = UploadCloudSave(true);
        }

        // On the title only, and not over another card.
        void OfferCloudSave()
        {
            if (_cloudOffer == null || State != GameState.Title || ModalOpen) return;
            SaveData cloud = _cloudOffer;
            Ask("PROGRESS FOUND", $"Google Play Games has a save with {CloudSave.Describe(cloud)}.\nThis device has {CloudSave.Describe(_save)}.\nUse the Play Games save?",
                "Use it", "Keep this", () => RestoreCloudSave(cloud), () =>
                {
                    _cloudOffer = null;
                    _ = UploadCloudSave(true);
                });
        }

        // This device's save is kept beside it, then the game starts again on the cloud's.
        void RestoreCloudSave(SaveData cloud)
        {
            _cloudOffer = null;
            try
            {
                if (File.Exists(_saves.FilePath)) File.Copy(_saves.FilePath, _saves.FilePath + ".device.bak", true);
                _saves.Save(cloud);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Echo] restoring the Play Games save failed: {e}");
                _hud.QueueToast("Couldn't restore the save");
                return;
            }
            s_carriedServices = _services;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void RefreshPlayGamesButton() =>
            _settings.SetPlayGames(PlayGames.Available, PlayGames.SignedIn ? "Achievements" : "Play Games");

        void OnPlayGamesClicked()
        {
            if (PlayGames.SignedIn)
            {
                PlayGames.ShowAchievements();
                return;
            }
            _ = SignInFromSettings();
        }

        async Task SignInFromSettings()
        {
            await StartPlayGames(interactive: true);
            if (this == null) return;
            RefreshPlayGamesButton();
            if (!PlayGames.SignedIn) _hud.QueueToast("Play Games sign-in didn't work");
        }
    }
}
