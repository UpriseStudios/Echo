using Echo.Editor.Art;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echo.Editor
{
    /// <summary>Post-processing (bloom for the glow), the camera, and the app icon.</summary>
    public static partial class EchoSceneBuilder
    {
        const string PostProfilePath = SettingsDir + "/EchoPost.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        /// <summary>The 2D renderer needs post-process data and the pipeline needs HDR for bloom on glow colours.</summary>
        static void EnsurePostProcessing(UniversalRenderPipelineAsset pipeline)
        {
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (renderer != null && data != null)
            {
                var so = new SerializedObject(renderer);
                var prop = so.FindProperty("m_PostProcessData");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static VolumeProfile EnsurePostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            // Only HDR colours above 1 (ghosts, trails, scarf) bloom; the flat world stays crisp.
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(2.4f);
            bloom.scatter.Override(0.78f);
            bloom.highQualityFiltering.Override(false);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void AddPostVolume(VolumeProfile profile)
        {
            var go = new GameObject("PostFX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void EnablePostOnCamera(GameObject cameraGo)
        {
            var data = cameraGo.GetComponent<UniversalAdditionalCameraData>() ?? cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        static void SetAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(EchoArt.IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            SetAndroidIcons(icon);
        }

        // Android adaptive icons: the launcher masks, crops and moves two layers (background, foreground). Unity 6.6
        // only has the Adaptive kind (every Android version it supports uses them).
        static void SetAndroidIcons(Texture2D flat)
        {
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(EchoArt.AdaptiveBackgroundPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(EchoArt.AdaptiveForegroundPath);
            if (background == null || foreground == null || flat == null) return;
            var android = NamedBuildTarget.Android;

            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon i in adaptive) i.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
        }
    }
}
