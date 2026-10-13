using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using Echo.Core.Challenges;
using UnityEditor.iOS.Xcode;
#endif

namespace Echo.Editor
{
    /// <summary>
    /// Version 2.0: iOS Universal Links for challenge links (https://echo.uprisestudios.co.uk/c/...). Adds the
    /// Associated Domains entitlement to the exported Xcode project; iOS checks it against the site's
    /// .well-known/apple-app-site-association. The App ID also needs the Associated Domains capability turned on
    /// in the Apple Developer portal.
    /// </summary>
    public static class IosAssociatedDomains
    {
        [PostProcessBuild(10)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
#if UNITY_IOS
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var capabilities = new ProjectCapabilityManager(projectPath, "Unity-iPhone/Echo.entitlements", null, project.GetUnityMainTargetGuid());
            capabilities.AddAssociatedDomains(new[] { "applinks:" + ChallengeLinks.Host });
            capabilities.WriteToFile();
#endif
        }
    }
}
