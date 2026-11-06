using Echo.Game.World;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>World: Time Run (art pass): five eras of 300 m, the last carrying on; the next warmed 240 m into each.</summary>
    public class TimeErasTests
    {
        [TestCase(0, 0)]
        [TestCase(299, 0)]
        [TestCase(300, 1)]
        [TestCase(599, 1)]
        [TestCase(600, 2)]
        [TestCase(900, 3)]
        [TestCase(1199, 3)]
        [TestCase(1200, 4)]
        [TestCase(5000, 4)]
        public void EraByDistance(int metres, int era) => Assert.AreEqual(era, TimeEras.At(metres, 5));

        [TestCase(0, -1)]
        [TestCase(239, -1)]
        [TestCase(240, 1)]
        [TestCase(299, 1)]
        [TestCase(300, -1)]
        [TestCase(540, 2)]
        [TestCase(840, 3)]
        [TestCase(1140, 4)]
        [TestCase(1199, 4)]
        [TestCase(1440, -1)]
        public void TheNextEra_IsWarmed240mIn(int metres, int warm) => Assert.AreEqual(warm, TimeEras.PreloadAt(metres, 5));

        [Test]
        public void NoEras_IsNone() => Assert.AreEqual(-1, TimeEras.At(300, 0));
    }
}
