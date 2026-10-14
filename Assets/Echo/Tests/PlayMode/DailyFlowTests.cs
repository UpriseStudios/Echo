using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Services.Ads;
using Echo.Services.Backend;
using Echo.Services.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// The Daily Haunt (version 2.0) in the built scene, through the mock backend: the lobby, a run against a leader
    /// ghost with no power-ups, the daily death screen, your own daily ghosts, the offline queue, and the main
    /// game's progress left alone.
    /// </summary>
    public class DailyFlowTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static GameObject Find(string path) => GameObject.Find("Canvas/SafeArea/" + path);

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        static void Click(string path)
        {
            Assert.IsTrue(Showing(path), path + " is showing");
            Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static string Text(string path) => Find(path).GetComponent<TMP_Text>().text;

        static IEnumerator ToDeathScreen(EchoGame game)
        {
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLobby_ADailyRun_ItsVerdict_YourOwnGhosts_AndTheOfflineQueue()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_daily_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 777,
                Seed = 31,
                NextRunIndex = 3,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
                PowerUps = new PowerUpsSaveData { Shield = 2, Phase = 1, ExtraLife = 1 },
            };
            save.Ghosts.Add(new GhostSaveData { RunIndex = 1, DeathTick = 120, SpeedPct = 100 });
            save.Ghosts.Add(new GhostSaveData { RunIndex = 2, DeathTick = 150, SpeedPct = 100 });
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.SecondsLeft = 250;   // the board closes in a little over 4 minutes
            backend.Leaders.Add((DailyRules.AsLeader(new GhostRecord { RunIndex = 1, DeathTick = 900, TapTicks = new int[0] }, 1, "SwiftGhost#4821"), 812));
            StringAssert.StartsWith("Daily Haunt", Find("Title/Daily").GetComponentInChildren<TMP_Text>().text);

            // The lobby: the top of today's board.
            Click("Title/Daily");
            yield return null;
            Assert.AreEqual(GameState.Menu, game.State);
            StringAssert.Contains("SwiftGhost#4821", Text("DailyLobby/Row0"));
            StringAssert.Contains("812 m", Text("DailyLobby/Row0"));
            game.TickFrame(1.1f, false);
            StringAssert.Contains("Board closes in 4:", Text("DailyLobby/Countdown"));

            // A daily run: the leader ghost, and no power-ups at all.
            game.ScriptedTaps = t => false;
            Click("DailyLobby/Play");
            yield return null;
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Daily);
            Assert.AreEqual(1, game.Run.GhostsAtStart, "today's leader");
            Assert.IsFalse(game.Run.ExtraLifeHeld, "the Extra 1-UP stays at home");
            Assert.IsFalse(Showing("Hud/PowerShield") || Showing("Hud/PowerPhase"));
            game.TickFrame(1.1f, false);
            Assert.IsTrue(Showing("Hud/BoardCloses"), "the last 5 minutes, on the HUD too");
            StringAssert.StartsWith("Board closes in 4:", Text("Hud/BoardCloses"));

            yield return ToDeathScreen(game);
            Assert.AreEqual("DISTANCE", Text("DeathScreen/ScoreLabel"));
            Assert.IsFalse(Showing("DeathScreen/Continue"));
            Assert.IsFalse(Showing("DeathScreen/Banish"));
            Assert.IsNotNull(game.LastDailyResult, "sent and answered");
            Assert.IsTrue(game.LastDailyResult.Ok, game.LastDailyResult.Error);
            Assert.AreEqual("Next: SwiftGhost#4821 · 812 m", Text("DeathScreen/Message"));
            Assert.AreEqual(1, backend.Submissions);
            Assert.AreEqual(1, game.Progress.Daily.DaysPlayed);

            // Tap to try again: the same course. The first run was today's best, so no ghost of yours yet; the second
            // isn't, so it becomes one, and the third run meets it.
            yield return new WaitForSecondsRealtime(0.4f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return null;
            Assert.AreEqual(GameState.Running, game.State);
            Assert.AreEqual(1, game.Run.GhostsAtStart);
            yield return ToDeathScreen(game);
            yield return new WaitForSecondsRealtime(0.4f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return null;
            Assert.AreEqual(2, game.Run.GhostsAtStart, "the leader and your own failed attempt");

            // Offline: the run waits in the save, then goes when the connection is back.
            backend.Offline = true;
            yield return ToDeathScreen(game);
            StringAssert.Contains("Saving your run", Text("DeathScreen/Message"));
            Assert.AreEqual(1, game.CurrentSave.Daily.Pending.Count);
            Click("DeathScreen/Menu");
            backend.Offline = false;
            Click("Title/Daily");
            yield return null;
            Assert.AreEqual(0, game.CurrentSave.Daily.Pending.Count, "sent when the lobby opened");
            Assert.AreEqual(3, backend.Submissions, "two sent at once, the offline one later");

            // Switched off on the server mid-run (Cloud Code checks the switch too): the run is kept, not lost, and
            // goes once the switch is back on; while it's off, the lobby rests.
            Click("DailyLobby/Play");
            yield return null;
            backend.ServerDailyOff = true;
            yield return ToDeathScreen(game);
            Assert.AreEqual(1, game.CurrentSave.Daily.Pending.Count, "kept for later");
            Click("DeathScreen/Menu");
            Click("Title/Daily");
            yield return null;
            StringAssert.Contains("resting", Text("DailyLobby/Status"));
            Assert.AreEqual(1, game.CurrentSave.Daily.Pending.Count);
            Click("DailyLobby/Back");
            backend.ServerDailyOff = false;
            Click("Title/Daily");
            yield return null;
            Assert.AreEqual(0, game.CurrentSave.Daily.Pending.Count, "sent once the switch was back on");
            Assert.AreEqual(4, backend.Submissions);
            Click("DailyLobby/Back");

            // Settings › Delete online data: asked first, then gone, with nothing of the old ID left to send.
            game.CurrentSave.Daily.Pending.Add(new PendingDailyRun { Date = "2026-10-14", LeaderVersion = "x", Packet = "x" });
            Click("Title/Settings");
            Click("Settings/DeleteOnline");
            StringAssert.Contains("DELETE ONLINE DATA", Text("Challenge/Title"));
            Click("Challenge/Primary");
            yield return null;
            Assert.AreEqual(1, backend.Deletions);
            Assert.IsTrue(game.LastDeleteOnlineData.Ok);
            Assert.AreEqual(0, game.CurrentSave.Daily.Pending.Count);
            StringAssert.Contains("DELETED", Text("Challenge/Title"));
            Click("Challenge/Primary");
            Click("Settings/Close");

            // The main game never noticed.
            Assert.AreEqual(777, game.Progress.Best);
            Assert.AreEqual(2, game.Progress.Ghosts.Count);
            Assert.AreEqual(3, game.CurrentSave.NextRunIndex);
            Assert.AreEqual(2, game.CurrentSave.PowerUps.Shield, "no power-up was used up");
        }

        [UnityTest]
        public IEnumerator YourOwnGhost_CanBeBanishedForAnAd_AndRunsOnTheBoardCanBeWatched()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_daily_banish_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 31,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
            }));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            var backend = (MockBackendService)game.Services.Backend;
            var ads = (MockAdsService)game.Services.Ads;
            backend.Flags = new BackendFlags(true, true);

            // Runs that get past the first chunks and then stop jumping: a best, then failed attempts that become your
            // own ghosts (met from chunk 2 on), until one of them ends a run. A run that ignores them soon meets one.
            Click("Title/Daily");
            yield return null;
            uint seed = (uint)backend.GetDailyAsync(null).Result.Seed;
            int speed = cfg.SpeedForPct(DailyRules.SpeedPct);
            var rng = new System.Random(5);
            Schedule schedule = null;
            game.ScriptedTaps = t => schedule != null && schedule.TapAt(t);
            game.ScriptedHeld = t => schedule == null || schedule.HeldAt(t);
            bool endedByOwnGhost = false;
            for (int run = 0; run < 10 && !endedByOwnGhost; run++)
            {
                // Each shorter than the last, so the first stays the best and the rest become your own ghosts.
                schedule = SurvivingSchedule.Build(new Course(library, seed), speed, 480 - 60 * (run % 4), rng, cfg);
                if (run == 0) Click("DailyLobby/Play");
                else
                {
                    yield return new WaitForSecondsRealtime(0.4f);
                    game.InjectTap();
                    game.TickFrame(Dt, false);
                }
                yield return null;
                yield return ToDeathScreen(game);
                endedByOwnGhost = game.Run.Killer != null && !game.Run.Killer.Leader;
            }
            game.ScriptedTaps = null;
            game.ScriptedHeld = null;
            Assert.IsTrue(endedByOwnGhost, "one of your own ghosts ended a run");
            Assert.AreEqual(DailyRules.BanishesPerDay, game.DailyBanishesLeft);

            // Banish: an ad, then the ghost is gone from your next run.
            Assert.IsTrue(Showing("DeathScreen/Banish"), "offered for your own ghost");
            StringAssert.Contains("3 left today", Find("DeathScreen/Banish").GetComponentInChildren<TMP_Text>().text);
            Click("DeathScreen/Banish");
            yield return null;
            Assert.AreEqual(AdPlacements.DailyBanish, ads.Shown.Last());
            Assert.AreEqual(1, backend.Banishes);
            Assert.AreEqual(DailyRules.BanishesPerDay - 1, game.DailyBanishesLeft);
            StringAssert.Contains("banished", Text("DeathScreen/Message"));
            Assert.IsFalse(Showing("DeathScreen/Banish"), "once per death");

            // Watching runs on the board: your own is free.
            Click("DeathScreen/Menu");
            Click("Title/Daily");
            yield return null;
            int adsBefore = ads.Shown.Count;
            Assert.IsTrue(Showing("DailyLobby/WatchHint"));
            Click("DailyLobby/Row0");
            yield return null;
            Assert.AreEqual(adsBefore, ads.Shown.Count, "your own run needs no ad");
            Assert.AreEqual(GameState.Replay, game.State);
            Assert.IsNotNull(game.CurrentReplay);
            Assert.IsTrue(game.CurrentReplay.Reel.Options.Daily);
            StringAssert.Contains("#1", Text("Replay/Label"));
            Click("Replay/Exit");
            yield return null;
            Assert.IsTrue(Showing("DailyLobby/Play"), "back in the lobby");

            // Someone else's run: an ad first.
            backend.Leaders.Add((DailyRules.AsLeader(new GhostRecord { RunIndex = 1, DeathTick = 900, TapTicks = new int[0] }, 1, "SwiftGhost#4821"), 99999));
            Click("DailyLobby/Back");
            Click("Title/Daily");
            yield return null;
            Click("DailyLobby/Row0");
            yield return null;
            Assert.AreEqual(AdPlacements.WatchRun, ads.Shown.Last());
            Assert.AreEqual(GameState.Replay, game.State);
            Assert.AreEqual("SwiftGhost#4821", game.LastDailyRunWatched.Name);
            StringAssert.Contains("SwiftGhost#4821", Text("Replay/Label"));
            Click("Replay/Exit");
            yield return null;
            Assert.IsTrue(Showing("DailyLobby/Play"));
        }
    }
}
