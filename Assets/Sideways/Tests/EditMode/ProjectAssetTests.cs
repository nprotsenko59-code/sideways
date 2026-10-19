using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Core;
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
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Sideways/Scenes/DrivingSandbox.unity"));
        }
    }
}
