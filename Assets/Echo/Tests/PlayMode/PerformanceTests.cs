using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// tech spec 17/18: a 60-second scripted run with 30 ghosts, zero GC allocations per frame while Running,
    /// and the sim + render cost per frame. The test drives EchoGame.TickFrame itself at exactly one tick
    /// per frame, so the run takes seconds instead of a minute and every frame is measured.
    /// </summary>
    public class PerformanceTests
    {
        const int Ghosts = 30;
        const int RunTicks = 60 * 60;
        const int WarmupFrames = 120;
        const uint Seed = 424242;

        // Ghost i fell on its own tick FirstDeath + i * DeathSpacing, so graves (and the overtakes just before
        // them) are spread over the whole 60 s run, as a real cycle of attempts leaves them.
        const int FirstDeath = 250;
        const int DeathSpacing = 110;
        const int GhostOwnTicks = FirstDeath + Ghosts * DeathSpacing + 60;

        string _savePath;

        static Aabb?[][] GhostBoxesPerTick(List<GhostRecord> records, Course course, int speed, int ticks, in SimConfig cfg)
        {
            var ghosts = new GhostManager();
            ghosts.Begin(records, cfg, course, speed);
            var boxes = new Aabb?[ticks][];
            for (int t = 0; t < ticks; t++)
            {
                ghosts.Step(course, t, cfg);
                boxes[t] = new Aabb?[ghosts.Count];
                for (int i = 0; i < ghosts.Count; i++)
                    if (ghosts.IsLethal(i)) boxes[t][i] = Runner.GhostBox(ghosts.ReplayAt(i).State, cfg);
            }
            return boxes;
        }

        static int[] TicksBefore(int[] ticks, int end) => ticks.Where(t => t < end).ToArray();

        static bool HitsGhost(Aabb player, Aabb?[] ghosts)
        {
            foreach (Aabb? g in ghosts)
                if (g.HasValue && player.Overlaps(g.Value)) return true;
            return false;
        }

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        [UnityTest]
        public IEnumerator SixtySecondRun_With30Ghosts_AllocatesNothingPerFrame()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            var rng = new Random(7);

            // 30 ghosts from earlier runs this cycle (same speed), each on a schedule that survives long enough
            // to still be replaying when the player passes it. The player's own schedule dodges every overtake:
            // it is solved against the ghosts' boxes, computed tick by tick with the real GhostManager.
            int speed = cfg.SpeedForPct(100);
            var records = new List<GhostRecord>();
            for (int i = 1; i <= Ghosts; i++)
            {
                Schedule own = SurvivingSchedule.Build(new Course(library, Seed), speed, GhostOwnTicks, rng, cfg);
                int death = FirstDeath + i * DeathSpacing;
                records.Add(new GhostRecord { RunIndex = i, TapTicks = TicksBefore(own.TapTicks, death), ReleaseTicks = TicksBefore(own.ReleaseTicks, death), DeathTick = death, SpeedPct = 100 });
            }
            Aabb?[][] ghostBoxes = GhostBoxesPerTick(records, new Course(library, Seed), speed, RunTicks + 60, cfg);
            Schedule player = SurvivingSchedule.Build(new Course(library, Seed), speed, RunTicks + 60, rng, cfg,
                (tick, player) => tick >= cfg.GhostGraceTicks && HitsGhost(Runner.PlayerBox(player, cfg), ghostBoxes[tick]));

            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 1_000_000,
                Seed = Seed,
                NextRunIndex = Ghosts + 1,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true },
                // The Frog's rain runs through the Graveyard, so a character flourish is measured too.
                Cosmetics = new CosmeticsSaveData
                {
                    Owned = new List<string> { "runner_default", "char_hooded", "char_frog" },
                    Equipped = new EquippedSaveData { Character = "char_frog" },
                },
            };
            foreach (GhostRecord r in records)
                save.Ghosts.Add(new GhostSaveData { RunIndex = r.RunIndex, TapTicks = r.TapTicks, ReleaseTicks = r.ReleaseTicks, DeathTick = r.DeathTick, SpeedPct = r.SpeedPct });
            _savePath = Path.Combine(Path.GetTempPath(), "echo_perf_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            game.enabled = false;
            game.ScriptedTaps = player.TapAt;
            game.ScriptedHeld = player.HeldAt;

            const float dt = 1f / 60f;
            game.TickFrame(dt, tapped: true);
            Assert.AreEqual(GameState.Running, game.State);
            Assert.AreEqual(Ghosts, game.Run.GhostsAtStart);
            for (int f = 0; f < WarmupFrames; f++) game.TickFrame(dt, false);

            // The editor and test runner allocate a little every frame on their own, so a whole-frame counter cannot
            // isolate the game. Instead the managed heap is read immediately before and after each TickFrame call;
            // nothing else runs on the main thread in between. One game frame per Unity frame, uncapped.
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            // Guard: the heap reading must register a small allocation, or a pass would mean nothing.
            long probe = GC.GetTotalMemory(false);
            var junk = new byte[256][];
            for (int i = 0; i < junk.Length; i++) junk[i] = new byte[64];
            Assert.Greater(GC.GetTotalMemory(false), probe, "heap reading did not register 16 KB of small allocations");
            junk = null;
            yield return null;

            long allocated = 0;
            var watch = new Stopwatch();
            int frames = 0;
            // One second (60 game frames) per measured window, with no test code inside it, so every allocation
            // the game makes lands in a window and they add up even when each one is tiny.
            const int FramesPerWindow = 60;
            int windows = 0, allocatingWindows = 0;
            while (game.Run.Tick < RunTicks)
            {
                long before = GC.GetTotalMemory(false);
                watch.Start();
                for (int f = 0; f < FramesPerWindow; f++) game.TickFrame(dt, false);
                watch.Stop();
                long delta = GC.GetTotalMemory(false) - before;
                frames += FramesPerWindow;
                windows++;
                if (delta > 0)
                {
                    allocated += delta;
                    allocatingWindows++;
                    Debug.Log($"[Perf] {delta} bytes allocated in the second ending at tick {game.Run.Tick}");
                }
                if (game.State != GameState.Running) Assert.Fail($"player died at tick {game.Run.Tick}");
                yield return null;
            }

            // Control: the same reading around a busy-wait of the same length, with no game code at all.
            int controlWindows = 0;
            long controlBytes = 0;
            double spinMs = watch.Elapsed.TotalMilliseconds / Math.Max(1, frames);
            for (int w = 0; w < windows; w++)
            {
                long before = GC.GetTotalMemory(false);
                var spin = Stopwatch.StartNew();
                while (spin.Elapsed.TotalMilliseconds < spinMs * FramesPerWindow) { }
                long delta = GC.GetTotalMemory(false) - before;
                if (delta > 0) { controlWindows++; controlBytes += delta; }
                yield return null;
            }
            Debug.Log($"[Perf] control: {controlBytes} bytes in {controlWindows} of {windows} no-op windows");
            Debug.Log($"[Perf] lives earned {game.Run.LivesEarned}, used {game.Run.LivesUsed}, near misses {game.Run.NearMisses}");

            int replaying = Enumerable.Range(0, game.Run.Ghosts.Count).Count(i => game.Run.Ghosts.ReplayAt(i).Phase == GhostPhase.Replaying);
            double msPerFrame = watch.Elapsed.TotalMilliseconds / frames;
            Debug.Log($"[Perf] {frames} frames, {game.Run.GhostsPassed} ghosts overtaken, {replaying} still replaying, {msPerFrame:F3} ms per frame (sim + views), " +
                      $"{allocated} bytes allocated in {allocatingWindows} of {windows} one-second windows");

            // Each ghost is met just before its grave (or arrives at it as an echo), so they come and go all run.
            Assert.GreaterOrEqual(game.Run.GhostsPassed, 8, "ghosts should keep being overtaken through the run");
            Assert.AreEqual(0, controlWindows, "the control allocated, so the measurement is unreliable");
            Assert.AreEqual(0, allocated, $"{allocated} bytes allocated in {allocatingWindows} of {windows} seconds while Running");
        }
    }
}
