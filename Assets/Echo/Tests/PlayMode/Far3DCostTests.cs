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
    /// Time Run, the Infinite City prototype: what the Far Far Future's 3D city costs to draw, mid-run in the era with full
    /// effects. Its camera renders forward with no shadows and no depth pre-pass, so each of the city's renderers inside the
    /// camera's view is one draw call; counted with the triangles they draw, over the camera's frustum. Written to
    /// TestResults/far3d_cost.txt.
    /// </summary>
    public class Far3DCostTests
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
            _savePath = Path.Combine(Path.GetTempPath(), "echo_far3dcost_" + System.Guid.NewGuid().ToString("N") + ".json");
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

        [UnityTest, Timeout(300000)]
        public IEnumerator TheFarFarFuture_DrawCallsAndTriangles()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.PreviewTimeRun = true;
            game.ForceEra = 4;
            game.WeakDeviceOverride = false;
            game.enabled = false;
            game.TickFrame(Dt, tapped: true);
            for (int f = 0; f < 30; f++) { game.TickFrame(Dt, false); yield return null; }
            Assert.AreEqual(Far3DView.Mode.RealTime, game.Scene3DShown);

            var scene = Object.FindAnyObjectByType<Far3DScene>();
            Camera cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(c => c.cullingMask == 1 << scene.gameObject.layer);
            // Sampled over a stretch of the run (the camera moves on), worst case kept.
            int most = 0, mostTriangles = 0, renderers = 0;
            long sum = 0;
            const int samples = 120;
            for (int i = 0; i < samples; i++)
            {
                for (int k = 0; k < 10; k++) game.TickFrame(Dt, false);
                yield return null;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
                int visible = 0, triangles = 0;
                renderers = 0;
                foreach (MeshRenderer r in scene.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled) continue;
                    renderers++;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    visible++;
                    Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                    if (m != null) triangles += (int)(m.GetIndexCount(0) / 3);
                }
                sum += visible;
                if (visible > most) most = visible;
                if (triangles > mostTriangles) mostTriangles = triangles;
            }
            string report = $"Far Far Future 3D city: {sum / (float)samples:0} draw calls on average, {most} at most " +
                            $"(renderers in the camera's view, of {renderers}), up to {mostTriangles:N0} triangles";
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/far3d_cost.txt", report + "\n");
            Debug.Log("[Far3DCost] " + report);
            Assert.Greater(most, 0);
        }
    }
}
