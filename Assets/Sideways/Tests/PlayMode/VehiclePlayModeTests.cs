using System.Collections;
using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.EnvironmentSystem;
using Sideways.UI;
using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sideways.Tests
{
    public sealed class VehiclePlayModeTests
    {
        [UnityTest]
        public IEnumerator SandboxSceneStartsAndDrives()
        {
            SceneManager.LoadScene("DrivingSandbox");
            yield return null;
            var controller = Object.FindFirstObjectByType<VehicleController>();
            var recovery = Object.FindFirstObjectByType<VehicleRecovery>();
            var session = Object.FindFirstObjectByType<RideSessionController>();
            var scoring = Object.FindFirstObjectByType<DriftScoreController>();
            var scoreHud = Object.FindFirstObjectByType<ScoreHud>();
            var roadGraph = Object.FindFirstObjectByType<RoadGraphComponent>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(recovery, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(scoring, Is.Not.Null);
            Assert.That(scoreHud, Is.Not.Null);
            Assert.That(roadGraph, Is.Not.Null);
            Assert.That(roadGraph.Graph.IsValid(out var graphError), Is.True, graphError);

            for (var i = 0; i < 45; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Grounded, Is.True);
            Assert.That(controller.Telemetry.Speed, Is.GreaterThan(1f));
            Assert.That(session.State, Is.EqualTo(RideSessionState.Driving));

            for (var i = 0; i < 20 && !recovery.CanReset; i++) yield return new WaitForFixedUpdate();
            Assert.That(recovery.CanReset, Is.True);
            controller.Body.position += new Vector3(0f, 12f, 20f);
            controller.Body.linearVelocity = new Vector3(8f, 4f, 3f);
            Assert.That(recovery.ResetToRoad(), Is.True);
            Assert.That(controller.Body.position.y, Is.LessThan(3f));
            Assert.That(controller.Body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator VehicleAcceleratesOnGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            var car = new GameObject("TestCar");
            car.layer = 6;
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("TestZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1000f;
            var input = car.AddComponent<TestInputSource>();
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.acceleration = 12f;
            profile.maxSpeed = 8f;
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));

            for (var i = 0; i < 90; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Speed, Is.GreaterThan(1f));
            Assert.That(controller.Telemetry.Speed, Is.LessThan(profile.maxSpeed + 2f));
            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator HandbrakeBuildsSlipWhileSteering()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);

            var car = new GameObject("HandbrakeTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1000f;
            body.linearVelocity = Vector3.forward * 12f;
            var input = car.AddComponent<TestInputSource>();
            input.SteeringValue = 1f;
            input.HandbrakeValue = true;
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.maxSpeed = 28f;
            profile.steeringAcceleration = 12f;
            profile.yawDamping = 1.5f;
            profile.driftMinimumSpeed = 3f;
            profile.handbrakeLateralGrip = 0.2f;
            profile.handbrakeYawMultiplier = 2.5f;
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));
            var collisionSensor = car.AddComponent<VehicleCollisionSensor>();
            collisionSensor.Configure(1 << 8);
            var scoringProfile = ScriptableObject.CreateInstance<ScoringProfile>();
            scoringProfile.minimumSpeed = 3f;
            scoringProfile.minimumSlipAngle = 5f;
            scoringProfile.idealSlipAngle = 25f;
            scoringProfile.maximumSlipAngle = 85f;
            scoringProfile.maximumYawRate = 720f;
            var scoring = car.AddComponent<DriftScoreController>();
            scoring.Configure(controller, collisionSensor, scoringProfile);

            for (var i = 0; i < 25; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Handbrake, Is.True);
            Assert.That(Mathf.Abs(controller.Telemetry.SlipAngle), Is.GreaterThan(8f));
            Assert.That(scoring.CurrentScore, Is.GreaterThan(0));

            input.HandbrakeValue = false;
            yield return new WaitForFixedUpdate();
            Assert.That(controller.Telemetry.Handbrake, Is.False);

            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
            Object.Destroy(scoringProfile);
        }

        [UnityTest]
        public IEnumerator SustainedFlipEndsRideButTransientFlipDoesNot()
        {
            var rules = ScriptableObject.CreateInstance<RideRulesProfile>();
            rules.flipAngle = 100f;
            rules.flipDuration = 0.15f;
            var player = new GameObject("Player");
            player.transform.rotation = Quaternion.Euler(0f, 0f, 120f);
            var sessionObject = new GameObject("Session");
            var session = sessionObject.AddComponent<RideSessionController>();
            session.Configure(player.transform, null, rules, null);

            yield return new WaitForSeconds(0.08f);
            player.transform.rotation = Quaternion.identity;
            yield return null;
            Assert.That(session.State, Is.EqualTo(RideSessionState.Driving));

            player.transform.rotation = Quaternion.Euler(0f, 0f, 120f);
            yield return new WaitForSeconds(0.2f);
            Assert.That(session.State, Is.EqualTo(RideSessionState.Ended));
            Assert.That(session.EndReason, Is.EqualTo(RideEndReason.Flipped));
            Object.Destroy(player);
            Object.Destroy(sessionObject);
            Object.Destroy(rules);
        }

        private sealed class TestInputSource : MonoBehaviour, IVehicleInputSource
        {
            public float SteeringValue { get; set; }
            public bool HandbrakeValue { get; set; }
            public float Steering => SteeringValue;
            public bool Handbrake => HandbrakeValue;
            public bool ConsumeResetRequest() => false;
        }
    }
}
