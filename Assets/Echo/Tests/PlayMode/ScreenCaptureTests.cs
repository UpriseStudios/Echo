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
    /// Renders the key screens to TestResults/Screenshots (portrait 540x960) and checks none is blank.
    /// The camera renders into a texture with the canvas moved into camera space for the capture, because
    /// end-of-frame screen capture never resumes in batch mode.
    /// </summary>
    public class ScreenCaptureTests
    {
        const int Width = 1080, Height = 1920;
        const int GhostCount = 8;
        const uint Seed = 777;

        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        [UnityTest]
        public IEnumerator KeyScreens_RenderAndAreNotBlank()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            var rng = new Random(3);
            int speed = cfg.SpeedForPct(100);

            // Mixed skins, as a real ghost crowd becomes (skins addendum); run 3, the Nemesis, is the Princess.
            string[] skins = { "char_knight", "char_robot", "char_princess", "char_ninja", "char_skeleton", "char_astronaut", "char_wizard", "char_cat" };
            var records = new List<GhostRecord>();
            for (int i = 1; i <= GhostCount; i++)
            {
                Schedule own = SurvivingSchedule.Build(new Course(library, Seed), speed, 3000, rng, cfg);
                records.Add(new GhostRecord { RunIndex = i, TapTicks = own.TapTicks, ReleaseTicks = own.ReleaseTicks, DeathTick = i == 1 ? 700 : 300 + i * 90, SpeedPct = 100, Kills = i == 3 ? 2 : 0, LastKillRun = 5, SkinId = skins[i - 1] });
            }
            var ghosts = new GhostManager();
            var course = new Course(library, Seed);
            ghosts.Begin(records, cfg, course, speed);
            var boxes = new List<Aabb?[]>();
            for (int t = 0; t < 1300; t++)
            {
                ghosts.Step(course, t, cfg);
                boxes.Add(Enumerable.Range(0, GhostCount).Select(i => ghosts.IsLethal(i) ? Runner.GhostBox(ghosts.ReplayAt(i).State, cfg) : (Aabb?)null).ToArray());
            }
            Schedule player = SurvivingSchedule.Build(new Course(library, Seed), speed, 1300, rng, cfg,
                (tick, p) => tick >= cfg.GhostGraceTicks && boxes[tick].Any(b => b.HasValue && Runner.PlayerBox(p, cfg).Overlaps(b.Value)));
            // Ghosts are met just before their graves, so the first overtake is the earliest lethal meeting.
            int firstMeet = Enumerable.Range(0, GhostCount).Where(i => !ghosts.ReplayAt(i).Harmless).Min(i => ghosts.ReplayAt(i).MeetTick);
            // The oldest ghost dies early, so its grave stands on the course before the 03 capture.
            int graveX = ghosts.DeathX(0, course, cfg);

            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 300, // low enough that the death screen offers Continue
                Seed = Seed,
                NextRunIndex = GhostCount + 1,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true },
                // A bought character, so the world shots show one besides the hooded runner.
                Cosmetics = new CosmeticsSaveData
                {
                    Owned = new List<string> { "runner_default", "char_hooded", "char_wizard" },
                    Equipped = new EquippedSaveData { Character = "char_wizard" },
                    AdsWatched = 3, // the Skins shot shows ad progress on Pumpkin and Reaper
                },
            };
            foreach (var r in records)
                save.Ghosts.Add(new GhostSaveData { RunIndex = r.RunIndex, TapTicks = r.TapTicks, ReleaseTicks = r.ReleaseTicks, DeathTick = r.DeathTick, SpeedPct = 100, Kills = r.Kills, LastKillRun = r.LastKillRun, SkinId = r.SkinId });
            _savePath = Path.Combine(Path.GetTempPath(), "echo_shots_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture("01_title");

            // Every menu screen, opened the way a player would.
            yield return OpenAndCapture("Title/Skins", "05_skins", "Skins/Close");
            yield return OpenAndCapture("Title/Stats", "06_stats", "Stats/Close");
            Click("Title/Settings");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("07_settings");
            Click("Settings/Privacy");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("08_consent");
            Click("Consent/EssentialOnly");
            Click("Settings/Close");
            Assert.AreEqual(GameState.Title, game.State);
            yield return new WaitForSecondsRealtime(0.3f);

            game.enabled = false;
            bool scripted = true;
            game.ScriptedTaps = t => scripted && player.TapAt(t);
            game.ScriptedHeld = t => !scripted || player.HeldAt(t);
            const float dt = 1f / 60f;
            game.TickFrame(dt, tapped: true);
            while (game.Run.Tick < firstMeet - 4) { game.TickFrame(dt, false); if (game.Run.Tick % 30 == 0) yield return null; }
            yield return Capture("02_overtake");

            while (game.Run.Tick < 900 && game.State == GameState.Running && game.Run.Player.X < graveX - 1500) { game.TickFrame(dt, false); if (game.Run.Tick % 30 == 0) yield return null; }
            Assert.AreEqual(GameState.Running, game.State);
            yield return Capture("09_grave");

            while (game.Run.Tick < 900 && game.State == GameState.Running) { game.TickFrame(dt, false); if (game.Run.Tick % 30 == 0) yield return null; }
            yield return Capture("03_running");

            scripted = false;
            game.ScriptedTaps = t => false;
            for (int f = 0; f < 600 && game.State != GameState.DeathScreen; f++) { game.TickFrame(dt, false); yield return null; }
            // The grave reveal plays on real time: the zoom, the headstone landing, then the chiselled epitaph.
            float revealed = Time.realtimeSinceStartup + 2.4f;
            while (Time.realtimeSinceStartup < revealed) { game.TickFrame(dt, false); yield return null; }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            yield return Capture("04_death");

            // The death screen can go back to the menu instead of retrying.
            Click("DeathScreen/Menu");
            Assert.AreEqual(GameState.Title, game.State);
            Assert.IsTrue(game.Run.Tick == 0 && !game.Run.IsDead, "a fresh run waits on the title");

            // Weekly Twists (value-adds doc): the title banner, then the opening of a run under each visual twist.
            var twists = new[] { Echo.Core.Rules.Twist.Mirror, Echo.Core.Rules.Twist.LightsOut, Echo.Core.Rules.Twist.ShadowWeek, Echo.Core.Rules.Twist.GiantGhosts };
            for (int t = 0; t < twists.Length; t++)
            {
                var flags = ScriptableObject.CreateInstance<FeatureFlags>();
                flags.OverrideTwist = true;
                flags.Twist = twists[t];
                game.UseFeatures(flags);
                Assert.AreEqual(twists[t], game.Run.Options.Twist);
                if (t == 0)
                {
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Capture("10_title_twist");
                }
                game.TickFrame(dt, tapped: true);
                while (game.Run.Tick < 150 && game.State == GameState.Running) { game.TickFrame(dt, false); if (game.Run.Tick % 30 == 0) yield return null; }
                yield return Capture($"1{t + 1}_twist_{twists[t]}");
                Object.Destroy(flags);
            }

            // Copycat (redesigned head-on): the mirror-you running at the player, a third of a second before they meet.
            var copyFlags = ScriptableObject.CreateInstance<FeatureFlags>();
            copyFlags.OverrideTwist = true;
            copyFlags.Twist = Echo.Core.Rules.Twist.Copycat;
            game.UseFeatures(copyFlags);
            int meet = game.Run.Copycat.MeetTick;
            Assert.Greater(meet, 0, "a copycat is planned");
            // Dodge this run's lethal ghosts as well as the course, using the same placement the run made.
            int copySpeed = cfg.SpeedForPct(game.Run.SpeedPct);
            var copyCourse = new Course(library, game.Run.Course.Seed);
            var copyGhosts = new GhostManager();
            copyGhosts.Begin(Enumerable.Range(0, game.Run.Ghosts.Count).Select(i => game.Run.Ghosts.RecordAt(i)).ToList(), cfg, copyCourse, copySpeed);
            var copyBoxes = new List<Aabb?[]>();
            for (int t = 0; t < meet + 60; t++)
            {
                copyGhosts.Step(copyCourse, t, cfg);
                copyBoxes.Add(Enumerable.Range(0, copyGhosts.Count).Select(i => copyGhosts.IsLethal(i) ? Runner.GhostBox(copyGhosts.ReplayAt(i).State, cfg) : (Aabb?)null).ToArray());
            }
            Schedule copyRun = SurvivingSchedule.Build(new Course(library, game.Run.Course.Seed), copySpeed, meet + 60, rng, cfg,
                (tick, p) => tick >= cfg.GhostGraceTicks && copyBoxes[tick].Any(b => b.HasValue && Runner.PlayerBox(p, cfg).Overlaps(b.Value)));
            game.ScriptedTaps = copyRun.TapAt;
            game.ScriptedHeld = copyRun.HeldAt;
            game.TickFrame(dt, tapped: true);
            while (game.Run.Tick < meet - 20 && game.State == GameState.Running) { game.TickFrame(dt, false); if (game.Run.Tick % 30 == 0) yield return null; }
            Assert.IsTrue(game.Run.Copycat.Active, $"the copycat is on its way: state {game.State}, tick {game.Run.Tick}, meet {meet}, " +
                $"planned {game.Run.Copycat.Planned} chunk {game.Run.Copycat.Chunk} from {game.Run.Copycat.CopyFrom} meet {game.Run.Copycat.MeetTick}, cause {game.Run.Cause}, twist {game.Run.Options.Twist}");
            yield return Capture("15_twist_Copycat");
            Object.Destroy(copyFlags);

            // Themed Biomes and the character flourishes: each character where its flourish shows, on a fresh run
            // waiting at the start (the scenery follows PreviewMetres) with the UI hidden so the world is the shot.
            var plain = ScriptableObject.CreateInstance<FeatureFlags>();
            game.UseFeatures(plain);
            var ui = Object.FindAnyObjectByType<Canvas>();
            ui.enabled = false;
            var scenes = new[]
            {
                ("char_frog", 150f, "16_graveyard_frog_rain"), ("char_lich", 120f, "17_graveyard_lich_wisps"),
                ("char_knight", 420f, "18_castle_knight_banners"), ("char_vampire", 600f, "19_night_vampire_bats"),
                ("char_werewolf", 640f, "20_night_werewolf_moon"), ("char_astronaut", 820f, "21_space_astronaut_stars"),
            };
            foreach (var (character, metres, shot) in scenes)
            {
                game.PreviewCharacter(character);
                game.PreviewMetres = metres;
                for (int f = 0; f < 20; f++) { game.TickFrame(dt, false); yield return null; }
                yield return Capture(shot);
            }
            // Seasons phase 1: the test theme (every override on) by day in the Graveyard and at night.
            game.PreviewCharacter("char_hooded");
            game.ForceSeason(Echo.Game.Seasons.SeasonIds.Test);
            foreach (var (metres, shot) in new[] { (150f, "22_season_test_graveyard"), (600f, "23_season_test_night") })
            {
                game.PreviewMetres = metres;
                for (int f = 0; f < 20; f++) { game.TickFrame(dt, false); yield return null; }
                yield return Capture(shot);
            }
            game.ForceSeason(null);
            game.PreviewMetres = null;
            ui.enabled = true;
            Object.Destroy(plain);
        }

        /// <summary>The Elementals in each world (Graveyard, Castle by day, the night sky) and ELEMENTS AWAKENED.</summary>
        [UnityTest]
        public IEnumerator Elementals_InEachWorld_AndTheirAwakening()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = Seed,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
            };
            _savePath = Path.Combine(Path.GetTempPath(), "echo_elementals_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            const float dt = 1f / 60f;
            var ui = Object.FindAnyObjectByType<Canvas>();
            ui.enabled = false;
            var worlds = new[] { (150f, "graveyard"), (380f, "castle"), (660f, "night") };
            foreach (string element in new[] { "ember", "frost", "storm", "tide" })
                foreach (var (metres, world) in worlds)
                {
                    game.PreviewCharacter("char_" + element);
                    game.PreviewMetres = metres;
                    for (int f = 0; f < 20; f++) { game.TickFrame(dt, false); yield return null; }
                    // Storm's lightning is a two-flash moment every 4.5 s: wait for one so the shot shows it.
                    if (element == "storm")
                    {
                        float until = Time.realtimeSinceStartup + 6f;
                        while (Time.time % 4.5f > 0.06f && Time.realtimeSinceStartup < until) { game.TickFrame(dt, false); yield return null; }
                    }
                    yield return Capture($"3x_{element}_{world}");
                }
            game.PreviewMetres = null;
            ui.enabled = true;

            var view = Object.FindAnyObjectByType<Echo.Game.UI.ElementsAwakenedView>(FindObjectsInactive.Include);
            var catalog = game.GetType().GetField("_catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(game) as Echo.Game.Cosmetics.CosmeticCatalog;
            var items = catalog.Items.Where(i => i.AllAchievements).ToArray();
            view.Play(items.Select(i => i.RunFrames[0]).ToArray(), items.Select(i => Echo.Game.Cosmetics.ElementalTrial.ElementColor(i.Id)).ToArray());
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Capture("3x_awakened_rising");
            view.Skip();
            yield return Capture("3x_awakened_done");
            view.Close();
        }

        static void Click(string path)
        {
            var go = GameObject.Find("Canvas/SafeArea/" + path);
            Assert.IsNotNull(go, path);
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static IEnumerator OpenAndCapture(string open, string name, string close)
        {
            Click(open);
            yield return new WaitForSecondsRealtime(0.3f); // let the screen fade in
            yield return Capture(name);
            Click(close);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static IEnumerator Capture(string name)
        {
            yield return null;
            var cam = Camera.main;

            // The world: through the pixel-perfect camera (288 px across, stretched to the target). HDR so URP
            // keeps colours above 1 for bloom, as it does when rendering to the screen.
            var worldRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR);
            var previousTarget = cam.targetTexture;
            cam.targetTexture = worldRt;
            Object.FindAnyObjectByType<Echo.Game.Views.CameraRig>()?.Follow(cam.transform.position.x - Echo.Game.Views.CameraRig.WidthUnits * 0.25f);
            cam.Render();
            cam.targetTexture = previousTarget;
            Color[] world = Read(worldRt);

            // The UI: in the game it is an overlay at full resolution, so it gets its own camera here rather
            // than going through the low-res world pass.
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var mode = canvas.renderMode;
            var uiCamGo = new GameObject("CaptureUiCamera");
            var uiCam = uiCamGo.AddComponent<Camera>();
            uiCam.clearFlags = CameraClearFlags.SolidColor;
            uiCam.cullingMask = 1 << canvas.gameObject.layer;
            uiCam.orthographic = true;
            uiCam.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 5f;
            Canvas.ForceUpdateCanvases();
            var uiRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR);
            uiCam.targetTexture = uiRt;
            uiCam.backgroundColor = Color.black;
            uiCam.Render();
            Color[] overBlack = Read(uiRt);
            uiCam.backgroundColor = Color.white;
            uiCam.Render();
            Color[] overWhite = Read(uiRt);
            uiCam.targetTexture = null;
            canvas.renderMode = mode;
            Object.Destroy(uiCamGo);

            // The UI was drawn over black and over white; the difference is how much background shows through,
            // so it composites over the world exactly without relying on the alpha channel. In linear, then
            // converted to gamma for the PNG as the screen does.
            var pixels = new Color[world.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float through = Mathf.Clamp01(overWhite[i].g - overBlack[i].g);
                Color c = new Color(overBlack[i].r, overBlack[i].g, overBlack[i].b) + world[i] * through;
                pixels[i] = new Color(c.r, c.g, c.b, 1f).gamma;
            }
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.SetPixels(pixels);
            tex.Apply();
            worldRt.Release();
            uiRt.Release();

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

            // Not blank: a rendered screen has many distinct colours.
            var distinct = new HashSet<Color32>(tex.GetPixels32());
            Object.Destroy(tex);
            Assert.Greater(distinct.Count, 8, $"{name} looks blank ({distinct.Count} colours)");
        }

        // Linear colour from an HDR render texture.
        static Color[] Read(RenderTexture rt)
        {
            RenderTexture.active = rt;
            var hdr = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false, true);
            hdr.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            Color[] pixels = hdr.GetPixels();
            Object.Destroy(hdr);
            return pixels;
        }
    }
}
