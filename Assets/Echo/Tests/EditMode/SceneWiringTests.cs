using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The built scene keeps its links to the settings assets (device testing: the scene was saved with no service
    /// config, so phone builds silently ran on mock ads, store and sharing). Read from the scene file itself.
    /// </summary>
    public class SceneWiringTests
    {
        static string SceneText() => File.ReadAllText(Path.Combine(Application.dataPath, "Echo", "Scenes", "Echo.unity"));

        [TestCase("_serviceConfig")]
        [TestCase("_features")]
        [TestCase("_catalog")]
        public void TheBuiltScene_LinksItsSettingsAsset(string field)
        {
            string scene = SceneText();
            int at = scene.IndexOf("  " + field + ":", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"{field} not found; run the scene build");
            string line = scene.Substring(at, scene.IndexOf('\n', at) - at);
            StringAssert.Contains("guid:", line, $"{field} is empty in the built scene: {line.Trim()}");
        }
    }
}
