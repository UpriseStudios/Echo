using Echo.Core.Ghosts;
using Echo.Core.Rules;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Gravestone epitaphs (skins addendum): short, stable lines from the record alone.</summary>
    public class EpitaphTests
    {
        [Test]
        public void KilledByAGhost_NamesIt()
        {
            var r = new GhostRecord { RunIndex = 12, Cause = DeathCause.Ghost, KillerRunIndex = 7 };
            Assert.AreEqual("Killed by The Seventh", Epitaphs.For(r));
        }

        [Test]
        public void FallsAndHazards_NameTheRun_AndNeverChange()
        {
            var fall = new GhostRecord { RunIndex = 12, Cause = DeathCause.Fall, DeathTick = 400 };
            var spike = new GhostRecord { RunIndex = 3, Cause = DeathCause.Hazard, DeathTick = 90 };
            StringAssert.StartsWith("Run #12 · ", Epitaphs.For(fall));
            StringAssert.StartsWith("Run #3 · ", Epitaphs.For(spike));
            Assert.AreEqual(Epitaphs.For(fall), Epitaphs.For(fall));
        }

        [Test]
        public void UnknownCause_FromAnOldSave_StillReads()
        {
            var old = new GhostRecord { RunIndex = 5, DeathTick = -1 };
            StringAssert.StartsWith("Run #5 · ", Epitaphs.For(old));
        }

        [Test]
        public void TheHeadstone_SplitsTheEpitaph_IntoTitleAndLine()
        {
            var fall = new GhostRecord { RunIndex = 12, DeathTick = 300, Cause = DeathCause.Fall };
            Assert.AreEqual("Run #12", Epitaphs.Title(fall));
            Assert.AreEqual(Epitaphs.Title(fall) + " · " + Epitaphs.Line(fall), Epitaphs.For(fall));
            var killed = new GhostRecord { RunIndex = 9, Cause = DeathCause.Ghost, KillerRunIndex = 7 };
            Assert.AreEqual("Killed by The Seventh", Epitaphs.Line(killed));
            Assert.AreEqual(Epitaphs.Line(killed), Epitaphs.For(killed));
        }
    }
}
