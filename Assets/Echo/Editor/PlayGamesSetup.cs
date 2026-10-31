using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Echo.Core.Achievements;
using Echo.Services;
using Echo.Services.PlayGames;
using UnityEditor;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// Echo > Play Games > Import Resources (StoreListing/PlayGames/README.md): takes the Android resources XML that
    /// Play Console gives for the game's achievements, runs the Play Games plugin's setup with it (the APP_ID in the
    /// manifest; the web client id from EchoServiceConfig) and matches each Echo achievement to its Play id by name,
    /// since the achievements were imported from the game's own titles (StoreListing/PlayGames/make_play_games.py).
    /// </summary>
    public static class PlayGamesSetup
    {
        public const string ResourcesPath = "Assets/Echo/Settings/PlayGamesResources.xml";
        const string ConfigPath = "Assets/Echo/Settings/Resources/" + EchoServiceConfig.ResourceName + ".asset";
        const string Prefix = "achievement_";

        [MenuItem("Echo/Play Games/Import Resources")]
        public static void Import()
        {
            if (!File.Exists(ResourcesPath))
            {
                Debug.LogError($"[PlayGames] Save Play Console's resources XML (Achievements > Get resources > Android) as {ResourcesPath}.");
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<EchoServiceConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[PlayGames] No service config at {ConfigPath}: run Echo/Build Scene first.");
                return;
            }
            string xml = File.ReadAllText(ResourcesPath);
            string webClientId = EchoServiceConfig.IsSet(config.PlayGamesWebClientId) ? config.PlayGamesWebClientId : null;
            if (!GooglePlayGames.Editor.GPGSAndroidSetupUI.PerformSetup(webClientId, "Assets/GooglePlayGames/Generated", "GPGSIds", xml, null))
            {
                Debug.LogError("[PlayGames] The plugin's setup refused the resources (see the messages above): check the XML and the web client id.");
                return;
            }

            config.PlayGamesAchievements = Match(xml, out List<string> unmatched);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayGames] Set up: {config.PlayGamesAchievements.Count} of {AchievementCatalog.All.Length} achievements matched."
                      + (webClientId == null ? " No web client id yet: the online account won't follow Play Games." : ""));
            if (unmatched.Count > 0) Debug.LogWarning("[PlayGames] Not on Play (or renamed): " + string.Join(", ", unmatched));
        }

        /// <summary>Echo achievements and their Play ids from the resources XML; <paramref name="unmatched"/> lists the rest.</summary>
        public static List<PlayGamesAchievement> Match(string resourcesXml, out List<string> unmatched)
        {
            var play = new Dictionary<string, string>();
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(resourcesXml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc.Load(reader);
            foreach (XmlNode node in doc.GetElementsByTagName("string"))
            {
                string name = node.Attributes?["name"]?.Value;
                if (name == null || !name.StartsWith(Prefix)) continue;
                play[Plain(name.Substring(Prefix.Length))] = node.InnerText.Trim();
            }

            var matched = new List<PlayGamesAchievement>();
            unmatched = new List<string>();
            foreach (AchievementDef def in AchievementCatalog.All)
            {
                // Play names resources from the title; letters it can't spell (é, à) are either dropped or plain.
                if (play.TryGetValue(Plain(def.Title), out string id) || play.TryGetValue(Plain(def.Title, dropAccents: true), out id))
                    matched.Add(new PlayGamesAchievement { EchoId = def.Id, PlayId = id });
                else
                    unmatched.Add(def.Id);
            }
            return matched;
        }

        // Lower-case letters and digits only: "That Was Me?!" and "that_was_me" are both "thatwasme".
        static string Plain(string s, bool dropAccents = false)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    if (dropAccents && sb.Length > 0) sb.Length--;   // "é" was "e" + accent: drop both
                    continue;
                }
                char l = char.ToLowerInvariant(c);
                if ((l >= 'a' && l <= 'z') || (l >= '0' && l <= '9')) sb.Append(l);
            }
            return sb.ToString();
        }
    }
}
