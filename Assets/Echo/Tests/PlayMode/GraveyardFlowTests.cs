using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
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
    /// The Community Graveyard (modes doc) in the built scene, through the mock backend: from the Modes screen to its
    /// lobby (size, days left, today's board and the week's), a run against the day's community ghosts tagged with their
    /// spot's toll, its death screen and an instant retry; a run made offline kept and sent later; last week's summary
    /// shown once.
    /// </summary>
    public class GraveyardFlowTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
            _savePath = null;
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

        static string Text(string path) => Find(path).GetComponentInChildren<TMP_Text>().text;

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath ??= Path.Combine(Path.GetTempPath(), "echo_graveyard_" + System.Guid.NewGuid().ToString("N") + ".json");
            if (!File.Exists(_savePath))
                File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
                {
                    ContentVersion = ContentVersion.Compute(library, cfg),
                    Best = 777,
                    Seed = 31,
                    Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                    Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
                }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        static MockBackendService Open(EchoGame game)
        {
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.GraveyardOpen = true;
            backend.GraveyardDay = 2;
            backend.GraveyardSize = 4213;
            backend.Modes.Add(new ModeState { Id = ModeIds.Graveyard, Open = true, SecondsLeft = 400000 });
            backend.GraveyardGhosts.Add(GraveyardRules.AsGhost(new GhostRecord { RunIndex = 4, DeathTick = 700, TapTicks = new int[0], SkinId = "char_santa" }, 0, 312));
            backend.GraveyardGhosts.Add(GraveyardRules.AsGhost(new GhostRecord { RunIndex = 9, DeathTick = 900, TapTicks = new int[0] }, 1, 40));
            return backend;
        }

        IEnumerator ToTheLobby()
        {
            Click("Title/Modes");
            yield return Frames(2);
            StringAssert.StartsWith("Community Graveyard", Text("Modes/Events/Event0"));
            Click("Modes/Events/Event0");
            yield return Frames(3);
            Assert.AreEqual("Community Graveyard", Find("DailyLobby/Heading").GetComponent<TMP_Text>().text);
        }

        // The death screen follows the death after a short real-time pause, so once the run is over, wait in real time.
        IEnumerator RunToDeath(EchoGame game)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            for (int i = 0; game.State != GameState.DeathScreen && Time.realtimeSinceStartup < deadline; i++)
            {
                game.TickFrame(Dt, false);
                if (game.Run.IsDead || i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State, "the run ended");
            yield return Frames(3);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ALobbyRunAndRetry_AgainstTheCommunity()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            game.ScriptedTaps = t => false;
            yield return ToTheLobby();

            StringAssert.Contains("4,213 deaths and counting", Find("DailyLobby/Yesterday").GetComponent<TMP_Text>().text);
            StringAssert.Contains("4 days left", Find("DailyLobby/Yesterday").GetComponent<TMP_Text>().text);
            StringAssert.Contains("2 ghosts rise", Find("DailyLobby/Status").GetComponent<TMP_Text>().text);
            StringAssert.StartsWith("Day 3 of 7", Find("DailyLobby/Countdown").GetComponent<TMP_Text>().text);
            Assert.IsFalse(Showing("DailyLobby/WatchHint"), "the board toggle takes the hint's place");
            StringAssert.Contains("this week", Text("DailyLobby/BoardToggle"));
            StringAssert.Contains("Play this week's course", Text("DailyLobby/Play"));
            Assert.AreEqual(Echo.Game.Seasons.SeasonIds.Graveyard, game.ActiveSeason.Id, "a user revision: the Graveyard's own look");

            Click("DailyLobby/Play");
            yield return Frames(2);
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Graveyard && game.Run.Options.Daily);
            Assert.AreEqual(2, game.Run.GhostsAtStart, "the day's community ghosts");
            Assert.IsTrue(game.Run.StartGhosts.All(g => g.Leader && g.Name.StartsWith("This spot has claimed")));
            int firstIndex = game.Run.RunIndex;
            yield return RunToDeath(game);

            Assert.AreEqual(1, backend.GraveyardSubmissions, "sent");
            Assert.IsNotNull(game.LastGraveyardResult);
            Assert.IsTrue(game.LastGraveyardResult.Ok, game.LastGraveyardResult.Error);
            StringAssert.Contains("Your week", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Assert.IsEmpty(game.CurrentSave.Graveyard.Pending, "nothing left waiting");

            // Tap: straight into another run, numbered on.
            yield return new WaitForSecondsRealtime(1f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return Frames(3);
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Graveyard);
            Assert.AreEqual(firstIndex + 1, game.Run.RunIndex);
            yield return RunToDeath(game);
            Assert.AreEqual(2, backend.GraveyardSubmissions);
            Assert.IsTrue(game.LastGraveyardResult.Ok, game.LastGraveyardResult.Error);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheBoards_ToggleBetweenTodayAndTheWeek()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Open(game);
            game.ScriptedTaps = t => false;
            yield return ToTheLobby();
            Click("DailyLobby/Play");
            yield return Frames(2);
            yield return RunToDeath(game);
            int distance = game.LastGraveyardResult.Distance;

            Assert.AreEqual(Echo.Game.Seasons.SeasonIds.Graveyard, game.ActiveSeason.Id, "still in the run's death screen");
            Click("DeathScreen/Menu");
            yield return Frames(2);
            Assert.AreNotEqual(Echo.Game.Seasons.SeasonIds.Graveyard, game.ActiveSeason?.Id, "back to the season's look at the title");
            yield return ToTheLobby();
            StringAssert.Contains($"{distance} m", Text("DailyLobby/Row0"));
            StringAssert.Contains("for this week", Text("DailyLobby/BoardToggle"));
            Click("DailyLobby/BoardToggle");
            yield return null;
            StringAssert.Contains("Showing this week", Text("DailyLobby/BoardToggle"));
            StringAssert.Contains("MockRunner", Text("DailyLobby/Row0"));

            // A user revision: the runs can be watched like the Daily Haunt's (your own needs no ad).
            StringAssert.Contains("Tap a run to watch it", Find("DailyLobby/Status").GetComponent<TMP_Text>().text);
            Click("DailyLobby/Row0");
            yield return Frames(2);
            Assert.AreEqual(GameState.Replay, game.State);
            Assert.IsTrue(game.CurrentReplay.Reel.Options.Graveyard, "the whole run, as played");
            StringAssert.Contains("best of the week", Text("Replay/Label"));
            Click("Replay/Exit");
            yield return Frames(3);
            Assert.AreEqual("Community Graveyard", Find("DailyLobby/Heading").GetComponent<TMP_Text>().text, "back in the Graveyard's lobby");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AnOfflineRun_WaitsAndIsSentLater()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            game.ScriptedTaps = t => false;
            yield return ToTheLobby();
            Click("DailyLobby/Play");
            yield return Frames(2);
            backend.Offline = true;
            yield return RunToDeath(game);
            Assert.AreEqual(1, game.CurrentSave.Graveyard.Pending.Count, "kept");
            StringAssert.Contains("back online", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);

            backend.Offline = false;
            Click("DeathScreen/Menu");
            yield return Frames(2);
            yield return ToTheLobby();
            yield return Frames(3);
            Assert.IsEmpty(game.CurrentSave.Graveyard.Pending, "sent when the lobby opened");
            Assert.AreEqual(1, backend.GraveyardSubmissions);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LastWeeksSummary_ShowsOnce()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            backend.GraveyardLastWeek = new GraveyardWeekSummary
            {
                Week = "2027-01-18", DeadliestMetres = 250, DeadliestDeaths = 312, Size = 4213, Champion = "BraveGhost#1111", ChampionTotal = 9100,
                Rank = 7, Total = 3200, DaysPlayed = 7,
            };
            yield return ToTheLobby();
            Assert.IsTrue(Showing("Challenge"), "the summary card");
            string body = string.Join("\n", Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            StringAssert.Contains("LAST WEEK'S GRAVEYARD", body);
            StringAssert.Contains("250-300 m", body);
            StringAssert.Contains("BraveGhost#1111", body);
            StringAssert.Contains("Gravekeeper", body);
            Click("Challenge/Primary");
            yield return null;

            Click("DailyLobby/Back");
            yield return Frames(2);
            yield return ToTheLobby();
            Assert.IsFalse(Showing("Challenge"), "once");
            Assert.AreEqual("2027-01-18", game.CurrentSave.Graveyard.SummarySeen);
        }
    }
}
