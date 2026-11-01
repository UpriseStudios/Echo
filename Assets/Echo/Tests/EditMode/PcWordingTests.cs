using Echo.Game.UI;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Google Play Games on PC: "tap" reads as "click" (Play's PC guidelines), in the text's own case.</summary>
    public class PcWordingTests
    {
        [TestCase("TAP TO RUN", "CLICK TO RUN")]
        [TestCase("Tap to wear", "Click to wear")]
        [TestCase("tap one to wear it", "click one to wear it")]
        [TestCase("Showing today · tap for this week", "Showing today · click for this week")]
        [TestCase("TAP AGAIN IN MID-AIR", "CLICK AGAIN IN MID-AIR")]
        [TestCase("two taps, one tapped, tapping", "two clicks, one clicked, clicking")]
        [TestCase("TAPS", "CLICKS")]
        public void TapReadsAsClick(string mobile, string pc) => Assert.AreEqual(pc, PcWording.Clicks(mobile));

        [TestCase("Tapestry, tape and a mishap stay as they are")]
        [TestCase("Score 1,204")]
        [TestCase("")]
        public void OtherWordsAreUntouched(string text) => Assert.AreEqual(text, PcWording.Clicks(text));

        [Test]
        public void AText_WithoutTap_IsReturnedAsIs()
        {
            const string score = "12345";
            Assert.AreSame(score, PcWording.Clicks(score), "no copy: the HUD's text stays allocation-free");
        }
    }
}
