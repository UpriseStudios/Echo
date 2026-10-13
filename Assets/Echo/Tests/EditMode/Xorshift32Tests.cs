using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class Xorshift32Tests
    {
        [Test]
        public void MatchesReferenceSequence()
        {
            var r = new Xorshift32(1);
            Assert.AreEqual(270369u, r.NextUInt());
            Assert.AreEqual(67634689u, r.NextUInt());
            Assert.AreEqual(2647435461u, r.NextUInt());
        }

        [Test]
        public void ZeroSeed_DoesNotStick()
        {
            var r = new Xorshift32(0);
            Assert.AreNotEqual(0u, r.NextUInt());
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            var r = new Xorshift32(99);
            for (int i = 0; i < 10_000; i++)
            {
                int v = r.NextInt(7);
                Assert.That(v, Is.InRange(0, 6));
            }
        }
    }
}
