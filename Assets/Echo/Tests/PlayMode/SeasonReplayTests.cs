using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Services.Backend;
using Echo.Services.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Seasons doc ground rules 1 to 4 in the built scene: recorded runs replay exactly the same with every season
    /// forced as with none (the views draw them but never write the run); the effects layer sorts behind the floor and
    /// everything on it; the season's ghost accessory, grave and death line show; and Remote Config's schedule is taken
    /// when the flags are read, then kept for the next session.
    /// </summary>
    public class SeasonReplayTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak", _savePath + ".seasons.json" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static GameObject Find(string path) => GameObject.Find("Canvas/SafeArea/" + path);

        static void Click(string path)
        {
            var go = Find(path);
            Assert.IsNotNull(go, path);
            Assert.IsTrue(go.activeInHierarchy, path + " is showing");
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        static IEnumerator Frames(EchoGame game, System.Func<bool> until, int max, string what)
        {
            for (int i = 0; i < max && !until(); i++)
            {
                game.TickFrame(Dt, false);
                if (i % 20 == 0) yield return null;
            }
            Assert.IsTrue(until(), "timed out waiting for " + what);
        }

        IEnumerator LoadGame()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath ??= Path.Combine(Path.GetTempPath(), "echo_seasons_" + System.Guid.NewGuid().ToString("N") + ".json");
            if (!File.Exists(_savePath))
            {
                var save = new SaveData
                {
                    ContentVersion = ContentVersion.Compute(library, cfg),
                    Seed = 31,
                    Best = 100000,   // never a new best, so every run leaves a ghost for the next
                    Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                    Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
                };
                File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            }
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return Frames(game, () => game.State == GameState.Title, 600, "title");
        }

        // Everything a run's outcome is made of: if a view had written to the run, something here would differ.
        static string Print(RunSimulation r)
        {
            var sb = new StringBuilder();
            sb.Append($"score {r.Score} distance {r.Distance} tick {r.Tick} maxX {r.MaxX} near {r.NearMisses} cause {r.Cause} ");
            sb.Append($"killer {(r.Killer != null ? r.Killer.RunIndex : -1)} player {r.Player.X},{r.Player.Y},{r.Player.Vy} ghosts {r.Ghosts.Count}:");
            for (int i = 0; i < r.Ghosts.Count; i++)
            {
                var g = r.Ghosts.ReplayAt(i);
                sb.Append($" {g.Phase}@{g.State.X},{g.State.Y}");
            }
            return sb.ToString();
        }

        static readonly System.Func<int, bool>[] Patterns =
        {
            t => false,
            t => t % 97 == 40,
            t => t % 61 == 20,
            t => t % 83 == 10 || t % 83 == 30,
        };

        [UnityTest]
        public IEnumerator EveryRecordedRun_ReplaysTheSame_InEverySeason()
        {
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.ForceSeason(EchoGame.NoSeason);
            Assert.IsNull(game.ActiveSeason);

            // Runs with no season, each saved as a reel; every one leaves a ghost for the next to meet.
            var prints = new string[Patterns.Length];
            for (int k = 0; k < Patterns.Length; k++)
            {
                game.ScriptedTaps = Patterns[k];
                game.InjectTap();
                game.TickFrame(Dt, false);
                yield return Frames(game, () => game.State == GameState.DeathScreen, 30000, "the death screen");
                prints[k] = Print(game.Run);
                Click("DeathScreen/SaveReel");
                Click("DeathScreen/Menu");
            }
            game.ScriptedTaps = null;
            Assert.AreEqual(Patterns.Length, game.CurrentSave.Reels.Count);

            var seasons = new[] { EchoGame.NoSeason }.Concat(game.SeasonThemes.Select(t => t.Id)).ToArray();
            CollectionAssert.Contains(seasons, SeasonIds.Test);
            game.PreviewResetShow = true;   // the New Year show too, so the fireworks run under the replays
            foreach (string id in seasons)
            {
                game.ForceSeason(id);
                Assert.AreEqual(id == EchoGame.NoSeason ? null : id, game.ActiveSeason != null ? game.ActiveSeason.Id : null);
                for (int k = 0; k < Patterns.Length; k++)
                {
                    Click("Title/Reels");
                    Click($"Reels/Slot{k}/Watch");
                    Assert.AreEqual(GameState.Replay, game.State);
                    bool checkedLayers = false;
                    for (int i = 0; i < 30000 && !game.ReplayEnded; i++)
                    {
                        game.TickFrame(Dt, false);
                        if (i == 30 && id == SeasonIds.Test) checkedLayers = CheckTestSeasonShows(game, k > 0);
                        if (i % 20 == 0) yield return null;
                    }
                    Assert.IsTrue(game.ReplayEnded, $"{id} reel {k} ended");
                    Assert.AreEqual(prints[k], Print(game.CurrentReplay.Run), $"season {id}, reel {k}: the run must replay exactly as it was played");
                    if (id == SeasonIds.Test && k > 0) Assert.IsTrue(checkedLayers, "the test season was checked on screen");
                    Click(Showing("Replay/Exit") ? "Replay/Exit" : "Replay/Back");
                    if (Showing("Reels")) Click("Reels/Close");
                    Assert.AreEqual(GameState.Title, game.State);
                }
            }
        }

        // Ground rule 3, and the test theme's overrides on screen.
        static bool CheckTestSeasonShows(EchoGame game, bool ghostsExpected)
        {
            var effects = GameObject.Find("World/SeasonEffects").GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            Assert.IsNotEmpty(effects, "the effects layer is drawing");
            int effectsTop = effects.Max(r => r.sortingOrder);
            foreach (string layer in new[] { "World/Course", "World/Graves", "World/Ghosts", "World/Player" })
            {
                var drawn = GameObject.Find(layer).GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
                if (drawn.Length == 0) continue;
                Assert.Less(effectsTop, drawn.Min(r => r.sortingOrder), $"seasonal effects must draw behind {layer}");
            }
            Assert.AreEqual(SeasonEffectsView.SortingOrder, effectsTop);
            if (ghostsExpected)
            {
                var hats = GameObject.Find("World/Ghosts").GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => r.enabled && r.name.StartsWith("Accessory")).ToArray();
                Assert.IsNotEmpty(hats, "ghosts wear the season's accessory");
                Assert.AreSame(game.ActiveSeason.GhostAccessory, hats[0].sprite);
            }
            return true;
        }

        [UnityTest]
        public IEnumerator TheTestSeason_ShowsItsGraveAndDeathLine()
        {
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.ForceSeason(SeasonIds.Test);
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return Frames(game, () => game.State == GameState.DeathScreen, 30000, "the death screen");
            StringAssert.Contains(game.ActiveSeason.DeathLine, Find("DeathScreen").GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Select(t => t.text).Aggregate("", (a, b) => a + "\n" + b));
            var graves = GameObject.Find("World/Graves").GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            Assert.IsNotEmpty(graves, "the fresh grave");
            Assert.AreSame(game.ActiveSeason.Grave, graves[0].sprite);
            // The headstone may not have risen yet, so it is found inactive too.
            var headstone = Find("DeathScreen").transform.Find("Headstone").GetComponent<UnityEngine.UI.Image>();
            SeasonTheme theme = game.ActiveSeason;
            Assert.AreSame(theme.Headstone, headstone.sprite);

            game.ForceSeason(EchoGame.NoSeason);
            Assert.IsNull(game.ActiveSeason);
            Assert.AreNotSame(theme.Headstone, headstone.sprite);
            Assert.IsNotNull(headstone.sprite, "back to the stone");
        }

        [UnityTest]
        public IEnumerator TheSeasonEvents_AreLogged()
        {
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var analytics = (MockAnalytics)game.Services.Analytics;
            game.ForceSeason(SeasonIds.Christmas);
            game.ForceSeason(EchoGame.NoSeason);
            game.ForceSeason(SeasonIds.Christmas);
            Assert.AreEqual(1, analytics.Events.Count(e => e == "season_active season=christmas forced=True"), "once a session per season");

            // A seasonal skin put on in the Skins screen.
            game.CurrentSave.Cosmetics.Owned.Add("char_santa");
            Click("Title/Skins");
            var santa = Find("Skins").GetComponentsInChildren<UnityEngine.UI.Button>()
                .First(b => b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("Santa"));
            santa.onClick.Invoke();
            CollectionAssert.Contains(analytics.Events, "seasonal_skin_equipped skin=char_santa");
            Click("Skins/Close");
        }

        [UnityTest]
        public IEnumerator RemoteConfigsSchedule_IsTakenWithTheFlags_AndKeptForNextTime()
        {
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Assert.IsNull(game.ForcedSeason);
            Assert.IsTrue(game.ActiveSeason == null || game.ActiveSeason.Id != SeasonIds.Test, "the built-in schedule never has the test theme");
            var backend = (MockBackendService)game.Services.Backend;
            const string allYear = @"{ ""seasons"": [ { ""id"": ""test"", ""start"": ""01-01"", ""end"": ""12-31"" } ] }";
            backend.Flags = new BackendFlags(true, true, allYear);

            // The Daily Haunt reads the flags (after sign-in); the schedule comes with them.
            Click("Title/Daily");
            yield return null;
            Assert.AreEqual(SeasonIds.Test, game.ActiveSeason != null ? game.ActiveSeason.Id : null, "Remote Config's schedule");
            Assert.IsTrue(File.Exists(_savePath + ".seasons.json"), "kept beside the save");
            Click("DailyLobby/Back");

            // Next session, before anything signs in: the kept copy.
            yield return LoadGame();
            game = Object.FindAnyObjectByType<EchoGame>();
            Assert.AreEqual(SeasonIds.Test, game.ActiveSeason != null ? game.ActiveSeason.Id : null, "the kept schedule");
        }
    }
}
