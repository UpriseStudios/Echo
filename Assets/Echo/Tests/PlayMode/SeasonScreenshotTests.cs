using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Seasons phase 4: every season's look with a 30-ghost graveyard, for checking readability (seasons doc: effects stay
    /// behind the action, checked with 30 ghosts). The same genuine run against 30 genuine failed runs (a December
    /// graveyard of Santas, Rudolphs and the rest) is played in each season and captured, to
    /// TestResults/Screenshots/season30_*.png (real play) and season30crowd_*.png (the worst case, a crowd on screen).
    /// The effects layer is checked to sort behind them all.
    /// </summary>
    public class SeasonScreenshotTests
    {
        const float Dt = 1f / 60f;
        const uint Seed = 4242;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak", _savePath + ".seasons.json" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static readonly MarketingClipSpec Graveyard = new MarketingClipSpec
        {
            Id = "seasons", Seed = Seed, Ghosts = 30, NemesisKills = 6, SurviveSeconds = 25, Ending = "any",
            GhostSkins = new[] { "char_santa", "char_rudolph", "char_hooded", "char_elf", "char_snowman", "char_pumpkin", "char_knight", "char_gingerbread" },
        };

        static readonly string[] Seasons =
            { EchoGame.NoSeason, SeasonIds.Halloween, SeasonIds.BonfireNight, SeasonIds.Christmas, SeasonIds.NewYear, SeasonIds.Valentines };

        /// <summary>
        /// Real play: genuine ghosts spread out along the course and are met a few at a time (rarely more than 3 on screen),
        /// so this graveyard is shot at the start, a third of the way and two thirds of the way.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator EverySeason_ARealRunThroughA30GhostGraveyard()
        {
            var cfg = SimConfig.Default;
            ChunkLibrary library = Library();
            var rng = new Random((int)Seed);
            int speed = cfg.SpeedForPct(100);
            List<GhostRecord> ghosts = MarketingClips.Ghosts(Graveyard, library, cfg, speed, rng);
            Schedule player = MarketingClips.PlayerRun(Graveyard, ghosts, library, cfg, speed, rng);
            int last = 0;
            yield return Measure(library, cfg, ghosts, player, (tick, seen, end) => last = end);
            Assert.Greater(last, 600, "the run gets well into the course");
            yield return Shoot("season30", library, cfg, ghosts, player, new[] { 60, last / 3, last * 2 / 3 }, -1);
        }

        /// <summary>
        /// The worst case for readability: a crowd. These ghosts' runs are cut very short (made at a crawl), so the whole
        /// graveyard bunches up near the start and about half of it is on screen at once.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator EverySeason_ACrowdOfGhostsOnScreen()
        {
            var cfg = SimConfig.Default;
            ChunkLibrary library = Library();
            var rng = new Random((int)Seed);
            List<GhostRecord> ghosts = MarketingClips.Ghosts(Graveyard, library, cfg, 100, rng);
            Schedule player = MarketingClips.PlayerRun(Graveyard, ghosts, library, cfg, 100, rng);
            int shot = 0, most = -1;
            yield return Measure(library, cfg, ghosts, player, (tick, seen, end) =>
            {
                if (seen <= most) return;
                most = seen;
                shot = tick;
            });
            Debug.Log($"[SeasonScreenshots] crowd: {most} ghosts in view at tick {shot}");
            Assert.GreaterOrEqual(most, 12, "a crowd on screen");
            yield return Shoot("season30crowd", library, cfg, ghosts, player, new[] { shot }, most);
        }

        static ChunkLibrary Library() =>
            ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json").Select(File.ReadAllText));

        // Plays the run once with no season, reporting each tick's ghosts in view (seasons never change the run).
        IEnumerator Measure(ChunkLibrary library, SimConfig cfg, List<GhostRecord> ghosts, Schedule player, Action<int, int, int> seen)
        {
            yield return Load(library, cfg, ghosts);
            var game = Object.FindAnyObjectByType<EchoGame>();
            Start(game, player, EchoGame.NoSeason);
            while (game.State == GameState.Running && game.Run.Tick < Graveyard.SurviveSeconds * 60)
            {
                game.TickFrame(Dt, false);
                seen(game.Run.Tick, GhostsInView(), game.Run.Tick);
                if (game.Run.Tick % 60 == 0) yield return null;
            }
        }

        // The same run in every season, captured at each tick; expectInView checks the first shot (-1 skips).
        IEnumerator Shoot(string name, ChunkLibrary library, SimConfig cfg, List<GhostRecord> ghosts, Schedule player, int[] ticks, int expectInView)
        {
            foreach (string id in Seasons)
            {
                yield return Load(library, cfg, ghosts);
                var game = Object.FindAnyObjectByType<EchoGame>();
                Start(game, player, id);
                Assert.AreEqual(id == EchoGame.NoSeason ? null : id, game.ActiveSeason != null ? game.ActiveSeason.Id : null);
                for (int k = 0; k < ticks.Length; k++)
                {
                    while (game.State == GameState.Running && game.Run.Tick < ticks[k])
                    {
                        game.TickFrame(Dt, false);
                        if (game.Run.Tick % 60 == 0) yield return null;
                    }
                    Assert.AreEqual(GameState.Running, game.State, $"{id}: the run is the same in every season");
                    for (int f = 0; f < 3; f++) yield return null;
                    if (k == 0 && expectInView >= 0) Assert.AreEqual(expectInView, GhostsInView(), $"{id}: the same ghosts on screen");
                    EffectsBehindTheAction(id);
                    string season = id == EchoGame.NoSeason ? "none" : id;
                    yield return ScreenCaptureTests.Capture(ticks.Length > 1 ? $"{name}_{season}_{k + 1}" : $"{name}_{season}");
                }
            }
        }

        IEnumerator Load(ChunkLibrary library, SimConfig cfg, List<GhostRecord> ghosts)
        {
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 999_999,
                Seed = Seed,
                NextRunIndex = ghosts.Count + 1,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            };
            foreach (GhostRecord r in ghosts)
                save.Ghosts.Add(new GhostSaveData
                {
                    RunIndex = r.RunIndex, TapTicks = r.TapTicks, ReleaseTicks = r.ReleaseTicks, ReviveTicks = r.ReviveTicks,
                    DeathTick = r.DeathTick, SpeedPct = 100, Kills = r.Kills, LastKillRun = r.LastKillRun, SkinId = r.SkinId, DeathX = r.DeathX,
                });
            _savePath ??= Path.Combine(Path.GetTempPath(), "echo_seasonshots_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        static void Start(EchoGame game, Schedule player, string season)
        {
            game.enabled = false;
            game.ForceSeason(season);
            game.PreviewResetShow = season == SeasonIds.NewYear;
            game.ScriptedTaps = t => player.TapAt(t);
            game.ScriptedHeld = t => player.HeldAt(t);
            game.TickFrame(Dt, tapped: true);
            Assert.AreEqual(GameState.Running, game.State);
        }

        // Ghost figures drawn inside the camera's view.
        static int GhostsInView()
        {
            var cam = Camera.main;
            return GameObject.Find("World/Ghosts").GetComponentsInChildren<SpriteRenderer>()
                .Count(r => r.enabled && r.name.StartsWith("Ghost") && InView(cam, r.transform.position));
        }

        static bool InView(Camera cam, Vector3 at)
        {
            Vector3 v = cam.WorldToViewportPoint(at);
            return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        // Seasons doc ground rule 3, with the whole graveyard on screen.
        static void EffectsBehindTheAction(string season)
        {
            var effects = GameObject.Find("World/SeasonEffects").GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            if (effects.Length == 0) return;
            int top = effects.Max(r => r.sortingOrder);
            foreach (string layer in new[] { "World/Course", "World/Graves", "World/Ghosts", "World/Player" })
            {
                var drawn = GameObject.Find(layer).GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
                if (drawn.Length > 0) Assert.Less(top, drawn.Min(r => r.sortingOrder), $"{season}: effects must draw behind {layer}");
            }
            Assert.AreEqual(SeasonEffectsView.SortingOrder, top);
        }
    }
}
