using System.IO;
using System.Xml;
using Echo.Core.Challenges;
using UnityEditor.Android;

namespace Echo.Editor
{
    /// <summary>
    /// Adds the Ghost Challenge intent filter (echoghost://challenge/...) to the launcher activity of the exported
    /// Gradle project, so tapping a challenge link opens Echo. Done at export rather than with a hand-kept
    /// manifest, so Unity's own manifest stays in charge of everything else.
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
                if (File.Exists(manifest)) AddFilter(manifest);
        }

        static void AddFilter(string file)
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
                if (activity.SelectSingleNode($"intent-filter/data[@android:scheme='{ChallengeCode.Scheme}']", ns) != null) continue;
                XmlElement filter = doc.CreateElement("intent-filter");
                filter.AppendChild(Element(doc, "action", "android.intent.action.VIEW"));
                filter.AppendChild(Element(doc, "category", "android.intent.category.DEFAULT"));
                filter.AppendChild(Element(doc, "category", "android.intent.category.BROWSABLE"));
                XmlElement data = doc.CreateElement("data");
                data.SetAttribute("scheme", AndroidNs, ChallengeCode.Scheme);
                data.SetAttribute("host", AndroidNs, "challenge");
                filter.AppendChild(data);
                activity.AppendChild(filter);
                changed = true;
            }
            if (changed) doc.Save(file);
        }

        static XmlElement Element(XmlDocument doc, string tag, string name)
        {
            XmlElement e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }
    }
}
