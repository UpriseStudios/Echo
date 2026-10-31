using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Echo.Editor
{
    /// <summary>
    /// Rule 4: nothing starts before the privacy choice. The Play Games SDK starts itself at launch through its
    /// PlayGamesInitProvider (and signs in, showing Play Games' welcome); this removes the provider from the launcher's
    /// manifest at Gradle export, so Play Games starts only when PlayGamesService first touches the plugin, which
    /// initialises the SDK itself, after consent and only once Play Games is set up.
    /// </summary>
    public sealed class PlayGamesManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string ToolsNs = "http://schemas.android.com/tools";
        public const string InitProvider = "com.google.android.gms.games.provider.PlayGamesInitProvider";

        public int callbackOrder => 11;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifest)) RemoveInitProvider(manifest);
        }

        public static void RemoveInitProvider(string file)
        {
            var doc = new XmlDocument();
            doc.Load(file);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);
            if (doc.SelectSingleNode($"//application/provider[@android:name='{InitProvider}']", ns) != null) return;
            XmlElement application = (XmlElement)doc.SelectSingleNode("/manifest/application");
            if (application == null) return;
            doc.DocumentElement.SetAttribute("xmlns:tools", ToolsNs);
            XmlElement provider = doc.CreateElement("provider");
            provider.SetAttribute("name", AndroidNs, InitProvider);
            provider.SetAttribute("authorities", AndroidNs, "${applicationId}.playgamesinitprovider");
            provider.SetAttribute("node", ToolsNs, "remove");
            application.AppendChild(provider);
            doc.Save(file);
        }
    }
}
