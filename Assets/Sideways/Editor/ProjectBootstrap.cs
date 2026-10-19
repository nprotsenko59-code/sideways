using System;
using System.Collections.Generic;
using System.IO;
using Sideways.CameraSystem;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.EnvironmentSystem;
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
            var roadGraph = EnsureRoadGraph();
            var materials = EnsureMaterials();
            var physicsMaterial = EnsurePhysicsMaterial();
            BuildCarPrefab(profiles, materials, physicsMaterial);
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
                Ride = EnsureAsset<RideRulesProfile>(Root + "/Configuration/DefaultRideRules.asset"),
                Scoring = EnsureAsset<ScoringProfile>(Root + "/Configuration/DefaultScoring.asset")
            };
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
            var collisionSensor = root.AddComponent<VehicleCollisionSensor>();
            collisionSensor.Configure(1 << 8);

            PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
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
            var ui = BuildUi(input, controller, recovery, scoring);
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
            CreateBox("Ground", parent, new Vector3(0f, -1.1f, 0f), new Vector3(900f, 2f, 800f), materials.Terrain, physicsMaterial, 0);

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

        private static UiReferences BuildUi(VehicleInputSource input, VehicleController controller, VehicleRecovery recovery, DriftScoreController scoring)
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

            var debugPanel = CreateUiImage("DebugPanel", safeArea.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.62f));
            SetRect(debugPanel.GetComponent<RectTransform>(), new Vector2(18f, -18f), new Vector2(330f, 325f), new Vector2(0f, 1f));
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

        private static void CreateRoadSegment(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material, PhysicsMaterial physicsMaterial)
        {
            var delta = end - start;
            var segment = CreateBox(name, parent, (start + end) * 0.5f + Vector3.up * 0.05f, new Vector3(width, 0.2f, delta.magnitude + 0.8f), material, physicsMaterial, 7);
            segment.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void CreateRoadMarker(string name, Transform parent, Vector3 start, Vector3 end, Material material)
        {
            var delta = end - start;
            var marker = CreateVisualBox(name, parent, (start + end) * 0.5f + Vector3.up * 0.17f, new Vector3(0.28f, 0.025f, Mathf.Max(1f, delta.magnitude - 2f)), material);
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
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Profiles
        {
            public VehicleHandlingProfile Vehicle;
            public ChaseCameraProfile Camera;
            public RideRulesProfile Ride;
            public ScoringProfile Scoring;
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
        }

        private sealed class UiReferences
        {
            public RideResultsView Results;
        }
    }
}
