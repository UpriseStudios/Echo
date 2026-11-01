using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.UI;
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
    /// Google Play Games on PC (Play's PC guidelines): every "tap" reads "click", on the scene's texts and on cards made at
    /// runtime, and nowhere on a phone.
    /// </summary>
    public class PcWordingFlowTests
    {
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            PcWording.ForcePc = null;
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static TMP_Text Text(string path) => GameObject.Find("Canvas/SafeArea/" + path).GetComponent<TMP_Text>();

        IEnumerator Title(bool pc)
        {
            PcWording.ForcePc = pc;
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_pc_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator OnPc_TapReadsClick_EvenOnCardsMadeLater()
        {
            yield return Title(pc: true);
            TMP_Text run = Text("Title/TapToRun");
            run.ForceMeshUpdate();
            Assert.AreEqual("CLICK TO RUN", run.GetParsedText().Trim());

            // Skin cards are cloned from templates when the screen opens.
            GameObject.Find("Canvas/SafeArea/Title/Skins").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            yield return null;
            var cards = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled && t.text.Contains("Tap to wear")).ToList();
            Assert.IsNotEmpty(cards, "a character to wear");
            foreach (TMP_Text card in cards)
            {
                card.ForceMeshUpdate();
                StringAssert.Contains("Click to wear", card.GetParsedText());
                StringAssert.DoesNotContain("Tap", card.GetParsedText());
            }
        }

        [UnityTest]
        public IEnumerator OnAPhone_TapStaysTap()
        {
            yield return Title(pc: false);
            TMP_Text run = Text("Title/TapToRun");
            run.ForceMeshUpdate();
            Assert.AreEqual("TAP TO RUN", run.GetParsedText().Trim());
        }
    }
}
