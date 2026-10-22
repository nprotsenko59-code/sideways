using System;
using System.Collections.Generic;
using System.IO;
using Sideways.CameraSystem;
using Sideways.Challenges;
using Sideways.Collectibles;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.EnvironmentSystem;
using Sideways.Police;
using Sideways.Traffic;
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
        private const string PolicePrefabPath = Root + "/Prefabs/PoliceCar.prefab";
        private const string TrafficPrefabPath = Root + "/Prefabs/TrafficCar.prefab";

        [MenuItem("Sideways/Rebuild Prototype")]
        public static void Configure()
        {
            ConfigureLayers();
            ConfigureProjectSettings();
            var pipeline = EnsureRenderPipeline();
            ConfigureRenderPipeline(pipeline);
            var profiles = EnsureProfiles();
            var roadGraph = EnsureRoadGraph();
            var materials = EnsureMaterials();
            var physicsMaterial = EnsurePhysicsMaterial();
            BuildCarPrefab(profiles, materials, physicsMaterial);
            BuildPolicePrefab(profiles, roadGraph, materials, physicsMaterial);
            BuildTrafficPrefab(profiles, roadGraph, materials, physicsMaterial);
            BuildSandboxScene(profiles, roadGraph, materials, physicsMaterial);
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

        [MenuItem("Sideways/Build iOS Development")]
        public static void BuildIos()
        {
            BuildIosInternal("Builds/iOS", BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("Sideways/Build iOS Release")]
        public static void BuildIosRelease()
        {
            BuildIosInternal("Builds/iOS-Release", BuildOptions.None);
        }

        private static void BuildIosInternal(string outputDirectory, BuildOptions options)
        {
            Configure();
            var output = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(output);
            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = options
            };
            var report = BuildPipeline.BuildPlayer(buildOptions);
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
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.enableFrameTimingStats = true;

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
                Ride = EnsureAsset<RideRulesProfile>(Root + "/Configuration/DefaultRideRules.asset"),
                Scoring = EnsureAsset<ScoringProfile>(Root + "/Configuration/DefaultScoring.asset"),
                Police = EnsureAsset<PoliceProfile>(Root + "/Configuration/DefaultPolice.asset"),
                Traffic = EnsureAsset<TrafficProfile>(Root + "/Configuration/DefaultTraffic.asset"),
                Fuel = EnsureAsset<FuelProfile>(Root + "/Configuration/DefaultFuel.asset"),
                Challenges = EnsureChallenges()
            };
        }

        private static ChallengeDefinition[] EnsureChallenges()
        {
            var signs = EnsureAsset<ChallengeDefinition>(Root + "/Configuration/DestroySignsChallenge.asset");
            signs.Configure("destroy-signs", "DESTROY 10 ROAD SIGNS", ChallengeType.DestroyRoadSigns, 60f, 10, 1500);
            EditorUtility.SetDirty(signs);
            var drift = EnsureAsset<ChallengeDefinition>(Root + "/Configuration/DriftScoreChallenge.asset");
            drift.Configure("drift-score", "EARN 5,000 DRIFT SCORE", ChallengeType.EarnDriftScore, 60f, 5000, 2000);
            EditorUtility.SetDirty(drift);
            return new[] { signs, drift };
        }

        private static RoadGraphAsset EnsureRoadGraph()
        {
            const string path = Root + "/Configuration/LAPrototypeRoadGraph.asset";
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<RoadGraphAsset>();
                AssetDatabase.CreateAsset(graph, path);
            }

            var nodes = new[]
            {
                N("G_NW", -120f, 100f), N("G_NC", 0f, 100f, RoadNodeRole.Landmark), N("G_NE", 120f, 100f),
                N("G_W", -120f, 0f), N("G_C", 0f, 0f, RoadNodeRole.Landmark), N("G_E", 120f, 0f),
                N("G_SW", -120f, -100f), N("G_SC", 0f, -100f), N("G_SE", 120f, -100f),
                N("B_W", -220f, -165f, RoadNodeRole.BoundaryLoop), N("B_SW", -120f, -165f),
                N("B_SC", 0f, -165f, RoadNodeRole.Landmark), N("B_SE", 120f, -165f), N("B_E", 220f, -165f, RoadNodeRole.BoundaryLoop),
                N("O_W", -225f, -40f, RoadNodeRole.BoundaryLoop), N("O_NW", -190f, 145f, RoadNodeRole.BoundaryLoop),
                N("O_N", 0f, 195f, RoadNodeRole.BoundaryLoop), N("O_NE", 190f, 145f, RoadNodeRole.BoundaryLoop),
                N("O_E", 225f, -40f, RoadNodeRole.BoundaryLoop)
            };

            var edges = new List<RoadGraphEdge>
            {
                E("Grid_N_WC", "G_NW", "G_NC", RoadKind.Street, 14f, 16f, P(-120f, 100f), P(0f, 100f)),
                E("Grid_N_CE", "G_NC", "G_NE", RoadKind.Street, 14f, 16f, P(0f, 100f), P(120f, 100f)),
                E("Grid_M_WC", "G_W", "G_C", RoadKind.Street, 16f, 18f, P(-120f, 0f), P(0f, 0f)),
                E("Grid_M_CE", "G_C", "G_E", RoadKind.Street, 16f, 18f, P(0f, 0f), P(120f, 0f)),
                E("Grid_S_WC", "G_SW", "G_SC", RoadKind.Street, 14f, 16f, P(-120f, -100f), P(0f, -100f)),
                E("Grid_S_CE", "G_SC", "G_SE", RoadKind.Street, 14f, 16f, P(0f, -100f), P(120f, -100f)),
                E("Grid_W_NS", "G_NW", "G_W", RoadKind.Street, 14f, 16f, P(-120f, 100f), P(-120f, 0f)),
                E("Grid_W_SS", "G_W", "G_SW", RoadKind.Street, 14f, 16f, P(-120f, 0f), P(-120f, -100f)),
                E("Grid_C_NS", "G_NC", "G_C", RoadKind.Street, 16f, 18f, P(0f, 100f), P(0f, 0f)),
                E("Grid_C_SS", "G_C", "G_SC", RoadKind.Street, 16f, 18f, P(0f, 0f), P(0f, -100f)),
                E("Grid_E_NS", "G_NE", "G_E", RoadKind.Street, 14f, 16f, P(120f, 100f), P(120f, 0f)),
                E("Grid_E_SS", "G_E", "G_SE", RoadKind.Street, 14f, 16f, P(120f, 0f), P(120f, -100f)),

                E("Boulevard_W0", "B_W", "B_SW", RoadKind.Boulevard, 22f, 24f, P(-220f, -165f), P(-120f, -165f)),
                E("Boulevard_W1", "B_SW", "B_SC", RoadKind.Boulevard, 22f, 24f, P(-120f, -165f), P(0f, -165f)),
                E("Boulevard_E1", "B_SC", "B_SE", RoadKind.Boulevard, 22f, 24f, P(0f, -165f), P(120f, -165f)),
                E("Boulevard_E0", "B_SE", "B_E", RoadKind.Boulevard, 22f, 24f, P(120f, -165f), P(220f, -165f)),
                E("BoulevardLink_W", "B_SW", "G_SW", RoadKind.Street, 16f, 18f, P(-120f, -165f), P(-120f, -100f)),
                E("BoulevardLink_C", "B_SC", "G_SC", RoadKind.Street, 16f, 18f, P(0f, -165f), P(0f, -100f)),
                E("BoulevardLink_E", "B_SE", "G_SE", RoadKind.Street, 16f, 18f, P(120f, -165f), P(120f, -100f)),

                E("Scenic_SW", "B_W", "O_W", RoadKind.Scenic, 15f, 18f, P(-220f, -165f), P(-230f, -130f), P(-232f, -85f), P(-225f, -40f)),
                E("Scenic_W", "O_W", "O_NW", RoadKind.Scenic, 15f, 18f, P(-225f, -40f), P(-232f, 20f), P(-220f, 82f), P(-190f, 145f)),
                E("Scenic_NW", "O_NW", "O_N", RoadKind.Scenic, 15f, 18f, P(-190f, 145f), P(-142f, 176f), P(-72f, 192f), P(0f, 195f)),
                E("Scenic_NE", "O_N", "O_NE", RoadKind.Scenic, 15f, 18f, P(0f, 195f), P(72f, 192f), P(142f, 176f), P(190f, 145f)),
                E("Scenic_E", "O_NE", "O_E", RoadKind.Scenic, 15f, 18f, P(190f, 145f), P(220f, 82f), P(232f, 20f), P(225f, -40f)),
                E("Scenic_SE", "O_E", "B_E", RoadKind.Scenic, 15f, 18f, P(225f, -40f), P(232f, -85f), P(230f, -130f), P(220f, -165f)),
                E("HillLink_W", "O_NW", "G_NW", RoadKind.Street, 14f, 15f, P(-190f, 145f), P(-120f, 100f)),
                E("HillLink_C", "O_N", "G_NC", RoadKind.Street, 14f, 15f, P(0f, 195f), P(0f, 100f)),
                E("HillLink_E", "O_NE", "G_NE", RoadKind.Street, 14f, 15f, P(190f, 145f), P(120f, 100f)),

                E("Alley_NW", "G_W", "G_NC", RoadKind.Alley, 8f, 11f, P(-120f, 0f), P(-82f, 31f), P(-43f, 69f), P(0f, 100f)),
                E("Alley_SE", "G_SC", "G_E", RoadKind.Alley, 8f, 11f, P(0f, -100f), P(43f, -69f), P(82f, -31f), P(120f, 0f)),
                E("Alley_Beach", "B_W", "G_SW", RoadKind.Alley, 9f, 12f, P(-220f, -165f), P(-178f, -141f), P(-148f, -114f), P(-120f, -100f)),
                E("Alley_Industrial", "G_SE", "B_E", RoadKind.Alley, 9f, 12f, P(120f, -100f), P(148f, -114f), P(178f, -141f), P(220f, -165f))
            };

            graph.Configure("sunset-basin", new Vector2(520f, 420f), nodes, edges);
            if (!graph.IsValid(out var error)) throw new InvalidOperationException($"Road graph is invalid: {error}");
            EditorUtility.SetDirty(graph);
            return graph;
        }

        private static RoadGraphNode N(string id, float x, float z, RoadNodeRole role = RoadNodeRole.Junction) => new(id, P(x, z), role);

        private static RoadGraphEdge E(string id, string from, string to, RoadKind kind, float width, float speed, params Vector3[] points) =>
            new(id, from, to, kind, width, speed, points);

        private static Vector3 P(float x, float z) => new(x, 0f, z);

        private static Materials EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Materials
            {
                Road = EnsureMaterial(Root + "/Materials/Road.mat", shader, new Color(0.18f, 0.20f, 0.23f)),
                Terrain = EnsureMaterial(Root + "/Materials/Terrain.mat", shader, new Color(0.30f, 0.38f, 0.31f)),
                Building = EnsureMaterial(Root + "/Materials/Building.mat", shader, new Color(0.50f, 0.52f, 0.56f)),
                Barrier = EnsureMaterial(Root + "/Materials/Barrier.mat", shader, new Color(0.74f, 0.25f, 0.20f)),
                Concrete = EnsureMaterial(Root + "/Materials/Concrete.mat", shader, new Color(0.43f, 0.45f, 0.47f)),
                Water = EnsureMaterial(Root + "/Materials/Water.mat", shader, new Color(0.08f, 0.32f, 0.52f)),
                Marking = EnsureMaterial(Root + "/Materials/RoadMarking.mat", shader, new Color(0.94f, 0.76f, 0.16f)),
                Car = EnsureMaterial(Root + "/Materials/PlayerCar.mat", shader, new Color(0.12f, 0.55f, 0.92f)),
                Accent = EnsureMaterial(Root + "/Materials/Accent.mat", shader, new Color(0.95f, 0.80f, 0.18f)),
                Police = EnsureMaterial(Root + "/Materials/PoliceCar.mat", shader, new Color(0.06f, 0.08f, 0.12f)),
                PoliceLight = EnsureMaterial(Root + "/Materials/PoliceLight.mat", shader, new Color(0.85f, 0.08f, 0.05f)),
                Traffic = EnsureMaterial(Root + "/Materials/TrafficCar.mat", shader, new Color(0.78f, 0.24f, 0.12f)),
                Sign = EnsureMaterial(Root + "/Materials/RoadSign.mat", shader, new Color(0.95f, 0.22f, 0.12f)),
                Coin = EnsureMaterial(Root + "/Materials/Coin.mat", shader, new Color(1f, 0.72f, 0.06f)),
                Fuel = EnsureMaterial(Root + "/Materials/FuelCanister.mat", shader, new Color(0.10f, 0.75f, 0.30f))
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
            recovery.Configure(controller, input, profiles.Ride, 1 << 7, (1 << 8) | (1 << 9) | (1 << 10));
            var collisionSensor = root.AddComponent<VehicleCollisionSensor>();
            collisionSensor.Configure((1 << 8) | (1 << 9) | (1 << 10));

            PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildPolicePrefab(Profiles profiles, RoadGraphAsset roadGraph, Materials materials, PhysicsMaterial physicsMaterial)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PolicePrefabPath) != null) AssetDatabase.DeleteAsset(PolicePrefabPath);

            var root = new GameObject("PoliceCar");
            root.layer = 9;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(2.1f, 1.1f, 4.4f);
            collider.material = physicsMaterial;

            var visual = CreateVisualBox("Visual", root.transform, new Vector3(0f, 0.58f, 0f), new Vector3(1.95f, 0.82f, 4.15f), materials.Police);
            visual.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            var cabin = CreateVisualBox("Cabin", root.transform, Vector3.zero, new Vector3(1.55f, 0.65f, 1.9f), materials.Concrete);
            cabin.transform.localPosition = new Vector3(0f, 1.25f, -0.1f);
            var lightBar = CreateVisualBox("LightBar", root.transform, Vector3.zero, new Vector3(1.25f, 0.18f, 0.34f), materials.PoliceLight);
            lightBar.transform.localPosition = new Vector3(0f, 1.68f, -0.1f);

            var navigator = root.AddComponent<PoliceVehicleController>();
            navigator.Configure(body, roadGraph, profiles.Police);
            root.AddComponent<PoliceVisionSensor>();

            PrefabUtility.SaveAsPrefabAsset(root, PolicePrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildTrafficPrefab(Profiles profiles, RoadGraphAsset roadGraph, Materials materials, PhysicsMaterial physicsMaterial)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TrafficPrefabPath) != null) AssetDatabase.DeleteAsset(TrafficPrefabPath);

            var root = new GameObject("TrafficCar");
            root.layer = 10;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(1.9f, 1.1f, 4f);
            collider.material = physicsMaterial;

            var visual = CreateVisualBox("Visual", root.transform, Vector3.zero, new Vector3(1.75f, 0.8f, 3.75f), materials.Traffic);
            visual.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            var cabin = CreateVisualBox("Cabin", root.transform, Vector3.zero, new Vector3(1.4f, 0.62f, 1.7f), materials.Concrete);
            cabin.transform.localPosition = new Vector3(0f, 1.2f, -0.1f);

            var nearMissObject = new GameObject("NearMissZone");
            nearMissObject.layer = 10;
            nearMissObject.transform.SetParent(root.transform, false);
            var trigger = nearMissObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = profiles.Traffic.nearMissRadius;
            trigger.center = new Vector3(0f, 0.6f, 0f);
            var nearMiss = nearMissObject.AddComponent<NearMissEncounter>();
            var vehicle = root.AddComponent<TrafficVehicle>();
            vehicle.Configure(body, roadGraph, profiles.Traffic, nearMiss);

            PrefabUtility.SaveAsPrefabAsset(root, TrafficPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildSandboxScene(Profiles profiles, RoadGraphAsset roadGraph, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DrivingSandbox";

            var systems = new GameObject("Systems");
            systems.AddComponent<PrototypeBootstrap>();

            var environment = new GameObject("Environment");
            BuildEnvironment(environment.transform, roadGraph, materials, physicsMaterial);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            car.transform.SetPositionAndRotation(new Vector3(-70f, 1.2f, -100f), Quaternion.Euler(0f, 90f, 0f));
            var controller = car.GetComponent<VehicleController>();
            var input = car.GetComponent<VehicleInputSource>();
            var recovery = car.GetComponent<VehicleRecovery>();
            var collisionSensor = car.GetComponent<VehicleCollisionSensor>();

            var policePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolicePrefabPath);
            var policeCar = (GameObject)PrefabUtility.InstantiatePrefab(policePrefab, scene);
            policeCar.transform.SetPositionAndRotation(new Vector3(120f, 0.65f, 100f), Quaternion.Euler(0f, 180f, 0f));
            var policeVehicle = policeCar.GetComponent<PoliceVehicleController>();
            var policeVision = policeCar.GetComponent<PoliceVisionSensor>();
            policeVision.Configure(car.transform, profiles.Police, 1 << 8);

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

            var scoring = systems.AddComponent<DriftScoreController>();
            scoring.Configure(controller, collisionSensor, profiles.Scoring);
            var recklessDriving = systems.AddComponent<RecklessDrivingTracker>();
            recklessDriving.Configure(controller, collisionSensor, profiles.Police);
            var policeDirector = systems.AddComponent<PoliceDirector>();
            policeDirector.Configure(car.transform, controller, recovery, policeVehicle, policeVision, recklessDriving, profiles.Police);
            var trafficScore = systems.AddComponent<TrafficScoreController>();
            trafficScore.Configure(scoring);
            var challengeController = systems.AddComponent<ChallengeController>();
            challengeController.Configure(profiles.Challenges, scoring);
            var resources = systems.AddComponent<RideResourceController>();
            resources.Configure(controller, scoring, profiles.Fuel);
            BuildCollectibles(environment.transform, resources, profiles.Fuel, materials);
            var trafficRoot = new GameObject("Traffic");
            var trafficSpawner = trafficRoot.AddComponent<TrafficSpawner>();
            trafficSpawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(TrafficPrefabPath), roadGraph, profiles.Traffic, car.transform);
            var ui = BuildUi(input, controller, recovery, scoring, policeDirector, challengeController, resources, chase);
            systems.AddComponent<GameFeedbackController>().Configure(controller, collisionSensor, scoring, resources, challengeController, ui.Preferences);
            var rideSession = systems.AddComponent<RideSessionController>();
            rideSession.Configure(car.transform, controller, profiles.Ride, ui.Results, scoring);

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

        private static void BuildEnvironment(Transform parent, RoadGraphAsset roadGraph, Materials materials, PhysicsMaterial physicsMaterial)
        {
            CreateBox("Ground", parent, new Vector3(0f, -1.11f, 0f), new Vector3(900f, 2f, 800f), materials.Terrain, physicsMaterial, 0);

            var roadsObject = new GameObject("Roads");
            roadsObject.transform.SetParent(parent, false);
            roadsObject.AddComponent<RoadGraphComponent>().Configure(roadGraph);
            for (var edgeIndex = 0; edgeIndex < roadGraph.Edges.Count; edgeIndex++)
            {
                var edge = roadGraph.Edges[edgeIndex];
                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    CreateRoadSegment($"{edge.Id}_{point:00}", roadsObject.transform, edge.Centerline[point - 1], edge.Centerline[point], edge.Width, materials.Road, physicsMaterial);
                    if (edge.Kind == RoadKind.Boulevard)
                    {
                        CreateRoadMarker($"{edge.Id}_Center_{point:00}", roadsObject.transform, edge.Centerline[point - 1], edge.Centerline[point], materials.Marking);
                    }
                }
            }

            var signs = new GameObject("RoadSigns").transform;
            signs.SetParent(parent, false);
            CreateRoadSign("Sign_01", signs, new Vector3(-35f, 0f, -106f), 0f, materials, physicsMaterial);
            CreateRoadSign("Sign_02", signs, new Vector3(35f, 0f, -94f), 180f, materials, physicsMaterial);
            CreateRoadSign("Sign_03", signs, new Vector3(114f, 0f, -55f), 90f, materials, physicsMaterial);
            CreateRoadSign("Sign_04", signs, new Vector3(126f, 0f, 45f), -90f, materials, physicsMaterial);
            CreateRoadSign("Sign_05", signs, new Vector3(-114f, 0f, 45f), 90f, materials, physicsMaterial);
            CreateRoadSign("Sign_06", signs, new Vector3(-126f, 0f, -55f), -90f, materials, physicsMaterial);
            CreateRoadSign("Sign_07", signs, new Vector3(-45f, 0f, 94f), 0f, materials, physicsMaterial);
            CreateRoadSign("Sign_08", signs, new Vector3(45f, 0f, 106f), 180f, materials, physicsMaterial);
            CreateRoadSign("Sign_09", signs, new Vector3(-55f, 0f, -159f), 0f, materials, physicsMaterial);
            CreateRoadSign("Sign_10", signs, new Vector3(55f, 0f, -171f), 180f, materials, physicsMaterial);
            CreateRoadSign("Sign_11", signs, new Vector3(-6f, 0f, 52f), 90f, materials, physicsMaterial);
            CreateRoadSign("Sign_12", signs, new Vector3(6f, 0f, -48f), -90f, materials, physicsMaterial);

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(parent, false);
            CreateBuilding("Downtown_NE_Tower", buildings, P(58f, 54f), new Vector2(58f, 54f), 38f, materials, physicsMaterial);
            CreateBuilding("Downtown_SW_Block", buildings, P(-58f, -52f), new Vector2(62f, 52f), 25f, materials, physicsMaterial);
            CreateBuilding("Downtown_NW_West", buildings, P(-91f, 67f), new Vector2(38f, 38f), 19f, materials, physicsMaterial);
            CreateBuilding("Downtown_NW_South", buildings, P(-47f, 24f), new Vector2(34f, 27f), 16f, materials, physicsMaterial);
            CreateBuilding("Downtown_SE_East", buildings, P(94f, -66f), new Vector2(30f, 38f), 22f, materials, physicsMaterial);
            CreateBuilding("Civic_West", buildings, P(-171f, 47f), new Vector2(48f, 58f), 17f, materials, physicsMaterial);
            CreateBuilding("Civic_East", buildings, P(172f, 57f), new Vector2(48f, 58f), 21f, materials, physicsMaterial);
            CreateBuilding("Commercial_West", buildings, P(-174f, -69f), new Vector2(50f, 42f), 13f, materials, physicsMaterial);
            CreateBuilding("Commercial_East", buildings, P(175f, -82f), new Vector2(48f, 32f), 15f, materials, physicsMaterial);
            CreateBuilding("Warehouse_West", buildings, P(-67f, -133f), new Vector2(68f, 28f), 11f, materials, physicsMaterial);
            CreateBuilding("Warehouse_East", buildings, P(67f, -133f), new Vector2(68f, 28f), 12f, materials, physicsMaterial);
            CreateBuilding("Warehouse_FarWest", buildings, P(-170f, -198f), new Vector2(48f, 24f), 9f, materials, physicsMaterial);
            CreateBuilding("Warehouse_FarEast", buildings, P(170f, -198f), new Vector2(48f, 24f), 10f, materials, physicsMaterial);
            CreateBuilding("Hillside_East", buildings, P(64f, 143f), new Vector2(48f, 27f), 14f, materials, physicsMaterial);

            var landmarks = new GameObject("Landmarks").transform;
            landmarks.SetParent(parent, false);
            CreateBuilding("SunsetHotel", landmarks, P(-64f, 145f), new Vector2(30f, 28f), 30f, materials, physicsMaterial, materials.Accent);
            CreateBuilding("BoulevardTheater", landmarks, P(60f, -198f), new Vector2(46f, 24f), 15f, materials, physicsMaterial, materials.Accent);
            CreateVisualBox("BeachOverlook", landmarks, new Vector3(-244f, 0.45f, -7f), new Vector3(10f, 0.8f, 34f), materials.Concrete);

            var boundaries = new GameObject("BoundaryFiction").transform;
            boundaries.SetParent(parent, false);
            CreateVisualBox("PacificWater", boundaries, new Vector3(-520f, 0f, 0f), new Vector3(500f, 0.2f, 800f), materials.Water);
            CreateBox("OceanSeawall", boundaries, new Vector3(-260f, 2.5f, 0f), new Vector3(8f, 5f, 470f), materials.Concrete, physicsMaterial, 8);
            CreateBox("EastRetainingWall", boundaries, new Vector3(260f, 3f, 0f), new Vector3(8f, 6f, 470f), materials.Concrete, physicsMaterial, 8);
            CreateBox("SouthRailWall", boundaries, new Vector3(0f, 3f, -225f), new Vector3(520f, 6f, 8f), materials.Concrete, physicsMaterial, 8);
            CreateBox("NorthHill_West", boundaries, new Vector3(-165f, 22f, 238f), new Vector3(190f, 44f, 70f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, -8f, -8f);
            CreateBox("NorthHill_Center", boundaries, new Vector3(0f, 30f, 245f), new Vector3(170f, 60f, 78f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, 0f, 5f);
            CreateBox("NorthHill_East", boundaries, new Vector3(165f, 24f, 238f), new Vector3(190f, 48f, 70f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, 8f, 8f);

            for (var x = -210f; x <= 210f; x += 70f)
            {
                CreateBuilding($"SouthWarehouse_{x:0}", boundaries, P(x, -250f), new Vector2(58f, 36f), 18f + Mathf.Abs(x) * 0.025f, materials, physicsMaterial);
            }
        }

        private static void BuildCollectibles(Transform environment, RideResourceController resources, FuelProfile profile, Materials materials)
        {
            var root = new GameObject("Collectibles").transform;
            root.SetParent(environment, false);

            var coins = new[]
            {
                P(-52f, -100f), P(-32f, -100f), P(-12f, -100f), P(12f, -100f), P(32f, -100f), P(52f, -100f),
                P(120f, -78f), P(120f, -52f), P(120f, -26f), P(120f, 24f), P(120f, 52f), P(120f, 78f),
                P(88f, 100f), P(58f, 100f), P(28f, 100f), P(-28f, 100f), P(-58f, 100f), P(-88f, 100f),
                P(-120f, 75f), P(-120f, 45f), P(-120f, 15f), P(-120f, -25f), P(-120f, -55f), P(-120f, -82f),
                P(-180f, -165f), P(-140f, -165f), P(140f, -165f), P(180f, -165f)
            };
            for (var i = 0; i < coins.Length; i++) CreateCoin($"Coin_{i + 1:00}", root, coins[i] + Vector3.up * 1.25f, resources, profile, materials.Coin);

            var canisters = new[]
            {
                P(5f, -100f), P(120f, 8f), P(5f, 100f), P(-120f, -8f),
                P(0f, -165f), P(-205f, -165f), P(205f, -165f), P(0f, 195f)
            };
            for (var i = 0; i < canisters.Length; i++) CreateFuelCanister($"Fuel_{i + 1:00}", root, canisters[i] + Vector3.up * 0.85f, resources, profile, materials);
        }

        private static UiReferences BuildUi(VehicleInputSource input, VehicleController controller, VehicleRecovery recovery, DriftScoreController scoring, PoliceDirector policeDirector, ChallengeController challengeController, RideResourceController resources, ChaseCameraController chaseCamera)
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
            leftZone.AddComponent<TouchHandbrakeGesture>().Configure(input);
            var rightZone = CreateUiImage("SteerRight", safeArea.transform, new Vector2(0.5f, 0f), Vector2.one, new Color(1f, 0.35f, 0f, 0.025f));
            rightZone.AddComponent<TouchSteeringZone>().Configure(input, 1);
            rightZone.AddComponent<TouchHandbrakeGesture>().Configure(input);

            var handbrakeHint = CreateText("HandbrakeHint", safeArea.transform, "SWIPE DOWN + HOLD  •  HANDBRAKE  •  SPACE", 22, TextAnchor.MiddleCenter);
            SetRect(handbrakeHint.rectTransform, new Vector2(0f, 20f), new Vector2(620f, 48f), new Vector2(0.5f, 0f));
            handbrakeHint.color = new Color(1f, 1f, 1f, 0.72f);

            var scorePanel = CreateUiImage("ScorePanel", safeArea.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.02f, 0.03f, 0.05f, 0.78f));
            SetRect(scorePanel.GetComponent<RectTransform>(), new Vector2(0f, -18f), new Vector2(520f, 126f), new Vector2(0.5f, 1f));
            scorePanel.GetComponent<Image>().raycastTarget = false;
            var totalScoreText = CreateText("TotalScore", scorePanel.transform, "0", 48, TextAnchor.MiddleCenter);
            SetRect(totalScoreText.rectTransform, new Vector2(0f, -8f), new Vector2(490f, 60f), new Vector2(0.5f, 1f));
            totalScoreText.raycastTarget = false;
            var comboText = CreateText("Combo", scorePanel.transform, "BUILD A DRIFT COMBO", 23, TextAnchor.MiddleCenter);
            SetRect(comboText.rectTransform, new Vector2(0f, -68f), new Vector2(490f, 42f), new Vector2(0.5f, 1f));
            comboText.raycastTarget = false;
            var graceBackground = CreateUiImage("ComboGraceBackground", scorePanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(1f, 1f, 1f, 0.15f));
            SetRect(graceBackground.GetComponent<RectTransform>(), new Vector2(0f, 8f), new Vector2(460f, 6f), new Vector2(0.5f, 0f));
            graceBackground.GetComponent<Image>().raycastTarget = false;
            var graceFillObject = CreateUiImage("Fill", graceBackground.transform, Vector2.zero, Vector2.one, new Color(1f, 0.72f, 0.1f, 1f));
            var graceFill = graceFillObject.GetComponent<Image>();
            graceFill.raycastTarget = false;
            graceFill.type = Image.Type.Filled;
            graceFill.fillMethod = Image.FillMethod.Horizontal;
            graceFill.fillOrigin = 0;
            graceFill.fillAmount = 0f;
            var scoreHud = canvasObject.AddComponent<ScoreHud>();
            scoreHud.Configure(scoring, totalScoreText, comboText, graceFill);

            var policePanelObject = CreateUiImage("PolicePanel", safeArea.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.08f, 0.12f, 0.18f, 0.8f));
            SetRect(policePanelObject.GetComponent<RectTransform>(), new Vector2(0f, -154f), new Vector2(360f, 54f), new Vector2(0.5f, 1f));
            policePanelObject.GetComponent<Image>().raycastTarget = false;
            var policeText = CreateText("PoliceState", policePanelObject.transform, "PATROL", 26, TextAnchor.MiddleCenter);
            Stretch(policeText.rectTransform, 4f);
            policeText.raycastTarget = false;
            var policeHud = canvasObject.AddComponent<PoliceHud>();
            policeHud.Configure(policeDirector, policeText, policePanelObject.GetComponent<Image>());

            var bonusText = CreateText("Bonus", safeArea.transform, string.Empty, 28, TextAnchor.MiddleCenter);
            SetRect(bonusText.rectTransform, new Vector2(0f, -222f), new Vector2(650f, 48f), new Vector2(0.5f, 1f));
            bonusText.color = new Color(1f, 0.78f, 0.16f, 1f);
            canvasObject.AddComponent<BonusHud>().Configure(scoring, bonusText);

            var challengePanel = CreateUiImage("ChallengePanel", safeArea.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Color(0.03f, 0.05f, 0.08f, 0.86f));
            SetRect(challengePanel.GetComponent<RectTransform>(), new Vector2(-18f, 0f), new Vector2(390f, 220f), new Vector2(1f, 0.5f));
            challengePanel.GetComponent<Image>().raycastTarget = false;
            var challengeTitle = CreateText("Title", challengePanel.transform, "OPTIONAL CHALLENGE", 25, TextAnchor.MiddleCenter);
            SetRect(challengeTitle.rectTransform, new Vector2(0f, -12f), new Vector2(360f, 42f), new Vector2(0.5f, 1f));
            var challengeProgress = CreateText("Progress", challengePanel.transform, "Destroy signs • Drift score", 22, TextAnchor.MiddleCenter);
            SetRect(challengeProgress.rectTransform, new Vector2(0f, -62f), new Vector2(360f, 38f), new Vector2(0.5f, 1f));
            var challengeTime = CreateText("Time", challengePanel.transform, "60 SECOND EVENTS", 18, TextAnchor.MiddleCenter);
            SetRect(challengeTime.rectTransform, new Vector2(0f, -102f), new Vector2(360f, 32f), new Vector2(0.5f, 1f));
            var challengeButtonObject = CreateButton("ChallengeButton", challengePanel.transform, "START  [C]", new Color(0.86f, 0.32f, 0.08f, 0.96f));
            SetRect(challengeButtonObject.GetComponent<RectTransform>(), new Vector2(0f, 12f), new Vector2(280f, 62f), new Vector2(0.5f, 0f));
            var challengeButton = challengeButtonObject.GetComponent<Button>();
            var challengeButtonText = challengeButtonObject.transform.Find("Label").GetComponent<Text>();
            var challengeHud = canvasObject.AddComponent<ChallengeHud>();
            challengeHud.Configure(challengeController, challengePanel, challengeTitle, challengeProgress, challengeTime, challengeButton, challengeButtonText);

            var resourcePanel = CreateUiImage("ResourcePanel", safeArea.transform, Vector2.zero, Vector2.zero, new Color(0.03f, 0.05f, 0.08f, 0.86f));
            SetRect(resourcePanel.GetComponent<RectTransform>(), new Vector2(18f, 18f), new Vector2(310f, 112f), Vector2.zero);
            resourcePanel.GetComponent<Image>().raycastTarget = false;
            var coinText = CreateText("Coins", resourcePanel.transform, "COINS  0", 25, TextAnchor.MiddleLeft);
            SetRect(coinText.rectTransform, new Vector2(16f, -12f), new Vector2(278f, 38f), new Vector2(0f, 1f));
            var fuelText = CreateText("Fuel", resourcePanel.transform, "FUEL  70", 22, TextAnchor.MiddleLeft);
            SetRect(fuelText.rectTransform, new Vector2(16f, -54f), new Vector2(278f, 34f), new Vector2(0f, 1f));
            var fuelBackground = CreateUiImage("FuelBackground", resourcePanel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(1f, 1f, 1f, 0.14f));
            SetRect(fuelBackground.GetComponent<RectTransform>(), new Vector2(16f, 13f), new Vector2(278f, 12f), Vector2.zero);
            fuelBackground.GetComponent<Image>().raycastTarget = false;
            var fuelFillObject = CreateUiImage("Fill", fuelBackground.transform, Vector2.zero, Vector2.one, new Color(0.20f, 0.82f, 0.36f, 1f));
            var fuelFill = fuelFillObject.GetComponent<Image>();
            fuelFill.raycastTarget = false;
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Horizontal;
            fuelFill.fillOrigin = 0;
            fuelFill.fillAmount = 0.7f;
            canvasObject.AddComponent<ResourceHud>().Configure(resources, coinText, fuelText, fuelFill);

            var debugPanel = CreateUiImage("DebugPanel", safeArea.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.62f));
            SetRect(debugPanel.GetComponent<RectTransform>(), new Vector2(18f, -18f), new Vector2(330f, 355f), new Vector2(0f, 1f));
            var debugText = CreateText("Telemetry", debugPanel.transform, "", 26, TextAnchor.UpperLeft);
            Stretch(debugText.rectTransform, 14f);
            var debugHud = canvasObject.AddComponent<DebugHud>();
            debugHud.Configure(controller, recovery, debugText, debugPanel);

            var resetButton = CreateButton("ResetButton", safeArea.transform, "RESET  [R]", new Color(0.12f, 0.15f, 0.2f, 0.86f));
            SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(-18f, -18f), new Vector2(220f, 84f), new Vector2(1f, 1f));
            resetButton.AddComponent<ResetButtonBinding>().Configure(input);

            var settingsButtonObject = CreateButton("SettingsButton", safeArea.transform, "OPTIONS  [ESC]", new Color(0.12f, 0.15f, 0.2f, 0.86f));
            SetRect(settingsButtonObject.GetComponent<RectTransform>(), new Vector2(-256f, -18f), new Vector2(220f, 84f), new Vector2(1f, 1f));

            var cameraButtonObject = CreateButton("CameraButton", safeArea.transform, "CAMERA: CHASE  [V]", new Color(0.12f, 0.15f, 0.2f, 0.86f));
            SetRect(cameraButtonObject.GetComponent<RectTransform>(), new Vector2(-494f, -18f), new Vector2(220f, 84f), new Vector2(1f, 1f));
            cameraButtonObject.AddComponent<CameraModeButtonBinding>().Configure(chaseCamera, cameraButtonObject.transform.Find("Label").GetComponent<Text>());

            var settingsPanel = CreateUiImage("SettingsPanel", safeArea.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.88f));
            var settingsCard = CreateUiImage("SettingsCard", settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.06f, 0.10f, 0.98f));
            SetRect(settingsCard.GetComponent<RectTransform>(), Vector2.zero, new Vector2(700f, 720f), new Vector2(0.5f, 0.5f));
            var settingsTitle = CreateText("Title", settingsCard.transform, "OPTIONS & ACCESSIBILITY", 38, TextAnchor.MiddleCenter);
            SetRect(settingsTitle.rectTransform, new Vector2(0f, -28f), new Vector2(640f, 62f), new Vector2(0.5f, 1f));
            var settingsHint = CreateText("Hint", settingsCard.transform, "Changes are saved automatically", 20, TextAnchor.MiddleCenter);
            SetRect(settingsHint.rectTransform, new Vector2(0f, -88f), new Vector2(620f, 42f), new Vector2(0.5f, 1f));
            settingsHint.color = new Color(1f, 1f, 1f, 0.68f);

            var audioButtonObject = CreateButton("AudioToggle", settingsCard.transform, "AUDIO  ON", new Color(0.12f, 0.55f, 0.92f, 0.95f));
            SetRect(audioButtonObject.GetComponent<RectTransform>(), new Vector2(0f, -154f), new Vector2(540f, 70f), new Vector2(0.5f, 1f));
            var hapticsButtonObject = CreateButton("HapticsToggle", settingsCard.transform, "HAPTICS  ON", new Color(0.12f, 0.55f, 0.92f, 0.95f));
            SetRect(hapticsButtonObject.GetComponent<RectTransform>(), new Vector2(0f, -238f), new Vector2(540f, 70f), new Vector2(0.5f, 1f));
            var contrastButtonObject = CreateButton("ContrastToggle", settingsCard.transform, "HIGH CONTRAST  OFF", new Color(0.12f, 0.55f, 0.92f, 0.95f));
            SetRect(contrastButtonObject.GetComponent<RectTransform>(), new Vector2(0f, -322f), new Vector2(540f, 70f), new Vector2(0.5f, 1f));
            var largeHudButtonObject = CreateButton("LargeHudToggle", settingsCard.transform, "LARGE HUD  OFF", new Color(0.12f, 0.55f, 0.92f, 0.95f));
            SetRect(largeHudButtonObject.GetComponent<RectTransform>(), new Vector2(0f, -406f), new Vector2(540f, 70f), new Vector2(0.5f, 1f));
            var reducedMotionButtonObject = CreateButton("ReducedMotionToggle", settingsCard.transform, "REDUCED MOTION  OFF", new Color(0.12f, 0.55f, 0.92f, 0.95f));
            SetRect(reducedMotionButtonObject.GetComponent<RectTransform>(), new Vector2(0f, -490f), new Vector2(540f, 70f), new Vector2(0.5f, 1f));
            var settingsCloseObject = CreateButton("CloseButton", settingsCard.transform, "RETURN TO DRIVE", new Color(0.86f, 0.32f, 0.08f, 0.98f));
            SetRect(settingsCloseObject.GetComponent<RectTransform>(), new Vector2(0f, 34f), new Vector2(540f, 78f), new Vector2(0.5f, 0f));

            var preferences = canvasObject.AddComponent<PlayerPreferencesController>();
            preferences.Configure(
                scaler,
                chaseCamera,
                settingsPanel,
                settingsButtonObject.GetComponent<Button>(),
                settingsCloseObject.GetComponent<Button>(),
                audioButtonObject.GetComponent<Button>(),
                audioButtonObject.transform.Find("Label").GetComponent<Text>(),
                hapticsButtonObject.GetComponent<Button>(),
                hapticsButtonObject.transform.Find("Label").GetComponent<Text>(),
                contrastButtonObject.GetComponent<Button>(),
                contrastButtonObject.transform.Find("Label").GetComponent<Text>(),
                largeHudButtonObject.GetComponent<Button>(),
                largeHudButtonObject.transform.Find("Label").GetComponent<Text>(),
                reducedMotionButtonObject.GetComponent<Button>(),
                reducedMotionButtonObject.transform.Find("Label").GetComponent<Text>());
            settingsPanel.SetActive(false);

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
            return new UiReferences { Results = results, Preferences = preferences };
        }

        private static void CreateRoadSegment(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material, PhysicsMaterial physicsMaterial)
        {
            var delta = end - start;
            // The terrain surface is 1 cm below the road: visually separate to
            // prevent z-fighting, but physically flush enough to avoid a curb.
            var segment = CreateBox(name, parent, (start + end) * 0.5f + Vector3.down * 0.2f, new Vector3(width, 0.2f, delta.magnitude + 0.8f), material, physicsMaterial, 7);
            segment.GetComponent<BoxCollider>().isTrigger = true;
            segment.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void CreateRoadMarker(string name, Transform parent, Vector3 start, Vector3 end, Material material)
        {
            var delta = end - start;
            var marker = CreateVisualBox(name, parent, (start + end) * 0.5f + Vector3.down * 0.08f, new Vector3(0.28f, 0.025f, Mathf.Max(1f, delta.magnitude - 2f)), material);
            marker.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void CreateBuilding(
            string name,
            Transform parent,
            Vector3 groundPosition,
            Vector2 footprint,
            float height,
            Materials materials,
            PhysicsMaterial physicsMaterial,
            Material roofMaterial = null)
        {
            var bodyPosition = groundPosition + Vector3.up * (height * 0.5f);
            CreateBox(name, parent, bodyPosition, new Vector3(footprint.x, height, footprint.y), materials.Building, physicsMaterial, 8);
            CreateVisualBox($"{name}_Roof", parent, bodyPosition + Vector3.up * (height * 0.5f + 0.3f), new Vector3(footprint.x * 0.82f, 0.6f, footprint.y * 0.82f), roofMaterial != null ? roofMaterial : materials.Concrete);
        }

        private static void CreateRoadSign(string id, Transform parent, Vector3 position, float yaw, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var root = new GameObject(id);
            root.layer = 11;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.25f, 0f);
            collider.size = new Vector3(0.55f, 2.5f, 0.45f);
            collider.material = physicsMaterial;
            collider.isTrigger = true;
            var pole = CreateVisualBox("Pole", root.transform, Vector3.zero, new Vector3(0.18f, 2.2f, 0.18f), materials.Concrete);
            pole.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var face = CreateVisualBox("Face", root.transform, Vector3.zero, new Vector3(1.1f, 0.85f, 0.12f), materials.Sign);
            face.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            root.AddComponent<BreakableRoadSign>().Configure(id, 1.5f, 12f, root.GetComponentsInChildren<Renderer>(), root.GetComponentsInChildren<Collider>());
        }

        private static void CreateCoin(string name, Transform parent, Vector3 position, RideResourceController resources, FuelProfile profile, Material material)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = name;
            coin.layer = 12;
            coin.transform.SetParent(parent, false);
            coin.transform.position = position;
            coin.transform.localScale = new Vector3(0.7f, 0.10f, 0.7f);
            coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            coin.GetComponent<Renderer>().sharedMaterial = material;
            var collider = coin.GetComponent<Collider>();
            collider.isTrigger = true;
            coin.AddComponent<RoadCollectible>().Configure(CollectibleKind.Coin, resources, profile.coinPoints, profile.coinRespawnDelay, coin.GetComponentsInChildren<Renderer>(), collider);
        }

        private static void CreateFuelCanister(string name, Transform parent, Vector3 position, RideResourceController resources, FuelProfile profile, Materials materials)
        {
            var canister = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canister.name = name;
            canister.layer = 12;
            canister.transform.SetParent(parent, false);
            canister.transform.position = position;
            canister.transform.localScale = new Vector3(0.85f, 1.2f, 0.5f);
            canister.GetComponent<Renderer>().sharedMaterial = materials.Fuel;
            var collider = canister.GetComponent<Collider>();
            collider.isTrigger = true;
            var handle = CreateVisualBox("Handle", canister.transform, position + Vector3.up * 0.82f, new Vector3(0.48f, 0.16f, 0.16f), materials.Concrete);
            canister.AddComponent<RoadCollectible>().Configure(CollectibleKind.FuelCanister, resources, profile.canisterFuel, profile.canisterRespawnDelay, canister.GetComponentsInChildren<Renderer>(), collider);
        }

        private static GameObject CreateVisualBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            return gameObject;
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
            layers.GetArrayElementAtIndex(9).stringValue = "Police";
            layers.GetArrayElementAtIndex(10).stringValue = "Traffic";
            layers.GetArrayElementAtIndex(11).stringValue = "Breakable";
            layers.GetArrayElementAtIndex(12).stringValue = "Collectible";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Profiles
        {
            public VehicleHandlingProfile Vehicle;
            public ChaseCameraProfile Camera;
            public RideRulesProfile Ride;
            public ScoringProfile Scoring;
            public PoliceProfile Police;
            public TrafficProfile Traffic;
            public FuelProfile Fuel;
            public ChallengeDefinition[] Challenges;
        }

        private sealed class Materials
        {
            public Material Road;
            public Material Terrain;
            public Material Building;
            public Material Barrier;
            public Material Concrete;
            public Material Water;
            public Material Marking;
            public Material Car;
            public Material Accent;
            public Material Police;
            public Material PoliceLight;
            public Material Traffic;
            public Material Sign;
            public Material Coin;
            public Material Fuel;
        }

        private sealed class UiReferences
        {
            public RideResultsView Results;
            public PlayerPreferencesController Preferences;
        }
    }
}
