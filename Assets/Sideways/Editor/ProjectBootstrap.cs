using System;
using System.Collections.Generic;
using System.IO;
using Sideways.CameraSystem;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.UI;
using Sideways.Vehicle;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Sideways.Editor
{
    public static class ProjectBootstrap
    {
        private const string Root = "Assets/Sideways";
        private const string ScenePath = Root + "/Scenes/DrivingSandbox.unity";
        private const string CarPrefabPath = Root + "/Prefabs/PlayerCar.prefab";

        [MenuItem("Sideways/Rebuild Prototype")]
        public static void Configure()
        {
            ConfigureLayers();
            ConfigureProjectSettings();
            var pipeline = EnsureRenderPipeline();
            ConfigureRenderPipeline(pipeline);
            var profiles = EnsureProfiles();
            var materials = EnsureMaterials();
            var physicsMaterial = EnsurePhysicsMaterial();
            BuildCarPrefab(profiles, materials, physicsMaterial);
            BuildSandboxScene(profiles, materials, physicsMaterial);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SIDEWAYS prototype assets rebuilt successfully.");
        }

        [MenuItem("Sideways/Open Driving Sandbox")]
        public static void OpenDrivingSandbox()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"SIDEWAYS scene is missing at {ScenePath}. Run Sideways > Rebuild Prototype first.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildIos()
        {
            Configure();
            var output = Path.GetFullPath("Builds/iOS");
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"iOS export failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
        }

        private static void ConfigureProjectSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Nikita Protsenko";
            PlayerSettings.productName = "SIDEWAYS";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.nprotsenko.sideways.prototype");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length > 0)
            {
                var settings = new SerializedObject(assets[0]);
                var inputHandler = settings.FindProperty("activeInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static UniversalRenderPipelineAsset EnsureRenderPipeline()
        {
            const string rendererPath = Root + "/Settings/SidewaysRenderer.asset";
            const string pipelinePath = Root + "/Settings/SidewaysURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline != null) return pipeline;

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "SidewaysURP";
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 2;
            pipeline.shadowDistance = 55f;
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            return pipeline;
        }

        private static void ConfigureRenderPipeline(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            // Unity 6 does not reliably persist the runtime property setters when a
            // render-pipeline asset is created during the same editor operation.
            // Write the backing project settings as well so reopening the project
            // cannot fall back to Built-in and render URP materials bright magenta.
            SetProjectObjectReference("ProjectSettings/GraphicsSettings.asset", "m_CustomRenderPipeline", pipeline);

            var qualityAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAssets.Length == 0) return;

            var qualitySettings = new SerializedObject(qualityAssets[0]);
            var levels = qualitySettings.FindProperty("m_QualitySettings");
            if (levels != null)
            {
                for (var i = 0; i < levels.arraySize; i++)
                {
                    var renderPipeline = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                    if (renderPipeline != null) renderPipeline.objectReferenceValue = pipeline;
                }
            }
            qualitySettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProjectObjectReference(string settingsPath, string propertyName, Object value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
            if (assets.Length == 0) return;

            var settings = new SerializedObject(assets[0]);
            var property = settings.FindProperty(propertyName);
            if (property == null) return;
            property.objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Profiles EnsureProfiles()
        {
            return new Profiles
            {
                Vehicle = EnsureAsset<VehicleHandlingProfile>(Root + "/Configuration/DefaultVehicleHandling.asset"),
                Camera = EnsureAsset<ChaseCameraProfile>(Root + "/Configuration/DefaultChaseCamera.asset"),
                Ride = EnsureAsset<RideRulesProfile>(Root + "/Configuration/DefaultRideRules.asset")
            };
        }

        private static Materials EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Materials
            {
                Road = EnsureMaterial(Root + "/Materials/Road.mat", shader, new Color(0.18f, 0.20f, 0.23f)),
                Terrain = EnsureMaterial(Root + "/Materials/Terrain.mat", shader, new Color(0.30f, 0.38f, 0.31f)),
                Building = EnsureMaterial(Root + "/Materials/Building.mat", shader, new Color(0.50f, 0.52f, 0.56f)),
                Barrier = EnsureMaterial(Root + "/Materials/Barrier.mat", shader, new Color(0.74f, 0.25f, 0.20f)),
                Car = EnsureMaterial(Root + "/Materials/PlayerCar.mat", shader, new Color(0.12f, 0.55f, 0.92f)),
                Accent = EnsureMaterial(Root + "/Materials/Accent.mat", shader, new Color(0.95f, 0.80f, 0.18f))
            };
        }

        private static PhysicsMaterial EnsurePhysicsMaterial()
        {
            const string path = Root + "/Materials/PrototypePhysics.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("PrototypePhysics");
                AssetDatabase.CreateAsset(material, path);
            }
            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            if (material.dynamicFriction != 0f || material.staticFriction != 0f)
            {
                throw new InvalidOperationException("Prototype physics material must be frictionless so the arcade grip model remains authoritative.");
            }
            return material;
        }

        private static void BuildCarPrefab(Profiles profiles, Materials materials, PhysicsMaterial physicsMaterial)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath) != null) AssetDatabase.DeleteAsset(CarPrefabPath);

            var root = new GameObject("PlayerCar");
            root.layer = 6;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.75f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(2f, 1.1f, 4.2f);
            collider.material = physicsMaterial;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            visual.transform.localScale = new Vector3(1.85f, 0.85f, 4f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = materials.Car;

            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(visual.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.75f, -0.1f);
            cabin.transform.localScale = new Vector3(0.82f, 0.72f, 0.48f);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());
            cabin.GetComponent<Renderer>().sharedMaterial = materials.Accent;

            var input = root.AddComponent<VehicleInputSource>();
            input.Configure(profiles.Vehicle.inputResponse);
            var controller = root.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profiles.Vehicle, (1 << 0) | (1 << 7), new Vector3(0f, -0.35f, 0f));
            var recovery = root.AddComponent<VehicleRecovery>();
            recovery.Configure(controller, input, profiles.Ride, 1 << 7, 1 << 8);

            PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildSandboxScene(Profiles profiles, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DrivingSandbox";

            var systems = new GameObject("Systems");
            systems.AddComponent<PrototypeBootstrap>();

            var environment = new GameObject("Environment");
            BuildEnvironment(environment.transform, materials, physicsMaterial);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            car.transform.SetPositionAndRotation(new Vector3(-70f, 1.2f, -100f), Quaternion.Euler(0f, 90f, 0f));
            var controller = car.GetComponent<VehicleController>();
            var input = car.GetComponent<VehicleInputSource>();
            var recovery = car.GetComponent<VehicleRecovery>();

            var cameraObject = new GameObject("ChaseCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            cameraObject.AddComponent<AudioListener>();
            var chase = cameraObject.AddComponent<ChaseCameraController>();
            chase.Configure(car.transform, car.GetComponent<Rigidbody>(), profiles.Camera, 1 << 8);
            cameraObject.transform.SetPositionAndRotation(car.transform.position + new Vector3(-10f, 7f, 0f), Quaternion.identity);

            var ui = BuildUi(input, controller, recovery);
            var rideSession = systems.AddComponent<RideSessionController>();
            rideSession.Configure(car.transform, controller, profiles.Ride, ui.Results);

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.2f, 0.18f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildEnvironment(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            CreateBox("Terrain", parent, new Vector3(0f, -1.1f, 0f), new Vector3(300f, 2f, 300f), materials.Terrain, physicsMaterial, 0);

            var roads = new GameObject("Roads").transform;
            roads.SetParent(parent, false);
            CreateRoadSegment(roads, new Vector3(-95f, 0f, -100f), new Vector3(95f, 0f, -100f), materials.Road, physicsMaterial);
            CreateArc(roads, new Vector3(95f, 0f, -75f), 25f, -90f, 0f, 6, materials.Road, physicsMaterial);
            CreateRoadSegment(roads, new Vector3(120f, 0f, -75f), new Vector3(120f, 0f, 75f), materials.Road, physicsMaterial);
            CreateArc(roads, new Vector3(95f, 0f, 75f), 25f, 0f, 90f, 6, materials.Road, physicsMaterial);
            CreateRoadSegment(roads, new Vector3(95f, 0f, 100f), new Vector3(-95f, 0f, 100f), materials.Road, physicsMaterial);
            CreateArc(roads, new Vector3(-95f, 0f, 75f), 25f, 90f, 180f, 6, materials.Road, physicsMaterial);
            CreateRoadSegment(roads, new Vector3(-120f, 0f, 75f), new Vector3(-120f, 0f, -75f), materials.Road, physicsMaterial);
            CreateArc(roads, new Vector3(-95f, 0f, -75f), 25f, 180f, 270f, 6, materials.Road, physicsMaterial);
            CreateRoadSegment(roads, new Vector3(-120f, 0f, 0f), new Vector3(120f, 0f, 0f), materials.Road, physicsMaterial);
            CreateRoadSegment(roads, new Vector3(0f, 0f, -100f), new Vector3(0f, 0f, 100f), materials.Road, physicsMaterial);

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(parent, false);
            CreateBox("Building_SW", buildings, new Vector3(-55f, 9f, -50f), new Vector3(62f, 18f, 52f), materials.Building, physicsMaterial, 8);
            CreateBox("Building_SE", buildings, new Vector3(58f, 13f, -48f), new Vector3(68f, 26f, 56f), materials.Building, physicsMaterial, 8);
            CreateBox("Building_NW", buildings, new Vector3(-58f, 12f, 52f), new Vector3(66f, 24f, 54f), materials.Building, physicsMaterial, 8);
            CreateBox("Building_NE", buildings, new Vector3(55f, 8f, 54f), new Vector3(60f, 16f, 48f), materials.Building, physicsMaterial, 8);

            var barriers = new GameObject("BoundaryBarriers").transform;
            barriers.SetParent(parent, false);
            CreateBox("North", barriers, new Vector3(0f, 1.5f, 149f), new Vector3(300f, 3f, 2f), materials.Barrier, physicsMaterial, 8);
            CreateBox("South", barriers, new Vector3(0f, 1.5f, -149f), new Vector3(300f, 3f, 2f), materials.Barrier, physicsMaterial, 8);
            CreateBox("East", barriers, new Vector3(149f, 1.5f, 0f), new Vector3(2f, 3f, 300f), materials.Barrier, physicsMaterial, 8);
            CreateBox("West", barriers, new Vector3(-149f, 1.5f, 0f), new Vector3(2f, 3f, 300f), materials.Barrier, physicsMaterial, 8);
        }

        private static UiReferences BuildUi(VehicleInputSource input, VehicleController controller, VehicleRecovery recovery)
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = CreateUiObject("SafeArea", canvasObject.transform, Vector2.zero, Vector2.one);
            safeArea.AddComponent<SafeAreaPanel>();
            var leftZone = CreateUiImage("SteerLeft", safeArea.transform, Vector2.zero, new Vector2(0.5f, 1f), new Color(0f, 0.35f, 1f, 0.025f));
            leftZone.AddComponent<TouchSteeringZone>().Configure(input, -1);
            var rightZone = CreateUiImage("SteerRight", safeArea.transform, new Vector2(0.5f, 0f), Vector2.one, new Color(1f, 0.35f, 0f, 0.025f));
            rightZone.AddComponent<TouchSteeringZone>().Configure(input, 1);

            var debugPanel = CreateUiImage("DebugPanel", safeArea.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.62f));
            SetRect(debugPanel.GetComponent<RectTransform>(), new Vector2(18f, -18f), new Vector2(310f, 290f), new Vector2(0f, 1f));
            var debugText = CreateText("Telemetry", debugPanel.transform, "", 26, TextAnchor.UpperLeft);
            Stretch(debugText.rectTransform, 14f);
            var debugHud = canvasObject.AddComponent<DebugHud>();
            debugHud.Configure(controller, recovery, debugText, debugPanel);

            var resetButton = CreateButton("ResetButton", safeArea.transform, "RESET  [R]", new Color(0.12f, 0.15f, 0.2f, 0.86f));
            SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(-18f, -18f), new Vector2(220f, 84f), new Vector2(1f, 1f));
            resetButton.AddComponent<ResetButtonBinding>().Configure(input);

            var resultsPanel = CreateUiImage("ResultsPanel", safeArea.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.82f));
            var reasonText = CreateText("Reason", resultsPanel.transform, "RIDE OVER", 54, TextAnchor.MiddleCenter);
            SetRect(reasonText.rectTransform, new Vector2(0f, 100f), new Vector2(900f, 100f), new Vector2(0.5f, 0.5f));
            var scoreGroup = CreateUiObject("ScoreGroup", resultsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            SetRect(scoreGroup.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(700f, 120f), new Vector2(0.5f, 0.5f));
            var scoreText = CreateText("Score", scoreGroup.transform, "", 36, TextAnchor.MiddleCenter);
            Stretch(scoreText.rectTransform, 0f);
            var restartButton = CreateButton("RestartButton", resultsPanel.transform, "DRIVE AGAIN", new Color(0.12f, 0.55f, 0.92f, 1f));
            SetRect(restartButton.GetComponent<RectTransform>(), new Vector2(0f, -125f), new Vector2(360f, 100f), new Vector2(0.5f, 0.5f));
            var results = canvasObject.AddComponent<RideResultsView>();
            results.Configure(resultsPanel, reasonText, scoreGroup, scoreText, restartButton.GetComponent<Button>());
            resultsPanel.SetActive(false);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return new UiReferences { Results = results };
        }

        private static void CreateArc(Transform parent, Vector3 center, float radius, float startDegrees, float endDegrees, int steps, Material material, PhysicsMaterial physicsMaterial)
        {
            var previous = center + Direction(startDegrees) * radius;
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var current = center + Direction(Mathf.Lerp(startDegrees, endDegrees, t)) * radius;
                CreateRoadSegment(parent, previous, current, material, physicsMaterial);
                previous = current;
            }
        }

        private static Vector3 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        }

        private static void CreateRoadSegment(Transform parent, Vector3 start, Vector3 end, Material material, PhysicsMaterial physicsMaterial)
        {
            var delta = end - start;
            var segment = CreateBox("Road", parent, (start + end) * 0.5f + Vector3.up * 0.05f, new Vector3(14f, 0.2f, delta.magnitude + 0.6f), material, physicsMaterial, 7);
            segment.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PhysicsMaterial physicsMaterial, int layer)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.layer = layer;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            gameObject.GetComponent<BoxCollider>().material = physicsMaterial;
            return gameObject;
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return gameObject;
        }

        private static GameObject CreateUiImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var gameObject = CreateUiObject(name, parent, anchorMin, anchorMax);
            gameObject.AddComponent<Image>().color = color;
            return gameObject;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static GameObject CreateButton(string name, Transform parent, string label, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            var text = CreateText("Label", gameObject.transform, label, 30, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 4f);
            return gameObject;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material EnsureMaterial(string path, Shader shader, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(6).stringValue = "Vehicle";
            layers.GetArrayElementAtIndex(7).stringValue = "Road";
            layers.GetArrayElementAtIndex(8).stringValue = "Obstacle";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Profiles
        {
            public VehicleHandlingProfile Vehicle;
            public ChaseCameraProfile Camera;
            public RideRulesProfile Ride;
        }

        private sealed class Materials
        {
            public Material Road;
            public Material Terrain;
            public Material Building;
            public Material Barrier;
            public Material Car;
            public Material Accent;
        }

        private sealed class UiReferences
        {
            public RideResultsView Results;
        }
    }
}
