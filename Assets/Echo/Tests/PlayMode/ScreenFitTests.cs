using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Play's Level Up guidelines: a portrait game fills every screen from 3:4 to 9:21 with nothing cut off. Opens the
    /// main screens on a tablet (3:4), today's tall phones (9:20) and the tallest (9:21), saves each to
    /// TestResults/Screenshots/fit_* and fails if any visible button or text leaves the screen.
    /// </summary>
    public class ScreenFitTests
    {
        static readonly (string Name, int Width, int Height)[] Shapes = { ("3x4", 1536, 2048), ("9x20", 1080, 2400), ("9x21", 1080, 2520) };

        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        [UnityTest]
        public IEnumerator MainScreens_FitTabletsAndTallPhones()
        {
            var save = new SaveData
            {
                Best = 300,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true },
            };
            _savePath = Path.Combine(Path.GetTempPath(), "echo_fit_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            for (int i = 0; i < 10; i++) yield return null;

            // Ghost Town with live events and the community's numbers, as on a busy day.
            var backend = (Echo.Services.Mocks.MockBackendService)game.Services.Backend;
            backend.Modes.Add(new Echo.Services.Backend.ModeState { Id = Echo.Core.Modes.ModeIds.Ranked, Open = true, SecondsLeft = 9 * 86400, Players = 2310 });
            backend.Modes.Add(new Echo.Services.Backend.ModeState { Id = Echo.Core.Modes.ModeIds.Graveyard, Open = true, SecondsLeft = 3 * 86400, Players = 418 });
            backend.Modes.Add(new Echo.Services.Backend.ModeState { Id = Echo.Core.Modes.ModeIds.OneLife, Open = false, SecondsLeft = 5 * 3600, Players = -1 });
            backend.Modes.Add(new Echo.Services.Backend.ModeState { Id = Echo.Core.Modes.ModeIds.NaughtyNice, Open = false, SecondsLeft = 30 * 86400, Players = -1 });
            backend.Pulse = new Echo.Services.Backend.DailyPulseInfo { Runners = 1204, LeaderName = "TessTheBrave#4512", LeaderDistance = 2318, Rank = 37, BestDistance = 1150, SecondsLeft = 11 * 3600 };

            var outside = new List<string>();
            IEnumerator All(string screen)
            {
                foreach (var shape in Shapes)
                    yield return ScreenCaptureTests.Capture($"fit_{shape.Name}_{screen}", shape.Width, shape.Height, outside);
            }

            yield return All("title");
            foreach (var (open, screen, close) in new[] { ("Title/Stats", "stats", "Stats/Close"), ("Title/Settings", "settings", "Settings/Close"), ("Title/Modes", "modes", "Modes/Back"), ("Title/Reels", "reels", "Reels/Close"), ("Title/Daily", "daily", "DailyLobby/Back") })
            {
                if (!Exists(open)) continue;
                Click(open);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return All(screen);
                Click(close);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Click("Title/Skins");
            foreach (string tab in new[] { "Runners", "Packs", "Worlds", "Style", "Extras", "Free" })
            {
                Click("Skins/Tabs/" + tab);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return All("skins_" + tab.ToLowerInvariant());
            }
            Click("Skins/Close");
            yield return new WaitForSecondsRealtime(0.3f);

            // A run that hits the first obstacle, then its death screen once the grave reveal has played.
            game.enabled = false;
            const float dt = 1f / 60f;
            game.TickFrame(dt, tapped: true);
            for (int f = 0; f < 3000 && game.State != GameState.DeathScreen; f++) { game.TickFrame(dt, false); if (f % 30 == 0) yield return null; }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            float revealed = Time.realtimeSinceStartup + 2.4f;
            while (Time.realtimeSinceStartup < revealed) { game.TickFrame(dt, false); yield return null; }
            yield return All("death");

            Assert.IsEmpty(outside, "off the screen:\n" + string.Join("\n", outside));
        }

        static bool Exists(string path) => GameObject.Find("Canvas/SafeArea/" + path) != null;

        static void Click(string path)
        {
            var go = GameObject.Find("Canvas/SafeArea/" + path);
            Assert.IsNotNull(go, path);
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
    }
}
