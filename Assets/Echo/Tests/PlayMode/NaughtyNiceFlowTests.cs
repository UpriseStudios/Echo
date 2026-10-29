using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
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
    /// Naughty vs Nice (modes doc) in the built scene, through the mock backend: picking a side on the first Daily Haunt
    /// visit (and being assigned when a side is full), the tally, "+N m for Team Nice", badges beside names on the
    /// board, the result revealed once, the Halo granted and worn by the runner and its ghost, and the Stats profile.
    /// </summary>
    public class NaughtyNiceFlowTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
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

        IEnumerator Load(MockBackendService configure = null)
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            // One instance runs every test here, so the save is written whenever TearDown has removed it.
            _savePath ??= Path.Combine(Path.GetTempPath(), "echo_nvn_" + System.Guid.NewGuid().ToString("N") + ".json");
            if (!File.Exists(_savePath))
            {
                File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
                {
                    ContentVersion = ContentVersion.Compute(library, cfg),
                    Best = 777,
                    Seed = 31,
                    NextRunIndex = 3,
                    Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                    Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
                }));
            }
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator PickASide_RunForIt_SeeTheResult_AndWearTheHalo()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.TeamOpen = true;
            backend.Naughty = 5000;
            backend.Leaders.Add((DailyRules.AsLeader(new GhostRecord { RunIndex = 1, DeathTick = 900, TapTicks = new int[0] }, 1, "SwiftGhost#4821"), 812));
            backend.BoardBadges["SwiftGhost#4821"] = new List<string> { "team_nice_2026" };

            // The first Daily Haunt visit of the event asks for a side.
            Click("Title/Daily");
            yield return Frames(3);
            Assert.IsTrue(Showing("Challenge"));
            StringAssert.Contains("NAUGHTY OR NICE", Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text).Aggregate((a, b) => a + b));
            Click("Challenge/Primary");   // Nice
            yield return Frames(2);
            Assert.AreEqual("nice", backend.Team);
            Assert.AreEqual("nice", game.CurrentSave.Modes.Team);
            Assert.IsTrue(Showing("DailyLobby/Tally"));
            StringAssert.Contains("NICE 0 m", Find("DailyLobby/Tally/Text").GetComponent<TMP_Text>().text);
            StringAssert.Contains("You: Team Nice", Find("DailyLobby/Tally/Text").GetComponent<TMP_Text>().text);
            StringAssert.Contains("[NICE]", Text("DailyLobby/Row0"), "the leader's badge beside their name");

            // A run that improves today's best counts for the team.
            game.ScriptedTaps = t => false;
            Click("DailyLobby/Play");
            yield return null;
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            yield return Frames(3);
            Assert.IsTrue(game.LastDailyResult.Ok, game.LastDailyResult.Error);
            int metres = game.LastDailyResult.TeamContribution;
            Assert.Greater(metres, 0);
            StringAssert.Contains($"+{metres:N0} m for Team Nice", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Click("DeathScreen/Menu");

            // Christmas: the result, once, and the Halo granted.
            backend.TeamOpen = false;
            backend.TeamEnded = true;
            backend.Nice = 9000;
            backend.YourDays = 1;
            Click("Title/Daily");
            yield return Frames(4);
            string reveal = Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text).Aggregate((a, b) => a + "\n" + b);
            StringAssert.Contains("TEAM NICE WINS!", reveal);
            StringAssert.Contains("Halo", reveal);
            Click("Challenge/Primary");
            yield return Frames(3);
            CollectionAssert.Contains(game.CurrentSave.Cosmetics.Owned, "acc_halo");
            CollectionAssert.Contains(game.CurrentSave.Modes.Revealed, "naughty_nice_2026");
            Click("DailyLobby/Back");
            Click("Title/Daily");
            yield return Frames(4);
            Assert.IsFalse(Showing("Challenge"), "revealed once");
            Click("DailyLobby/Back");

            // Wear the Halo: on the runner, and on the ghost its run leaves.
            Click("Title/Skins");
            yield return null;
            var skinButtons = Find("Skins").GetComponentsInChildren<UnityEngine.UI.Button>();
            var haloButton = skinButtons.FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>() is TMP_Text t && t.text.StartsWith("Halo"));
            Assert.IsNotNull(haloButton, "no Halo in Skins: " + string.Join(" | ", skinButtons.Select(b => b.GetComponentInChildren<TMP_Text>()?.text ?? "?").Where(t => !t.Contains("<size=70%>"))));
            haloButton.onClick.Invoke();
            Assert.AreEqual("acc_halo", game.CurrentSave.Cosmetics.Equipped.Accessory);
            Click("Skins/Close");
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            Assert.AreEqual("acc_halo", game.Run.AccessoryId);
            var halo = GameObject.Find("World/Player").GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.name == "Accessory");
            Assert.IsNotNull(halo);
            Assert.IsTrue(halo.enabled, "the runner wears it");
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            // The run is committed when the death screen is left (Continue is on offer until then).
            Click("DeathScreen/Menu");
            Assert.Greater(game.Progress.Ghosts.Count, 0, "the run left a ghost");
            Assert.AreEqual("acc_halo", game.Progress.Ghosts[game.Progress.Ghosts.Count - 1].AccessoryId, "its ghost will wear it too");

            // Stats: the profile at the top.
            backend.ProfileBadges.Add(new BadgeInfo { Id = "team_nice_2026", Until = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400 });
            Click("Title/Stats");
            yield return Frames(4);
            string profile = Find("Stats/Profile").GetComponent<TMP_Text>().text;
            StringAssert.Contains("MockRunner#1234", profile);
            StringAssert.Contains("Team Nice", profile);
            StringAssert.Contains("Team Nice 2026", profile);
        }

        [UnityTest]
        public IEnumerator AFullSide_HasTheElvesAssignYouTheOther()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.TeamOpen = true;
            backend.AssignNaughty = true;
            Click("Title/Daily");
            yield return Frames(3);
            Click("Challenge/Primary");   // Nice, but Nice is full
            yield return Frames(2);
            Assert.AreEqual("naughty", backend.Team);
            string card = Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text).Aggregate((a, b) => a + "\n" + b);
            StringAssert.Contains("THE ELVES HAVE SPOKEN", card);
            StringAssert.Contains("Team Naughty", card);
        }
    }
}
