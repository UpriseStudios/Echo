using Echo.Game.Flow;
using NUnit.Framework;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Every PlayMode test starts from a save that hasn't seen the how-to-play clips; only HowToPlayFlowTests wants them
    /// to start on their own.
    /// </summary>
    [SetUpFixture]
    public class PlayModeSetup
    {
        [OneTimeSetUp]
        public void NoAutoHowToPlay() => EchoGame.AutoHowToPlay = false;

        [OneTimeTearDown]
        public void Restore() => EchoGame.AutoHowToPlay = true;
    }
}
