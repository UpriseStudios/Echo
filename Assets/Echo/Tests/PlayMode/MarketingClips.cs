using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Clips;
using Echo.Game.Flow;
using Echo.Game.Persistence;
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
    /// The marketing clip generator (marketing content plan): renders every clip in Marketing/clip-list.json into
    /// Marketing/Clips (ignored by git) through the game's own Vertical export. Each run is played in the real game by
    /// a bot: the ghosts are genuine failed runs, the player's run avoids them with ordinary jumps and then stops jumping,
    /// so nothing in a clip is beyond a real player. Explicit: it runs only when asked for (Marketing/render-clips.ps1).
    /// </summary>
    public class MarketingClips
    {
        const float Dt = 1f / 60f;
        const int Attempts = 8;
        string _savePath;

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            EchoGame.AutoHowToPlay = false;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
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

        [UnityTest, Explicit("Renders Marketing/Clips; run Marketing/render-clips.ps1"), Timeout(2 * 60 * 60 * 1000)]
        public IEnumerator RenderClips()
        {
            string listPath = Path.Combine(ProjectRoot, "Marketing", "clip-list.json");
            var specs = JsonConvert.DeserializeObject<List<MarketingClipSpec>>(File.ReadAllText(listPath));
            string only = Environment.GetEnvironmentVariable("ECHO_CLIP");   // render one clip by id
            // Every hook is checked before anything renders ({distance} and {ghosts} count as one word each).
            foreach (MarketingClipSpec s in specs)
                Assert.LessOrEqual(s.Hook.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length, ClipHook.MaxWords,
                    $"{s.Id}: the hook \"{s.Hook}\" is over {ClipHook.MaxWords} words");
            string outDir = Path.Combine(ProjectRoot, "Marketing", "Clips");
            Directory.CreateDirectory(outDir);
            var made = new List<string>();
            var skipped = new List<string>();
            foreach (MarketingClipSpec spec in specs)
            {
                if (!string.IsNullOrEmpty(only) && spec.Id != only) continue;
                string result = null;
                yield return Render(spec, outDir, r => result = r);
                (result != null ? made : skipped).Add(result ?? spec.Id);
            }
            Debug.Log($"[MarketingClips] made {made.Count}: {string.Join(", ", made)}" + (skipped.Count > 0 ? $"; SKIPPED {string.Join(", ", skipped)}" : ""));
            Assert.IsEmpty(skipped, "every clip in the list was made");
        }

        IEnumerator Render(MarketingClipSpec spec, string outDir, Action<string> done)
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            int speed = cfg.SpeedForPct(100);
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new Random((int)spec.Seed * 31 + attempt);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                List<GhostRecord> ghosts = Ghosts(spec, library, cfg, speed, rng);
                Debug.Log($"[MarketingClips] {spec.Id}: {ghosts.Count} ghosts in {watch.ElapsedMilliseconds} ms");
                watch.Restart();
                Schedule player = PlayerRun(spec, ghosts, library, cfg, speed, rng);
                Debug.Log($"[MarketingClips] {spec.Id}: the bot's run planned in {watch.ElapsedMilliseconds} ms");
                watch.Restart();
                yield return Play(spec, library, cfg, ghosts, player);
                Debug.Log($"[MarketingClips] {spec.Id}: played in {watch.ElapsedMilliseconds} ms");
                var game = Object.FindAnyObjectByType<EchoGame>();
                if (game.State != GameState.DeathScreen || !EndedAsAsked(spec, game))
                {
                    Debug.Log($"[MarketingClips] {spec.Id}: attempt {attempt + 1} didn't end as asked ({spec.Ending}), trying again");
                    continue;
                }
                string path = null;
                yield return Export(spec, game, outDir, p => path = p);
                done(path != null ? Path.GetFileName(path) : null);
                yield break;
            }
            done(null);
        }

        // Genuine failed runs on the course, each lasting a little longer than the last so the graves spread out.
        static List<GhostRecord> Ghosts(MarketingClipSpec spec, ChunkLibrary library, in SimConfig cfg, int speed, Random rng)
        {
            var records = new List<GhostRecord>();
            for (int i = 1; i <= spec.Ghosts; i++)
            {
                var course = new Course(library, spec.Seed);
                int survive = 90 + i * 1800 / Math.Max(1, spec.Ghosts) + rng.Next(60);
                Schedule s = SurvivingSchedule.Build(new Course(library, spec.Seed), speed, survive, rng, cfg);
                var run = new RunSimulation(course, new GhostSet(), i, 100, cfg);
                for (int t = 0; t < 20000 && !run.IsDead; t++) run.Step(s.TapAt(t), s.HeldAt(t));
                GhostRecord g = run.ToGhostRecord();
                g.SkinId = spec.GhostSkins.Length > 0 ? spec.GhostSkins[(i - 1) % spec.GhostSkins.Length] : "";
                records.Add(g);
            }
            if (spec.NemesisKills > 0 && records.Count > 0)
            {
                GhostRecord nemesis = records[records.Count - 1];
                nemesis.Kills = spec.NemesisKills;
                nemesis.LastKillRun = spec.Ghosts;
            }
            return records;
        }

        // The bot avoids every lethal ghost and hazard with ordinary jumps for a while, then stops jumping. For a Nemesis
        // ending it stops just before the Nemesis is met.
        static Schedule PlayerRun(MarketingClipSpec spec, List<GhostRecord> records, ChunkLibrary library, in SimConfig cfg, int speed, Random rng)
        {
            var course = new Course(library, spec.Seed);
            var manager = new GhostManager();
            manager.Begin(records, cfg, course, speed);
            int ticks = spec.SurviveSeconds * 60;
            if (spec.Ending == "nemesis")
            {
                int n = records.Count - 1;
                if (n >= 0 && !manager.ReplayAt(n).Harmless) ticks = Math.Max(60, manager.ReplayAt(n).MeetTick - 25);
            }
            SimConfig c = cfg;   // lambdas can't use an in parameter
            var boxes = new List<Aabb?[]>();
            for (int t = 0; t < ticks + 1; t++)
            {
                manager.Step(course, t, c);
                boxes.Add(Enumerable.Range(0, records.Count).Select(i => manager.IsLethal(i) ? Runner.GhostBox(manager.ReplayAt(i).State, c) : (Aabb?)null).ToArray());
            }
            return SurvivingSchedule.Build(new Course(library, spec.Seed), speed, ticks, rng, cfg,
                (tick, p) => tick >= c.GhostGraceTicks && tick < boxes.Count && boxes[tick].Any(b => b.HasValue && Runner.PlayerBox(p, c).Overlaps(b.Value)));
        }

        IEnumerator Play(MarketingClipSpec spec, ChunkLibrary library, SimConfig cfg, List<GhostRecord> ghosts, Schedule player)
        {
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 999_999,   // no new best: the graveyard stays, no photo freeze
                Seed = spec.Seed,
                NextRunIndex = ghosts.Count + 1,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
                Cosmetics = new CosmeticsSaveData
                {
                    Owned = new List<string> { "runner_default", "char_hooded", spec.Skin },
                    Equipped = new EquippedSaveData { Character = spec.Skin },
                },
            };
            foreach (GhostRecord r in ghosts)
                save.Ghosts.Add(new GhostSaveData
                {
                    RunIndex = r.RunIndex, TapTicks = r.TapTicks, ReleaseTicks = r.ReleaseTicks, ReviveTicks = r.ReviveTicks,
                    DeathTick = r.DeathTick, SpeedPct = 100, Kills = r.Kills, LastKillRun = r.LastKillRun, SkinId = r.SkinId, DeathX = r.DeathX,
                });
            _savePath = Path.Combine(Path.GetTempPath(), "echo_marketing_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;
            EchoGame.AutoHowToPlay = false;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            game.enabled = false;
            game.ScriptedTaps = t => player.TapAt(t);
            game.ScriptedHeld = t => player.HeldAt(t);
            game.TickFrame(Dt, tapped: true);
            for (int i = 0; i < 60 * 60 * 3 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 60 == 0) yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                game.TickFrame(Dt, false);
                yield return null;
            }
        }

        static bool EndedAsAsked(MarketingClipSpec spec, EchoGame game)
        {
            RunSimulation run = game.Run;
            switch (spec.Ending)
            {
                case "nemesis":
                    return run.Killer != null && run.Killer.Kills >= NemesisRules.MinKills && run.Killer.RunIndex == spec.Ghosts;
                case "bounty":
                    return run.BountyClaimed;
                default:
                    return true;
            }
        }

        IEnumerator Export(MarketingClipSpec spec, EchoGame game, string outDir, Action<string> done)
        {
            int distance = game.Run.Distance, ghosts = game.Run.GhostsAtStart;
            string hook = spec.Hook.Replace("{distance}", distance.ToString()).Replace("{ghosts}", ghosts.ToString());
            // The plan's rule: a hook is at most 8 words. One that's longer is an error in the list, not trimmed silently.
            int words = hook.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.LessOrEqual(words, ClipHook.MaxWords, $"{spec.Id}: the hook has {words} words; the most is {ClipHook.MaxWords}");
            Click("DeathScreen/SaveReel");
            Click("DeathScreen/Menu");
            yield return null;
            Click("Title/Reels");
            yield return null;
            game.OpenClipEditorFor(game.Progress.Reels.Count - 1);
            game.SetClipFormat(ClipFormat.Vertical);
            Find("ClipEditor/VerticalOptions/Hook").GetComponent<TMPro.TMP_InputField>().text = hook;
            if (!spec.EndCard) Click("ClipEditor/VerticalOptions/EndCard");
            int end = ClipRange.Length(game.Progress.Reels[game.Progress.Reels.Count - 1].Run.DeathTick);
            game.SetClipRange(end - spec.Seconds * ClipRange.TicksPerSecond, end);
            Click("ClipEditor/Make");
            float started = Time.realtimeSinceStartup, until = started + 30 * 60f, nextNote = started + 30f;
            while ((game.MakingClip || game.LastClipPath == null) && Time.realtimeSinceStartup < until)
            {
                game.TickFrame(Dt, false);
                if (Time.realtimeSinceStartup >= nextNote)
                {
                    nextNote += 30f;
                    Debug.Log($"[MarketingClips] {spec.Id}: recording, tick {game.Run.Tick}, stage {game.ClipStage}");
                }
                yield return null;
            }
            Debug.Log($"[MarketingClips] {spec.Id}: export took {Time.realtimeSinceStartup - started:0} s");
            if (game.LastClipPath == null)
            {
                done(null);
                yield break;
            }
            string target = Path.Combine(outDir, spec.Id + ".mp4");
            File.Copy(game.LastClipPath, target, overwrite: true);
            Debug.Log($"[MarketingClips] {spec.Id}.mp4: {new FileInfo(target).Length / 1024} KB, {distance} m, {ghosts} ghosts, {game.ClipRange.Seconds:0.0} s");
            Click("ClipEditor/Back");
            done(target);
        }
    }
}
