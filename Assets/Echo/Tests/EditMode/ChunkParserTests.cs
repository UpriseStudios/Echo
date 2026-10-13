using System.IO;
using Echo.Core.Courses;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class ChunkParserTests
    {
        const string SpecExample = @"{
            ""id"": ""t2_ceiling_squeeze"",
            ""tier"": 2,
            ""length"": 10000,
            ""gaps"":     [{ ""x"": 4000, ""w"": 2500 }],
            ""spikes"":   [{ ""x"": 7000, ""w"": 800, ""h"": 700 }],
            ""ceilings"": [{ ""x"": 2000, ""w"": 3000, ""y"": 2600 }],
            ""movers"":   [{ ""x"": 6000, ""w"": 800, ""h"": 800, ""yMin"": 0, ""yMax"": 2400, ""period"": 120, ""phase"": 0 }]
        }";

        [Test]
        public void Parses_TheTechSpecExample()
        {
            Chunk c = ChunkParser.Parse(SpecExample);
            Assert.AreEqual("t2_ceiling_squeeze", c.Id);
            Assert.AreEqual(2, c.Tier);
            Assert.AreEqual(10000, c.Length);
            Assert.AreEqual(4000, c.Gaps[0].X);
            Assert.AreEqual(2500, c.Gaps[0].W);
            Assert.AreEqual(700, c.Spikes[0].H);
            Assert.AreEqual(2600, c.Ceilings[0].Y);
            Assert.AreEqual(2400, c.Movers[0].YMax);
            Assert.AreEqual(120, c.Movers[0].Period);
        }

        [Test]
        public void MissingArrays_AreEmpty()
        {
            Chunk c = ChunkParser.Parse(TestCourses.FlatTier1);
            Assert.IsEmpty(c.Gaps);
            Assert.IsEmpty(c.Spikes);
            Assert.IsEmpty(c.Ceilings);
            Assert.IsEmpty(c.Movers);
        }

        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000.5 }", "non-integer")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 1e4 }", "non-integer")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000, ""spike"": [] }", "unknown key")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000, ""gaps"": [{ ""x"": 4000, ""w"": 100, ""h"": 1 }] }", "unknown key")]
        [TestCase(@"{ ""tier"": 1, ""length"": 10000 }", "\"id\"")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 5, ""length"": 10000 }", "tier")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000, ""gaps"": [{ ""x"": 500, ""w"": 1000 }] }", "clear floor")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000, ""spikes"": [{ ""x"": 8500, ""w"": 600, ""h"": 600 }] }", "clear floor")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000, ""spikes"": [{ ""x"": 5000, ""w"": 0, ""h"": 600 }] }", "positive")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 4, ""length"": 10000, ""movers"": [{ ""x"": 5000, ""w"": 800, ""h"": 800, ""yMin"": 0, ""yMax"": 2400, ""period"": 1, ""phase"": 0 }] }", "period")]
        [TestCase(@"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000 } x", "trailing")]
        [TestCase(@"{ ""id"": ""a"", ""id"": ""b"", ""tier"": 1, ""length"": 10000 }", "duplicate key")]
        public void Rejects_InvalidChunks(string json, string messageFragment)
        {
            var ex = Assert.Throws<ChunkFormatException>(() => ChunkParser.Parse(json));
            StringAssert.Contains(messageFragment, ex.Message);
        }

        [Test]
        public void Library_RejectsDuplicateIds()
        {
            Assert.Throws<ChunkFormatException>(() => TestCourses.Library(TestCourses.FlatTier1, TestCourses.FlatTier1));
        }

        [Test]
        public void Library_NeedsATier1Chunk()
        {
            Assert.Throws<ChunkFormatException>(() =>
                TestCourses.Library(@"{ ""id"": ""a"", ""tier"": 0, ""length"": 10000 }"));
        }

        [Test]
        public void Library_SortsByIdRegardlessOfInputOrder()
        {
            var a = @"{ ""id"": ""b"", ""tier"": 1, ""length"": 10000 }";
            var b = @"{ ""id"": ""a"", ""tier"": 1, ""length"": 10000 }";
            Assert.AreEqual("a", TestCourses.Library(a, b).Pool[0].Id);
            Assert.AreEqual("a", TestCourses.Library(b, a).Pool[0].Id);
        }

        [Test]
        public void ContentChunks_AllParse_WithIdMatchingFileName()
        {
            string[] files = Directory.GetFiles(TestCourses.ContentDir, "*.json");
            Assert.AreEqual(33, files.Length);
            foreach (string file in files)
                Assert.AreEqual(Path.GetFileNameWithoutExtension(file), ChunkParser.Parse(File.ReadAllText(file)).Id);
        }
    }
}
