#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine;

namespace Echo.Services.PlayGames
{
    /// <summary>
    /// The Google Play Games plugin (v2) behind IPlayGamesService (rule 4). Created after consent, and only once
    /// Play Games is configured: touching PlayGamesPlatform starts the Play Games SDK, which needs the APP_ID that
    /// Echo > Play Games > Import Resources puts in the manifest. Every callback arrives on the main thread.
    /// </summary>
    public sealed class PlayGamesService : IPlayGamesService
    {
        const string SaveName = "echo_save";

        readonly Dictionary<string, string> _ids = new Dictionary<string, string>();
        string _status = "not signed in";

        public PlayGamesService(EchoServiceConfig config)
        {
            foreach (PlayGamesAchievement a in config.PlayGamesAchievements)
                if (!string.IsNullOrEmpty(a.EchoId) && !string.IsNullOrEmpty(a.PlayId)) _ids[a.EchoId] = a.PlayId;
        }

        /// <summary>Configured in this build: the import tool has set the app id.</summary>
        public static bool Configured => !string.IsNullOrEmpty(PlayGamesSettings.LoadInstance()?.AppId);

        public bool Available => true;
        public bool SignedIn => PlayGamesPlatform.Instance.IsAuthenticated();
        public string Status => _status;

        // Signed in on this device before: from then on, each launch signs in again.
        const string SignedInBeforeKey = "echo_play_games_signed_in";

        // A user report: after the first sign-in, every launch still needed Settings > Play Games. Echo starts the SDK
        // only after the privacy choice (PlayGamesManifest removes its launch-time provider), and that provider is what
        // runs Play Games' own automatic sign-in, so the quiet check finds no one signed in. Once a player has signed in
        // here, a failed quiet check is followed by a real sign-in, which for an account that has already agreed
        // completes by itself (Play's welcome banner only); a player who never signed in still chooses to in Settings.
        public async Task<bool> SignInAsync(bool interactive)
        {
            bool ok = await AuthenticateAsync(interactive);
            if (!ok && !interactive && PlayerPrefs.GetInt(SignedInBeforeKey, 0) == 1) ok = await AuthenticateAsync(true);
            if (ok)
            {
                PlayerPrefs.SetInt(SignedInBeforeKey, 1);
                PlayerPrefs.Save();
            }
            return ok;
        }

        Task<bool> AuthenticateAsync(bool interactive)
        {
            var done = new TaskCompletionSource<bool>();
            Action<SignInStatus> signedIn = status =>
            {
                _status = status == SignInStatus.Success ? "signed in" : "not signed in (" + status + ")";
                done.TrySetResult(status == SignInStatus.Success);
            };
            try
            {
                if (interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(signedIn);
                else PlayGamesPlatform.Instance.Authenticate(signedIn);
            }
            catch (Exception e)
            {
                _status = "sign-in failed: " + e.Message;
                Debug.LogWarning("[PlayGames] " + _status);
                done.TrySetResult(false);
            }
            return done.Task;
        }

        public Task<string> GetServerAuthCodeAsync()
        {
            if (!SignedIn || string.IsNullOrEmpty(PlayGamesSettings.LoadInstance()?.WebClientId)) return Task.FromResult<string>(null);
            var done = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => done.TrySetResult(string.IsNullOrEmpty(code) ? null : code));
            return done.Task;
        }

        public void ReportAchievement(string achievementId, int progress, int target)
        {
            if (!SignedIn || progress <= 0 || !_ids.TryGetValue(achievementId, out string playId)) return;
            if (target > 1) PlayGamesPlatform.Instance.SetStepsAtLeast(playId, Math.Min(progress, target), _ => { });
            else PlayGamesPlatform.Instance.UnlockAchievement(playId);
        }

        // StoreListing/PlayGames/game_stats_events.csv declares these events and properties.
        public void RecordRun(int distance, int score, int ghostsPassed, int nearMisses)
        {
            if (!SignedIn) return;
            PlayGamesPlatform.Instance.RecordEvent(new PlayerGameEvent.Builder("runEnded")
                .AddProperty("distance", distance)
                .AddProperty("score", score)
                .AddProperty("ghostsPassed", ghostsPassed)
                .AddProperty("nearMisses", nearMisses)
                .Build());
        }

        public void RecordBestDistance(int metres)
        {
            if (!SignedIn) return;
            PlayGamesPlatform.Instance.RecordEvent(new PlayerGameEvent.Builder("progressUpdate").AddProperty("currentProgress", metres).Build());
        }

        public void FlushEvents()
        {
            if (SignedIn) PlayGamesPlatform.Instance.RequestEventsUpload();
        }

        public async Task<byte[]> LoadCloudSaveAsync()
        {
            ISavedGameMetadata game = await OpenAsync();
            if (game == null) return null;
            var done = new TaskCompletionSource<byte[]>();
            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(game, (status, data) =>
                done.TrySetResult(status == SavedGameRequestStatus.Success && data != null && data.Length > 0 ? data : null));
            return await done.Task;
        }

        public async Task<bool> WriteCloudSaveAsync(byte[] data, string description, int runsPlayed)
        {
            ISavedGameMetadata game = await OpenAsync();
            if (game == null) return false;
            SavedGameMetadataUpdate update = new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription(description)
                .WithUpdatedPlayedTime(TimeSpan.FromMinutes(runsPlayed))
                .Build();
            var done = new TaskCompletionSource<bool>();
            PlayGamesPlatform.Instance.SavedGame.CommitUpdate(game, update, data, (status, _) => done.TrySetResult(status == SavedGameRequestStatus.Success));
            return await done.Task;
        }

        // The one Saved Game. Between two devices' writes, Play keeps the one with the longest play time, which
        // WriteCloudSaveAsync sets to runs played: the same rule the game uses (CloudSave.CloudIsAhead).
        Task<ISavedGameMetadata> OpenAsync()
        {
            if (!SignedIn) return Task.FromResult<ISavedGameMetadata>(null);
            var done = new TaskCompletionSource<ISavedGameMetadata>();
            try
            {
                PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(SaveName, DataSource.ReadNetworkOnly,
                    ConflictResolutionStrategy.UseLongestPlaytime, (status, game) =>
                    {
                        if (status != SavedGameRequestStatus.Success) _status = "Saved Games: " + status;
                        done.TrySetResult(status == SavedGameRequestStatus.Success ? game : null);
                    });
            }
            catch (Exception e)
            {
                _status = "Saved Games failed: " + e.Message;
                done.TrySetResult(null);
            }
            return done.Task;
        }

        public void ShowAchievements()
        {
            if (SignedIn) PlayGamesPlatform.Instance.ShowAchievementsUI();
        }
    }
}
#endif
