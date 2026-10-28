using System.Linq;
using NUnit.Framework;
using Sideways.CameraSystem;
using Sideways.Configuration;
using Sideways.Challenges;
using Sideways.Collectibles;
using Sideways.Core;
using Sideways.EnvironmentSystem;
using Sideways.Police;
using Sideways.Stunts;
using Sideways.Traffic;
using Sideways.UI;
using Sideways.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class ProjectAssetTests
    {
        [Test]
        public void SandboxAndPlayerPrefabExist()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Sideways/Scenes/DrivingSandbox.unity"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sideways/Prefabs/PlayerCar.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ScoringProfile>("Assets/Sideways/Configuration/DefaultScoring.asset"), Is.Not.Null);
            var stuntProfile = AssetDatabase.LoadAssetAtPath<StuntProfile>("Assets/Sideways/Configuration/DefaultStunts.asset");
            Assert.That(stuntProfile, Is.Not.Null);
            Assert.That(stuntProfile.IsValid(out var stuntError), Is.True, stuntError);
            var vehicleProfile = AssetDatabase.LoadAssetAtPath<VehicleHandlingProfile>("Assets/Sideways/Configuration/DefaultVehicleHandling.asset");
            Assert.That(vehicleProfile, Is.Not.Null);
            Assert.That(vehicleProfile.IsValid(out var vehicleError), Is.True, vehicleError);
            Assert.That(vehicleProfile.highSpeedSteeringAngle, Is.GreaterThanOrEqualTo(18f));
            Assert.That(vehicleProfile.fullSteeringReductionSpeed, Is.GreaterThan(vehicleProfile.maxSpeed));
            var cameraProfile = AssetDatabase.LoadAssetAtPath<ChaseCameraProfile>("Assets/Sideways/Configuration/DefaultChaseCamera.asset");
            Assert.That(cameraProfile, Is.Not.Null);
            Assert.That(cameraProfile.overheadHeight, Is.InRange(30f, 34f));
            Assert.That(cameraProfile.overheadBackOffset, Is.GreaterThanOrEqualTo(14f));
            Assert.That(cameraProfile.overheadLookAheadDistance, Is.InRange(7f, 10f));
            Assert.That(cameraProfile.overheadFieldOfView, Is.InRange(42f, 48f));
            Assert.That(AssetDatabase.LoadAssetAtPath<RoadGraphAsset>("Assets/Sideways/Configuration/LAPrototypeRoadGraph.asset"), Is.Not.Null);
            var portraitCourse = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>("Assets/Sideways/Configuration/PortraitDriftCourse.asset");
            Assert.That(portraitCourse, Is.Not.Null);
            Assert.That(portraitCourse.GraphId, Is.EqualTo("portrait-drift-run"));
            Assert.That(portraitCourse.IsValid(out var courseError), Is.True, courseError);
            var policeProfile = AssetDatabase.LoadAssetAtPath<PoliceProfile>("Assets/Sideways/Configuration/DefaultPolice.asset");
            Assert.That(policeProfile, Is.Not.Null);
            Assert.That(policeProfile.IsValid(out var policeError), Is.True, policeError);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sideways/Prefabs/PoliceCar.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sideways/Prefabs/TrafficCar.prefab"), Is.Not.Null);
            var trafficProfile = AssetDatabase.LoadAssetAtPath<TrafficProfile>("Assets/Sideways/Configuration/DefaultTraffic.asset");
            Assert.That(trafficProfile, Is.Not.Null);
            Assert.That(trafficProfile.IsValid(out var trafficError), Is.True, trafficError);
            var fuelProfile = AssetDatabase.LoadAssetAtPath<FuelProfile>("Assets/Sideways/Configuration/DefaultFuel.asset");
            Assert.That(fuelProfile, Is.Not.Null);
            Assert.That(fuelProfile.IsValid(out var fuelError), Is.True, fuelError);
            Assert.That(AssetDatabase.LoadAssetAtPath<ChallengeDefinition>("Assets/Sideways/Configuration/DestroySignsChallenge.asset").IsValid(out var signChallengeError), Is.True, signChallengeError);
            Assert.That(AssetDatabase.LoadAssetAtPath<ChallengeDefinition>("Assets/Sideways/Configuration/DriftScoreChallenge.asset").IsValid(out var driftChallengeError), Is.True, driftChallengeError);
        }

        [Test]
        public void SandboxOpensWithRequiredObjects()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Sideways/Scenes/DrivingSandbox.unity", OpenSceneMode.Single);
            Assert.That(scene.GetRootGameObjects(), Has.Some.Matches<GameObject>(go => go.name == "PlayerCar"));
            Assert.That(GameObject.FindWithTag("MainCamera"), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ChaseCameraController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<CameraModeButtonBinding>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<DriftScoreController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ScoreHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<VehicleCollisionSensor>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceDirector>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceVehicleController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceVisionSensor>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PoliceVehicleController>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            Assert.That(Object.FindFirstObjectByType<PoliceRoadblockController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<TrafficSpawner>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<TrafficScoreController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ChallengeController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ChallengeHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<BonusHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<RideResourceController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ResourceHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerPreferencesController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GameFeedbackController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<VehicleDriftEffects>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<StuntController>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<StuntRamp>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindFirstObjectByType<TouchBrakeButton>(), Is.Not.Null);
            var collectibles = Object.FindObjectsByType<RoadCollectible>(FindObjectsSortMode.None);
            Assert.That(collectibles.Count(item => item.Kind == CollectibleKind.Coin), Is.GreaterThanOrEqualTo(20));
            Assert.That(collectibles.Count(item => item.Kind == CollectibleKind.FuelCanister), Is.GreaterThanOrEqualTo(4));
            Assert.That(Object.FindObjectsByType<BreakableRoadSign>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(10));
            var roadGraph = Object.FindFirstObjectByType<RoadGraphComponent>();
            Assert.That(roadGraph, Is.Not.Null);
            Assert.That(roadGraph.Graph, Is.Not.Null);
            Assert.That(roadGraph.Graph.GraphId, Is.EqualTo("portrait-drift-run"));

            var environment = GameObject.Find("Environment");
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment.transform.Find("BoundaryFiction"), Is.Not.Null);
            Assert.That(environment.transform.Find("BoundaryFiction/WestCourseWall"), Is.Not.Null);
            Assert.That(environment.transform.Find("Landmarks/DriftRunTower"), Is.Not.Null);
            Assert.That(environment.transform.Find("Roads/Course_Straight"), Is.Not.Null);
            Assert.That(environment.transform.Find("Roads/Course_Crossover"), Is.Not.Null);
            Assert.That(environment.transform.Find("CityInfrastructure/StartFinish"), Is.Not.Null);
            var roadBoundaries = environment.transform.Find("RoadFurniture/GroundRoadBoundaries");
            Assert.That(roadBoundaries, Is.Not.Null);
            Assert.That(roadBoundaries.gameObject.layer, Is.EqualTo(13));
            Assert.That(LayerMask.LayerToName(13), Is.EqualTo("RoadBoundary"));
            Assert.That(roadBoundaries.GetComponent<MeshCollider>(), Is.Not.Null);
            Assert.That(roadBoundaries.GetComponent<MeshCollider>().isTrigger, Is.False);
            Assert.That(environment.transform.Find("Roads").childCount, Is.LessThanOrEqualTo(150));
            Assert.That(environment.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(320));
            Assert.That(environment.GetComponentsInChildren<Collider>(true).Length, Is.LessThanOrEqualTo(260));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Sideways/Scenes/DrivingSandbox.unity"));
            Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("15.0"));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
        }
    }
}
