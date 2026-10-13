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
            if (_services.Backend is MockBackendService mock) mock.LocalReplay = LocalReplayCheck;
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

        /// <summary>The last server check's answer, for tests.</summary>
        public ReplayCheckResult LastServerCheck { get; private set; }
    }
}
