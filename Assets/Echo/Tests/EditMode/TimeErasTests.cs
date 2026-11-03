using Echo.Game.World;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>World: Time Run: one era every 250 m, the last carrying on for the rest of the run.</summary>
    public class TimeErasTests
    {
        [TestCase(0, 0)]
        [TestCase(249, 0)]
        [TestCase(250, 1)]
        [TestCase(499, 1)]
        [TestCase(500, 2)]
        [TestCase(750, 3)]
        [TestCase(5000, 3)]
        public void EraByDistance(int metres, int era) => Assert.AreEqual(era, TimeEras.At(metres, 4));

        [Test]
        public void NoEras_IsNone() => Assert.AreEqual(-1, TimeEras.At(300, 0));
    }
}
