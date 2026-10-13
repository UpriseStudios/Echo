using System.IO;
using System.Xml;
using Echo.Core.Challenges;
using UnityEditor.Android;

namespace Echo.Editor
{
    /// <summary>
    /// Adds the Ghost Challenge intent filters to the launcher activity of the exported Gradle project, so a
    /// challenge link opens Echo: the game's own scheme (echoghost://challenge/CODE for Lite challenges and
    /// echoghost://c/OWNER/ID from the web page's button) and, since 2.0, verified Android App Links for
    /// https://echo.uprisestudios.co.uk/c/... (checked against the site's .well-known/assetlinks.json). Done at
    /// export rather than with a hand-kept manifest, so Unity's own manifest stays in charge of everything else.
    /// </summary>
    public sealed class AndroidDeepLinks : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path is the unityLibrary module; the launcher module sits beside it.
            foreach (string manifest in new[]
                     {
                         Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                         Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"),
                     })
                if (File.Exists(manifest)) AddFilters(manifest);
        }

        static void AddFilters(string file)
        {
            var doc = new XmlDocument();
            doc.Load(file);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);
            XmlNodeList launchers = doc.SelectNodes("//activity[intent-filter/category[@android:name='android.intent.category.LAUNCHER']]", ns);
            if (launchers == null || launchers.Count == 0) return;
            bool changed = false;
            foreach (XmlElement activity in launchers)
            {
                if (activity.SelectSingleNode($"intent-filter/data[@android:scheme='{ChallengeCode.Scheme}' and @android:host='c']", ns) == null)
                {
                    XmlElement filter = Filter(doc, verify: false);
                    filter.AppendChild(Data(doc, ChallengeCode.Scheme, "challenge", null));
                    filter.AppendChild(Data(doc, ChallengeCode.Scheme, "c", null));
                    activity.AppendChild(filter);
                    changed = true;
                }
                if (activity.SelectSingleNode($"intent-filter/data[@android:host='{ChallengeLinks.Host}']", ns) == null)
                {
                    XmlElement filter = Filter(doc, verify: true);
                    filter.AppendChild(Data(doc, "https", ChallengeLinks.Host, "/c/"));
                    activity.AppendChild(filter);
                    changed = true;
                }
            }
            if (changed) doc.Save(file);
        }

        static XmlElement Filter(XmlDocument doc, bool verify)
        {
            XmlElement filter = doc.CreateElement("intent-filter");
            if (verify) filter.SetAttribute("autoVerify", AndroidNs, "true");
            filter.AppendChild(Element(doc, "action", "android.intent.action.VIEW"));
            filter.AppendChild(Element(doc, "category", "android.intent.category.DEFAULT"));
            filter.AppendChild(Element(doc, "category", "android.intent.category.BROWSABLE"));
            return filter;
        }

        static XmlElement Data(XmlDocument doc, string scheme, string host, string pathPrefix)
        {
            XmlElement data = doc.CreateElement("data");
            data.SetAttribute("scheme", AndroidNs, scheme);
            data.SetAttribute("host", AndroidNs, host);
            if (pathPrefix != null) data.SetAttribute("pathPrefix", AndroidNs, pathPrefix);
            return data;
        }

        static XmlElement Element(XmlDocument doc, string tag, string name)
        {
            XmlElement e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }
    }
}
