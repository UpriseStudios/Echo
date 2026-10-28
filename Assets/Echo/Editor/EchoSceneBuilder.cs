using System;
using System.IO;
using System.Linq;
using Echo.Editor.Art;
using Echo.Game.Audio;
using Echo.Game.Flow;
using Echo.Game.UI;
using Echo.Game.Views;
using Echo.Services;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Echo.Editor
{
    /// <summary>
    /// Builds the scene, render and player settings, art, fonts, cosmetic catalog and placeholder audio from
    /// code, since nothing is hand-built. Menu: Echo/Build Scene.
    /// Batch: -executeMethod Echo.Editor.EchoSceneBuilder.Build -quit
    /// The brand (palette, type, materials), render setup and screens live in the other partial files.
    /// </summary>
    public static partial class EchoSceneBuilder
    {
        public const string ScenePath = "Assets/Echo/Scenes/Echo.unity";
        const string SettingsDir = "Assets/Echo/Settings";
        const string RendererPath = SettingsDir + "/Echo2DRenderer.asset";
        const string PipelinePath = SettingsDir + "/EchoURP.asset";
        const string MaterialDir = "Assets/Echo/Content/Materials";
        const string MaterialPath = MaterialDir + "/SpriteUnlit.mat";
        const string ChunkDir = "Assets/Echo/Content/Chunks";
        // In a Resources folder so the game can always load it, even if the scene's reference is lost (device
        // testing: a build shipped with no config, and so ran on mocks).
        const string ServiceConfigDir = SettingsDir + "/Resources";
        const string ServiceConfigPath = ServiceConfigDir + "/" + EchoServiceConfig.ResourceName + ".asset";
        const string FeatureFlagsPath = SettingsDir + "/FeatureFlags.asset";
        const string ApplicationId = "com.uprise.echo";
        const string TmpPackageInUgui = "Package Resources/TMP Essential Resources.unitypackage";
        const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Echo/Build Scene")]
        public static void Build()
        {
            EnsureTmpEssentials();
            EnsureFonts();
            DeepLinkSetup.Apply();
            var pipeline = EnsureUrp();
            EnsurePostProcessing(pipeline);
            Material material = EnsureSpriteMaterial();
            EchoArt.Generate();
            ConfigurePlayer();
            SetAppIcon();
            PlaceholderAudio.EnsureAll();
            var catalog = CosmeticCatalogBuilder.Build();
            var serviceConfig = EnsureServiceConfig();
            BuildScene(material, catalog, serviceConfig);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EchoSceneBuilder] built {ScenePath}");
        }

        static void EnsureTmpEssentials()
        {
            if (File.Exists(TmpSettings)) return;
            var ugui = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui")
                       ?? throw new InvalidOperationException("com.unity.ugui is not installed");
            string package = Path.Combine(ugui.resolvedPath, TmpPackageInUgui);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            int files = UnityPackageExtractor.Extract(package, projectRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[EchoSceneBuilder] unpacked {files} TextMeshPro essential files");
        }

        static UniversalRenderPipelineAsset EnsureUrp()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                EnsureFolder(SettingsDir);
                var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, "Packages/com.unity.render-pipelines.universal");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            return pipeline;
        }

        static Material EnsureSpriteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            EnsureFolder(MaterialDir);
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP 2D unlit sprite shader not found");
            material = new Material(shader) { name = "SpriteUnlit" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>Created once with placeholders and never overwritten, so real keys survive rebuilds.</summary>
        static EchoServiceConfig EnsureServiceConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EchoServiceConfig>(ServiceConfigPath);
            if (config != null) return config;
            EnsureFolder(ServiceConfigDir);
            config = ScriptableObject.CreateInstance<EchoServiceConfig>();
            AssetDatabase.CreateAsset(config, ServiceConfigPath);
            return config;
        }

        /// <summary>Created once with every feature on and never overwritten, so switches survive rebuilds.</summary>
        static FeatureFlags EnsureFeatureFlags()
        {
            var flags = AssetDatabase.LoadAssetAtPath<FeatureFlags>(FeatureFlagsPath);
            if (flags != null) return flags;
            EnsureFolder(SettingsDir);
            flags = ScriptableObject.CreateInstance<FeatureFlags>();
            AssetDatabase.CreateAsset(flags, FeatureFlagsPath);
            return flags;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.productName = "Echo";

            // tech spec 19: IL2CPP on both; Android ARMv7 + ARM64 (a user revision: older 32-bit phones too); Android App
            // Bundle, API 26+, target the latest installed;
            // iOS 15+ (tech spec 1). The application id is a placeholder until the store listing exists.
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                if (PlayerSettings.GetApplicationIdentifier(target).StartsWith("com.DefaultCompany", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(target)))
                    PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
            }
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            // Native debug symbols (function names only) inside the bundle, so Play Console can read native crashes and
            // ANRs without a separate symbols upload.
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            // tech spec 1: Input System only. No public API for this setting.
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 1)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.LogWarning("[EchoSceneBuilder] switched to the Input System; restart the editor for it to take effect");
                }
            }
        }

        static void BuildScene(Material material, Echo.Game.Cosmetics.CosmeticCatalog catalog, EchoServiceConfig serviceConfig)
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material ghostGlow = GlowMaterial("GhostGlow", 2.6f);
            Material scarfGlow = GlowMaterial("ScarfGlow", 1.4f);

            // Camera with post-processing for the glow.
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            cameraGo.transform.position = new Vector3(0, 0, -10);
            // 16-bit look: the world renders at 288 px across (9 units at 32 px per unit) and is scaled up to
            // fill the screen. CameraRig sets the height for each screen so the view stays 9 units wide.
            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = (int)EchoArt.WorldPpu;
            pixelPerfect.refResolutionX = CameraRig.PixelsAcross;
            pixelPerfect.refResolutionY = 512;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
            // Point, not RetroAA: the final stretch stays crisp rather than bilinear-soft at 3-4x.
            var pixelSo = new SerializedObject(pixelPerfect);
            pixelSo.FindProperty("m_FilterMode").enumValueIndex = (int)PixelPerfectCamera.PixelPerfectFilterMode.Point;
            pixelSo.ApplyModifiedPropertiesWithoutUndo();
            var cameraRig = cameraGo.AddComponent<CameraRig>();
            cameraGo.AddComponent<AudioListener>();
            // Highlights reel video clips record what the player hears.
            var audioTap = cameraGo.AddComponent<Echo.Game.Clips.ClipAudioTap>();
            EnablePostOnCamera(cameraGo);
            AddPostVolume(EnsurePostProfile());

            // World views. Everything sits at the origin so local and world positions match.
            var world = new GameObject("World");
            var background = Child<BackgroundView>(world, "Background");
            Wire(background, "_material", material);
            Wire(background, "_far", LoadSprite(EchoArt.MountainsPath));
            // Themed Biomes: the Graveyard is the near layer at the start, then the Castle, then Space.
            Wire(background, "_near", LoadSprite(EchoArt.GraveyardPath));
            Wire(background, "_castle", LoadSprite(EchoArt.CastlePath));
            Wire(background, "_castleBanners", LoadSprite(EchoArt.CastleBannersPath));
            Wire(background, "_space", LoadSprite(EchoArt.MoonSurfacePath));
            Wire(background, "_planet", LoadSprite(EchoArt.PlanetPath));

            // The character flourishes (a user revision): drawn by the equipped character's reaction.
            var ambient = Child<AmbientView>(world, "Ambient");
            Wire(ambient, "_material", material);
            Wire(ambient, "_star", LoadSprite(EchoArt.StarPath));
            Wire(ambient, "_shootingStar", LoadSprite(EchoArt.ShootingStarPath));
            Wire(ambient, "_ufo", LoadSprite(EchoArt.UfoPath));
            Wire(ambient, "_fullMoon", LoadSprite(EchoArt.FullMoonPath));
            WireArray(ambient, "_bat", new Object[] { LoadSprite(EchoArt.BatPath(0)), LoadSprite(EchoArt.BatPath(1)) });
            Wire(ambient, "_glow", LoadSprite(EchoArt.GlowPath));
            Wire(ambient, "_beam", LoadSprite(EchoArt.BeamPath));
            Wire(ambient, "_firefly", LoadSprite(EchoArt.FireflyPath));
            Wire(ambient, "_sparkle", LoadSprite(EchoArt.SparklePath));
            Wire(ambient, "_wisp", LoadSprite(EchoArt.WispPath));
            Wire(ambient, "_rain", LoadSprite(EchoArt.RainPath));
            Wire(ambient, "_fog", LoadSprite(EchoArt.FogPath));
            Wire(ambient, "_petal", LoadSprite(EchoArt.PetalPath));
            Wire(ambient, "_bolt", LoadSprite(EchoArt.BoltPath));
            Wire(ambient, "_shimmer", LoadSprite(EchoArt.ShimmerPath));
            Wire(ambient, "_ember", LoadSprite(EchoArt.EmberPath));
            Wire(ambient, "_frost", LoadSprite(EchoArt.FrostPath));
            Wire(ambient, "_frostCap", LoadSprite(EchoArt.FrostCapPath));
            Wire(ambient, "_icicles", LoadSprite(EchoArt.IciclesPath));
            Wire(ambient, "_glint", LoadSprite(EchoArt.GlintPath));
            Wire(ambient, "_torch", LoadSprite(EchoArt.TorchPath));
            {
                var so = new SerializedObject(ambient);
                SerializedProperty stones = so.FindProperty("_graveyardStones");
                stones.arraySize = PixelWorld.GraveyardStones.Length;
                for (int i = 0; i < PixelWorld.GraveyardStones.Length; i++)
                {
                    int x = PixelWorld.GraveyardStones[i];
                    stones.GetArrayElementAtIndex(i).vector2IntValue = new Vector2Int(x, PixelWorld.GraveyardTop(x));
                }
                SerializedProperty towers = so.FindProperty("_castleTowers");
                towers.arraySize = PixelWorld.TowerTops.Length;
                for (int i = 0; i < PixelWorld.TowerTops.Length; i++)
                    towers.GetArrayElementAtIndex(i).vector2IntValue = PixelWorld.TowerTops[i];
                SerializedProperty torches = so.FindProperty("_gateTorches");
                torches.arraySize = PixelWorld.GateTorches.Length;
                for (int i = 0; i < PixelWorld.GateTorches.Length; i++)
                    torches.GetArrayElementAtIndex(i).intValue = PixelWorld.GateTorches[i];
                so.FindProperty("_castleWallTop").intValue = PixelWorld.CastleWallTop;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Seasons doc: the season's effects layer, sorted behind the floor and everything on it.
            var seasonEffects = Child<SeasonEffectsView>(world, "SeasonEffects");
            Wire(seasonEffects, "_material", material);

            var courseView = Child<CourseView>(world, "Course");
            Wire(courseView, "_material", material);
            Wire(courseView, "_spike", LoadSprite(EchoArt.SpikePath));
            Wire(courseView, "_block", LoadSprite(EchoArt.BlockPath));
            Wire(courseView, "_groundTop", LoadSprite(EchoArt.GroundTopPath));
            Wire(courseView, "_groundFill", LoadSprite(EchoArt.GroundFillPath));

            var graves = Child<GraveViews>(world, "Graves");
            Wire(graves, "_material", material);
            Wire(graves, "_grave", LoadSprite(EchoArt.GravePath));

            var ghostViews = Child<GhostViews>(world, "Ghosts");
            Wire(ghostViews, "_material", ghostGlow);
            WireArray(ghostViews, "_runFrames", GhostFrames(PixelCharacters.Hooded));
            Wire(ghostViews, "_jumpFrame", LoadSprite(EchoArt.GhostJumpPath(PixelCharacters.Hooded)));
            Wire(ghostViews, "_horns", LoadSprite(EchoArt.HornsPath));
            Wire(ghostViews, "_bountyIcon", LoadSprite(EchoArt.CoinPath));
            Wire(ghostViews, "_nemesisLabel", NemesisLabel(world));

            var runnerView = Child<RunnerView>(world, "Player");
            Wire(runnerView, "_material", material);
            Wire(runnerView, "_scarfMaterial", scarfGlow);
            WireArray(runnerView, "_runFrames", RunnerFrames(PixelCharacters.Hooded));
            Wire(runnerView, "_jumpFrame", LoadSprite(EchoArt.CharacterJumpPath(PixelCharacters.Hooded)));

            var copycat = Child<CopycatView>(world, "Copycat");
            Wire(copycat, "_material", material);
            WireArray(copycat, "_runFrames", RunnerFrames(PixelCharacters.Hooded));
            Wire(copycat, "_jumpFrame", LoadSprite(EchoArt.CharacterJumpPath(PixelCharacters.Hooded)));

            var twistOverlay = Child<TwistOverlay>(world, "TwistOverlay");
            Wire(twistOverlay, "_material", material);
            Wire(twistOverlay, "_spotlight", LoadSprite(EchoArt.SpotlightPath));

            var shards = Child<ShardBurst>(world, "Shards");
            Wire(shards, "_material", material);
            Wire(graves, "_shards", shards);

            // Audio
            var audio = new GameObject("Audio");
            var music = audio.AddComponent<MusicPlayer>();
            WireArray(music, "_stems", PlaceholderAudio.StemNames.Select(n => Clip(n)).ToArray());
            var sfxGo = new GameObject("Sfx");
            sfxGo.transform.SetParent(audio.transform, false);
            sfxGo.AddComponent<AudioSource>().playOnAwake = false;
            var sfx = sfxGo.AddComponent<SfxPlayer>();
            Wire(sfx, "_jump", Clip(PlaceholderAudio.Jump));
            Wire(sfx, "_nearMiss", Clip(PlaceholderAudio.NearMiss));
            Wire(sfx, "_death", Clip(PlaceholderAudio.Death));
            Wire(sfx, "_ghostClear", Clip(PlaceholderAudio.Clear));
            Wire(sfx, "_nemesisSting", Clip(PlaceholderAudio.Sting));
            Wire(sfx, "_graveThud", Clip(PlaceholderAudio.Thud));
            Wire(sfx, "_chisel", Clip(PlaceholderAudio.Chisel));
            Wire(sfx, "_howl", Clip(PlaceholderAudio.Howl));

            // UI
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Labels pinned to world positions (gravestone epitaphs), drawn under every screen at full resolution.
            RectTransform worldLabels = Stretch(NewRect("WorldLabels", canvasGo.transform));
            var epitaph = MakeText(worldLabels, "Epitaph", "Run #1", 34, _medium, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 48));
            epitaph.rectTransform.pivot = new Vector2(0.5f, 1f);
            epitaph.color = new Color(1f, 1f, 1f, 0.85f);
            Wire(graves, "_epitaph", epitaph);
            Wire(graves, "_rig", cameraRig);

            RectTransform safe = Stretch(NewRect("SafeArea", canvasGo.transform));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            HudView hud = BuildHud(safe);
            TitleView title = BuildTitle(safe);
            DeathScreenView death = BuildDeathScreen(safe);
            StatsView stats = BuildStats(safe);
            SettingsView settings = BuildSettings(safe);
            SkinsView skins = BuildSkins(safe);
            ConsentView consent = BuildConsent(safe);
            ChallengeView challenge = BuildChallenge(safe);
            PowerUpsView powerUps = BuildPowerUps(safe);
            ReelsView reels = BuildReels(safe);
            ReplayView replay = BuildReplay(safe);
            ClipEditorView clipEditor = BuildClipEditor(safe);
            DailyLobbyView dailyLobby = BuildDailyLobby(safe);
            ElementsAwakenedView awakened = BuildElementsAwakened(safe);
            HowToPlayView howTo = BuildHowToPlay(safe);

            var flashRect = Stretch(NewRect("Flash", canvasGo.transform));
            var flashImage = flashRect.gameObject.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;
            var flash = flashRect.gameObject.AddComponent<ScreenFlash>();

            // Game
            var game = new GameObject("EchoGame").AddComponent<EchoGame>();
            var chunks = AssetDatabase.FindAssets("t:TextAsset", new[] { ChunkDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToArray();
            if (chunks.Length == 0) throw new InvalidOperationException($"no chunk JSON in {ChunkDir}");
            WireArray(game, "_chunkFiles", chunks);
            Wire(game, "_catalog", catalog);
            Wire(game, "_cameraRig", cameraRig);
            Wire(game, "_courseView", courseView);
            Wire(game, "_ghostViews", ghostViews);
            Wire(game, "_graves", graves);
            Wire(game, "_runnerView", runnerView);
            Wire(game, "_shards", shards);
            Wire(game, "_background", background);
            Wire(game, "_hud", hud);
            Wire(game, "_title", title);
            Wire(game, "_deathScreen", death);
            Wire(game, "_stats", stats);
            Wire(game, "_settings", settings);
            Wire(game, "_challenge", challenge);
            Wire(game, "_powerUpsView", powerUps);
            Wire(game, "_awakened", awakened);
            Wire(game, "_reels", reels);
            Wire(game, "_replay", replay);
            Wire(game, "_clipEditor", clipEditor);
            Wire(game, "_dailyLobby", dailyLobby);
            Wire(game, "_howTo", howTo);
            Wire(game, "_audioTap", audioTap);
            Wire(game, "_skins", skins);
            Wire(game, "_flash", flash);
            Wire(game, "_music", music);
            Wire(game, "_sfx", sfx);
            Wire(game, "_consentView", consent);
            // Loaded again here, not passed in from the start of the build: building the scene unloads unused assets,
            // which left the earlier reference dead and the scene saved with no config (so device builds ran on mocks).
            serviceConfig = EnsureServiceConfig();
            if (serviceConfig == null || !EditorUtility.IsPersistent(serviceConfig))
                throw new InvalidOperationException($"{ServiceConfigPath} did not load; a build without it runs on mock services");
            Wire(game, "_serviceConfig", serviceConfig);
            Wire(game, "_features", EnsureFeatureFlags());
            Wire(game, "_twistOverlay", twistOverlay);
            Wire(game, "_copycatView", copycat);
            Wire(game, "_ambient", ambient);
            Wire(game, "_seasonEffects", seasonEffects);
            WireArray(game, "_seasonThemes", SeasonThemes.Build());
            Wire(game, "_seasonSchedule", AssetDatabase.LoadAssetAtPath<TextAsset>(SeasonThemes.SchedulePath)
                ?? throw new InvalidOperationException($"missing {SeasonThemes.SchedulePath}"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static TMP_Text NemesisLabel(GameObject world)
        {
            var go = new GameObject("NemesisLabel", typeof(RectTransform));
            go.transform.SetParent(world.transform, false);
            var label = go.AddComponent<TextMeshPro>();
            label.font = _bold;
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(6f, 1f);
            label.text = "The First";
            label.sortingOrder = 7;
            return label;
        }

        static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudio.PathFor(name))
            ?? throw new InvalidOperationException($"audio clip missing: {PlaceholderAudio.PathFor(name)}");

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
