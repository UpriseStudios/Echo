using Echo.Game.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>The screen stays on while a reel, a clip or the how-to-play clips play, and through a run.</summary>
    public class ScreenSleepTests
    {
        [Test]
        public void PlayingWithoutTouches_KeepsTheScreenOn_MenusUseThePhonesSetting()
        {
            Assert.AreEqual(SleepTimeout.NeverSleep, EchoGame.SleepTimeoutFor(GameState.Replay), "reels, clips, how to play");
            Assert.AreEqual(SleepTimeout.NeverSleep, EchoGame.SleepTimeoutFor(GameState.Running));
            Assert.AreEqual(SleepTimeout.NeverSleep, EchoGame.SleepTimeoutFor(GameState.Dying));
            foreach (GameState s in new[] { GameState.Title, GameState.Menu, GameState.DeathScreen, GameState.Paused, GameState.Consent })
                Assert.AreEqual(SleepTimeout.SystemSetting, EchoGame.SleepTimeoutFor(s), s.ToString());
        }
    }
}
