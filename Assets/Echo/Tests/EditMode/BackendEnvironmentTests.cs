using Echo.Services;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// Which UGS environment the game talks to: development in the editor and in development or development-server
    /// builds, production only in an ordinary release build; a player build on development wears a DEV SERVER tag.
    /// </summary>
    public class BackendEnvironmentTests
    {
        [Test]
        public void TheEditor_UsesDevelopment_WithoutTheTag()
        {
            var config = ScriptableObject.CreateInstance<EchoServiceConfig>();
            Assert.AreEqual("development", config.UgsEnvironment);
            Assert.IsFalse(EchoServiceConfig.DevelopmentServerBuild, "only the development-server build sets ECHO_DEV_SERVER");
            Assert.IsFalse(config.ShowsDevServerTag, "the editor never shows the tag");
            Object.DestroyImmediate(config);
        }
    }
}
