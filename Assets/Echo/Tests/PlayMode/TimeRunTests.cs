using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// World: Time Run, phase 1 (World Time Run doc): a run past 750 m watched back in Time Run goes through all four eras
    /// by distance, each later one through a time rift with its label, and replays exactly as it was played without Time
    /// Run; the development setting shows any era.
    /// </summary>
    public class TimeRunTests
    {
        const float Dt = 1f / 60f;
        const uint Seed = 41;
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
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        static ChunkLibrary Library() => ChunkLibrary.FromJson(
            Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json").Select(File.ReadAllText));

        IEnumerator Title()
        {
            var cfg = SimConfig.Default;
            _savePath = Path.Combine(Path.GetTempPath(), "echo_timerun_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(Library(), cfg),
                Seed = Seed,
                Best = 100000,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        static string Print(RunSimulation r) =>
            $"score {r.Score} distance {r.Distance} tick {r.Tick} maxX {r.MaxX} near {r.NearMisses} cause {r.Cause} player {r.Player.X},{r.Player.Y}";

        [UnityTest, Timeout(600000)]
        public IEnumerator ARunThroughAllFourEras_RiftsAtEachBoundary_AndReplaysTheSame()
        {
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Assert.AreEqual(-1, game.EraShown, "Time Run is off");

            // A run past 800 m with ordinary jumps, played without Time Run and kept as a reel.
            var cfg = SimConfig.Default;
            int speed = cfg.SpeedForPct(game.Run.SpeedPct);
            Schedule run = SurvivingSchedule.Build(new Course(Library(), game.Run.Course.Seed), speed, 9000, new Random(7), cfg);
            game.enabled = false;
            bool tapping = true;
            game.ScriptedTaps = t => tapping && run.TapAt(t);
            game.ScriptedHeld = t => tapping && run.HeldAt(t);
            game.TickFrame(Dt, tapped: true);
            for (int f = 0; f < 60000 && game.State == GameState.Running && game.Run.Distance < 820; f++)
            {
                game.TickFrame(Dt, false);
                if (f % 60 == 0) yield return null;
            }
            Assert.GreaterOrEqual(game.Run.Distance, 760, "the run reaches the Far Future");
            tapping = false;
            for (int f = 0; f < 60000 && game.State != GameState.DeathScreen; f++)
            {
                game.TickFrame(Dt, false);
                if (f % 60 == 0) yield return null;
            }
            float revealed = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < revealed) { game.TickFrame(Dt, false); yield return null; }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            string played = Print(game.Run);
            Click("DeathScreen/SaveReel");
            Click("DeathScreen/Menu");
            game.ScriptedTaps = null;
            game.ScriptedHeld = null;

            // Watched back in Time Run: Dino Land first, then a rift and a label at each era.
            game.PreviewTimeRun = true;
            Click("Title/Reels");
            Click("Reels/Slot0/Watch");
            Assert.AreEqual(GameState.Replay, game.State);
            Assert.AreEqual(0, game.EraShown, "Dino Land at the start");
            Assert.AreEqual(SeasonIds.EraDino, game.ActiveBackdrop.Id);
            int era = 0, rifts = 0;
            var labels = new System.Collections.Generic.List<string>();
            var hudEra = Find("Hud/Era").GetComponent<TMP_Text>();
            for (int i = 0; i < 60000 && !game.ReplayEnded; i++)
            {
                game.TickFrame(Dt, false);
                if (game.EraShown != era)
                {
                    Assert.AreEqual(era + 1, game.EraShown, "eras come in order");
                    era = game.EraShown;
                    Assert.IsTrue(game.TimeRiftPlaying, $"a time rift into era {era + 1}");
                    rifts++;
                    labels.Add(hudEra.text);
                    Assert.AreEqual(era * 250, game.CurrentReplay.Run.Distance / 250 * 250, "the era changes at its boundary");
                }
                if (i % 60 == 0) yield return null;
            }
            Assert.IsTrue(game.ReplayEnded);
            Assert.AreEqual(3, rifts, "a rift into each later era");
            CollectionAssert.AreEqual(new[] { "1200 AD", "2026", "3026" }, labels);
            Assert.AreEqual(SeasonIds.EraFuture, game.ActiveBackdrop.Id);
            Assert.AreEqual(played, Print(game.CurrentReplay.Run), "Time Run is looks only: the run replays exactly as it was played");
            Click(Showing("Replay/Exit") ? "Replay/Exit" : "Replay/Back");
            if (Showing("Reels")) Click("Reels/Close");
        }

        [UnityTest]
        public IEnumerator TheDevSetting_ShowsAnyEra()
        {
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.PreviewTimeRun = true;
            game.ForceEra = 2;
            game.TickFrame(Dt, false);
            Assert.AreEqual(2, game.EraShown);
            Assert.AreEqual(SeasonIds.EraToday, game.ActiveBackdrop.Id);
            game.ForceEra = 3;
            game.TickFrame(Dt, false);
            Assert.AreEqual(SeasonIds.EraFuture, game.ActiveBackdrop.Id);
            Assert.IsFalse(game.TimeRiftPlaying, "no rift on the title");

            // Each era on screen, for review (TestResults/Screenshots/timerun_era*).
            for (int era = 0; era < 4; era++)
            {
                game.ForceEra = era;
                game.TickFrame(Dt, false);
                yield return null;
                yield return ScreenCaptureTests.Capture($"timerun_era{era + 1}");
            }
        }
    }
}
