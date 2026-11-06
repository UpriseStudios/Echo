using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Echo.Game.Dev;
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
    /// Run; the development setting shows any era. Phase 2: hazards, graves and ghosts take the era they stand in, and the
    /// Nemesis stays red and horned in every era. Phase 3: each era's art style (8-bit, 16-bit, 32-bit, HD), full and
    /// reduced effects, and the benchmark's frame-rate report.
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
            Schedule run = SurvivingSchedule.Build(new Course(Library(), game.Run.Course.Seed), speed, 16000, new Random(7), cfg);
            game.enabled = false;
            bool tapping = true;
            game.ScriptedTaps = t => tapping && run.TapAt(t);
            game.ScriptedHeld = t => tapping && run.HeldAt(t);
            game.TickFrame(Dt, tapped: true);
            for (int f = 0; f < 60000 && game.State == GameState.Running && game.Run.Distance < 1260; f++)
            {
                game.TickFrame(Dt, false);
                if (f % 60 == 0) yield return null;
            }
            Assert.GreaterOrEqual(game.Run.Distance, 1210, "the run reaches the Far Far Future");
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
            int era = 0, rifts = 0, preloads = 0;
            var labels = new System.Collections.Generic.List<string>();
            var hudEra = Find("Hud/Era").GetComponent<TMP_Text>();
            SeasonTheme[] eras = game.ActiveWorld.Eras;
            for (int i = 0; i < 90000 && !game.ReplayEnded; i++)
            {
                game.TickFrame(Dt, false);
                // The next era is warmed from 240 m into each one, and only then.
                int metres = game.CurrentReplay.Run.Distance, into = metres - era * 300;
                if (era < eras.Length - 1 && into >= 241 && into < 299)
                {
                    Assert.AreSame(eras[era + 1], game.EraPreloading, $"era {era + 2} warming at {metres} m");
                    if (into == 241) preloads++;
                }
                else if (into > 1 && into < 239) Assert.IsNull(game.EraPreloading, $"nothing to warm at {metres} m");
                if (game.EraShown != era)
                {
                    Assert.AreEqual(era + 1, game.EraShown, "eras come in order");
                    era = game.EraShown;
                    Assert.IsTrue(game.TimeRiftPlaying, $"a time rift into era {era + 1}");
                    rifts++;
                    labels.Add(hudEra.text);
                    Assert.AreEqual(era * 300, game.CurrentReplay.Run.Distance / 300 * 300, "the era changes at its boundary");
                    // For review: the rift into each era, then the era itself a moment later.
                    yield return ScreenCaptureTests.Capture($"timerun_rift_era{era + 1}");
                    for (int f = 0; f < 90; f++) game.TickFrame(Dt, false);
                    yield return ScreenCaptureTests.Capture($"timerun_run_era{era + 1}");
                }
                if (i % 60 == 0) yield return null;
            }
            Assert.Greater(preloads, 0, "the next era was warmed before its boundary");
            Assert.IsTrue(game.ReplayEnded);
            Assert.AreEqual(4, rifts, "a rift into each later era");
            CollectionAssert.AreEqual(new[] { "1200 AD", "2026", "3026", "10,000 AD" }, labels);
            Assert.AreEqual(SeasonIds.EraFarFarFuture, game.ActiveBackdrop.Id);
            Assert.AreEqual(played, Print(game.CurrentReplay.Run), "Time Run is looks only: the run replays exactly as it was played");
            Click(Showing("Replay/Exit") ? "Replay/Exit" : "Replay/Back");
            if (Showing("Reels")) Click("Reels/Close");
        }

        static bool Era(Sprite s, string era) => s != null && s.name.StartsWith(era + "_");

        [UnityTest]
        public IEnumerator EachThingTakesTheEraItStandsIn_AndTheNemesisStaysRedAndHorned()
        {
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var course = Object.FindAnyObjectByType<CourseView>();
            var graves = Object.FindAnyObjectByType<GraveViews>();
            var ghosts = Object.FindAnyObjectByType<GhostViews>();
            Sprite spike = course.SpikeAt(150), block = course.BlockAt(150), grave = graves.GraveAt(150), horns = ghosts.HornsAt(150);
            Color ghost = ghosts.BodyAt(150, false);
            Assert.IsNull(course.GapAt(150), "outside Time Run a gap is a plain hole");

            game.PreviewTimeRun = true;
            game.TickFrame(Dt, false);
            // Each era by position, 300 m each: Dino Land, then Medieval, Today, the Far Future and the Far Far Future.
            Assert.IsTrue(Era(course.SpikeAt(150), SeasonIds.EraDino), "a rock spike");
            Assert.IsTrue(Era(course.BlockAt(150), SeasonIds.EraDino), "a cliff");
            Assert.IsTrue(Era(course.GapAt(150), SeasonIds.EraDino), "lava");
            Assert.AreSame(spike, course.SpikeAt(450), "Medieval keeps the spike");
            Assert.IsTrue(Era(course.BlockAt(450), SeasonIds.EraMedieval), "a portcullis");
            Assert.IsTrue(Era(course.GapAt(450), SeasonIds.EraMedieval), "a moat");
            Assert.IsTrue(Era(course.SpikeAt(750), SeasonIds.EraToday), "a traffic cone");
            Assert.IsTrue(Era(course.GapAt(750), SeasonIds.EraToday), "between rooftops");
            Assert.IsTrue(Era(course.SpikeAt(1050), SeasonIds.EraFuture), "an energy spike");
            Assert.IsTrue(Era(course.BlockAt(1050), SeasonIds.EraFuture), "a hover block");
            Assert.IsTrue(Era(course.SpikeAt(299), SeasonIds.EraDino), "the boundary is at 300 m");
            Assert.IsFalse(Era(course.SpikeAt(300), SeasonIds.EraDino));

            Assert.IsTrue(Era(graves.GraveAt(150), SeasonIds.EraDino), "a stone slab");
            Assert.AreSame(grave, graves.GraveAt(450), "Medieval keeps the tombstone");
            Assert.IsTrue(Era(graves.GraveAt(750), SeasonIds.EraToday));
            Assert.IsTrue(Era(graves.GraveAt(1050), SeasonIds.EraFuture), "a holographic memorial");

            Assert.AreNotEqual(ghost, ghosts.BodyAt(150, false), "fossil ghosts");
            Assert.AreEqual(ghost, ghosts.BodyAt(450, false), "standard ghosts in Medieval");
            Assert.AreNotEqual(ghost, ghosts.BodyAt(1050, false), "hologram ghosts");
            // The Nemesis is always red and horned: the fossil's skull is red art (SeasonArtTests), otherwise the red horns.
            Assert.IsTrue(Era(ghosts.HornsAt(150), SeasonIds.EraDino), "the fossil Nemesis's horned skull");
            foreach (int x in new[] { 450, 750, 1050 }) Assert.AreSame(horns, ghosts.HornsAt(x), $"horns at {x} m");
            foreach (int x in new[] { 150, 1050 })
            {
                Color n = ghosts.BodyAt(x, true);
                Assert.Greater(n.r, Mathf.Max(n.g, n.b) + 0.3f, $"a red Nemesis at {x} m");
            }

            // A forced era shows everywhere; off again, everything is as it was.
            game.ForceEra = 3;
            Assert.IsTrue(Era(course.SpikeAt(150), SeasonIds.EraFuture));
            Assert.IsTrue(Era(graves.GraveAt(150), SeasonIds.EraFuture));
            game.ForceEra = null;
            game.PreviewTimeRun = false;
            Assert.AreSame(spike, course.SpikeAt(150));
            Assert.AreSame(block, course.BlockAt(150));
            Assert.AreSame(grave, graves.GraveAt(150));
            Assert.AreEqual(ghost, ghosts.BodyAt(1050, false));
            Assert.IsNull(course.GapAt(1050));
        }

        [UnityTest]
        public IEnumerator EachEraHasItsArtStyle_AndTheFarFutureBreaksOutOfPixelArt()
        {
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var background = Object.FindAnyObjectByType<BackgroundView>();
            var runner = Object.FindAnyObjectByType<RunnerView>();
            var course = Object.FindAnyObjectByType<CourseView>();
            var lights = Object.FindAnyObjectByType<FutureLightsView>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            game.PreviewTimeRun = true;

            // 8-bit Dino Land: its own chunky scenery, still on the pixel grid.
            game.ForceEra = 0;
            game.TickFrame(Dt, false);
            Assert.IsTrue(Era(game.ActiveBackdrop.FarLayer, SeasonIds.EraDino));
            Assert.IsFalse(game.HighDefinitionShown);
            Assert.IsTrue(rig.PixelPerfect, "pixel-perfect");
            Assert.AreEqual(0, background.ExtraLayersShown);

            // 32-bit Today: the neon city with two extra parallax layers.
            game.ForceEra = 2;
            game.TickFrame(Dt, false);
            Assert.AreEqual(2, background.ExtraLayersShown, "a distant skyline and a mid-distance block");
            Assert.IsFalse(game.HighDefinitionShown);

            // The Far Future in HD: full resolution, lit, bloom and lights, the runner's glow and trail, the neon grid floor.
            game.ForceEra = 3;
            game.TickFrame(Dt, false);
            Assert.IsTrue(game.HighDefinitionShown);
            Assert.IsFalse(rig.PixelPerfect, "the pixel-perfect camera steps aside");
            Assert.IsTrue(background.Lit, "the HD backdrop takes the 2D lights");
            Assert.AreEqual(3, background.ExtraLayersShown, "its sky, its moons and a distant skyline");
            Assert.IsTrue(runner.Future, "glow, rim light and afterimages");
            Assert.IsTrue(Era(course.FloorAt(1050), SeasonIds.EraFuture), "the neon grid floor");
            Assert.Greater(SimUnits.SnapPerUnit, CameraRig.PixelsPerUnit, "things glide rather than step");
            Assert.IsTrue(game.FullEffects);
            Assert.AreEqual(6, lights.LightsOn, "a global light and five neon pools");
            Assert.IsTrue(rig.GlowOn, "bloom");

            // Reduced effects (a weaker phone): no bloom, one light, the same world.
            game.WeakDeviceOverride = true;
            Assert.IsFalse(game.FullEffects);
            Assert.AreEqual(1, lights.LightsOn);
            Assert.IsFalse(rig.GlowOn);
            Assert.IsTrue(game.HighDefinitionShown, "still HD");
            yield return ScreenCaptureTests.Capture("timerun_era4_reduced");
            game.WeakDeviceOverride = false;
            Assert.AreEqual(6, lights.LightsOn);

            // Back to pixel art, and out of Time Run.
            game.ForceEra = 1;
            game.TickFrame(Dt, false);
            Assert.IsFalse(game.HighDefinitionShown);
            Assert.IsTrue(rig.PixelPerfect);
            Assert.AreEqual(0, lights.LightsOn);
            Assert.IsFalse(runner.Future);
            Assert.AreEqual(CameraRig.PixelsPerUnit, SimUnits.SnapPerUnit);
            game.ForceEra = null;
            game.PreviewTimeRun = false;
            game.TickFrame(Dt, false);
            Assert.IsFalse(game.HighDefinitionShown);
            Assert.AreEqual(0, background.ExtraLayersShown);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheBenchmark_ReportsTheFrameRateInEachEra()
        {
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Click("Title/Settings");
            Assert.AreEqual(GameState.Menu, game.State);
            var row = GameObject.Find("Canvas/SafeArea/Settings").GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Benchmark");
            Assert.IsTrue(row.gameObject.activeInHierarchy, "a development row");
            row.onClick.Invoke();
            Assert.IsTrue(game.BenchmarkRunning);
            float give = Time.realtimeSinceStartup + 500f;
            bool sawFuture = false;
            while (game.BenchmarkRunning && Time.realtimeSinceStartup < give)
            {
                sawFuture |= game.HighDefinitionShown && game.State == GameState.Replay;
                yield return null;
            }
            Assert.IsFalse(game.BenchmarkRunning, "the benchmark finished");
            Assert.IsTrue(sawFuture, "the Far Future was measured in HD");
            string report = game.BenchmarkReport;
            Debug.Log("[Benchmark] editor report:\n" + report);
            StringAssert.Contains("30 ghosts in the graveyard", report);
            foreach (string era in new[] { "Dino Land (8-bit)", "Medieval (16-bit)", "Today (32-bit)", "Far Future (HD)", "Far Future (HD, reduced effects)" })
                StringAssert.IsMatch(System.Text.RegularExpressions.Regex.Escape(era) + @": \d+ fps, 1% low \d+", report);
            Assert.IsTrue(Showing("Challenge"), "the report shows on a card");
            Assert.IsFalse(game.PreviewTimeRun, "the world is put back");
            Assert.IsTrue(game.FullEffects, "and its effects");
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
            for (int era = 0; era < 5; era++)
            {
                game.ForceEra = era;
                game.TickFrame(Dt, false);
                yield return null;
                yield return ScreenCaptureTests.Capture($"timerun_era{era + 1}");
            }
        }
    }
}
