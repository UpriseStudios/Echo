using Echo.Core.Courses;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class ContentVersionTests
    {
        const string Spiky = @"{ ""id"": ""s"", ""tier"": 1, ""length"": 10000, ""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 600 }] }";

        [Test]
        public void IsStable_AndEightHexChars()
        {
            string v = ContentVersion.Compute(TestCourses.Content(), SimConfig.Default);
            Assert.AreEqual(v, ContentVersion.Compute(TestCourses.Content(), SimConfig.Default));
            StringAssert.IsMatch("^[0-9a-f]{8}$", v);
        }

        [Test]
        public void IgnoresWhitespace()
        {
            string compact = Spiky.Replace(" ", "");
            string crlf = Spiky.Replace(", ", ",\r\n  ");
            Assert.AreEqual(
                ContentVersion.Compute(TestCourses.Library(compact), SimConfig.Default),
                ContentVersion.Compute(TestCourses.Library(crlf), SimConfig.Default));
        }

        [Test]
        public void ChangesWhenAChunkValueChanges()
        {
            string tweaked = Spiky.Replace(@"""h"": 600", @"""h"": 601");
            Assert.AreNotEqual(
                ContentVersion.Compute(TestCourses.Library(Spiky), SimConfig.Default),
                ContentVersion.Compute(TestCourses.Library(tweaked), SimConfig.Default));
        }

        [Test]
        public void ChangesWhenASimConstantChanges()
        {
            var d = SimConfig.Default;
            var heavier = new SimConfig(d.TickRate, d.RunSpeedBase, d.SpeedPerClearPct, d.MaxSpeedPct,
                d.Gravity + 1, d.JumpVelocity, d.MaxFallSpeed, d.CoyoteTicks, d.JumpBufferTicks,
                d.PlayerHitboxW, d.PlayerHitboxH, d.GhostHitboxW, d.GhostHitboxH,
                d.GhostGraceTicks, d.NearMissDistance, d.DeathFloorY, d.GhostCap, d.ReviveInvulnTicks);
            var lib = TestCourses.Library(Spiky);
            Assert.AreNotEqual(ContentVersion.Compute(lib, d), ContentVersion.Compute(lib, heavier));
        }
    }
}
