using System.Collections.Generic;
using System.Linq;
using System.Text;
using Echo.Core.Achievements;
using Echo.Editor;
using Echo.Game.Persistence;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Google Play Games: the cloud save's format and conflict policy, and matching achievements to Play's ids.</summary>
    public class PlayGamesTests
    {
        static SaveData Save(int runs, int best) => new SaveData { Best = best, Stats = new StatsSaveData { TotalRuns = runs } };

        [Test]
        public void CloudSave_RoundTrips()
        {
            SaveData save = Save(312, 1240);
            save.Cosmetics.Owned.Add("char_wizard");
            SaveData back = CloudSave.Unpack(CloudSave.Pack(save));
            Assert.IsNotNull(back);
            Assert.AreEqual(312, back.Stats.TotalRuns);
            Assert.AreEqual(1240, back.Best);
            CollectionAssert.Contains(back.Cosmetics.Owned, "char_wizard");
        }

        [Test]
        public void CloudSave_RefusesGarbageAndNewerVersions()
        {
            Assert.IsNull(CloudSave.Unpack(null));
            Assert.IsNull(CloudSave.Unpack(new byte[0]));
            Assert.IsNull(CloudSave.Unpack(Encoding.UTF8.GetBytes("not a save")));
            SaveData newer = Save(1, 1);
            newer.Schema = SaveData.CurrentSchema + 1;
            Assert.IsNull(CloudSave.Unpack(CloudSave.Pack(newer)), "a save from a newer Echo is never taken");
        }

        [Test]
        public void ConflictPolicy_MoreRunsWins_ThenTheBest()
        {
            Assert.IsTrue(CloudSave.CloudIsAhead(Save(10, 500), Save(11, 100)), "more runs");
            Assert.IsFalse(CloudSave.CloudIsAhead(Save(11, 100), Save(10, 500)), "fewer runs, whatever the best");
            Assert.IsTrue(CloudSave.CloudIsAhead(Save(10, 500), Save(10, 501)), "same runs, higher best");
            Assert.IsFalse(CloudSave.CloudIsAhead(Save(10, 500), Save(10, 500)), "the same save is not offered");
            Assert.IsFalse(CloudSave.CloudIsAhead(Save(5, 300), Save(0, 0)), "an empty cloud never replaces progress");
        }

        // Play Console names each achievement's resource from its title, lower-case with underscores.
        static string Resources(IEnumerable<(string Name, string Id)> achievements)
        {
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n");
            xml.Append("  <string name=\"app_id\" translatable=\"false\">123456789012</string>\n");
            xml.Append("  <string name=\"package_name\" translatable=\"false\">com.uprise.echo</string>\n");
            foreach (var (name, id) in achievements)
                xml.Append($"  <string name=\"achievement_{name}\" translatable=\"false\">{id}</string>\n");
            return xml.Append("</resources>\n").ToString();
        }

        static string Snake(string title, bool spellAccents)
        {
            var sb = new StringBuilder();
            foreach (char c in title.ToLowerInvariant())
            {
                char l = spellAccents ? c switch { 'é' => 'e', 'à' => 'a', _ => c } : c;
                if ((l >= 'a' && l <= 'z') || (l >= '0' && l <= '9')) sb.Append(l);
                else if (l == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        [Test]
        public void Match_FindsEveryAchievementByItsTitle([Values(false, true)] bool spellAccents)
        {
            string xml = Resources(AchievementCatalog.All.Select((d, i) => (Snake(d.Title, spellAccents), "CgkI" + i)));
            var matched = PlayGamesSetup.Match(xml, out List<string> unmatched);
            CollectionAssert.IsEmpty(unmatched);
            Assert.AreEqual(AchievementCatalog.All.Length, matched.Count);
            for (int i = 0; i < AchievementCatalog.All.Length; i++)
            {
                Assert.AreEqual(AchievementCatalog.All[i].Id, matched[i].EchoId);
                Assert.AreEqual("CgkI" + i, matched[i].PlayId);
            }
        }

        [Test]
        public void Match_ListsWhatPlayDoesNotHave()
        {
            string xml = Resources(new[] { ("hello_again", "CgkIa"), ("not_an_echo_achievement", "CgkIb") });
            var matched = PlayGamesSetup.Match(xml, out List<string> unmatched);
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual(AchievementIds.HelloAgain, matched[0].EchoId);
            Assert.AreEqual(AchievementCatalog.All.Length - 1, unmatched.Count);
        }

        [Test]
        public void Titles_AreDistinctOnPlay()
        {
            // Play needs unique names, and the import matches on them.
            var plain = AchievementCatalog.All.Select(d => Snake(d.Title, true).Replace("_", "")).ToList();
            CollectionAssert.AllItemsAreUnique(plain);
        }
    }
}
