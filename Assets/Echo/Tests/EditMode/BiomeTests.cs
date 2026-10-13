using Echo.Game.World;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Themed Biomes (value-adds doc) and the night the character flourishes react to.</summary>
    public class BiomeTests
    {
        [Test]
        public void TheRunGoesGraveyard_Castle_Space()
        {
            Assert.AreEqual(Biome.Graveyard, Biomes.At(0f).Biome);
            Assert.AreEqual(Biome.Graveyard, Biomes.At(299f).Biome);
            Assert.AreEqual(Biome.Castle, Biomes.At(300f).Biome);
            Assert.AreEqual(Biome.Castle, Biomes.At(699f).Biome);
            Assert.AreEqual(Biome.Space, Biomes.At(700f).Biome);
            Assert.AreEqual(Biome.Space, Biomes.At(5000f).Biome);
        }

        [Test]
        public void EachBiome_HasFullyBlendedIn_WhenItIsAnnounced()
        {
            Assert.AreEqual(0f, Biomes.At(Biomes.CastleFrom - Biomes.BlendMetres).Castle);
            Assert.AreEqual(1f, Biomes.At(Biomes.CastleFrom).Castle, 1e-5f);
            Assert.AreEqual(1f, Biomes.At(Biomes.SpaceFrom).Space, 1e-5f);
            Assert.AreEqual(1f, Biomes.At(Biomes.SpaceFrom).Castle, 1e-5f, "Space keeps the Castle blend at 1, so its layer hands over cleanly");
        }

        [Test]
        public void NightFalls_InTheCastle_AndStaysThroughSpace()
        {
            Assert.AreEqual(0f, Biomes.At(100f).Night);
            Assert.AreEqual(0f, Biomes.At(Biomes.DuskFrom).Night);
            float dusk = Biomes.At((Biomes.DuskFrom + Biomes.NightFrom) / 2f).Night;
            Assert.That(dusk, Is.InRange(0.3f, 0.7f));
            Assert.AreEqual(1f, Biomes.At(Biomes.NightFrom).Night, 1e-5f);
            Assert.AreEqual(1f, Biomes.At(900f).Night, 1e-5f);
        }
    }
}
