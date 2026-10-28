using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using System.IO;
using UnityEditor.iOS.Xcode;
#endif

namespace Echo.Editor
{
    /// <summary>
    /// Saving clips to Photos (a user revision, Plugins/iOS/EchoGallery.mm): links Photos.framework into UnityFramework,
    /// where plugins compile, and adds the add-only permission text iOS shows the first time a clip is saved.
    /// </summary>
    public static class IosPhotoLibrary
    {
        public const string AddUsage = "Echo saves your clips to Photos so you can post them.";

        [PostProcessBuild(11)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
#if UNITY_IOS
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "Photos.framework", false);
            project.WriteToFile(projectPath);

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSPhotoLibraryAddUsageDescription", AddUsage);
            plist.WriteToFile(plistPath);
#endif
        }
    }
}
