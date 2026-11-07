using Echo.Editor.Art;
using Echo.Game.Flow;
using Echo.Game.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace Echo.Editor
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): the Far Far Future's real-time 3D city. A Universal (3D) renderer beside the
    /// game's 2D renderer in the pipeline's list, a perspective camera on that renderer seeing only the Far3D layer, a
    /// moonlight, the city (Far3DScene, built at runtime from these materials) and Far3DView, which shows it behind the
    /// gameplay; the reduced-effects video loop (Content/Video/farfar_loop.mp4, made by Echo/Generate Far Far Future Loop)
    /// is wired when present. Fog for the 3D camera is part of the scene's lighting settings, so its shader variants ship.
    /// </summary>
    public static partial class EchoSceneBuilder
    {
        const string Renderer3DPath = SettingsDir + "/Echo3DRenderer.asset";
        public const string Far3DLoopPath = "Assets/Echo/Content/Video/farfar_loop.mp4";

        // The Universal renderer, added to the pipeline's renderer list once; returns its index there.
        static int EnsureUniversalRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, Renderer3DPath);
                ResourceReloader.ReloadAllNullIn(data, "Packages/com.unity.render-pipelines.universal");
            }
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) return i;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return list.arraySize - 1;
        }

        static Material Far3DMaterial(string name, string shader, Texture baseMap, Color colour, Texture emission = null, Color emissionColour = default)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var s = Shader.Find(shader) ?? throw new System.InvalidOperationException(shader + " not found");
            if (m == null)
            {
                m = new Material(s) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = s;
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", colour);
            if (emission != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", emissionColour);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Far3DView BuildFar3D(GameObject world, Camera main, Material spriteMaterial)
        {
            int layer = EnsureLayer(Far3DView.LayerName);
            main.cullingMask &= ~(1 << layer);
            int renderer = EnsureUniversalRenderer();

            Texture2D Tex(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            Material tower = Far3DMaterial("Far3DTower", "Universal Render Pipeline/Simple Lit", Tex(Far3DArt.Facade), Color.white,
                Tex(Far3DArt.Windows), new Color(1.1f, 1.1f, 1.15f));
            Material ground = Far3DMaterial("Far3DGround", "Universal Render Pipeline/Unlit", Tex(Far3DArt.Ground), new Color(1.4f, 1.4f, 1.5f));
            // A tile every 100 units, which divides the city's repeat, so the street never jumps (1200 x Copies * Period).
            ground.SetTextureScale("_BaseMap", new Vector2(12f, 6f * Far3DScene.Period / 100f));
            // Art fix phase 3: the sky, the skyline and the planet never fog (Echo/Far3D Unfogged); the lights are fogged unlit.
            Material sky = Far3DMaterial("Far3DSky", "Echo/Far3D Unfogged", Tex(Far3DArt.Sky), Color.white);
            Material unfogged = Far3DMaterial("Far3DUnfogged", "Echo/Far3D Unfogged", Texture2D.whiteTexture, Color.white);
            Material strip = Far3DMaterial("Far3DTraffic", "Universal Render Pipeline/Unlit", Texture2D.whiteTexture, Color.white);

            var camGo = new GameObject("Far3DCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = Far3DScene.FieldOfView;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Far3DArt.FogColour;
            cam.cullingMask = 1 << layer;
            cam.depth = main.depth - 2;
            cam.enabled = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.SetRenderer(renderer);
            camData.renderPostProcessing = false;
            camData.renderShadows = false;

            var sceneGo = new GameObject("Far3DScene") { layer = layer };
            var scene = sceneGo.AddComponent<Far3DScene>();
            Wire(scene, "_tower", tower);
            Wire(scene, "_ground", ground);
            Wire(scene, "_sky", sky);
            Wire(scene, "_unfogged", unfogged);
            Wire(scene, "_strip", strip);
            var lightGo = new GameObject("Far3DMoonlight") { layer = layer };
            lightGo.transform.SetParent(sceneGo.transform, false);
            // From the left and a little ahead: the canyon's right wall lit, its left wall in shade, roofs catching the light.
            lightGo.transform.rotation = Quaternion.Euler(35f, 70f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.66f, 0.74f, 1f);
            light.intensity = 2f;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << layer;

            // Exponential coloured fog for the 3D camera (sprites don't use it), the sky's horizon colour, so the city fades
            // into the horizon's glow: about 65% clear at the street the gameplay stands over, a third at 400 units, gone by 1500.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Far3DArt.FogColour;
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.08f, 0.16f);

            var view = Child<Far3DView>(world, "Far3D");
            var screenGo = new GameObject("Screen");
            screenGo.transform.SetParent(view.transform, false);
            screenGo.AddComponent<MeshFilter>();
            var screen = screenGo.AddComponent<MeshRenderer>();
            var video = view.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.isLooping = true;
            Wire(view, "_camera", cam);
            Wire(view, "_scene", scene);
            Wire(view, "_screen", screen);
            Wire(view, "_material", spriteMaterial);
            Wire(view, "_video", video);
            var loop = AssetDatabase.LoadAssetAtPath<VideoClip>(Far3DLoopPath);
            if (loop != null) Wire(view, "_loop", loop);
            return view;
        }
    }
}
