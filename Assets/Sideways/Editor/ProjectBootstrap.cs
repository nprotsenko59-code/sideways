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
using Sideways.Stunts;
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
            var roadGraph = EnsurePortraitDriftCourse();
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
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
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
                Stunts = EnsureAsset<StuntProfile>(Root + "/Configuration/DefaultStunts.asset"),
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

        private static RoadGraphAsset EnsurePortraitDriftCourse()
        {
            const string path = Root + "/Configuration/PortraitDriftCourse.asset";
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<RoadGraphAsset>();
                AssetDatabase.CreateAsset(graph, path);
            }

            var nodes = new[]
            {
                new RoadGraphNode("COURSE_START", P(0f, -340f), RoadNodeRole.Landmark, true),
                N("COURSE_01", 0f, -260f),
                N("COURSE_02", -90f, -150f),
                N("COURSE_03", -90f, -55f),
                N("COURSE_04", 90f, 65f),
                N("COURSE_05", 90f, 165f),
                N("COURSE_06", -75f, 275f),
                new RoadGraphNode("COURSE_FINISH", P(0f, 350f), RoadNodeRole.Landmark, true)
            };
            var route = RoadFeature.MainRoute | RoadFeature.ComboRoute | RoadFeature.Signature;
            var edges = new List<RoadGraphEdge>
            {
                EF("Course_Straight", "COURSE_START", "COURSE_01", RoadKind.Boulevard, 18f, 22f, RoadAccess.All, route,
                    P(0f, -340f), P(0f, -300f), P(0f, -260f)),
                EF("Course_LeftEntry", "COURSE_01", "COURSE_02", RoadKind.Boulevard, 18f, 21f, RoadAccess.All, route,
                    P(0f, -260f), P(-5f, -230f), P(-55f, -175f), P(-90f, -150f)),
                EF("Course_LeftArc", "COURSE_02", "COURSE_03", RoadKind.Boulevard, 18f, 20f, RoadAccess.All, route,
                    P(-90f, -150f), P(-110f, -125f), P(-110f, -82f), P(-90f, -55f)),
                EF("Course_Crossover", "COURSE_03", "COURSE_04", RoadKind.Boulevard, 18f, 22f, RoadAccess.All, route,
                    P(-90f, -55f), P(-65f, -25f), P(-10f, 15f), P(55f, 48f), P(90f, 65f)),
                EF("Course_RightArc", "COURSE_04", "COURSE_05", RoadKind.Boulevard, 18f, 20f, RoadAccess.All, route,
                    P(90f, 65f), P(110f, 90f), P(110f, 138f), P(90f, 165f)),
                EF("Course_NorthSweep", "COURSE_05", "COURSE_06", RoadKind.Boulevard, 18f, 22f, RoadAccess.All, route,
                    P(90f, 165f), P(65f, 195f), P(15f, 225f), P(-45f, 255f), P(-75f, 275f)),
                EF("Course_Finish", "COURSE_06", "COURSE_FINISH", RoadKind.Boulevard, 18f, 22f, RoadAccess.All, route,
                    P(-75f, 275f), P(-45f, 305f), P(-10f, 330f), P(0f, 350f))
            };

            WidenRoads(edges);
            RoundRoadCenterlines(edges);
            graph.Configure("portrait-drift-run", new Vector2(320f, 720f), nodes, edges);
            if (!graph.IsValid(out var error)) throw new InvalidOperationException($"Portrait drift course is invalid: {error}");
            EditorUtility.SetDirty(graph);
            return graph;
        }

        private static RoadGraphAsset EnsureExpresswayMazeRoadGraph()
        {
            const string path = Root + "/Configuration/LAPrototypeRoadGraph.asset";
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<RoadGraphAsset>();
                AssetDatabase.CreateAsset(graph, path);
            }

            // Coordinates deliberately follow the supplied top-down schema. The
            // legacy ids remain stable, but now describe the recognizable maze:
            // loop in the northwest, spiral in the northeast, canal on the east,
            // and the start/finish plus escape arc along the south edge.
            var nodes = new[]
            {
                N("G_NW", -245f, 10f), N("G_NC", -65f, 25f, RoadNodeRole.Landmark), N("G_NE", 115f, 20f),
                N("G_W", -255f, -85f), N("G_C", -45f, -75f, RoadNodeRole.Landmark), N("G_E", 155f, -70f),
                N("G_SW", -175f, -185f), N("G_SC", 0f, -225f), N("G_SE", 165f, -210f),
                N("B_W", -355f, -285f, RoadNodeRole.BoundaryLoop), N("B_SW", -185f, -285f),
                N("B_SC", 0f, -330f, RoadNodeRole.Landmark), N("B_SE", 190f, -300f), N("B_E", 315f, -270f, RoadNodeRole.BoundaryLoop),
                N("O_W", -390f, -135f, RoadNodeRole.BoundaryLoop), N("O_NW", -335f, 15f, RoadNodeRole.BoundaryLoop),
                N("O_N", 0f, 335f, RoadNodeRole.BoundaryLoop), N("O_NE", 390f, 275f, RoadNodeRole.BoundaryLoop),
                N("O_E", 405f, -70f, RoadNodeRole.BoundaryLoop),

                N("U_W", -105f, 190f, 8f), N("U_C", 35f, 180f, 8f, RoadNodeRole.Landmark), N("U_E", 180f, 175f, 8f),
                N("R_W", -95f, -20f, 6f), N("R_C", 0f, -5f, 6f, RoadNodeRole.Landmark), N("R_E", 70f, -55f, 6f),
                N("H_W", -25f, 255f, 3f), N("H_C", 0f, 300f, 6f, RoadNodeRole.Landmark), N("H_E", 90f, 260f, 4f),
                N("E_RN", 180f, -125f), N("E_RE", 235f, -155f, RoadNodeRole.Landmark), N("E_RS", 205f, -205f),
                N("P_LOW", -20f, -35f, 2.5f), N("P_N", 25f, -12f, 5f), N("P_S", 55f, -55f, 5f),
                N("I_W", -305f, -320f), N("I_C", -120f, -320f, RoadNodeRole.Landmark), N("I_E", 95f, -320f),
                N("D_W", -125f, -185f), N("D_E", -35f, -175f),

                N("L_S", -260f, 85f, 8f), N("L_E", -115f, 190f, 8f),
                N("L_N", -260f, 300f, 8f, RoadNodeRole.Landmark), N("L_W", -410f, 190f, 8f),
                N("J_W", 180f, 165f, 8f), N("J_N", 285f, 235f, 8f),
                N("J_E", 375f, 155f, 8f), N("J_S", 285f, 60f, 8f, RoadNodeRole.Landmark),
                N("C_M", 410f, -55f, 8f), N("C_S", 300f, -260f, 8f),
                N("ESC_W", -405f, -245f, 3f)
            };

            var main = RoadFeature.MainRoute | RoadFeature.ComboRoute;
            var service = RoadFeature.ServiceRoute;
            var elevated = RoadFeature.Elevated;
            var edges = new List<RoadGraphEdge>
            {
                // Pink street-level combo route and its dense alternate turns.
                EF("Grid_N_WC", "G_NW", "G_NC", RoadKind.Street, 15f, 17f, RoadAccess.All, service,
                    P(-245f, 10f), P(-175f, 35f), P(-115f, 5f), P(-65f, 25f)),
                EF("Grid_N_CE", "G_NC", "G_NE", RoadKind.Street, 15f, 17f, RoadAccess.All, service,
                    P(-65f, 25f), P(-5f, 58f), P(65f, 52f), P(115f, 20f)),
                EF("Grid_M_WC", "G_W", "G_C", RoadKind.Street, 16f, 18f, RoadAccess.All, main,
                    P(-255f, -85f), P(-190f, -95f), P(-120f, -65f), P(-45f, -75f)),
                EF("Grid_M_CE", "G_C", "G_E", RoadKind.Street, 16f, 18f, RoadAccess.All, main,
                    P(-45f, -75f), P(20f, -48f), P(85f, -95f), P(155f, -70f)),
                EF("Grid_S_WC", "G_SW", "G_SC", RoadKind.Street, 15f, 17f, RoadAccess.All, main,
                    P(-175f, -185f), P(-120f, -205f), P(-62f, -190f), P(0f, -225f)),
                EF("Grid_S_CE", "G_SC", "G_SE", RoadKind.Street, 15f, 17f, RoadAccess.All, main,
                    P(0f, -225f), P(60f, -245f), P(112f, -235f), P(165f, -210f)),
                EF("Grid_W_NS", "G_NW", "G_W", RoadKind.Street, 14f, 15f, RoadAccess.All, service,
                    P(-245f, 10f), P(-280f, -30f), P(-255f, -85f)),
                EF("Grid_W_SS", "G_W", "G_SW", RoadKind.Street, 14f, 15f, RoadAccess.All, main,
                    P(-255f, -85f), P(-225f, -140f), P(-175f, -185f)),
                EF("Grid_C_NS", "G_NC", "G_C", RoadKind.Street, 14f, 15f, RoadAccess.All, service,
                    P(-65f, 25f), P(-100f, -12f), P(-45f, -75f)),
                EF("Grid_C_SS", "G_C", "G_SC", RoadKind.Street, 14f, 16f, RoadAccess.All, service,
                    P(-45f, -75f), P(-10f, -125f), P(-35f, -170f), P(0f, -225f)),
                EF("Grid_E_NS", "G_NE", "G_E", RoadKind.Street, 14f, 16f, RoadAccess.All, main,
                    P(115f, 20f), P(145f, -20f), P(155f, -70f)),
                EF("Grid_E_SS", "G_E", "G_SE", RoadKind.Street, 14f, 16f, RoadAccess.All, main,
                    P(155f, -70f), P(190f, -130f), P(165f, -210f)),

                // Start/finish boulevard and southwest industrial continuation.
                EF("Boulevard_W0", "B_W", "B_SW", RoadKind.Boulevard, 22f, 24f, RoadAccess.All, service,
                    P(-355f, -285f), P(-275f, -310f), P(-185f, -285f)),
                EF("Boulevard_W1", "B_SW", "B_SC", RoadKind.Boulevard, 22f, 24f, RoadAccess.All, main,
                    P(-185f, -285f), P(-95f, -330f), P(0f, -330f)),
                EF("Boulevard_E1", "B_SC", "B_SE", RoadKind.Boulevard, 22f, 24f, RoadAccess.All, main,
                    P(0f, -330f), P(95f, -330f), P(190f, -300f)),
                EF("Boulevard_E0", "B_SE", "B_E", RoadKind.Boulevard, 22f, 24f, RoadAccess.All, service,
                    P(190f, -300f), P(260f, -290f), P(315f, -270f)),
                EF("BoulevardLink_W", "B_SW", "G_SW", RoadKind.Street, 15f, 17f, RoadAccess.All, main,
                    P(-185f, -285f), P(-205f, -235f), P(-175f, -185f)),
                EF("BoulevardLink_C", "B_SC", "G_SC", RoadKind.Street, 16f, 18f, RoadAccess.All, main,
                    P(0f, -330f), P(-10f, -280f), P(0f, -225f)),
                EF("BoulevardLink_E", "B_SE", "G_SE", RoadKind.Street, 15f, 17f, RoadAccess.All, main,
                    P(190f, -300f), P(205f, -255f), P(165f, -210f)),

                // Lower service road across the middle of the maze.
                EF("Scenic_SW", "B_W", "O_W", RoadKind.Scenic, 15f, 18f, RoadAccess.All, service,
                    P(-355f, -285f), P(-410f, -225f), P(-390f, -135f)),
                EF("Scenic_W", "O_W", "O_NW", RoadKind.Scenic, 15f, 18f, RoadAccess.All, service,
                    P(-390f, -135f), P(-385f, -70f), P(-350f, -20f), P(-335f, 15f)),
                EF("Scenic_NW", "O_NW", "O_N", RoadKind.Scenic, 15f, 18f, RoadAccess.All, service,
                    P(-335f, 15f), P(-285f, -25f), P(-220f, -35f), P(-150f, -15f), P(-70f, 45f), P(-25f, 180f), P(0f, 335f)),
                EF("Scenic_NE", "O_N", "O_NE", RoadKind.Scenic, 15f, 19f, RoadAccess.All, service,
                    P(0f, 335f), P(95f, 330f), P(205f, 310f), P(300f, 285f), P(390f, 275f)),
                EF("Scenic_E", "O_NE", "O_E", RoadKind.Scenic, 15f, 19f, RoadAccess.All, service,
                    P(390f, 275f), P(425f, 185f), P(425f, 65f), P(405f, -70f)),
                EF("Scenic_SE", "O_E", "B_E", RoadKind.Scenic, 15f, 18f, RoadAccess.All, service,
                    P(405f, -70f), P(400f, -155f), P(365f, -220f), P(315f, -270f)),
                E("HillLink_W", "O_NW", "G_NW", RoadKind.Street, 13f, 15f, P(-335f, 15f), P(-290f, 30f), P(-245f, 10f)),
                E("HillLink_C", "O_N", "G_NC", RoadKind.Street, 13f, 15f, P(0f, 335f), P(-25f, 220f), P(-20f, 110f), P(-65f, 25f)),
                E("HillLink_E", "O_NE", "G_NE", RoadKind.Street, 13f, 15f, P(390f, 275f), P(300f, 210f), P(220f, 110f), P(115f, 20f)),
                E("Alley_NW", "G_W", "G_NC", RoadKind.Alley, 9f, 12f, P(-255f, -85f), P(-180f, -35f), P(-65f, 25f)),
                E("Alley_SE", "G_SC", "G_E", RoadKind.Alley, 9f, 12f, P(0f, -225f), P(55f, -160f), P(155f, -70f)),
                E("Alley_Beach", "B_W", "G_SW", RoadKind.Alley, 10f, 13f, P(-355f, -285f), P(-285f, -250f), P(-225f, -220f), P(-175f, -185f)),
                E("Alley_Industrial", "G_SE", "B_E", RoadKind.Alley, 10f, 13f, P(165f, -210f), P(220f, -230f), P(270f, -245f), P(315f, -270f)),

                // Northwest signature drift loop and blue elevated spine.
                EF("SignatureRamp_S", "O_NW", "L_S", RoadKind.Boulevard, 18f, 18f, RoadAccess.All, RoadFeature.Ramp | elevated | RoadFeature.Signature,
                    P(-335f, 15f), P(-315f, 35f, 2.5f), P(-285f, 60f, 5.5f), P(-260f, 85f, 8f)),
                EF("Signature_SW", "L_S", "L_W", RoadKind.Boulevard, 20f, 24f, RoadAccess.All, elevated | RoadFeature.Signature,
                    P(-260f, 85f, 8f), P(-340f, 90f, 8f), P(-400f, 125f, 8f), P(-410f, 190f, 8f)),
                EF("Signature_NW", "L_W", "L_N", RoadKind.Boulevard, 20f, 24f, RoadAccess.All, elevated | RoadFeature.Signature,
                    P(-410f, 190f, 8f), P(-405f, 255f, 8f), P(-340f, 300f, 8f), P(-260f, 300f, 8f)),
                EF("Signature_NE", "L_N", "L_E", RoadKind.Boulevard, 20f, 24f, RoadAccess.All, elevated | RoadFeature.Signature,
                    P(-260f, 300f, 8f), P(-180f, 300f, 8f), P(-120f, 255f, 8f), P(-115f, 190f, 8f)),
                EF("Signature_SE", "L_E", "L_S", RoadKind.Boulevard, 20f, 24f, RoadAccess.All, elevated | RoadFeature.Signature,
                    P(-115f, 190f, 8f), P(-120f, 125f, 8f), P(-180f, 85f, 8f), P(-260f, 85f, 8f)),
                EF("SignatureRamp_E", "L_E", "U_W", RoadKind.Boulevard, 18f, 22f, RoadAccess.All, RoadFeature.Ramp | elevated | RoadFeature.Signature,
                    P(-115f, 190f, 8f), P(-105f, 190f, 8f)),
                EF("UpperRamp_W", "G_NW", "U_W", RoadKind.Street, 16f, 17f, RoadAccess.All, RoadFeature.Ramp | elevated,
                    P(-245f, 10f), P(-210f, 55f, 2.5f), P(-155f, 125f, 6f), P(-105f, 190f, 8f)),
                EF("Upper_Crosstown_W", "U_W", "U_C", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, elevated,
                    P(-105f, 190f, 8f), P(-35f, 205f, 8f), P(35f, 180f, 8f)),
                EF("Upper_Crosstown_E", "U_C", "U_E", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, elevated,
                    P(35f, 180f, 8f), P(105f, 155f, 8f), P(180f, 175f, 8f)),
                EF("UpperRamp_E", "U_E", "G_NE", RoadKind.Street, 16f, 17f, RoadAccess.All, RoadFeature.Ramp | elevated,
                    P(180f, 175f, 8f), P(165f, 120f, 5.5f), P(145f, 65f, 2.5f), P(115f, 20f)),

                // Downtown climb at the top and northeast multi-level spiral.
                EF("Hill_Hairpin_W", "U_C", "H_W", RoadKind.Scenic, 14f, 15f, RoadAccess.All, RoadFeature.Ramp | elevated,
                    P(35f, 180f, 8f), P(0f, 220f, 5.5f), P(-25f, 255f, 3f)),
                EF("Hill_Hairpin_C", "H_W", "H_C", RoadKind.Scenic, 14f, 14f, RoadAccess.All, RoadFeature.Ramp,
                    P(-25f, 255f, 3f), P(-25f, 290f, 4.5f), P(0f, 300f, 6f)),
                EF("Hill_Hairpin_E", "H_C", "H_E", RoadKind.Scenic, 14f, 15f, RoadAccess.All, RoadFeature.Ramp,
                    P(0f, 300f, 6f), P(45f, 320f, 5f), P(90f, 260f, 4f)),
                EF("Hill_Return", "H_E", "O_N", RoadKind.Scenic, 14f, 16f, RoadAccess.All, RoadFeature.Ramp,
                    P(90f, 260f, 4f), P(45f, 305f, 2f), P(0f, 335f)),
                EF("Interchange_Link", "U_E", "J_W", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, RoadFeature.Ramp | elevated | RoadFeature.Interchange,
                    P(180f, 175f, 8f), P(180f, 165f, 8f)),
                EF("Interchange_NW", "J_W", "J_N", RoadKind.Boulevard, 18f, 21f, RoadAccess.All, elevated | RoadFeature.Interchange,
                    P(180f, 165f, 8f), P(205f, 220f, 8f), P(250f, 245f, 8f), P(285f, 235f, 8f)),
                EF("Interchange_NE", "J_N", "J_E", RoadKind.Boulevard, 18f, 21f, RoadAccess.All, elevated | RoadFeature.Interchange,
                    P(285f, 235f, 8f), P(345f, 235f, 8f), P(385f, 205f, 8f), P(375f, 155f, 8f)),
                EF("Interchange_SE", "J_E", "J_S", RoadKind.Boulevard, 18f, 21f, RoadAccess.All, elevated | RoadFeature.Interchange,
                    P(375f, 155f, 8f), P(380f, 95f, 8f), P(335f, 55f, 8f), P(285f, 60f, 8f)),
                EF("Interchange_SW", "J_S", "J_W", RoadKind.Boulevard, 18f, 21f, RoadAccess.All, elevated | RoadFeature.Interchange,
                    P(285f, 60f, 8f), P(225f, 65f, 8f), P(190f, 105f, 8f), P(180f, 165f, 8f)),
                EF("CanalRamp_N", "J_N", "O_NE", RoadKind.Scenic, 16f, 20f, RoadAccess.All, RoadFeature.Ramp | elevated | RoadFeature.Interchange,
                    P(285f, 235f, 8f), P(330f, 265f, 5f), P(390f, 275f)),
                EF("Canal_North", "J_E", "C_M", RoadKind.Boulevard, 18f, 24f, RoadAccess.All, elevated,
                    P(375f, 155f, 8f), P(410f, 95f, 8f), P(420f, 15f, 8f), P(410f, -55f, 8f)),
                EF("Canal_South", "C_M", "C_S", RoadKind.Boulevard, 18f, 24f, RoadAccess.All, elevated,
                    P(410f, -55f, 8f), P(420f, -135f, 8f), P(385f, -215f, 8f), P(300f, -260f, 8f)),
                EF("CanalRamp_S", "C_S", "B_SE", RoadKind.Boulevard, 18f, 20f, RoadAccess.All, RoadFeature.Ramp | elevated,
                    P(300f, -260f, 8f), P(260f, -275f, 5.5f), P(225f, -290f, 2.5f), P(190f, -300f)),

                // Yellow blind shortcut / rooftop drop in the center.
                EF("ParkingRamp_In", "G_NC", "P_LOW", RoadKind.Alley, 11f, 11f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(-65f, 25f), P(-45f, 5f, 1.2f), P(-20f, -35f, 2.5f)),
                EF("ParkingSwitchback_Up", "P_LOW", "P_N", RoadKind.Alley, 11f, 10f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(-20f, -35f, 2.5f), P(5f, -55f, 3.8f), P(25f, -12f, 5f)),
                EF("ParkingDeck_Run", "P_N", "P_S", RoadKind.Alley, 12f, 12f, RoadAccess.Player | RoadAccess.Police, elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(25f, -12f, 5f), P(55f, -15f, 5f), P(70f, -35f, 5f), P(55f, -55f, 5f)),
                EF("ParkingSwitchback_Out", "P_S", "G_C", RoadKind.Alley, 11f, 10f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(55f, -55f, 5f), P(5f, -65f, 2.5f), P(-45f, -75f)),
                EF("RoofRamp_W", "G_W", "R_W", RoadKind.Alley, 12f, 13f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(-255f, -85f), P(-175f, -55f, 3f), P(-95f, -20f, 6f)),
                EF("RoofRun_W", "R_W", "R_C", RoadKind.Alley, 12f, 14f, RoadAccess.Player | RoadAccess.Police, elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(-95f, -20f, 6f), P(-45f, 5f, 6f), P(0f, -5f, 6f)),
                EF("RoofRun_E", "R_C", "R_E", RoadKind.Alley, 12f, 14f, RoadAccess.Player | RoadAccess.Police, elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(0f, -5f, 6f), P(45f, -20f, 6f), P(70f, -55f, 6f)),
                EF("RoofRamp_E", "R_E", "G_E", RoadKind.Alley, 12f, 13f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(70f, -55f, 6f), P(110f, -60f, 3f), P(155f, -70f)),

                // Southeast service loop, industrial yard, and player-only stunt spur.
                E("East_SCurve_N", "G_E", "E_RN", RoadKind.Scenic, 14f, 16f, P(155f, -70f), P(195f, -95f), P(180f, -125f)),
                E("East_Roundabout_N", "E_RN", "E_RE", RoadKind.Street, 14f, 14f, P(180f, -125f), P(225f, -115f), P(235f, -155f)),
                E("East_Roundabout_S", "E_RE", "E_RS", RoadKind.Street, 14f, 14f, P(235f, -155f), P(245f, -195f), P(205f, -205f)),
                E("East_Return", "E_RS", "G_SE", RoadKind.Street, 14f, 16f, P(205f, -205f), P(185f, -225f), P(165f, -210f)),
                E("GasStation_CutThrough", "G_SE", "E_RN", RoadKind.Alley, 10f, 13f, P(165f, -210f), P(150f, -165f), P(180f, -125f)),
                E("Industrial_W", "B_W", "I_W", RoadKind.Street, 16f, 18f, P(-355f, -285f), P(-335f, -315f), P(-305f, -320f)),
                E("Industrial_CW", "I_W", "I_C", RoadKind.Boulevard, 20f, 22f, P(-305f, -320f), P(-220f, -345f), P(-120f, -320f)),
                E("Industrial_CE", "I_C", "I_E", RoadKind.Boulevard, 20f, 22f, P(-120f, -320f), P(-20f, -345f), P(95f, -320f)),
                E("Industrial_E", "I_E", "B_SC", RoadKind.Street, 16f, 18f, P(95f, -320f), P(45f, -305f), P(0f, -330f)),
                EF("Drainage_Entry", "G_SW", "D_W", RoadKind.Alley, 10f, 12f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-175f, -185f), P(-150f, -205f), P(-125f, -185f)),
                EF("Drainage_Run", "D_W", "D_E", RoadKind.Alley, 10f, 15f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-125f, -185f), P(-95f, -155f), P(-65f, -205f), P(-35f, -175f)),
                EF("Drainage_Exit", "D_E", "G_SC", RoadKind.Alley, 10f, 12f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-35f, -175f), P(-5f, -195f), P(0f, -225f)),

                // Green police escape route: broad southwest arc plus hillside exit.
                EF("EscapeRamp_W", "B_SC", "ESC_W", RoadKind.Scenic, 16f, 19f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.EscapeRoute,
                    P(0f, -330f), P(-120f, -355f, 1f), P(-260f, -330f, 2f), P(-405f, -245f, 3f)),
                EF("EscapeRamp_S", "ESC_W", "O_W", RoadKind.Scenic, 16f, 19f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.EscapeRoute,
                    P(-405f, -245f, 3f), P(-430f, -205f, 2f), P(-420f, -165f, 1f), P(-390f, -135f)),
                EF("Escape_Hillside", "J_N", "O_NE", RoadKind.Scenic, 16f, 21f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Elevated | RoadFeature.EscapeRoute,
                    P(285f, 235f, 8f), P(335f, 260f, 6f), P(390f, 275f))
            };

            WidenRoads(edges);
            RoundRoadCenterlines(edges);
            graph.Configure("sunset-basin-schema", new Vector2(900f, 720f), nodes, edges);
            if (!graph.IsValid(out var error)) throw new InvalidOperationException($"Expressway Maze graph is invalid: {error}");
            EditorUtility.SetDirty(graph);
            return graph;
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
                N("O_E", 225f, -40f, RoadNodeRole.BoundaryLoop),

                N("U_W", -190f, 20f, 7f, RoadNodeRole.Junction), N("U_C", -20f, 35f, 7f, RoadNodeRole.Landmark),
                N("U_E", 190f, 25f, 7f, RoadNodeRole.Junction),
                N("R_W", -100f, -135f, 6f, RoadNodeRole.Junction), N("R_C", 0f, -135f, 6f, RoadNodeRole.Landmark),
                N("R_E", 100f, -135f, 6f, RoadNodeRole.Junction),
                N("H_W", -70f, 235f, 3f, RoadNodeRole.Junction), N("H_C", 20f, 265f, 6f, RoadNodeRole.Landmark),
                N("H_E", 120f, 235f, 4f, RoadNodeRole.Junction),
                N("E_RN", 270f, 100f, RoadNodeRole.Junction), N("E_RE", 310f, 40f, RoadNodeRole.Landmark),
                N("E_RS", 270f, -20f, RoadNodeRole.Junction),
                N("P_LOW", 235f, 85f, 2.5f, RoadNodeRole.Junction), N("P_N", 270f, 85f, 5f, RoadNodeRole.Junction),
                N("P_S", 295f, 55f, 5f, RoadNodeRole.Junction),
                N("I_W", -250f, -235f, RoadNodeRole.Junction), N("I_C", 0f, -255f, RoadNodeRole.Landmark),
                N("I_E", 250f, -235f, RoadNodeRole.Junction),
                N("D_W", -185f, -245f, RoadNodeRole.Junction), N("D_E", -75f, -245f, RoadNodeRole.Junction),

                N("L_S", -285f, 35f, 8f, RoadNodeRole.Junction), N("L_E", -155f, 155f, 8f, RoadNodeRole.Junction),
                N("L_N", -285f, 275f, 8f, RoadNodeRole.Landmark), N("L_W", -410f, 155f, 8f, RoadNodeRole.Junction),
                N("J_W", 255f, 85f, 8f, RoadNodeRole.Junction), N("J_N", 330f, 160f, 8f, RoadNodeRole.Junction),
                N("J_E", 415f, 75f, 8f, RoadNodeRole.Junction), N("J_S", 330f, -15f, 8f, RoadNodeRole.Landmark),
                N("C_M", 415f, -105f, 8f, RoadNodeRole.Junction), N("C_S", 365f, -275f, 8f, RoadNodeRole.Junction),
                N("ESC_W", -365f, -185f, 3f, RoadNodeRole.Junction)
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

            edges.AddRange(new[]
            {
                EF("UpperRamp_W", "O_W", "U_W", RoadKind.Street, 15f, 16f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated,
                    P(-225f, -40f), P(-215f, -8f, 3.5f), P(-190f, 20f, 7f)),
                EF("Upper_Crosstown_W", "U_W", "U_C", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated,
                    P(-190f, 20f, 7f), P(-110f, 52f, 7f), P(-20f, 35f, 7f)),
                EF("Upper_Crosstown_E", "U_C", "U_E", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated,
                    P(-20f, 35f, 7f), P(70f, 18f, 7f), P(135f, 48f, 7f), P(190f, 25f, 7f)),
                EF("UpperRamp_E", "U_E", "O_E", RoadKind.Street, 15f, 16f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated,
                    P(190f, 25f, 7f), P(215f, -5f, 3.5f), P(225f, -40f)),

                EF("RoofRamp_W", "B_SW", "R_W", RoadKind.Alley, 12f, 13f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Rooftop,
                    P(-120f, -165f), P(-112f, -150f, 3f), P(-100f, -135f, 6f)),
                EF("RoofRun_W", "R_W", "R_C", RoadKind.Alley, 12f, 14f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Elevated | RoadFeature.Rooftop,
                    P(-100f, -135f, 6f), P(-50f, -125f, 6f), P(0f, -135f, 6f)),
                EF("RoofRun_E", "R_C", "R_E", RoadKind.Alley, 12f, 14f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Elevated | RoadFeature.Rooftop,
                    P(0f, -135f, 6f), P(50f, -145f, 6f), P(100f, -135f, 6f)),
                EF("RoofRamp_E", "R_E", "B_SE", RoadKind.Alley, 12f, 13f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Rooftop,
                    P(100f, -135f, 6f), P(112f, -150f, 3f), P(120f, -165f)),

                EF("Hill_Hairpin_W", "O_N", "H_W", RoadKind.Scenic, 14f, 14f, RoadAccess.All, RoadFeature.Ramp,
                    P(0f, 195f), P(-35f, 210f, 1.5f), P(-70f, 235f, 3f)),
                EF("Hill_Hairpin_C", "H_W", "H_C", RoadKind.Scenic, 14f, 13f, RoadAccess.All, RoadFeature.Ramp,
                    P(-70f, 235f, 3f), P(-45f, 270f, 4.5f), P(20f, 265f, 6f)),
                EF("Hill_Hairpin_E", "H_C", "H_E", RoadKind.Scenic, 14f, 14f, RoadAccess.All, RoadFeature.Ramp,
                    P(20f, 265f, 6f), P(72f, 275f, 5f), P(120f, 235f, 4f)),
                EF("Hill_Return", "H_E", "O_NE", RoadKind.Scenic, 14f, 15f, RoadAccess.All, RoadFeature.Ramp,
                    P(120f, 235f, 4f), P(158f, 198f, 2f), P(190f, 145f)),

                E("East_SCurve_N", "O_NE", "E_RN", RoadKind.Scenic, 15f, 18f, P(190f, 145f), P(225f, 130f), P(242f, 112f), P(270f, 100f)),
                E("East_Roundabout_N", "E_RN", "E_RE", RoadKind.Street, 16f, 14f, P(270f, 100f), P(302f, 82f), P(310f, 40f)),
                E("East_Roundabout_S", "E_RE", "E_RS", RoadKind.Street, 16f, 14f, P(310f, 40f), P(302f, 0f), P(270f, -20f)),
                E("East_Return", "E_RS", "O_E", RoadKind.Street, 15f, 17f, P(270f, -20f), P(245f, -28f), P(225f, -40f)),
                E("GasStation_CutThrough", "G_SE", "E_RS", RoadKind.Alley, 10f, 13f,
                    P(120f, -100f), P(175f, -112f), P(212f, -104f), P(238f, -58f), P(270f, -20f)),

                EF("ParkingRamp_In", "E_RN", "P_LOW", RoadKind.Alley, 11f, 11f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(270f, 100f), P(252f, 108f, 1.2f), P(235f, 85f, 2.5f)),
                EF("ParkingSwitchback_Up", "P_LOW", "P_N", RoadKind.Alley, 11f, 10f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(235f, 85f, 2.5f), P(250f, 68f, 3.75f), P(270f, 85f, 5f)),
                EF("ParkingDeck_Run", "P_N", "P_S", RoadKind.Alley, 12f, 12f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(270f, 85f, 5f), P(282f, 70f, 5f), P(295f, 55f, 5f)),
                EF("ParkingSwitchback_Out", "P_S", "E_RE", RoadKind.Alley, 11f, 10f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Rooftop | RoadFeature.Shortcut,
                    P(295f, 55f, 5f), P(325f, 58f, 2.5f), P(310f, 40f)),

                E("Industrial_W", "B_W", "I_W", RoadKind.Street, 16f, 17f, P(-220f, -165f), P(-270f, -188f), P(-250f, -235f)),
                E("Industrial_CW", "I_W", "I_C", RoadKind.Boulevard, 20f, 22f, P(-250f, -235f), P(-170f, -260f), P(-82f, -242f), P(0f, -255f)),
                E("Industrial_CE", "I_C", "I_E", RoadKind.Boulevard, 20f, 22f, P(0f, -255f), P(82f, -268f), P(170f, -245f), P(250f, -235f)),
                E("Industrial_E", "I_E", "B_E", RoadKind.Street, 16f, 17f, P(250f, -235f), P(270f, -190f), P(220f, -165f)),
                EF("Drainage_Entry", "I_W", "D_W", RoadKind.Alley, 10f, 12f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-250f, -235f), P(-220f, -252f), P(-185f, -245f)),
                EF("Drainage_Run", "D_W", "D_E", RoadKind.Alley, 10f, 15f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-185f, -245f), P(-150f, -225f), P(-108f, -265f), P(-75f, -245f)),
                EF("Drainage_Exit", "D_E", "I_C", RoadKind.Alley, 10f, 12f, RoadAccess.Player, RoadFeature.Stunt | RoadFeature.Shortcut,
                    P(-75f, -245f), P(-35f, -268f), P(0f, -255f)),

                EF("SignatureRamp_S", "O_W", "L_S", RoadKind.Boulevard, 17f, 18f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Signature,
                    P(-225f, -40f), P(-250f, -5f, 4f), P(-285f, 35f, 8f)),
                EF("Signature_SW", "L_S", "L_W", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Signature,
                    P(-285f, 35f, 8f), P(-345f, 45f, 8f), P(-405f, 95f, 8f), P(-410f, 155f, 8f)),
                EF("Signature_NW", "L_W", "L_N", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Signature,
                    P(-410f, 155f, 8f), P(-405f, 220f, 8f), P(-350f, 270f, 8f), P(-285f, 275f, 8f)),
                EF("Signature_NE", "L_N", "L_E", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Signature,
                    P(-285f, 275f, 8f), P(-220f, 270f, 8f), P(-165f, 220f, 8f), P(-155f, 155f, 8f)),
                EF("Signature_SE", "L_E", "L_S", RoadKind.Boulevard, 18f, 23f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Signature,
                    P(-155f, 155f, 8f), P(-165f, 90f, 8f), P(-220f, 40f, 8f), P(-285f, 35f, 8f)),
                EF("SignatureRamp_E", "O_NW", "L_E", RoadKind.Street, 16f, 17f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Signature,
                    P(-190f, 145f), P(-150f, 100f, 4f), P(-120f, 145f, 6f), P(-155f, 155f, 8f)),

                EF("Interchange_Link", "U_E", "J_W", RoadKind.Boulevard, 17f, 22f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Interchange,
                    P(190f, 25f, 7f), P(220f, 65f, 7.5f), P(255f, 85f, 8f)),
                EF("Interchange_NW", "J_W", "J_N", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Interchange,
                    P(255f, 85f, 8f), P(270f, 135f, 8f), P(330f, 160f, 8f)),
                EF("Interchange_NE", "J_N", "J_E", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Interchange,
                    P(330f, 160f, 8f), P(390f, 145f, 8f), P(415f, 75f, 8f)),
                EF("Interchange_SE", "J_E", "J_S", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Interchange,
                    P(415f, 75f, 8f), P(405f, 10f, 8f), P(330f, -15f, 8f)),
                EF("Interchange_SW", "J_S", "J_W", RoadKind.Boulevard, 17f, 21f, RoadAccess.All, RoadFeature.Elevated | RoadFeature.Interchange,
                    P(330f, -15f, 8f), P(270f, 15f, 8f), P(255f, 85f, 8f)),
                EF("CanalRamp_N", "O_NE", "J_N", RoadKind.Scenic, 16f, 19f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated | RoadFeature.Interchange,
                    P(190f, 145f), P(240f, 180f, 3f), P(290f, 180f, 6f), P(330f, 160f, 8f)),
                EF("Canal_North", "J_E", "C_M", RoadKind.Boulevard, 18f, 24f, RoadAccess.All, RoadFeature.Elevated,
                    P(415f, 75f, 8f), P(425f, -15f, 8f), P(415f, -105f, 8f)),
                EF("Canal_South", "C_M", "C_S", RoadKind.Boulevard, 18f, 24f, RoadAccess.All, RoadFeature.Elevated,
                    P(415f, -105f, 8f), P(420f, -175f, 8f), P(400f, -235f, 8f), P(365f, -275f, 8f)),
                EF("CanalRamp_S", "C_S", "I_E", RoadKind.Boulevard, 17f, 19f, RoadAccess.All, RoadFeature.Ramp | RoadFeature.Elevated,
                    P(365f, -275f, 8f), P(330f, -285f, 5f), P(290f, -270f, 2.5f), P(250f, -235f)),

                EF("EscapeRamp_W", "B_W", "ESC_W", RoadKind.Scenic, 15f, 18f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.EscapeRoute,
                    P(-220f, -165f), P(-290f, -175f, 1.5f), P(-365f, -185f, 3f)),
                EF("EscapeRamp_S", "ESC_W", "I_W", RoadKind.Scenic, 15f, 19f, RoadAccess.Player | RoadAccess.Police, RoadFeature.Ramp | RoadFeature.EscapeRoute,
                    P(-365f, -185f, 3f), P(-350f, -245f, 1.5f), P(-300f, -285f), P(-250f, -235f))
            });

            graph.Configure("sunset-basin", new Vector2(900f, 720f), nodes, edges);
            if (!graph.IsValid(out var error)) throw new InvalidOperationException($"Road graph is invalid: {error}");
            EditorUtility.SetDirty(graph);
            return graph;
        }

        private static RoadGraphNode N(string id, float x, float z, RoadNodeRole role = RoadNodeRole.Junction) => new(id, P(x, z), role);

        private static RoadGraphNode N(string id, float x, float z, float y, RoadNodeRole role = RoadNodeRole.Junction) => new(id, P(x, z, y), role);

        private static RoadGraphEdge E(string id, string from, string to, RoadKind kind, float width, float speed, params Vector3[] points) =>
            new(id, from, to, kind, width, speed, points);

        private static RoadGraphEdge EF(string id, string from, string to, RoadKind kind, float width, float speed, RoadAccess access, RoadFeature features, params Vector3[] points)
        {
            var edge = new RoadGraphEdge(id, from, to, kind, width, speed, points)
            {
                Access = access,
                Features = features
            };
            return edge;
        }

        private static void RoundRoadCenterlines(IEnumerable<RoadGraphEdge> edges)
        {
            foreach (var edge in edges)
            {
                if (edge.Centerline == null || edge.Centerline.Count < 3) continue;
                var controls = edge.Centerline;
                var rounded = new List<Vector3> { controls[0] };
                for (var point = 1; point < controls.Count - 1; point++)
                {
                    var previous = controls[point - 1];
                    var corner = controls[point];
                    var next = controls[point + 1];
                    var incoming = corner - previous;
                    var outgoing = next - corner;
                    var incomingLength = incoming.magnitude;
                    var outgoingLength = outgoing.magnitude;
                    if (incomingLength < 0.1f || outgoingLength < 0.1f) continue;

                    var trim = Mathf.Min(edge.Width * 1.6f, incomingLength * 0.42f, outgoingLength * 0.42f);
                    var entry = corner - incoming / incomingLength * trim;
                    var exit = corner + outgoing / outgoingLength * trim;
                    rounded.Add(entry);
                    const int curveSteps = 6;
                    for (var step = 1; step < curveSteps; step++)
                    {
                        var t = step / (float)curveSteps;
                        var a = Vector3.Lerp(entry, corner, t);
                        var b = Vector3.Lerp(corner, exit, t);
                        rounded.Add(Vector3.Lerp(a, b, t));
                    }
                    rounded.Add(exit);
                }
                rounded.Add(controls[^1]);
                edge.Centerline = rounded;
            }
        }

        private static void WidenRoads(IEnumerable<RoadGraphEdge> edges)
        {
            foreach (var edge in edges)
            {
                if ((edge.Features & RoadFeature.Rooftop) != 0)
                {
                    edge.Width = Mathf.Max(edge.Width, 13f);
                    continue;
                }

                var addedWidth = edge.Kind switch
                {
                    RoadKind.Boulevard => 4f,
                    RoadKind.Street => 4f,
                    RoadKind.Scenic => 4f,
                    RoadKind.Alley => 2.5f,
                    _ => 3f
                };
                edge.Width = Mathf.Min(28f, edge.Width + addedWidth);
            }
        }

        private static Vector3 P(float x, float z) => new(x, 0f, z);
        private static Vector3 P(float x, float z, float y) => new(x, y, z);

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
                RouteMain = EnsureMaterial(Root + "/Materials/RouteMain.mat", shader, new Color(0.95f, 0.08f, 0.42f)),
                RouteElevated = EnsureMaterial(Root + "/Materials/RouteElevated.mat", shader, new Color(0.08f, 0.46f, 1f)),
                RouteShortcut = EnsureMaterial(Root + "/Materials/RouteShortcut.mat", shader, new Color(1f, 0.72f, 0.05f)),
                RouteEscape = EnsureMaterial(Root + "/Materials/RouteEscape.mat", shader, new Color(0.08f, 0.72f, 0.24f)),
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
            input.Configure(
                profiles.Vehicle.inputResponse,
                profiles.Vehicle.initialSteeringStrength,
                profiles.Vehicle.fullDriftHoldDuration);
            var controller = root.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profiles.Vehicle, (1 << 0) | (1 << 7), new Vector3(0f, -0.35f, 0f));
            root.AddComponent<VehicleDriftEffects>().Configure(controller, visual.transform);
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
            navigator.Configure(
                body,
                roadGraph,
                profiles.Police,
                roadGraph.GraphId == "portrait-drift-run"
                    ? new[] { "COURSE_02", "COURSE_04", "COURSE_06", "COURSE_01" }
                    : null);
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
            // Start in the broad south boulevard lane. The old west-biased
            // position put the overhead camera against the warehouse massing,
            // which could hide the car on the first frame.
            car.transform.SetPositionAndRotation(new Vector3(0f, 0.12f, -330f), Quaternion.identity);
            var controller = car.GetComponent<VehicleController>();
            var input = car.GetComponent<VehicleInputSource>();
            var recovery = car.GetComponent<VehicleRecovery>();
            var collisionSensor = car.GetComponent<VehicleCollisionSensor>();

            var policePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PolicePrefabPath);
            var policeVehicles = new PoliceVehicleController[3];
            var policeVisions = new PoliceVisionSensor[3];
            var portraitCourse = roadGraph.GraphId == "portrait-drift-run";
            var policePositions = portraitCourse
                ? new[] { new Vector3(-90f, 0.65f, -115f), new Vector3(90f, 0.65f, 105f), new Vector3(-65f, 0.65f, 290f) }
                : new[] { new Vector3(-245f, 0.65f, 10f), new Vector3(155f, 0.65f, -70f), new Vector3(315f, 0.65f, -270f) };
            var policeRotations = portraitCourse ? new[] { 0f, 0f, 0f } : new[] { 90f, 180f, -90f };
            for (var i = 0; i < policeVehicles.Length; i++)
            {
                var policeCar = (GameObject)PrefabUtility.InstantiatePrefab(policePrefab, scene);
                policeCar.name = i == 0 ? "PoliceCar" : $"PoliceCar_{i + 1:00}";
                policeCar.transform.SetPositionAndRotation(policePositions[i], Quaternion.Euler(0f, policeRotations[i], 0f));
                policeVehicles[i] = policeCar.GetComponent<PoliceVehicleController>();
                policeVisions[i] = policeCar.GetComponent<PoliceVisionSensor>();
                policeVisions[i].Configure(car.transform, profiles.Police, 1 << 8);
                policeVehicles[i].SetOperational(i == 0);
            }

            var cameraObject = new GameObject("ChaseCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            cameraObject.AddComponent<AudioListener>();
            var chase = cameraObject.AddComponent<ChaseCameraController>();
            chase.Configure(car.transform, car.GetComponent<Rigidbody>(), profiles.Camera, 1 << 8);
            var initialCameraPosition = car.transform.position - car.transform.forward * profiles.Camera.overheadBackOffset + Vector3.up * profiles.Camera.overheadHeight;
            var initialLookPoint = car.transform.position + Vector3.up * profiles.Camera.lookHeight + car.transform.forward * profiles.Camera.overheadLookAheadDistance;
            cameraObject.transform.SetPositionAndRotation(initialCameraPosition, Quaternion.LookRotation(initialLookPoint - initialCameraPosition, Vector3.up));

            var scoring = systems.AddComponent<DriftScoreController>();
            scoring.Configure(controller, collisionSensor, profiles.Scoring);
            var stunts = systems.AddComponent<StuntController>();
            stunts.Configure(controller, scoring, profiles.Stunts);
            var recklessDriving = systems.AddComponent<RecklessDrivingTracker>();
            recklessDriving.Configure(controller, collisionSensor, profiles.Police);
            var policeDirector = systems.AddComponent<PoliceDirector>();
            var roadblock = BuildPoliceRoadblock(environment.transform, materials, physicsMaterial);
            policeDirector.Configure(car.transform, controller, recovery, policeVehicles, policeVisions, recklessDriving, profiles.Police, scoring, roadblock);
            var trafficScore = systems.AddComponent<TrafficScoreController>();
            trafficScore.Configure(scoring);
            var challengeController = systems.AddComponent<ChallengeController>();
            challengeController.Configure(profiles.Challenges, scoring);
            var resources = systems.AddComponent<RideResourceController>();
            resources.Configure(controller, scoring, profiles.Fuel);
            BuildCollectibles(environment.transform, roadGraph, resources, profiles.Fuel, materials);
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
            if (roadGraph.GraphId == "portrait-drift-run")
            {
                BuildPortraitDriftEnvironment(parent, roadGraph, materials, physicsMaterial);
                return;
            }

            CreateBox("Ground", parent, new Vector3(0f, -1.11f, 0f), new Vector3(1400f, 2f, 1150f), materials.Terrain, physicsMaterial, 0);

            var roadsObject = new GameObject("Roads");
            roadsObject.transform.SetParent(parent, false);
            roadsObject.AddComponent<RoadGraphComponent>().Configure(roadGraph);
            var roadFurniture = new GameObject("RoadFurniture").transform;
            roadFurniture.SetParent(parent, false);
            for (var edgeIndex = 0; edgeIndex < roadGraph.Edges.Count; edgeIndex++)
            {
                var edge = roadGraph.Edges[edgeIndex];
                var edgeRoot = new GameObject(edge.Id).transform;
                edgeRoot.SetParent(roadsObject.transform, false);
                var requiresDriveSurface = false;
                for (var point = 0; point < edge.Centerline.Count; point++)
                {
                    if (edge.Centerline[point].y <= 0.05f) continue;
                    requiresDriveSurface = true;
                    break;
                }
                if (requiresDriveSurface)
                {
                    CreateRoadRibbon(edge, edgeRoot, materials.Road, physicsMaterial);
                    CreateRouteGuardrails(edge, roadFurniture, materials.Barrier, physicsMaterial);
                }
                else
                {
                    var roadVertices = new List<Vector3>();
                    var roadTriangles = new List<int>();
                    for (var point = 1; point < edge.Centerline.Count; point++)
                    {
                        AppendRoadBox(roadVertices, roadTriangles, edge.Centerline[point - 1], edge.Centerline[point], edge.Width, 0.2f, 0.1f, 0.8f);
                    }
                    var roadVisual = CreateCombinedBoxVisual($"{edge.Id}_RoadVisual", edgeRoot, materials.Road, roadVertices, roadTriangles);
                    roadVisual.layer = 7;
                    var roadTrigger = roadVisual.AddComponent<MeshCollider>();
                    roadTrigger.sharedMesh = roadVisual.GetComponent<MeshFilter>().sharedMesh;
                    roadTrigger.convex = true;
                    roadTrigger.isTrigger = true;
                }
            }
            CreateGroundRoadBoundaries(roadGraph, roadFurniture, materials.Barrier, physicsMaterial);
            CreateCombinedRoadMarkers(roadGraph, roadsObject.transform, materials.Marking);
            CreateRouteDiagramMarkers(roadGraph, roadsObject.transform, materials);

            var signs = new GameObject("RoadSigns").transform;
            signs.SetParent(parent, false);
            CreateRoadSign("Sign_01", signs, new Vector3(-330f, 0f, -5f), 20f, materials, physicsMaterial);
            CreateRoadSign("Sign_02", signs, new Vector3(-300f, 0f, -35f), 25f, materials, physicsMaterial);
            CreateRoadSign("Sign_03", signs, new Vector3(-280f, 0f, -70f), 45f, materials, physicsMaterial);
            CreateRoadSign("Sign_04", signs, new Vector3(-245f, 0f, -105f), 80f, materials, physicsMaterial);
            CreateRoadSign("Sign_05", signs, new Vector3(-210f, 0f, -135f), 60f, materials, physicsMaterial);
            CreateRoadSign("Sign_06", signs, new Vector3(-175f, 0f, -175f), 35f, materials, physicsMaterial);
            CreateRoadSign("Sign_07", signs, new Vector3(-95f, 0f, -82f), 0f, materials, physicsMaterial);
            CreateRoadSign("Sign_08", signs, new Vector3(80f, 0f, -92f), 180f, materials, physicsMaterial);
            CreateRoadSign("Sign_09", signs, new Vector3(150f, 0f, -220f), 25f, materials, physicsMaterial);
            CreateRoadSign("Sign_10", signs, new Vector3(60f, 0f, -245f), 180f, materials, physicsMaterial);
            CreateRoadSign("Sign_11", signs, new Vector3(-65f, 0f, 35f), 90f, materials, physicsMaterial);
            CreateRoadSign("Sign_12", signs, new Vector3(155f, 0f, -58f), -90f, materials, physicsMaterial);
            CreateRoadSign("Sign_13", signs, new Vector3(-355f, 0f, -275f), 10f, materials, physicsMaterial);
            CreateRoadSign("Sign_14", signs, new Vector3(-405f, 0f, -235f), 10f, materials, physicsMaterial);
            CreateRoadSign("Sign_15", signs, new Vector3(-385f, 0f, -135f), 70f, materials, physicsMaterial);
            CreateRoadSign("Sign_16", signs, new Vector3(390f, 0f, -205f), -8f, materials, physicsMaterial);

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(parent, false);
            CreateBuilding("Downtown_NE_Tower", buildings, P(82f, 285f), new Vector2(48f, 54f), 38f, materials, physicsMaterial);
            CreateBuilding("Downtown_SW_Block", buildings, P(-165f, -125f), new Vector2(58f, 48f), 25f, materials, physicsMaterial);
            CreateBuilding("Downtown_NW_West", buildings, P(-78f, 110f), new Vector2(36f, 44f), 19f, materials, physicsMaterial);
            CreateBuilding("Downtown_NW_South", buildings, P(-125f, 70f), new Vector2(34f, 28f), 16f, materials, physicsMaterial);
            CreateBuilding("Downtown_SE_East", buildings, P(112f, -132f), new Vector2(34f, 42f), 22f, materials, physicsMaterial);
            CreateBuilding("Civic_West", buildings, P(-305f, -95f), new Vector2(46f, 54f), 17f, materials, physicsMaterial);
            CreateBuilding("Civic_East", buildings, P(250f, 145f), new Vector2(38f, 46f), 21f, materials, physicsMaterial);
            CreateBuilding("Commercial_West", buildings, P(-205f, -245f), new Vector2(48f, 38f), 13f, materials, physicsMaterial);
            CreateBuilding("Commercial_East", buildings, P(270f, -205f), new Vector2(46f, 34f), 15f, materials, physicsMaterial);
            CreateDriveableBuilding("Warehouse_West", buildings, P(-55f, -20f), new Vector2(72f, 34f), 5.3f, true, materials, physicsMaterial);
            CreateDriveableBuilding("Warehouse_East", buildings, P(42f, -45f), new Vector2(68f, 34f), 5.3f, false, materials, physicsMaterial);
            CreateBuilding("Warehouse_FarWest", buildings, P(-275f, -215f), new Vector2(44f, 26f), 9f, materials, physicsMaterial);
            CreateBuilding("Warehouse_FarEast", buildings, P(285f, -105f), new Vector2(44f, 28f), 10f, materials, physicsMaterial);
            CreateBuilding("Hillside_East", buildings, P(150f, 295f), new Vector2(44f, 30f), 14f, materials, physicsMaterial);

            var landmarks = new GameObject("Landmarks").transform;
            landmarks.SetParent(parent, false);
            CreateBuilding("SunsetHotel", landmarks, P(-70f, 285f), new Vector2(30f, 28f), 30f, materials, physicsMaterial, materials.Accent);
            CreateBuilding("BoulevardTheater", landmarks, P(90f, -270f), new Vector2(46f, 24f), 15f, materials, physicsMaterial, materials.Accent);
            CreateVisualBox("BeachOverlook", landmarks, new Vector3(-430f, 0.45f, -100f), new Vector3(10f, 0.8f, 34f), materials.Concrete);
            CreateSignatureTower(landmarks, materials, physicsMaterial);

            var infrastructure = new GameObject("CityInfrastructure").transform;
            infrastructure.SetParent(parent, false);
            BuildCityInfrastructure(infrastructure, materials, physicsMaterial);
            BuildStuntZones(infrastructure, materials, physicsMaterial);

            var boundaries = new GameObject("BoundaryFiction").transform;
            boundaries.SetParent(parent, false);
            CreateVisualBox("PacificWater", boundaries, new Vector3(-585f, 0f, 0f), new Vector3(270f, 0.2f, 1150f), materials.Water);
            CreateBox("OceanSeawall", boundaries, new Vector3(-450f, 2.5f, 0f), new Vector3(8f, 5f, 760f), materials.Concrete, physicsMaterial, 8);
            CreateVisualBox("EastCanalWater", boundaries, new Vector3(520f, -0.15f, -55f), new Vector3(140f, 0.2f, 760f), materials.Water);
            CreateBox("EastCanalWall", boundaries, new Vector3(450f, 3f, -25f), new Vector3(8f, 6f, 730f), materials.Concrete, physicsMaterial, 8);
            CreateBox("SouthRailWall", boundaries, new Vector3(0f, 3f, -370f), new Vector3(900f, 6f, 8f), materials.Concrete, physicsMaterial, 8);
            CreateBox("NorthHill_West", boundaries, new Vector3(-285f, 24f, 410f), new Vector3(330f, 48f, 88f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, -8f, -8f);
            CreateBox("NorthHill_Center", boundaries, new Vector3(0f, 32f, 420f), new Vector3(260f, 64f, 96f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, 0f, 5f);
            CreateBox("NorthHill_East", boundaries, new Vector3(285f, 26f, 410f), new Vector3(330f, 52f, 88f), materials.Terrain, physicsMaterial, 8).transform.rotation = Quaternion.Euler(0f, 8f, 8f);

            for (var x = -350f; x <= 350f; x += 100f)
            {
                CreateBuilding($"SouthWarehouse_{x:0}", boundaries, P(x, -415f), new Vector2(76f, 42f), 18f + Mathf.Abs(x) * 0.018f, materials, physicsMaterial);
            }
        }

        private static void BuildPortraitDriftEnvironment(Transform parent, RoadGraphAsset roadGraph, Materials materials, PhysicsMaterial physicsMaterial)
        {
            CreateBox("Ground", parent, new Vector3(0f, -1.11f, 5f), new Vector3(520f, 2f, 850f), materials.Terrain, physicsMaterial, 0);

            var roadsObject = new GameObject("Roads");
            roadsObject.transform.SetParent(parent, false);
            roadsObject.AddComponent<RoadGraphComponent>().Configure(roadGraph);
            var roadFurniture = new GameObject("RoadFurniture").transform;
            roadFurniture.SetParent(parent, false);
            for (var edgeIndex = 0; edgeIndex < roadGraph.Edges.Count; edgeIndex++)
            {
                var edge = roadGraph.Edges[edgeIndex];
                var edgeRoot = new GameObject(edge.Id).transform;
                edgeRoot.SetParent(roadsObject.transform, false);
                var roadVertices = new List<Vector3>();
                var roadTriangles = new List<int>();
                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    AppendRoadBox(roadVertices, roadTriangles, edge.Centerline[point - 1], edge.Centerline[point], edge.Width, 0.2f, 0.1f, 0.8f);
                }
                var roadVisual = CreateCombinedBoxVisual($"{edge.Id}_RoadVisual", edgeRoot, materials.Road, roadVertices, roadTriangles);
                roadVisual.layer = 7;
                var roadTrigger = roadVisual.AddComponent<MeshCollider>();
                roadTrigger.sharedMesh = roadVisual.GetComponent<MeshFilter>().sharedMesh;
                roadTrigger.convex = true;
                roadTrigger.isTrigger = true;
            }
            CreateGroundRoadBoundaries(roadGraph, roadFurniture, materials.Barrier, physicsMaterial);
            CreateCombinedRoadMarkers(roadGraph, roadsObject.transform, materials.Marking);

            var signs = new GameObject("RoadSigns").transform;
            signs.SetParent(parent, false);
            var signPositions = new[]
            {
                new Vector3(-38f, 0f, -285f), new Vector3(35f, 0f, -235f), new Vector3(-125f, 0f, -185f),
                new Vector3(-128f, 0f, -105f), new Vector3(-122f, 0f, -35f), new Vector3(118f, 0f, 25f),
                new Vector3(132f, 0f, 95f), new Vector3(128f, 0f, 155f), new Vector3(45f, 0f, 215f),
                new Vector3(-115f, 0f, 250f), new Vector3(-92f, 0f, 310f), new Vector3(34f, 0f, 340f)
            };
            for (var i = 0; i < signPositions.Length; i++)
            {
                CreateRoadSign($"Sign_{i + 1:00}", signs, signPositions[i], i % 2 == 0 ? 0f : 180f, materials, physicsMaterial);
            }

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(parent, false);
            for (var row = 0; row < 6; row++)
            {
                var z = -285f + row * 120f;
                CreateBuilding($"CourseWest_{row:00}", buildings, P(-178f, z), new Vector2(38f, 54f), 10f + row * 2f, materials, physicsMaterial);
                CreateBuilding($"CourseEast_{row:00}", buildings, P(178f, z + 40f), new Vector2(42f, 58f), 16f - row, materials, physicsMaterial);
            }

            var landmarks = new GameObject("Landmarks").transform;
            landmarks.SetParent(parent, false);
            CreateBuilding("DriftRunTower", landmarks, P(165f, 315f), new Vector2(34f, 34f), 32f, materials, physicsMaterial, materials.Accent);
            CreateVisualBox("SouthStartPylon", landmarks, new Vector3(-42f, 3f, -330f), new Vector3(3f, 6f, 3f), materials.Accent);
            CreateVisualBox("NorthFinishPylon", landmarks, new Vector3(42f, 3f, 350f), new Vector3(3f, 6f, 3f), materials.Accent);

            var infrastructure = new GameObject("CityInfrastructure").transform;
            infrastructure.SetParent(parent, false);
            CreateStartFinishMarker(infrastructure, materials);
            CreateVisualBox("FinishLine", infrastructure, new Vector3(0f, 0.16f, 350f), new Vector3(20f, 0.04f, 2f), materials.Marking);

            var boundaries = new GameObject("BoundaryFiction").transform;
            boundaries.SetParent(parent, false);
            CreateBox("WestCourseWall", boundaries, new Vector3(-245f, 2.5f, 5f), new Vector3(8f, 5f, 850f), materials.Concrete, physicsMaterial, 8);
            CreateBox("EastCourseWall", boundaries, new Vector3(245f, 2.5f, 5f), new Vector3(8f, 5f, 850f), materials.Concrete, physicsMaterial, 8);
            CreateBox("SouthCourseWall", boundaries, new Vector3(0f, 2.5f, -415f), new Vector3(500f, 5f, 8f), materials.Concrete, physicsMaterial, 8);
            CreateBox("NorthCourseWall", boundaries, new Vector3(0f, 2.5f, 425f), new Vector3(500f, 5f, 8f), materials.Concrete, physicsMaterial, 8);
        }

        private static PoliceRoadblockController BuildPoliceRoadblock(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var root = new GameObject("PoliceRoadblock");
            root.transform.SetParent(parent, false);
            var left = CreateBox("BarrierLeft", root.transform, new Vector3(-6.5f, 0.55f, -100f), new Vector3(7f, 1.1f, 1.4f), materials.Police, physicsMaterial, 8);
            var right = CreateBox("BarrierRight", root.transform, new Vector3(6.5f, 0.55f, -100f), new Vector3(7f, 1.1f, 1.4f), materials.Police, physicsMaterial, 8);
            CreateVisualBox("LeftLights", left.transform, left.transform.position + Vector3.up * 0.75f, new Vector3(5.5f, 0.18f, 0.3f), materials.PoliceLight);
            CreateVisualBox("RightLights", right.transform, right.transform.position + Vector3.up * 0.75f, new Vector3(5.5f, 0.18f, 0.3f), materials.PoliceLight);
            var controller = root.AddComponent<PoliceRoadblockController>();
            controller.Configure(root);
            return controller;
        }

        private static void BuildCityInfrastructure(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var supportPositions = new[]
            {
                new Vector3(-105f, 3.75f, 190f), new Vector3(-35f, 3.75f, 205f),
                new Vector3(35f, 3.75f, 180f), new Vector3(105f, 3.75f, 155f),
                new Vector3(180f, 3.75f, 175f), new Vector3(225f, 3.75f, 210f),
                new Vector3(-405f, 3.75f, 190f), new Vector3(-340f, 3.75f, 300f),
                new Vector3(-180f, 3.75f, 300f), new Vector3(-120f, 3.75f, 125f),
                new Vector3(285f, 3.75f, 235f), new Vector3(375f, 3.75f, 155f),
                new Vector3(415f, 3.75f, 15f), new Vector3(390f, 3.75f, -170f),
                new Vector3(300f, 3.75f, -260f)
            };
            var supportVertices = new List<Vector3>();
            var supportTriangles = new List<int>();
            for (var i = 0; i < supportPositions.Length; i++)
            {
                AppendBoxMesh(supportVertices, supportTriangles, supportPositions[i], Quaternion.identity, new Vector3(2.2f, 6.5f, 2.2f));
            }
            CreateCombinedSolidVisual("OverpassSupports", parent, materials.Concrete, physicsMaterial, supportVertices, supportTriangles);

            CreateStartFinishMarker(parent, materials);

            CreateVisualBox("ParkingDeck", parent, new Vector3(20f, 2.5f, -35f), new Vector3(62f, 5f, 54f), materials.Concrete);
            CreateVisualBox("ParkingDeckTop", parent, new Vector3(20f, 5.25f, -35f), new Vector3(64f, 0.5f, 56f), materials.Accent);

            CreateVisualBox("GasStationCanopy", parent, new Vector3(155f, 4.2f, -165f), new Vector3(34f, 0.7f, 22f), materials.Accent);
            CreateVisualBox("GasStationPost_W", parent, new Vector3(143f, 2f, -165f), new Vector3(0.8f, 4f, 0.8f), materials.Concrete);
            CreateVisualBox("GasStationPost_E", parent, new Vector3(167f, 2f, -165f), new Vector3(0.8f, 4f, 0.8f), materials.Concrete);

            var medianVertices = new List<Vector3>();
            var medianTriangles = new List<int>();
            for (var i = 0; i < 4; i++)
            {
                AppendBoxMesh(medianVertices, medianTriangles, new Vector3(-315f + i * 70f, 0.35f, -320f + (i % 2 == 0 ? 7f : -7f)), Quaternion.identity, new Vector3(22f, 0.7f, 1.6f));
            }
            CreateCombinedBoxVisual("IndustrialMedians", parent, materials.Barrier, medianVertices, medianTriangles);

            var shelterPositions = new[] { new Vector3(-185f, 1.4f, -92f), new Vector3(145f, 1.4f, -78f), new Vector3(18f, 1.4f, -240f) };
            for (var i = 0; i < shelterPositions.Length; i++)
            {
                CreateVisualBox($"BusShelter_{i + 1:00}", parent, shelterPositions[i], new Vector3(7f, 2.8f, 1f), materials.Accent);
            }

            for (var row = 0; row < 2; row++)
            {
                var containerVertices = new List<Vector3>();
                var containerTriangles = new List<int>();
                for (var column = 0; column < 5; column++)
                {
                    var height = 2.4f + (column % 3) * 1.2f;
                    AppendBoxMesh(containerVertices, containerTriangles, new Vector3(-275f + column * 13f, height * 0.5f, -345f + row * 10f), Quaternion.identity, new Vector3(10f, height, 4.5f));
                }
                CreateCombinedSolidVisual($"ContainerRow_{row}", parent, row == 0 ? materials.Barrier : materials.Accent, physicsMaterial, containerVertices, containerTriangles);
            }
        }

        private static void CreateStartFinishMarker(Transform parent, Materials materials)
        {
            var root = new GameObject("StartFinish").transform;
            root.SetParent(parent, false);
            var lightVertices = new List<Vector3>();
            var lightTriangles = new List<int>();
            var darkVertices = new List<Vector3>();
            var darkTriangles = new List<int>();
            const float segmentWidth = 1.5f;
            const int segmentCount = 12;
            for (var i = 0; i < segmentCount; i++)
            {
                var x = (i - (segmentCount - 1) * 0.5f) * segmentWidth;
                var vertices = i % 2 == 0 ? lightVertices : darkVertices;
                var triangles = i % 2 == 0 ? lightTriangles : darkTriangles;
                AppendBoxMesh(vertices, triangles, new Vector3(x, 0.16f, -330f), Quaternion.identity, new Vector3(segmentWidth, 0.04f, 1.8f));
            }
            CreateCombinedBoxVisual("StartFinishLight", root, materials.Concrete, lightVertices, lightTriangles);
            CreateCombinedBoxVisual("StartFinishDark", root, materials.Road, darkVertices, darkTriangles);
        }

        private static void BuildStuntZones(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            CreateStuntLine("RailYard", parent, -105f, -345f, -320f, -307f, 4.5f, -284f, 3f, -262f, -235f, 10f, 14f, materials, physicsMaterial);
            CreateStuntLine("Container", parent, 115f, -345f, -320f, -307f, 4.5f, -284f, 3f, -262f, -235f, 9f, 13f, materials, physicsMaterial);
            CreateElevatedStuntTransfer(parent, materials, physicsMaterial);
        }

        private static void CreateElevatedStuntTransfer(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var line = new GameObject("ExpresswayTransferStuntLine").transform;
            line.SetParent(parent, false);

            var runUpStart = new Vector3(-105f, 8f, 190f);
            var launchStart = new Vector3(-65f, 8f, 195f);
            var launchLip = new Vector3(-57f, 10.9f, 194f);
            var landingHigh = new Vector3(-28f, 10.4f, 188f);
            var landingLow = new Vector3(-12f, 8f, 184f);
            var runoutEnd = new Vector3(35f, 8f, 180f);
            const float roadWidth = 12f;

            CreateRampSurface("ExpresswayTransferRunUp", line, runUpStart, launchStart, roadWidth, materials.Road, physicsMaterial);
            CreateRoadVolume("ExpresswayTransferRunUp_Road", line, runUpStart, launchStart, roadWidth);
            CreateStuntRamp("ExpresswayTransferLaunch", line, launchStart, launchLip, roadWidth, materials, physicsMaterial, true);
            CreateLandingRamp("ExpresswayTransferLanding", line, landingLow, landingHigh, 15f, materials, physicsMaterial, true);
            CreateRampSurface("ExpresswayTransferRunout", line, landingLow, runoutEnd, 15f, materials.Road, physicsMaterial);
            CreateRoadVolume("ExpresswayTransferRunout_Road", line, landingLow, runoutEnd, 15f);

            var travel = Vector3.ProjectOnPlane(runoutEnd - runUpStart, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, travel) * 18f;
            var bailoutStart = runUpStart + side;
            var bailoutEnd = landingLow + side;
            CreateRampSurface("ExpresswayTransferBailout", line, bailoutStart, bailoutEnd, 7f, materials.Road, physicsMaterial);
            CreateRoadVolume("ExpresswayTransferBailout_Road", line, bailoutStart, bailoutEnd, 7f);
            CreateRampSurface("ExpresswayTransferBailoutEntry", line, runUpStart, bailoutStart, 7f, materials.Road, physicsMaterial);
            CreateRoadVolume("ExpresswayTransferBailoutEntry_Road", line, runUpStart, bailoutStart, 7f);
            CreateRampSurface("ExpresswayTransferBailoutExit", line, bailoutEnd, landingLow, 7f, materials.Road, physicsMaterial);
            CreateRoadVolume("ExpresswayTransferBailoutExit_Road", line, bailoutEnd, landingLow, 7f);
        }

        private static void CreateStuntLine(
            string name,
            Transform parent,
            float x,
            float runUpStartZ,
            float launchStartZ,
            float launchLipZ,
            float launchHeight,
            float landingHighZ,
            float landingHeight,
            float landingLowZ,
            float exitZ,
            float launchWidth,
            float landingWidth,
            Materials materials,
            PhysicsMaterial physicsMaterial)
        {
            var line = new GameObject($"{name}StuntLine").transform;
            line.SetParent(parent, false);

            CreateGroundStuntStrip($"{name}RunUp", line, new Vector3(x, 0f, runUpStartZ), new Vector3(x, 0f, launchStartZ), launchWidth + 2f);
            CreateStuntRamp($"{name}Launch", line, new Vector3(x, 0f, launchStartZ), new Vector3(x, launchHeight, launchLipZ), launchWidth, materials, physicsMaterial);
            CreateLandingRamp($"{name}Landing", line, new Vector3(x, 0f, landingLowZ), new Vector3(x, landingHeight, landingHighZ), landingWidth, materials, physicsMaterial);
            CreateGroundStuntStrip($"{name}Runout", line, new Vector3(x, 0f, landingLowZ), new Vector3(x, 0f, exitZ), landingWidth);

            var bailoutX = x + landingWidth * 0.95f;
            CreateGroundStuntStrip($"{name}Bailout", line, new Vector3(bailoutX, 0f, launchStartZ), new Vector3(bailoutX, 0f, exitZ), 6f);
            var roadVolumeX = (x + bailoutX) * 0.5f;
            CreateRoadVolume($"{name}RoadVolume", line, new Vector3(roadVolumeX, 0f, runUpStartZ), new Vector3(roadVolumeX, 0f, exitZ), bailoutX - x + launchWidth + 2f);

            // Consolidate the stunt visuals into three renderers per line. The
            // authored colliders remain separate, while the visual mesh stays
            // comfortably inside the scene's mobile renderer budget.
            var roadVertices = new List<Vector3>();
            var roadTriangles = new List<int>();
            AppendRoadBox(roadVertices, roadTriangles, new Vector3(x, 0f, runUpStartZ), new Vector3(x, 0f, launchStartZ), launchWidth + 2f, 0.2f, 0.1f, 0.8f);
            AppendRampBox(roadVertices, roadTriangles, new Vector3(x, 0f, landingLowZ), new Vector3(x, landingHeight, landingHighZ), landingWidth, 0.5f);
            AppendRoadBox(roadVertices, roadTriangles, new Vector3(x, 0f, landingLowZ), new Vector3(x, 0f, exitZ), landingWidth, 0.2f, 0.1f, 0.8f);
            CreateCombinedBoxVisual($"{name}RoadVisual", line, materials.Road, roadVertices, roadTriangles);

            var launchVertices = new List<Vector3>();
            var launchTriangles = new List<int>();
            AppendRampBox(launchVertices, launchTriangles, new Vector3(x, 0f, launchStartZ), new Vector3(x, launchHeight, launchLipZ), launchWidth, 0.5f);
            CreateCombinedBoxVisual($"{name}LaunchVisual", line, materials.Accent, launchVertices, launchTriangles);

            var markingVertices = new List<Vector3>();
            var markingTriangles = new List<int>();
            AppendRoadBox(markingVertices, markingTriangles, new Vector3(bailoutX, 0f, launchStartZ), new Vector3(bailoutX, 0f, exitZ), 6f, 0.2f, 0.1f, 0.8f);
            AppendBoxMesh(markingVertices, markingTriangles, new Vector3(x, launchHeight + 0.04f, launchLipZ - 0.35f), Quaternion.identity, new Vector3(launchWidth, 0.08f, 0.7f));
            AppendBoxMesh(markingVertices, markingTriangles, new Vector3(x, landingHeight + 0.06f, landingHighZ + 0.45f), Quaternion.identity, new Vector3(landingWidth - 1f, 0.1f, 0.9f));
            var markerSide = name == "RailYard" ? -1f : 1f;
            AppendBoxMesh(markingVertices, markingTriangles, new Vector3(x + markerSide * 18f, 5.5f, (launchLipZ + landingHighZ) * 0.5f), Quaternion.identity, new Vector3(0.5f, 5f, 12f));
            CreateCombinedBoxVisual($"{name}MarkingVisual", line, materials.Marking, markingVertices, markingTriangles);
        }

        private static void CreateStuntRamp(string name, Transform parent, Vector3 start, Vector3 end, float width, Materials materials, PhysicsMaterial physicsMaterial, bool keepRenderer = false)
        {
            var surface = CreateRampSurface(name, parent, start, end, width, materials.Accent, physicsMaterial);
            if (!keepRenderer) Object.DestroyImmediate(surface.GetComponent<Renderer>());
            var trigger = CreateRoadVolume($"{name}_Trigger", parent, start, end, width);
            trigger.AddComponent<StuntRamp>().Configure(name);
        }

        private static void CreateLandingRamp(string name, Transform parent, Vector3 start, Vector3 end, float width, Materials materials, PhysicsMaterial physicsMaterial, bool keepRenderer = false)
        {
            var surface = CreateRampSurface(name, parent, start, end, width, materials.Road, physicsMaterial);
            if (!keepRenderer) Object.DestroyImmediate(surface.GetComponent<Renderer>());
            CreateRoadVolume($"{name}_Road", parent, start, end, width);
        }

        private static void CreateGroundStuntStrip(string name, Transform parent, Vector3 start, Vector3 end, float width)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            var delta = end - start;
            marker.transform.SetPositionAndRotation((start + end) * 0.5f, Quaternion.LookRotation(delta.normalized, Vector3.up));
            marker.transform.localScale = new Vector3(width, 0.01f, delta.magnitude);
        }

        private static GameObject CreateRampSurface(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material, PhysicsMaterial physicsMaterial)
        {
            var delta = end - start;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            var roadUp = rotation * Vector3.up;
            var ramp = CreateBox(name, parent, (start + end) * 0.5f - roadUp * 0.25f, new Vector3(width, 0.5f, delta.magnitude), material, physicsMaterial, 0);
            ramp.transform.rotation = rotation;
            return ramp;
        }

        private static void BuildCollectibles(Transform environment, RoadGraphAsset roadGraph, RideResourceController resources, FuelProfile profile, Materials materials)
        {
            var root = new GameObject("Collectibles").transform;
            root.SetParent(environment, false);

            var portraitCourse = roadGraph.GraphId == "portrait-drift-run";
            var coins = portraitCourse
                ? new[]
                {
                    P(0f, -305f), P(0f, -275f), P(-12f, -235f), P(-42f, -205f), P(-72f, -175f), P(-94f, -140f),
                    P(-102f, -100f), P(-92f, -62f), P(-62f, -28f), P(-20f, 2f), P(25f, 30f), P(68f, 53f),
                    P(96f, 82f), P(102f, 120f), P(90f, 162f), P(62f, 195f), P(25f, 222f), P(-15f, 242f),
                    P(-52f, 262f), P(-70f, 282f), P(-45f, 308f), P(-15f, 334f)
                }
                : new[]
                {
                    P(-150f, -95f), P(-115f, -75f), P(-80f, -65f), P(-15f, -55f), P(35f, -65f), P(85f, -90f),
                    P(175f, -110f), P(190f, -145f), P(205f, -180f), P(150f, -220f), P(100f, -235f), P(50f, -245f),
                    P(-55f, 35f), P(-115f, 15f), P(-175f, 30f), P(-245f, 10f), P(-275f, -35f), P(-255f, -85f),
                    P(-195f, -155f), P(-155f, -195f), P(-95f, -205f), P(-35f, -205f), P(0f, -255f), P(0f, -300f),
                    P(-260f, -320f), P(-210f, -330f), P(210f, -295f), P(270f, -280f)
                };
            for (var i = 0; i < coins.Length; i++) CreateCoin($"Coin_{i + 1:00}", root, coins[i] + Vector3.up * 1.25f, resources, profile, materials.Coin);

            var canisters = portraitCourse
                ? new[] { P(0f, -250f), P(-95f, -75f), P(95f, 105f), P(-60f, 285f) }
                : new[]
                {
                    P(-45f, -75f), P(115f, 20f), P(-245f, 10f), P(-255f, -85f),
                    P(0f, -225f), P(-305f, -320f), P(165f, -210f), P(0f, 335f)
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
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = CreateUiObject("SafeArea", canvasObject.transform, Vector2.zero, Vector2.one);
            safeArea.AddComponent<SafeAreaPanel>();
            var leftZone = CreateUiImage("SteerLeft", safeArea.transform, Vector2.zero, new Vector2(0.5f, 0.82f), new Color(0f, 0.35f, 1f, 0.035f));
            leftZone.AddComponent<TouchSteeringZone>().Configure(input, -1);
            var rightZone = CreateUiImage("SteerRight", safeArea.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0.82f), new Color(1f, 0.35f, 0f, 0.035f));
            rightZone.AddComponent<TouchSteeringZone>().Configure(input, 1);

            var controlHint = CreateText("ControlHint", safeArea.transform, "HOLD LEFT / RIGHT TO BUILD DRIFT", 24, TextAnchor.MiddleCenter);
            SetRect(controlHint.rectTransform, new Vector2(0f, 34f), new Vector2(760f, 48f), new Vector2(0.5f, 0f));
            controlHint.color = new Color(1f, 1f, 1f, 0.78f);

            var brakeButton = CreateButton("BrakeButton", safeArea.transform, "BRAKE  [SPACE]", new Color(0.86f, 0.20f, 0.12f, 0.9f));
            SetRect(brakeButton.GetComponent<RectTransform>(), new Vector2(0f, 105f), new Vector2(280f, 96f), new Vector2(0.5f, 0f));
            brakeButton.AddComponent<TouchBrakeButton>().Configure(input);

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
            SetRect(policePanelObject.GetComponent<RectTransform>(), new Vector2(0f, -154f), new Vector2(620f, 54f), new Vector2(0.5f, 1f));
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

            var cameraButtonObject = CreateButton("CameraButton", safeArea.transform, "CAMERA: TOP  [V]", new Color(0.12f, 0.15f, 0.2f, 0.86f));
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

        private static GameObject CreateRoadSegment(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material, PhysicsMaterial physicsMaterial, bool visibleSurface = true)
        {
            var delta = end - start;
            // The terrain surface is 1 cm below the road: visually separate to
            // prevent z-fighting, but physically flush enough to avoid a curb.
            var segment = visibleSurface
                ? CreateBox(name, parent, Vector3.zero, Vector3.one, material, physicsMaterial, 7)
                : CreateRoadVolume(name, parent, start, end, width);
            if (visibleSurface)
            {
                var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                var roadUp = rotation * Vector3.up;
                segment.transform.SetPositionAndRotation((start + end) * 0.5f - roadUp * 0.1f, rotation);
                segment.transform.localScale = new Vector3(width, 0.2f, delta.magnitude + 0.8f);
                segment.GetComponent<BoxCollider>().isTrigger = true;
            }
            return segment;
        }

        private static GameObject CreateRoadVolume(string name, Transform parent, Vector3 start, Vector3 end, float width)
        {
            var delta = end - start;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            var roadUp = rotation * Vector3.up;
            var gameObject = new GameObject(name);
            gameObject.layer = 7;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation((start + end) * 0.5f - roadUp * 0.1f, rotation);
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 0.25f, delta.magnitude + 0.8f);
            collider.isTrigger = true;
            return gameObject;
        }

        private static void CreateRoadRibbon(RoadGraphEdge edge, Transform parent, Material material, PhysicsMaterial physicsMaterial)
        {
            var root = new GameObject($"{edge.Id}_DriveSurface");
            root.layer = 0;
            root.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{edge.Id}_Ribbon" };
            var vertices = new Vector3[edge.Centerline.Count * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(edge.Centerline.Count - 1) * 6];
            var distance = 0f;
            for (var i = 0; i < edge.Centerline.Count; i++)
            {
                var previous = edge.Centerline[Mathf.Max(0, i - 1)];
                var next = edge.Centerline[Mathf.Min(edge.Centerline.Count - 1, i + 1)];
                var tangent = (next - previous).normalized;
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                if (i > 0) distance += Vector3.Distance(edge.Centerline[i - 1], edge.Centerline[i]);
                var bankDirection = edge.Id.EndsWith("_C", StringComparison.Ordinal) ? -1f : 1f;
                var bankBlend = edge.Id.StartsWith("Hill_Hairpin", StringComparison.Ordinal) && edge.Centerline.Count > 2
                    ? Mathf.Sin(Mathf.PI * i / (edge.Centerline.Count - 1f))
                    : 0f;
                var bankHeight = Mathf.Tan(6f * Mathf.Deg2Rad) * edge.Width * 0.5f * bankBlend * bankDirection;
                vertices[i * 2] = edge.Centerline[i] - right * edge.Width * 0.5f - Vector3.up * bankHeight;
                vertices[i * 2 + 1] = edge.Centerline[i] + right * edge.Width * 0.5f + Vector3.up * bankHeight;
                uvs[i * 2] = new Vector2(0f, distance / 8f);
                uvs[i * 2 + 1] = new Vector2(1f, distance / 8f);
                if (i >= edge.Centerline.Count - 1) continue;
                var triangle = i * 6;
                var vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 3;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = physicsMaterial;

            var trigger = new GameObject($"{edge.Id}_RoadTrigger");
            trigger.layer = 7;
            trigger.transform.SetParent(root.transform, false);
            var triggerCollider = trigger.AddComponent<MeshCollider>();
            triggerCollider.sharedMesh = CreateRoadTriggerMesh(edge);
            triggerCollider.convex = true;
            triggerCollider.isTrigger = true;
        }

        private static Mesh CreateRoadTriggerMesh(RoadGraphEdge edge)
        {
            const float halfThickness = 0.16f;
            var count = edge.Centerline.Count;
            var vertices = new Vector3[count * 4];
            var triangles = new List<int>((count - 1) * 24 + 12);
            for (var i = 0; i < count; i++)
            {
                var previous = edge.Centerline[Mathf.Max(0, i - 1)];
                var next = edge.Centerline[Mathf.Min(count - 1, i + 1)];
                var tangent = (next - previous).normalized;
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                var leftPoint = edge.Centerline[i] - right * edge.Width * 0.5f;
                var rightPoint = edge.Centerline[i] + right * edge.Width * 0.5f;
                vertices[i * 4] = leftPoint + Vector3.up * halfThickness;
                vertices[i * 4 + 1] = rightPoint + Vector3.up * halfThickness;
                vertices[i * 4 + 2] = leftPoint - Vector3.up * halfThickness;
                vertices[i * 4 + 3] = rightPoint - Vector3.up * halfThickness;
                if (i >= count - 1) continue;
                var a = i * 4;
                var b = (i + 1) * 4;
                triangles.AddRange(new[]
                {
                    a, b, a + 1, a + 1, b, b + 1,
                    a + 2, a + 3, b + 2, a + 3, b + 3, b + 2,
                    a, a + 2, b, a + 2, b + 2, b,
                    a + 1, b + 1, a + 3, a + 3, b + 1, b + 3
                });
            }
            triangles.AddRange(new[]
            {
                0, 1, 2, 1, 3, 2,
                (count - 1) * 4, (count - 1) * 4 + 2, (count - 1) * 4 + 1,
                (count - 1) * 4 + 1, (count - 1) * 4 + 2, (count - 1) * 4 + 3
            });
            var triggerMesh = new Mesh { name = $"{edge.Id}_RoadTriggerMesh" };
            triggerMesh.vertices = vertices;
            triggerMesh.SetTriangles(triangles, 0);
            triggerMesh.RecalculateNormals();
            triggerMesh.RecalculateBounds();
            return triggerMesh;
        }

        private static void CreateGroundRoadBoundaries(
            RoadGraphAsset graph,
            Transform parent,
            Material material,
            PhysicsMaterial physicsMaterial)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var edgeIndex = 0; edgeIndex < graph.Edges.Count; edgeIndex++)
            {
                var edge = graph.Edges[edgeIndex];
                var groundRoad = true;
                for (var point = 0; point < edge.Centerline.Count; point++)
                {
                    if (edge.Centerline[point].y <= 0.25f) continue;
                    groundRoad = false;
                    break;
                }
                if (!groundRoad) continue;

                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    var start = edge.Centerline[point - 1];
                    var end = edge.Centerline[point];
                    var delta = end - start;
                    var length = delta.magnitude;
                    if (length < 0.5f) continue;
                    var direction = delta / length;
                    var junctionClearance = Mathf.Max(9f, edge.Width * 0.55f);
                    if (point == 1) start += direction * Mathf.Min(junctionClearance, length * 0.42f);
                    else start -= direction * 0.18f;
                    if (point == edge.Centerline.Count - 1) end -= direction * Mathf.Min(junctionClearance, length * 0.42f);
                    else end += direction * 0.18f;
                    delta = end - start;
                    if (delta.magnitude < 0.6f) continue;

                    var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    var right = rotation * Vector3.right;
                    var center = (start + end) * 0.5f + Vector3.up * 0.45f;
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var boundaryCenter = center + right * side * (edge.Width * 0.5f - 0.22f);
                        AppendBoxMesh(vertices, triangles, boundaryCenter, rotation, new Vector3(0.55f, 0.9f, delta.magnitude));
                    }
                }
            }

            if (vertices.Count > 0)
            {
                var boundaries = CreateCombinedSolidVisual("GroundRoadBoundaries", parent, material, physicsMaterial, vertices, triangles);
                boundaries.layer = 13;
            }
        }

        private static void CreateRouteGuardrails(RoadGraphEdge edge, Transform parent, Material material, PhysicsMaterial physicsMaterial)
        {
            // Very short elevated links are open junction throats, not bridge
            // spans. Rails on them read as caps and can trap a turning car.
            if (edge.Length < edge.Width * 1.5f) return;
            var root = new GameObject($"{edge.Id}_Guardrails");
            root.layer = 8;
            root.transform.SetParent(parent, false);
            var colliderVertices = new List<Vector3>();
            var colliderTriangles = new List<int>();
            var visualVertices = new List<Vector3>();
            var visualTriangles = new List<int>();
            for (var i = 1; i < edge.Centerline.Count; i++)
            {
                var start = edge.Centerline[i - 1];
                var end = edge.Centerline[i];
                if (Mathf.Max(start.y, end.y) < 2f) continue;
                var untrimmedDelta = end - start;
                var untrimmedLength = untrimmedDelta.magnitude;
                var direction = untrimmedDelta.normalized;
                var junctionClearance = Mathf.Max(10f, edge.Width * 0.65f);
                var startTrim = i == 1 ? Mathf.Min(junctionClearance, untrimmedLength * 0.45f) : -0.18f;
                var endTrim = i == edge.Centerline.Count - 1 ? Mathf.Min(junctionClearance, untrimmedLength * 0.45f) : -0.18f;
                start += direction * startTrim;
                end -= direction * endTrim;
                var delta = end - start;
                if (delta.magnitude < 1f) continue;
                var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                var right = rotation * Vector3.right;
                var roadUp = rotation * Vector3.up;
                var center = (start + end) * 0.5f + roadUp * 0.45f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var railCenter = center + right * side * (edge.Width * 0.5f - 0.18f);
                    var railSize = new Vector3(0.35f, 0.9f, delta.magnitude);
                    AppendBoxMesh(visualVertices, visualTriangles, railCenter, rotation, railSize);
                    AppendBoxMesh(colliderVertices, colliderTriangles, railCenter, rotation, railSize);
                }
            }

            if (colliderVertices.Count == 0)
            {
                Object.DestroyImmediate(root);
                return;
            }

            CreateCombinedBoxVisual($"{edge.Id}_GuardrailVisual", root.transform, material, visualVertices, visualTriangles);

            var mesh = new Mesh { name = $"{edge.Id}_GuardrailCollider" };
            mesh.SetVertices(colliderVertices);
            mesh.SetTriangles(colliderTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = physicsMaterial;
        }

        private static void AppendBoxMesh(List<Vector3> vertices, List<int> triangles, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var first = vertices.Count;
            var half = size * 0.5f;
            var corners = new[]
            {
                new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z),
                new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z)
            };
            for (var i = 0; i < corners.Length; i++) vertices.Add(center + rotation * corners[i]);
            var faces = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };
            for (var i = 0; i < faces.Length; i++) triangles.Add(first + faces[i]);
        }

        private static void AppendRoadBox(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 start,
            Vector3 end,
            float width,
            float thickness,
            float verticalOffset,
            float lengthPadding)
        {
            var delta = end - start;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            var roadUp = rotation * Vector3.up;
            AppendBoxMesh(vertices, triangles, (start + end) * 0.5f - roadUp * verticalOffset, rotation, new Vector3(width, thickness, delta.magnitude + lengthPadding));
        }

        private static void AppendRampBox(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, float width, float thickness)
        {
            var delta = end - start;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            var roadUp = rotation * Vector3.up;
            AppendBoxMesh(vertices, triangles, (start + end) * 0.5f - roadUp * (thickness * 0.5f), rotation, new Vector3(width, thickness, delta.magnitude));
        }

        private static GameObject CreateCombinedBoxVisual(string name, Transform parent, Material material, List<Vector3> vertices, List<int> triangles)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{name}Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        private static GameObject CreateCombinedSolidVisual(
            string name,
            Transform parent,
            Material material,
            PhysicsMaterial physicsMaterial,
            List<Vector3> vertices,
            List<int> triangles)
        {
            var visual = CreateCombinedBoxVisual(name, parent, material, vertices, triangles);
            visual.layer = 8;
            var collider = visual.AddComponent<MeshCollider>();
            collider.sharedMesh = visual.GetComponent<MeshFilter>().sharedMesh;
            collider.sharedMaterial = physicsMaterial;
            return visual;
        }

        private static void CreateCombinedRoadMarkers(RoadGraphAsset graph, Transform parent, Material material)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var edgeIndex = 0; edgeIndex < graph.Edges.Count; edgeIndex++)
            {
                var edge = graph.Edges[edgeIndex];
                if (edge.Kind != RoadKind.Boulevard) continue;
                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    var start = edge.Centerline[point - 1];
                    var end = edge.Centerline[point];
                    var delta = end - start;
                    var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    AppendBoxMesh(vertices, triangles, (start + end) * 0.5f + Vector3.down * 0.08f, rotation, new Vector3(0.28f, 0.025f, Mathf.Max(1f, delta.magnitude - 2f)));
                }
            }
            if (vertices.Count == 0) return;
            var marker = new GameObject("BoulevardCenterMarkings");
            marker.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "BoulevardCenterMarkingsMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            marker.AddComponent<MeshFilter>().sharedMesh = mesh;
            marker.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateRouteDiagramMarkers(RoadGraphAsset graph, Transform parent, Materials materials)
        {
            var groups = new[]
            {
                (Name: "MainRouteGuide", Feature: RoadFeature.MainRoute, Material: materials.RouteMain),
                (Name: "ElevatedRouteGuide", Feature: RoadFeature.Elevated, Material: materials.RouteElevated),
                (Name: "ShortcutRouteGuide", Feature: RoadFeature.Shortcut, Material: materials.RouteShortcut),
                (Name: "EscapeRouteGuide", Feature: RoadFeature.EscapeRoute, Material: materials.RouteEscape)
            };

            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (var edgeIndex = 0; edgeIndex < graph.Edges.Count; edgeIndex++)
                {
                    var edge = graph.Edges[edgeIndex];
                    if ((edge.Features & groups[groupIndex].Feature) == 0) continue;
                    // Priority follows the sketch legend: escape and shortcut
                    // colors remain visible even when those roads are elevated.
                    if (groups[groupIndex].Feature == RoadFeature.Elevated &&
                        (edge.Features & (RoadFeature.Shortcut | RoadFeature.EscapeRoute)) != 0) continue;
                    for (var point = 1; point < edge.Centerline.Count; point++)
                    {
                        var start = edge.Centerline[point - 1] + Vector3.up * 0.16f;
                        var end = edge.Centerline[point] + Vector3.up * 0.16f;
                        AppendRoadBox(vertices, triangles, start, end, 0.8f, 0.05f, 0f, 0.25f);
                    }
                }
                if (vertices.Count == 0) continue;
                CreateCombinedBoxVisual(groups[groupIndex].Name, parent, groups[groupIndex].Material, vertices, triangles);
            }
        }

        private static void CreateSignatureTower(Transform parent, Materials materials, PhysicsMaterial physicsMaterial)
        {
            var root = new GameObject("SignatureLoopTower").transform;
            root.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "TowerBody";
            body.layer = 8;
            body.transform.SetParent(root, false);
            body.transform.position = new Vector3(-260f, 17f, 190f);
            body.transform.localScale = new Vector3(15f, 17f, 15f);
            body.GetComponent<Renderer>().sharedMaterial = materials.Building;
            body.GetComponent<Collider>().material = physicsMaterial;

            var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "AccentCrown";
            crown.transform.SetParent(root, false);
            crown.transform.position = new Vector3(-260f, 35f, 190f);
            crown.transform.localScale = new Vector3(18f, 1.2f, 18f);
            crown.GetComponent<Renderer>().sharedMaterial = materials.Accent;
            Object.DestroyImmediate(crown.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "ConcreteCap";
            cap.transform.SetParent(root, false);
            cap.transform.position = new Vector3(-260f, 38f, 190f);
            cap.transform.localScale = new Vector3(8f, 2f, 8f);
            cap.GetComponent<Renderer>().sharedMaterial = materials.Concrete;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
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

        private static void CreateDriveableBuilding(
            string name,
            Transform parent,
            Vector3 groundPosition,
            Vector2 footprint,
            float height,
            bool accessFromWest,
            Materials materials,
            PhysicsMaterial physicsMaterial)
        {
            const float accessDepth = 14f;
            var accessSouthZ = groundPosition.z - 13f;
            var accessNorthZ = groundPosition.z + 9f;
            var minX = groundPosition.x - footprint.x * 0.5f;
            var maxX = groundPosition.x + footprint.x * 0.5f;
            var minZ = groundPosition.z - footprint.y * 0.5f;
            var maxZ = groundPosition.z + footprint.y * 0.5f;
            var innerX = accessFromWest ? minX + accessDepth : maxX - accessDepth;
            var bodyY = groundPosition.y + height * 0.5f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            var mainMinX = accessFromWest ? innerX : minX;
            var mainMaxX = accessFromWest ? maxX : innerX;
            AppendBoxMesh(
                vertices,
                triangles,
                new Vector3((mainMinX + mainMaxX) * 0.5f, bodyY, groundPosition.z),
                Quaternion.identity,
                new Vector3(mainMaxX - mainMinX, height, footprint.y));

            var accessMinX = accessFromWest ? minX : innerX;
            var accessMaxX = accessFromWest ? innerX : maxX;
            AppendBoxMesh(
                vertices,
                triangles,
                new Vector3((accessMinX + accessMaxX) * 0.5f, bodyY, (minZ + accessSouthZ) * 0.5f),
                Quaternion.identity,
                new Vector3(accessMaxX - accessMinX, height, accessSouthZ - minZ));
            AppendBoxMesh(
                vertices,
                triangles,
                new Vector3((accessMinX + accessMaxX) * 0.5f, bodyY, (accessNorthZ + maxZ) * 0.5f),
                Quaternion.identity,
                new Vector3(accessMaxX - accessMinX, height, maxZ - accessNorthZ));

            var building = new GameObject(name);
            building.layer = 8;
            building.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{name}_AccessShell" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            building.AddComponent<MeshFilter>().sharedMesh = mesh;
            building.AddComponent<MeshRenderer>().sharedMaterial = materials.Building;
            var collider = building.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = physicsMaterial;
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
            layers.GetArrayElementAtIndex(13).stringValue = "RoadBoundary";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Profiles
        {
            public VehicleHandlingProfile Vehicle;
            public ChaseCameraProfile Camera;
            public RideRulesProfile Ride;
            public ScoringProfile Scoring;
            public StuntProfile Stunts;
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
            public Material RouteMain;
            public Material RouteElevated;
            public Material RouteShortcut;
            public Material RouteEscape;
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
