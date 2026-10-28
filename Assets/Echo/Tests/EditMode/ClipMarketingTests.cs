using Echo.Game.Clips;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The Vertical clip preset (marketing content plan): 9-20 s, a hook kept in the top third and out of the right 15%
    /// and bottom 20% where TikTok and Reels put their buttons, and the end card.
    /// </summary>
    public class ClipMarketingTests
    {
        const int W = ClipCapture.Width, H = ClipCapture.Height;

        [Test]
        public void AVerticalClip_IsNineToTwentySeconds_FifteenByDefault()
        {
            int death = 60 * 60;
            ClipRange d = ClipRange.Default(ClipFormat.Vertical, death);
            Assert.AreEqual(ClipRange.VerticalDefaultTicks, d.Ticks);
            Assert.AreEqual(ClipRange.Length(death), d.End, "the final moments");
            Assert.AreEqual(ClipRange.VerticalMaxTicks, ClipRange.Clamp(0, death, ClipFormat.Vertical, death, movingEnd: true).Ticks);
            Assert.AreEqual(ClipRange.VerticalMinTicks, ClipRange.Clamp(600, 660, ClipFormat.Vertical, death, movingEnd: true).Ticks);
            Assert.AreEqual(ClipRange.Length(120), ClipRange.Clamp(0, 9999, ClipFormat.Vertical, 120, movingEnd: true).Ticks,
                "a run shorter than 9 s gives its whole timeline");
        }

        [Test]
        public void TheHook_IsCapitals_TheFontsCharacters_AndAtMostEightWords()
        {
            Assert.AreEqual("I'M BACK & HUNGRY?", ClipHook.Clean("I\u2019m  back & hungry? \U0001F47B"));
            Assert.AreEqual("ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT", ClipHook.Clean("one two three four five six seven eight nine ten"));
            Assert.AreEqual("", ClipHook.Clean("   "));
        }

        [TestCase("Every time I die, I make the game harder.")]
        [TestCase("My last 30 deaths are now trying to kill me")]
        [TestCase("Supercalifragilisticexpialidocious ghosts everywhere tonight")]
        public void TheHook_StaysInTheTopThird_ClearOfTheRightAndBottomEdges(string hook)
        {
            var frame = new byte[W * H * 4];
            Assert.IsTrue(ClipHook.Draw(frame, W, H, hook));
            int drawn = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (frame[(y * W + x) * 4 + 3] == 0) continue;
                    drawn++;
                    Assert.Less(x, W * 85 / 100, $"right 15% kept clear ({x},{y})");
                    Assert.Less(y, H / 3, $"top third only ({x},{y})");
                    Assert.Less(y, H * 80 / 100, "bottom 20% kept clear");
                    Assert.GreaterOrEqual(y, ClipHook.Top - 1, "below the score stamp");
                }
            Assert.Greater(drawn, 200, "the hook was drawn");
        }

        [Test]
        public void TheEndCard_FillsTheFrame_WithTheWordmarkAndStoreLine()
        {
            var frame = new byte[W * H * 4];
            ClipEndCard.Draw(frame, W, H, "Free on Google Play");
            int cyan = 0, opaque = 0;
            for (int i = 0; i < W * H; i++)
            {
                if (frame[i * 4 + 3] == 255) opaque++;
                if (frame[i * 4] == 0x73 && frame[i * 4 + 1] == 0xF2 && frame[i * 4 + 2] == 0xFF) cyan++;
            }
            Assert.AreEqual(W * H, opaque, "every pixel painted");
            Assert.Greater(cyan, 1000, "the ECHO wordmark");
        }

        [Test]
        public void TheFont_DrawsTheNewPunctuation()
        {
            foreach (char c in "?'&/()+")
            {
                Assert.IsTrue(ClipFont.Supports(c), c.ToString());
                bool any = false;
                for (int y = 0; y < ClipFont.Height; y++)
                    for (int x = 0; x < ClipFont.Width; x++)
                        any |= ClipFont.Pixel(c, x, y);
                Assert.IsTrue(any, c.ToString());
            }
        }
    }
}
