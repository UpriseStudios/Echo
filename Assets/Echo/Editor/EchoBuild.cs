using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// Release builds with the tech spec 19 settings (applied by EchoSceneBuilder).
    /// Batch: -executeMethod Echo.Editor.EchoBuild.Android (or .Ios) -quit
    /// </summary>
    public static class EchoBuild
    {
        const string OutDir = "Builds";

        [MenuItem("Echo/Build/Android App Bundle")]
        public static void Android() => Run(BuildTarget.Android, Path.Combine(OutDir, "Android", "Echo.aab"));

        /// <summary>
        /// The same release bundle, but talking to the UGS development environment (ECHO_DEV_SERVER), for testing server
        /// changes through Play's internal testing track. The title shows DEV SERVER; never upload it to production.
        /// </summary>
        [MenuItem("Echo/Build/Android App Bundle (development server)")]
        public static void AndroidDevelopmentServer() =>
            Run(BuildTarget.Android, Path.Combine(OutDir, "Android", "Echo-devserver.aab"), DevServerDefine);

        public const string DevServerDefine = "ECHO_DEV_SERVER";

        /// <summary>
        /// A release APK to sideload for recording footage (marketing content plan): the store game (production backend,
        /// release performance) but signed with Android's debug key, so it builds without the keystore passwords, batch
        /// included. It can't install over a Play-installed Echo (different signature) and challenge links won't open
        /// it. Never upload it. The project's own settings are restored afterwards. Every skin and pack is unlocked in it
        /// (<see cref="RecordingDefine"/>), so footage can show any character.
        /// </summary>
        [MenuItem("Echo/Build/Android APK (recording, debug-signed)")]
        public static void AndroidRecordingApk()
        {
            bool customKeystore = PlayerSettings.Android.useCustomKeystore;
            try
            {
                // After the scene build, which sets the bundle flag, so this build's own choices win.
                Run(BuildTarget.Android, Path.Combine(OutDir, "Android", "Echo-recording.apk"), () =>
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    EditorUserBuildSettings.buildAppBundle = false;
                }, RecordingDefine);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                EditorUserBuildSettings.buildAppBundle = true;
            }
        }

        public const string RecordingDefine = "ECHO_RECORDING";

        [MenuItem("Echo/Build/iOS Xcode Project")]
        public static void Ios() => Run(BuildTarget.iOS, Path.Combine(OutDir, "iOS"));

        /// <summary>
        /// Runs the Mobile Dependency Resolver so the LevelPlay and IAP Android libraries are written into
        /// mainTemplate.gradle. It is loaded by its own version handler, so it is called by name.
        /// </summary>
        [MenuItem("Echo/Build/Resolve Android Dependencies")]
        public static void ResolveAndroidDependencies()
        {
            Type resolver = Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
            MethodInfo resolve = resolver?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null)
                ?? throw new InvalidOperationException("Mobile Dependency Resolver not loaded (Assets/MobileDependencyResolver)");
            bool ok = (bool)resolve.Invoke(null, new object[] { true });
            Debug.Log($"[EchoBuild] Android dependencies resolved: {ok}");
            if (!ok) throw new InvalidOperationException("Android dependency resolution failed");
        }

        static void Run(BuildTarget target, string path, params string[] defines) => Run(target, path, null, defines);

        static void Run(BuildTarget target, string path, Action afterSceneBuild, params string[] defines)
        {
            EchoSceneBuilder.Build();
            afterSceneBuild?.Invoke();
            if (target == BuildTarget.Android) ResolveAndroidDependencies();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { EchoSceneBuilder.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = defines,   // for this build only; the project's settings are untouched
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[EchoBuild] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"{target} build {report.summary.result}");
        }
    }
}
