using Echo.Core.Challenges;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The challenge name filter: blocked names become the default, ordinary names pass untouched.</summary>
    public class ChallengeNamesTests
    {
        [TestCase("fuck")]
        [TestCase("FUCKER")]
        [TestCase("F u c k")]
        [TestCase("fuuuuck")]
        [TestCase("5h1t")]
        [TestCase("B1tch")]
        [TestCase("big ass")]
        [TestCase("ASSSS")]
        [TestCase("a s s")]
        [TestCase("Mr Dick")]
        [TestCase("n4z1")]
        [TestCase("SuperWanker")]
        public void BlockedNames_BecomeTheDefault(string name)
        {
            Assert.IsTrue(ChallengeNames.IsBlocked(name), name);
            Assert.AreEqual(ChallengeNames.Default, ChallengeNames.Clean(name));
        }

        [TestCase("Jamie")]
        [TestCase("Cassie")]
        [TestCase("Hitchcock")]
        [TestCase("Dickens")]
        [TestCase("Analyst")]
        [TestCase("Peacock")]
        [TestCase("Raccoon")]
        [TestCase("Grape")]
        [TestCase("As")]
        [TestCase("Con")]
        [TestCase("Sussex")]
        [TestCase("Swank")]
        [TestCase("Zoë 99")]
        public void OrdinaryNames_PassUntouched(string name)
        {
            Assert.IsFalse(ChallengeNames.IsBlocked(name), name);
            Assert.AreEqual(name, ChallengeNames.Clean(name));
        }

        [Test]
        public void Clean_TrimsCapsAndDropsControlCharacters_AndLeavesEmptyEmpty()
        {
            Assert.AreEqual("", ChallengeNames.Clean(null));
            Assert.AreEqual("", ChallengeNames.Clean("   "));
            Assert.AreEqual("Jamie", ChallengeNames.Clean("  Ja\nmie\t "));
            Assert.AreEqual(Challenge.MaxNameLength, ChallengeNames.Clean("abcdefghijklmnopqrstuvwxyz").Length);
        }
    }
}
