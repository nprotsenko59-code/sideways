using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.EnvironmentSystem;
using Sideways.Police;
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
            Assert.That(AssetDatabase.LoadAssetAtPath<RoadGraphAsset>("Assets/Sideways/Configuration/LAPrototypeRoadGraph.asset"), Is.Not.Null);
            var policeProfile = AssetDatabase.LoadAssetAtPath<PoliceProfile>("Assets/Sideways/Configuration/DefaultPolice.asset");
            Assert.That(policeProfile, Is.Not.Null);
            Assert.That(policeProfile.IsValid(out var policeError), Is.True, policeError);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sideways/Prefabs/PoliceCar.prefab"), Is.Not.Null);
        }

        [Test]
        public void SandboxOpensWithRequiredObjects()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Sideways/Scenes/DrivingSandbox.unity", OpenSceneMode.Single);
            Assert.That(scene.GetRootGameObjects(), Has.Some.Matches<GameObject>(go => go.name == "PlayerCar"));
            Assert.That(GameObject.FindWithTag("MainCamera"), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<DriftScoreController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ScoreHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<VehicleCollisionSensor>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceDirector>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceVehicleController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceVisionSensor>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceHud>(), Is.Not.Null);
            var roadGraph = Object.FindFirstObjectByType<RoadGraphComponent>();
            Assert.That(roadGraph, Is.Not.Null);
            Assert.That(roadGraph.Graph, Is.Not.Null);

            var environment = GameObject.Find("Environment");
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment.transform.Find("BoundaryFiction"), Is.Not.Null);
            Assert.That(environment.transform.Find("Roads").childCount, Is.LessThanOrEqualTo(90));
            Assert.That(environment.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(220));
            Assert.That(environment.GetComponentsInChildren<Collider>(true).Length, Is.LessThanOrEqualTo(180));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Sideways/Scenes/DrivingSandbox.unity"));
        }
    }
}
