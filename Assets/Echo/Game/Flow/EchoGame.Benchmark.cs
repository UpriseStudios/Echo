using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Sim;
using Echo.Game.Dev;
using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// World Time Run doc, phase 3: "report frame rate on a mid-range Android phone in each era with 30 ghosts". A
    /// development tool (Settings › Dev: Time Run benchmark, in development builds and the recording APK): a graveyard of
    /// 30 genuine failed runs and a run through it (CrowdRun), watched through the replay player in each era with full
    /// effects and then the Far Future with reduced effects, timing every frame. It touches no progress, save or ghosts.
    /// </summary>
    public sealed partial class EchoGame
    {
        const int BenchmarkGhosts = 30;
        const uint BenchmarkSeed = 4242;
        const int BenchmarkFromTick = 60;
        const int BenchmarkUntilTick = 60 + 15 * 60;
        static readonly string[] BenchmarkPasses =
        {
            "Dino Land (8-bit)", "Medieval (16-bit)", "Today (32-bit)", "Far Future (HD)", "Far Far Future (3D)",
            "Dino Land (8-bit, reduced effects)", "Medieval (16-bit, reduced effects)", "Today (32-bit, reduced effects)",
            "Far Future (HD, reduced effects)", "Far Far Future (video, reduced effects)",
        };
        // Each pass's era: every era with full effects, then every era again with reduced effects (art fix phase 4: the
        // brief's "frame rates with 30 ghosts in each era, effects on and off").
        static readonly int[] BenchmarkEras = { 0, 1, 2, 3, 4, 0, 1, 2, 3, 4 };
        const int FirstReducedPass = 5;

        sealed class BenchmarkRun
        {
            public Reel Reel;
            public int Pass;
            public readonly FrameStats[] Stats = new FrameStats[BenchmarkPasses.Length];
            public bool PreviewBefore;
            public string ModeBefore = "";
            public int? EraBefore;
        }

        BenchmarkRun _benchmark;
        Task<Reel> _benchmarkBuild;
        bool _benchmarkReduced;

        /// <summary>The benchmark is building or playing (tests).</summary>
        public bool BenchmarkRunning => _benchmark != null || _benchmarkBuild != null;

        /// <summary>The last benchmark's report, also written to the log as [Benchmark] lines.</summary>
        public string BenchmarkReport { get; private set; }

        void StartBenchmark()
        {
            if (BenchmarkRunning || State != GameState.Menu) return;
            ChunkLibrary library = _library;
            SimConfig cfg = _cfg;
            string version = _contentVersion;
            Debug.Log("[Benchmark] building 30 ghosts");
            _benchmarkBuild = Task.Run(() => BuildBenchmarkReel(library, cfg, version));
        }

        static Reel BuildBenchmarkReel(ChunkLibrary library, SimConfig cfg, string version)
        {
            var rng = new System.Random((int)BenchmarkSeed);
            string[] skins = { "char_knight", "char_robot", "char_princess", "char_ninja", "char_skeleton", "char_astronaut", "char_wizard", "char_cat" };
            // Planned at a crawl and replayed at full speed, the failed runs are cut short and bunch up: a crowd on screen.
            List<GhostRecord> ghosts = CrowdRun.Ghosts(library, cfg, BenchmarkSeed, BenchmarkGhosts, 100, rng, skins, 6);
            Schedule player = CrowdRun.PlayerRun(ghosts, library, cfg, BenchmarkSeed, cfg.SpeedForPct(100), BenchmarkUntilTick + 60, rng);
            var reel = new Reel
            {
                ContentVersion = version,
                Seed = BenchmarkSeed,
                SpeedPct = 100,
                RunIndex = BenchmarkGhosts + 1,
                Run = new GhostRecord { RunIndex = BenchmarkGhosts + 1, TapTicks = player.TapTicks, ReleaseTicks = player.ReleaseTicks },
            };
            reel.Ghosts.AddRange(ghosts);
            return reel;
        }

        // Each frame: the build finishing, then the passes, each timed from its second second until 15 s or its end.
        void TickBenchmark()
        {
            if (_benchmarkBuild != null)
            {
                if (!_benchmarkBuild.IsCompleted) return;
                Task<Reel> build = _benchmarkBuild;
                _benchmarkBuild = null;
                if (build.IsFaulted || State != GameState.Menu)
                {
                    Debug.LogError($"[Benchmark] couldn't start: {build.Exception?.GetBaseException().Message ?? "left the menu"}");
                    return;
                }
                _benchmark = new BenchmarkRun { Reel = build.Result, PreviewBefore = _previewTimeRun, EraBefore = _forceEra, ModeBefore = EffectsMode };
                LogEffects("benchmark start");
                _settings.Hide();
                BeginBenchmarkPass();
                return;
            }
            if (_benchmark == null || State != GameState.Replay || _reelPlayer == null) return;
            int tick = _reelPlayer.Run.Tick;
            if (tick >= BenchmarkFromTick) _benchmark.Stats[_benchmark.Pass].Add(Time.unscaledDeltaTime, _ghostViews.Visible(_cameraRig.Left, _cameraRig.Right));
            if (_replayEnded || tick >= BenchmarkUntilTick) EndReplay();
        }

        void BeginBenchmarkPass()
        {
            BenchmarkRun b = _benchmark;
            _previewTimeRun = true;
            _forceEra = BenchmarkEras[b.Pass];
            _benchmarkReduced = b.Pass >= FirstReducedPass;
            RefreshSeason();
            ApplyEffects();
            b.Stats[b.Pass] = new FrameStats();
            // Art fix phase 1: the pass label says which quality is really running.
            StartExternalReplay(b.Reel, $"Benchmark {b.Pass + 1}/{BenchmarkPasses.Length} · {BenchmarkPasses[b.Pass]} · {(FullEffects ? "full" : "reduced")} effects",
                NextBenchmarkPass);
            LogEffects($"benchmark pass {b.Pass + 1}");
        }

        // EndReplay has entered the title: the next pass from a menu, or the report.
        void NextBenchmarkPass()
        {
            BenchmarkRun b = _benchmark;
            if (b == null) return;
            b.Pass++;
            if (b.Pass < BenchmarkPasses.Length)
            {
                OpenMenu(() => { });
                BeginBenchmarkPass();
                return;
            }
            _benchmark = null;
            _benchmarkReduced = false;
            _previewTimeRun = b.PreviewBefore;
            _forceEra = b.EraBefore;
            RefreshSeason();
            ApplyEffects();
            BenchmarkReport = Report(b);
            foreach (string line in BenchmarkReport.Split('\n')) Debug.Log("[Benchmark] " + line);
            Ask("TIME RUN BENCHMARK", BenchmarkReport, "OK", null, null, null);
        }

        static string Report(BenchmarkRun b)
        {
            var s = new StringBuilder();
            int most = 0;
            foreach (FrameStats f in b.Stats) if (f != null && f.MostGhostsOnScreen > most) most = f.MostGhostsOnScreen;
            s.Append($"{SystemInfo.deviceModel} · {Screen.width}x{Screen.height} · {SystemInfo.graphicsDeviceName}\n");
            s.Append($"{BenchmarkGhosts} ghosts in the graveyard, up to {most} on screen\n");
            s.Append($"Quality outside the benchmark: {b.ModeBefore}\n");
            for (int i = 0; i < BenchmarkPasses.Length; i++)
            {
                FrameStats f = b.Stats[i];
                if (f == null) continue;
                s.Append($"{BenchmarkPasses[i]}: {f.AverageFps:0} fps, 1% low {f.LowFps():0}");
                if (i < BenchmarkPasses.Length - 1) s.Append('\n');
            }
            return s.ToString();
        }
    }
}
