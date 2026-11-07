using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Time Run art fix, phase 1: are the effects actually on? For Today, the Far Future and the Far Far Future, mid-run:
    /// post-processing and HDR on the camera that draws the era, the Far Future's bloom volume weighted in, and how much of
    /// the frame is brighter than 1 (what bloom feeds on), with full effects and with reduced, the same frozen moment
    /// captured both ways (fx_*_on / fx_*_off). Everything measured goes to TestResults/effects_audit.txt.
    /// </summary>
    public class EffectsAuditTests
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

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_fxaudit_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 100000,
                Seed = 41,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        // The world as the main camera draws it, in HDR (before tone mapping and post, which clamp to 1): the share of
        // pixels over 1 and the brightest.
        static (float share, float max) HdrStats()
        {
            Camera cam = Camera.main;
            var rig = Object.FindAnyObjectByType<CameraRig>();
            bool post = rig.GlowOn;
            rig.SetGlow(false);
            var rt = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGBHalf);
            RenderTexture before = cam.targetTexture;
            cam.targetTexture = rt;
            rig.Follow(cam.transform.position.x - CameraRig.WidthUnits * 0.25f);
            cam.Render();
            cam.targetTexture = before;
            rig.SetGlow(post);
            var tex = new Texture2D(540, 960, TextureFormat.RGBAFloat, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            RenderTexture.active = null;
            Color[] px = tex.GetPixels();
            Object.Destroy(tex);
            rt.Release();
            int over = 0;
            float max = 0f;
            foreach (Color c in px)
            {
                float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (m > 1f) over++;
                if (m > max) max = m;
            }
            return (over / (float)px.Length, max);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheEffects_AreOnInTheLaterEras_AndOffWhenReduced()
        {
            var report = new StringBuilder();
            foreach (var (era, name) in new[] { (2, "today"), (3, "future"), (4, "farfar") })
            {
                yield return Load();
                var game = Object.FindAnyObjectByType<EchoGame>();
                var rig = Object.FindAnyObjectByType<CameraRig>();
                game.PreviewTimeRun = true;
                game.ForceEra = era;
                game.WeakDeviceOverride = false;
                game.enabled = false;
                game.TickFrame(Dt, tapped: true);
                for (int f = 0; f < 40; f++) game.TickFrame(Dt, false);
                yield return null;

                Assert.IsTrue(game.FullEffects, name + ": a capable machine gets full effects");
                Assert.IsTrue(rig.GlowOn, name + ": post-processing on the camera that draws it");
                string full = game.EffectsReport();
                if (era >= 3) StringAssert.Contains("Bloom 'FutureFX': weight 1", full, name + ": the Far Future's bloom is weighted in");
                else StringAssert.Contains("Bloom 'FutureFX': weight 0", full, name + ": only the eras after Today get the stronger bloom");
                StringAssert.Contains("HDR on", full);
                var (shareOn, maxOn) = HdrStats();
                // Art fix phase 2: the Far Future's neon, windows and signs are emissive, so there is light for the bloom.
                if (era == 3)
                {
                    Assert.Greater(shareOn, 0.01f, name + ": over 1% of the picture brighter than 1");
                    Assert.Greater(maxOn, 1.5f, name + ": its brightest lights well over 1");
                }
                yield return ScreenCaptureTests.Capture($"fx_{name}_on");

                game.WeakDeviceOverride = true;
                Assert.IsFalse(rig.GlowOn, name + ": reduced effects: no post-processing");
                string reduced = game.EffectsReport();
                var (shareOff, maxOff) = HdrStats();
                yield return ScreenCaptureTests.Capture($"fx_{name}_off");
                game.WeakDeviceOverride = false;

                report.Append($"== {name} ==\n--- full ---\n{full}\nHDR: {shareOn:P1} of pixels over 1, brightest {maxOn:0.00}\n");
                report.Append($"--- reduced ---\n{reduced}\nHDR: {shareOff:P1} of pixels over 1, brightest {maxOff:0.00}\n\n");
            }
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/effects_audit.txt", report.ToString());
            Debug.Log("[EffectsAudit]\n" + report);
        }
    }
}
