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

        static void Run(BuildTarget target, string path)
        {
            EchoSceneBuilder.Build();
            if (target == BuildTarget.Android) ResolveAndroidDependencies();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { EchoSceneBuilder.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[EchoBuild] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"{target} build {report.summary.result}");
        }
    }
}
