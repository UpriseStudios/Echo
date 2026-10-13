#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Echo.Editor
{
    /// <summary>
    /// iOS needs NSUserTrackingUsageDescription for the ATT prompt (tech spec 19); the iOS support package
    /// does not add it, so it is written into Info.plist after every iOS build.
    /// </summary>
    public static class IosTrackingPlist
    {
        const string Description =
            "Lets ad partners show rewarded ads that suit you. Echo only shows ads when you choose to watch one.";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", Description);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
