using Echo.Core.Courses;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Services.Backend;
using Echo.Services.Mocks;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// The backend from the game's side (version 2.0). Phase 2 has the development server check: the newest reel is
    /// replayed on the device and by the Cloud Code module, and both distances shown, proving they agree. In the
    /// editor the mock backend replays locally with the same code.
    /// </summary>
    public sealed partial class EchoGame
    {
        void WireBackend()
        {
            if (_services.Backend is MockBackendService mock)
            {
                mock.LocalReplay = LocalReplayCheck;
                mock.ContentVersion = _contentVersion;
            }
        }

        // What the server does, done here: decode, refuse other content, replay with the game's code.
        ReplayCheckResult LocalReplayCheck(string packet)
        {
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return ReplayCheckResult.Failed("malformed");
            var result = new ReplayCheckResult { ServerContentVersion = _contentVersion, ClaimedDistance = reel.Distance };
            if (reel.ContentVersion != _contentVersion)
            {
                result.Error = "content-version";
                return result;
            }
            ReplayResult replay = RunReplay.Replay(reel, new Course(_library, reel.Seed), _cfg);
            result.Accepted = true;
            result.Distance = replay.Distance;
            result.Score = replay.Score;
            result.DeathTick = replay.DeathTick;
            result.Finished = replay.Finished;
            return result;
        }

        /// <summary>Development builds: the newest reel, replayed here and on the server. Also a test hook.</summary>
        public async void OnServerCheck()
        {
            if (_progress.Reels.Count == 0)
            {
                _settings.SetStatus("Server check: save a reel first (Save reel on the death screen), then try again.");
                return;
            }
            Reel reel = _progress.Reels[_progress.Reels.Count - 1];
            if (!reel.PlayableOn(_contentVersion))
            {
                _settings.SetStatus("Server check: the newest reel is from an older version. Save a new one.");
                return;
            }
            ReplayResult device = RunReplay.Replay(reel, new Course(_library, reel.Seed), _cfg);
            _settings.SetStatus($"Server check: run #{reel.RunIndex} replays to {device.Distance} m here. Asking the server...");
            ReplayCheckResult server = await _services.Backend.ReplayCheckAsync(ReelPacket.Encode(reel));
            LastServerCheck = server;
            string verdict = !server.Accepted
                ? $"the server refused it: {server.Error}" + (server.Error == "content-version" ? $" (server {server.ServerContentVersion}, game {_contentVersion})" : "")
                : server.Distance == device.Distance && server.Score == device.Score && server.DeathTick == device.DeathTick
                    ? $"server {server.Distance} m. They match."
                    : $"server {server.Distance} m. MISMATCH (score {device.Score} vs {server.Score}, death tick {device.DeathTick} vs {server.DeathTick})";
            _settings.SetStatus($"Server check, run #{reel.RunIndex}: device {device.Distance} m, {verdict}\n{_services.Backend.Status}");
            Debug.Log("[Backend] " + verdict);
        }

        // ---- Privacy policy: Settings › Delete online data ----

        void AskDeleteOnlineData() =>
            Ask("DELETE ONLINE DATA?",
                "This deletes your Daily Haunt runs, your place on today's board, the challenges you've sent and your online ID from our server. Your game on this phone stays as it is.",
                "Delete", "Cancel", DeleteOnlineData, null);

        /// <summary>Also a test hook.</summary>
        public async void DeleteOnlineData()
        {
            if (!_servicesSettled)
            {
                Ask("ONE MOMENT", "Echo is still starting up. Try again in a moment.", "OK", null, null, null);
                return;
            }
            DeleteOnlineDataResult r = await _services.Backend.DeleteOnlineDataAsync();
            if (this == null) return;
            LastDeleteOnlineData = r;
            if (!r.Ok)
            {
                Ask("COULDN'T DELETE", r.Error == "offline"
                    ? "Deleting needs a connection. Check yours and try again."
                    : "Something went wrong. Try again, or email hello@uprisestudios.co.uk.", "OK", null, null, null);
                return;
            }
            // Nothing of the old online ID is sent again: unsent daily runs and today's fetched day go too.
            _save.Daily.Pending.Clear();
            ForgetModes();
            ForgetDailyDay();
            Save();
            Ask("ONLINE DATA DELETED", "Your Daily Haunt runs, board entry and sent challenges are gone, and links to your challenges no longer open.",
                "OK", null, null, null);
        }

        /// <summary>The last Delete online data answer, for tests.</summary>
        public DeleteOnlineDataResult LastDeleteOnlineData { get; private set; }

        /// <summary>The last server check's answer, for tests.</summary>
        public ReplayCheckResult LastServerCheck { get; private set; }
    }
}
