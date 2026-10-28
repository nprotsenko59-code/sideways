using System.Collections;
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
using UnityEngine;
using UnityEngine.EventSystems;
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
            var police = Object.FindFirstObjectByType<PoliceDirector>();
            var policeVehicles = Object.FindObjectsByType<PoliceVehicleController>(FindObjectsSortMode.None);
            var policeVehicle = System.Array.Find(policeVehicles, unit => unit.IsOperational);
            var policeHud = Object.FindFirstObjectByType<PoliceHud>();
            var traffic = Object.FindFirstObjectByType<TrafficSpawner>();
            var challenges = Object.FindFirstObjectByType<ChallengeController>();
            var challengeHud = Object.FindFirstObjectByType<ChallengeHud>();
            var resources = Object.FindFirstObjectByType<RideResourceController>();
            var resourceHud = Object.FindFirstObjectByType<ResourceHud>();
            var preferences = Object.FindFirstObjectByType<PlayerPreferencesController>();
            var feedback = Object.FindFirstObjectByType<GameFeedbackController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(recovery, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(scoring, Is.Not.Null);
            Assert.That(scoreHud, Is.Not.Null);
            Assert.That(roadGraph, Is.Not.Null);
            Assert.That(police, Is.Not.Null);
            Assert.That(policeVehicle, Is.Not.Null);
            Assert.That(policeHud, Is.Not.Null);
            Assert.That(police.State, Is.EqualTo(PoliceState.Patrol));
            Assert.That(policeVehicles.Length, Is.EqualTo(3));
            Assert.That(police.ActiveUnitCount, Is.EqualTo(1));
            Assert.That(police.RoadblockDeployed, Is.False);
            Assert.That(traffic, Is.Not.Null);
            Assert.That(challenges, Is.Not.Null);
            Assert.That(challengeHud, Is.Not.Null);
            Assert.That(resources, Is.Not.Null);
            Assert.That(resourceHud, Is.Not.Null);
            Assert.That(preferences, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);
            Assert.That(preferences.IsOpen, Is.False);
            Assert.That(resources.Snapshot.Fuel, Is.GreaterThan(0f));
            var startingBoundaryOverlaps = Physics.OverlapBox(
                controller.transform.position + Vector3.up * 0.55f,
                new Vector3(1.05f, 0.6f, 2.15f),
                controller.transform.rotation,
                1 << 13,
                QueryTriggerInteraction.Ignore);
            Assert.That(startingBoundaryOverlaps, Is.Empty,
                $"Player spawn must be clear of road boundaries: {string.Join(", ", System.Array.ConvertAll(startingBoundaryOverlaps, collider => collider.name))}");
            var fuelLabel = GameObject.Find("Fuel")?.GetComponent<UnityEngine.UI.Text>();
            Assert.That(fuelLabel, Is.Not.Null);
            Assert.That(fuelLabel.text, Does.Not.Contain("EMPTY"), "The resource HUD must render the initialized tank on its first runtime frame.");
            var playerRenderer = controller.GetComponentInChildren<Renderer>();
            var mainCamera = UnityEngine.Camera.main;
            Assert.That(playerRenderer, Is.Not.Null);
            Assert.That(mainCamera, Is.Not.Null);
            Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(mainCamera), playerRenderer.bounds), Is.True,
                "The generated scene must start with the player inside the camera frustum.");
            var playerViewport = mainCamera.WorldToViewportPoint(controller.transform.position + Vector3.up * 0.5f);
            var roadAheadViewport = mainCamera.WorldToViewportPoint(controller.transform.position + controller.transform.forward * 35f);
            var topViewObliqueness = Vector3.Angle(mainCamera.transform.forward, Vector3.down);
            Assert.That(playerViewport.y, Is.InRange(0.18f, 0.38f), "The overhead composition should keep the car low in frame.");
            Assert.That(topViewObliqueness, Is.InRange(32f, 45f), "TOP view should be visibly oblique rather than nearly vertical.");
            Assert.That(roadAheadViewport.z, Is.GreaterThan(0f));
            Assert.That(roadAheadViewport.x, Is.InRange(0f, 1f));
            Assert.That(roadAheadViewport.y, Is.InRange(0f, 1f), "At least 35 metres of road ahead should be visible.");
            var cameraToPlayer = controller.transform.position - mainCamera.transform.position;
            Assert.That(Physics.Raycast(mainCamera.transform.position, cameraToPlayer.normalized, out _, cameraToPlayer.magnitude, 1 << 8, QueryTriggerInteraction.Ignore), Is.False,
                "Buildings must not block the initial camera-to-player sightline.");
            Assert.That(roadGraph.Graph.IsValid(out var graphError), Is.True, graphError);
            var roadVolumes = roadGraph.GetComponentsInChildren<Collider>(true);
            Assert.That(roadVolumes.Length, Is.GreaterThan(0));
            for (var i = 0; i < roadVolumes.Length; i++)
            {
                if (roadVolumes[i].gameObject.layer == 7)
                {
                    Assert.That(roadVolumes[i].isTrigger, Is.True, $"{roadVolumes[i].name} road-identification volumes must remain triggers");
                }
            }
            var courseBoundaries = GameObject.Find("GroundRoadBoundaries")?.GetComponent<MeshCollider>();
            Assert.That(courseBoundaries, Is.Not.Null);
            Assert.That(courseBoundaries.isTrigger, Is.False, "The portrait course must have solid visible side boundaries.");

            var policeStart = policeVehicle.transform.position;

            for (var i = 0; i < 45; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Grounded, Is.True,
                $"Player should remain grounded after launch. Position={controller.Body.position}, velocity={controller.Body.linearVelocity}, up={controller.transform.up}.");
            Assert.That(controller.Telemetry.Speed, Is.GreaterThan(1f));
            Assert.That(session.State, Is.EqualTo(RideSessionState.Driving));
            Assert.That(Vector3.Distance(policeStart, policeVehicle.transform.position), Is.GreaterThan(0.25f));
            Assert.That(traffic.ActiveCount, Is.GreaterThan(0));
            Assert.That(challenges.Snapshot.State, Is.EqualTo(ChallengeState.Idle));

            for (var i = 0; i < 20 && !recovery.CanReset; i++) yield return new WaitForFixedUpdate();
            Assert.That(recovery.CanReset, Is.True);
            controller.Body.position += new Vector3(0f, 12f, 20f);
            controller.Body.linearVelocity = new Vector3(8f, 4f, 3f);
            Assert.That(recovery.ResetToRoad(), Is.True);
            Assert.That(controller.Body.position.y, Is.LessThan(3f));
            Assert.That(controller.Body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator SignChallengeConsumesSceneEventsWithoutEndingRide()
        {
            SceneManager.LoadScene("DrivingSandbox");
            yield return null;
            var challenges = Object.FindFirstObjectByType<ChallengeController>();
            var session = Object.FindFirstObjectByType<RideSessionController>();
            var signs = Object.FindObjectsByType<BreakableRoadSign>(FindObjectsSortMode.None);
            var scoring = Object.FindFirstObjectByType<DriftScoreController>();
            Assert.That(signs.Length, Is.GreaterThanOrEqualTo(10));

            challenges.StartChallenge(0);
            for (var i = 0; i < 10; i++) Assert.That(signs[i].DestroySign(), Is.True);
            yield return null;

            Assert.That(challenges.Snapshot.State, Is.EqualTo(ChallengeState.Succeeded));
            Assert.That(challenges.Snapshot.Progress, Is.EqualTo(10));
            Assert.That(scoring.Snapshot.BankedScore, Is.GreaterThanOrEqualTo(challenges.Snapshot.Reward));
            Assert.That(session.State, Is.EqualTo(RideSessionState.Driving));
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
        public IEnumerator AutomaticAccelerationRetainsLaunchEnvelope()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);

            var car = new GameObject("LaunchEnvelopeTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("LaunchZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            var input = car.AddComponent<TestInputSource>();
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));

            for (var i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            var originalEnvelope = profile.acceleration * 60f * Time.fixedDeltaTime;
            Assert.That(controller.Telemetry.Speed, Is.InRange(originalEnvelope * 0.85f, originalEnvelope * 1.15f), "Launch speed should remain within 15% of the original acceleration envelope.");
            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator HeldSteeringProgressivelyBuildsSlipAndSwitchesDirection()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(400f, 1f, 400f);

            var car = new GameObject("HoldToDriftTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("DirectionSwitchZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1000f;
            body.linearVelocity = Vector3.forward * 18f;
            var input = car.AddComponent<TestInputSource>();
            input.SteeringValue = 1f;
            input.DriftStrengthValue = 1f;
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.maxSpeed = 28f;
            profile.lowSpeedSteeringAngle = 38f;
            profile.highSpeedSteeringAngle = 24f;
            profile.driftMinimumSpeed = 6f;
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

            for (var i = 0; i < 35; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Brake, Is.False);
            Assert.That(controller.Telemetry.ControlDriftStrength, Is.GreaterThan(0.55f));
            Assert.That(controller.SteeringDriftControl, Is.GreaterThan(0.55f));
            Assert.That(Mathf.Abs(controller.Telemetry.SlipAngle), Is.GreaterThan(8f));
            Assert.That(Mathf.Abs(controller.Telemetry.RearSlipAngle), Is.GreaterThan(profile.driftEnterAngle));
            Assert.That(scoring.CurrentScore, Is.GreaterThan(0));

            var originalSlipSign = Mathf.Sign(controller.Telemetry.SlipAngle);
            var originalSteering = input.SteeringValue;
            input.SteeringValue = -originalSteering;
            var recovered = false;
            var switchedDirection = false;
            var maximumYaw = 0f;
            var maximumHeadingAngle = Mathf.Abs(controller.Telemetry.SlipAngle);
            var minimumDriftControl = controller.SteeringDriftControl;
            for (var i = 0; i < 160; i++)
            {
                yield return new WaitForFixedUpdate();
                maximumYaw = Mathf.Max(maximumYaw, Mathf.Abs(controller.Telemetry.YawRateDegrees));
                maximumHeadingAngle = Mathf.Max(maximumHeadingAngle, Mathf.Abs(controller.Telemetry.SlipAngle));
                minimumDriftControl = Mathf.Min(minimumDriftControl, controller.SteeringDriftControl);
                if (Mathf.Abs(controller.Telemetry.RearSlipAngle) <= profile.driftExitAngle)
                {
                    recovered = true;
                }

                if (recovered && Mathf.Abs(controller.Telemetry.SlipAngle) >= profile.driftEnterAngle &&
                    Mathf.Sign(controller.Telemetry.SlipAngle) == -originalSlipSign)
                {
                    switchedDirection = true;
                    break;
                }
            }
            Assert.That(controller.Telemetry.Brake, Is.False);
            Assert.That(input.SteeringValue, Is.EqualTo(-originalSteering), "The test must apply genuinely opposite steering input.");
            Assert.That(input.DriftStrengthValue, Is.EqualTo(1f), "Direction switching must work without releasing the held drift input.");
            Assert.That(minimumDriftControl, Is.LessThan(-0.2f), "The signed drift controller must cross neutral and build the opposite side.");
            Assert.That(recovered, Is.True, "Opposite steering should recover the original rear slip while drift input remains held.");
            Assert.That(switchedDirection, Is.True,
                $"Continuing to hold the opposite side should establish a drift in the new direction. Original sign={originalSlipSign}, final slip={controller.Telemetry.SlipAngle:F2}, rear={controller.Telemetry.RearSlipAngle:F2}, yaw={controller.Telemetry.YawRateDegrees:F2}, speed={controller.Telemetry.Speed:F2}, grounded={controller.Telemetry.Grounded}, control={controller.Telemetry.ControlDriftStrength:F2}.");
            Assert.That(maximumHeadingAngle, Is.LessThanOrEqualTo(profile.antiSpinHardAngle + 2f), "The arcade anti-spin envelope must prevent a 180-degree rotation.");
            Assert.That(maximumYaw, Is.LessThan(profile.maximumYawRate + 25f));

            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
            Object.Destroy(scoringProfile);
        }

        [UnityTest]
        public IEnumerator TouchSteeringDragSwitchesFromLeftToRightWithoutReleasing()
        {
            var eventSystemObject = new GameObject("TouchDirectionEventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var inputObject = new GameObject("TouchDirectionInput");
            var input = inputObject.AddComponent<VehicleInputSource>();
            input.Configure(20f, 0.38f, 0.4f);
            var zoneObject = new GameObject("TouchDirectionZone");
            var zone = zoneObject.AddComponent<TouchSteeringZone>();
            zone.Configure(input, -1);
            var pointer = new PointerEventData(eventSystem)
            {
                pointerId = 7,
                position = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f)
            };

            zone.OnPointerDown(pointer);
            yield return new WaitForSeconds(0.15f);
            Assert.That(input.Steering, Is.LessThan(-0.2f));

            pointer.position = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            zone.OnDrag(pointer);
            yield return new WaitForSeconds(0.3f);
            Assert.That(input.Steering, Is.GreaterThan(0.2f), "Dragging the held steering finger across centre must activate the opposite direction.");
            Assert.That(input.DriftStrength, Is.GreaterThan(0.1f), "The new drift direction should build strength while the pointer remains held.");

            zone.OnPointerUp(pointer);
            yield return new WaitForSeconds(0.15f);
            Assert.That(Mathf.Abs(input.Steering), Is.LessThan(0.2f));
            Object.Destroy(zoneObject);
            Object.Destroy(inputObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator ServiceBrakeSlowsTheCarWithoutTriggeringDriftControl()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);

            var car = new GameObject("ServiceBrakeTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("BrakeZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.linearVelocity = Vector3.forward * 16f;
            var input = car.AddComponent<TestInputSource>();
            input.BrakeValue = true;
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));
            controller.SetPropulsionEnabled(false);

            for (var i = 0; i < 45; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.Brake, Is.True);
            Assert.That(controller.Telemetry.Speed, Is.LessThan(4f));
            Assert.That(controller.Telemetry.ControlDriftStrength, Is.Zero.Within(0.001f));
            Assert.That(controller.Telemetry.Drifting, Is.False);
            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator SpeedSensitiveAxleSteeringTurnsWithoutPivoting()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);

            var car = new GameObject("AxleSteeringTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("AxleSteeringZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.linearVelocity = Vector3.forward * 20f;
            var input = car.AddComponent<TestInputSource>();
            input.SteeringValue = 1f;
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));
            controller.SetPropulsionEnabled(false);
            var startPosition = car.transform.position;
            var startForward = car.transform.forward;

            for (var i = 0; i < 45; i++) yield return new WaitForFixedUpdate();

            Assert.That(controller.Telemetry.SteeringAngleDegrees, Is.LessThan(profile.lowSpeedSteeringAngle));
            Assert.That(Mathf.Abs(controller.Telemetry.SteeringAngleDegrees), Is.GreaterThan(20f), "Full input must retain useful steering authority at road speed.");
            Assert.That(Mathf.Abs(controller.Telemetry.YawRateDegrees), Is.GreaterThan(5.5f));
            Assert.That(Vector3.Angle(startForward, car.transform.forward), Is.GreaterThan(8f), "A sustained input must keep rotating the car instead of plateauing.");
            Assert.That(Vector3.Distance(startPosition, car.transform.position), Is.GreaterThan(3f), "Steering must turn a moving car rather than pivoting it in place.");
            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator SteeringReversalCanSaturateRearGripWithoutBrake()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);

            var car = new GameObject("FeintTestCar");
            car.transform.position = new Vector3(0f, 0.7f, 0f);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 4f);
            collider.material = new PhysicsMaterial("FeintZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.linearVelocity = Vector3.forward * 16f;
            var input = car.AddComponent<TestInputSource>();
            input.SteeringValue = -1f;
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            var controller = car.AddComponent<VehicleController>();
            controller.Configure(body, input, input, profile, 1 << 0, new Vector3(0f, -0.3f, 0f));
            controller.SetPropulsionEnabled(false);

            for (var i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            input.SteeringValue = 1f;
            var maximumRearSlip = 0f;
            for (var i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                maximumRearSlip = Mathf.Max(maximumRearSlip, Mathf.Abs(controller.Telemetry.RearSlipAngle));
            }

            Assert.That(controller.Telemetry.Brake, Is.False);
            Assert.That(maximumRearSlip, Is.GreaterThan(profile.rearPeakSlipAngle), "A high-speed steering reversal should be able to saturate rear grip without a discrete drift trigger.");
            Object.Destroy(car);
            Object.Destroy(ground);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator PlayerTraversesPortraitCourseThenResetsToRoad()
        {
            SceneManager.LoadScene("DrivingSandbox");
            yield return null;
            var controller = Object.FindFirstObjectByType<VehicleController>();
            var recovery = Object.FindFirstObjectByType<VehicleRecovery>();
            var graph = Object.FindFirstObjectByType<RoadGraphComponent>().Graph;
            var routeIds = new[] { "Course_Straight", "Course_LeftEntry", "Course_LeftArc", "Course_Crossover" };
            controller.Body.isKinematic = true;
            var surfaceNormal = Vector3.up;
            var surfaceRotation = Quaternion.identity;
            var safePosition = Vector3.zero;
            foreach (var routeId in routeIds)
            {
                var edge = System.Linq.Enumerable.First(graph.Edges, candidate => candidate.Id == routeId);
                for (var segment = 1; segment < edge.Centerline.Count; segment++)
                {
                    var start = edge.Centerline[segment - 1];
                    var end = edge.Centerline[segment];
                    var direction = (end - start).normalized;
                    var right = Vector3.Cross(Vector3.up, direction).normalized;
                    surfaceNormal = Vector3.Cross(direction, right).normalized;
                    surfaceRotation = Quaternion.LookRotation(direction, surfaceNormal);
                    for (var step = 0; step <= 8; step++)
                    {
                        safePosition = Vector3.Lerp(start, end, step / 8f);
                        controller.Body.position = safePosition + surfaceNormal * 0.65f;
                        controller.Body.rotation = surfaceRotation;
                        Physics.SyncTransforms();
                        Assert.That(
                            Physics.Raycast(safePosition + surfaceNormal * 1.5f, -surfaceNormal, out _, 3f, 1 << 0, QueryTriggerInteraction.Ignore),
                            Is.True,
                            $"{routeId} must provide continuous solid support at sample {segment}:{step}.");
                        yield return new WaitForFixedUpdate();
                    }
                }
            }

            typeof(VehicleRecovery)
                .GetMethod("SetSafePoseForTests", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(recovery, new object[] { safePosition, surfaceRotation });
            controller.Body.isKinematic = false;
            controller.Body.position = Vector3.up * 25f;
            controller.Body.rotation = Quaternion.identity;
            controller.Body.linearVelocity = new Vector3(12f, -4f, 2f);

            var emergencyFuelProfile = ScriptableObject.CreateInstance<FuelProfile>();
            emergencyFuelProfile.startingFuel = 0f;
            emergencyFuelProfile.emergencyResetFuel = 15f;
            var emergencyResourcesObject = new GameObject("EmergencyResetFuelTest");
            var emergencyResources = emergencyResourcesObject.AddComponent<RideResourceController>();
            emergencyResources.Configure(controller, null, emergencyFuelProfile);
            Assert.That(emergencyResources.Snapshot.IsEmpty, Is.True);

            Assert.That(recovery.ResetToRoad(), Is.True);
            Assert.That(controller.Body.position.y, Is.GreaterThan(safePosition.y).And.LessThan(2f));
            Assert.That(Vector3.Angle(controller.Body.rotation * Vector3.up, surfaceNormal), Is.LessThan(0.1f));
            Assert.That(controller.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(emergencyResources.Snapshot.Fuel, Is.EqualTo(15f), "Road reset must rescue an empty tank from a hard lock.");
            Object.Destroy(emergencyResourcesObject);
            Object.Destroy(emergencyFuelProfile);
        }

        [UnityTest]
        public IEnumerator PortraitCourseHasOpenEndsSolidSidesAndNoStuntRamps()
        {
            SceneManager.LoadScene("DrivingSandbox");
            yield return null;

            var graph = Object.FindFirstObjectByType<RoadGraphComponent>().Graph;
            foreach (var nodeId in new[] { "COURSE_START", "COURSE_FINISH" })
            {
                Assert.That(graph.TryGetNode(nodeId, out var node), Is.True);
                var nearby = Physics.OverlapSphere(node.Position + Vector3.up * 0.6f, 2.2f, 1 << 13, QueryTriggerInteraction.Ignore);
                Assert.That(nearby, Is.Empty, $"{nodeId} must remain open for entering or finishing the run.");
            }

            Assert.That(Object.FindObjectsByType<StuntRamp>(FindObjectsSortMode.None), Is.Empty);
            var boundaries = GameObject.Find("GroundRoadBoundaries");
            Assert.That(boundaries, Is.Not.Null);
            Assert.That(boundaries.GetComponent<MeshCollider>().isTrigger, Is.False);
        }

        [UnityTest]
        public IEnumerator TrafficAndPoliceFollowPortraitCourseCenterline()
        {
            SceneManager.LoadScene("DrivingSandbox");
            yield return null;
            var graph = Object.FindFirstObjectByType<RoadGraphComponent>().Graph;
            var courseEdge = System.Array.Find(System.Linq.Enumerable.ToArray(graph.Edges), edge => edge.Id == "Course_Crossover");
            Assert.That(courseEdge, Is.Not.Null);

            var trafficObject = new GameObject("ElevatedTrafficTest");
            var trafficBody = trafficObject.AddComponent<Rigidbody>();
            trafficBody.isKinematic = true;
            var trafficProfile = ScriptableObject.CreateInstance<TrafficProfile>();
            var traffic = trafficObject.AddComponent<TrafficVehicle>();
            traffic.Configure(trafficBody, graph, trafficProfile, null);
            traffic.PlaceOnEdge(courseEdge, true, 0, 0.05f);

            var policeObject = new GameObject("ElevatedPoliceTest");
            var policeBody = policeObject.AddComponent<Rigidbody>();
            policeBody.isKinematic = true;
            graph.TryGetNode("COURSE_03", out var courseStart);
            policeBody.position = courseStart.Position + Vector3.up * 0.65f;
            var policeProfile = ScriptableObject.CreateInstance<PoliceProfile>();
            var police = policeObject.AddComponent<PoliceVehicleController>();
            police.Configure(policeBody, graph, policeProfile, new[] { "COURSE_04" });

            for (var i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

            Assert.That(trafficBody.position.y, Is.InRange(0.4f, 1.2f), "Traffic should remain aligned to the flat portrait course.");
            Assert.That(Vector3.Angle(trafficBody.rotation * Vector3.forward, Vector3.ProjectOnPlane(traffic.Velocity, trafficBody.rotation * Vector3.right).normalized), Is.LessThan(5f));
            Assert.That(policeBody.position.y, Is.InRange(0.4f, 1.2f), "Police should remain aligned to the flat portrait course.");
            Assert.That(police.WaypointCount, Is.GreaterThan(0));

            Object.Destroy(trafficObject);
            Object.Destroy(policeObject);
            Object.Destroy(trafficProfile);
            Object.Destroy(policeProfile);
        }

        [UnityTest]
        public IEnumerator BothCameraModesResolveOverpassObstructions()
        {
            var target = new GameObject("CameraObstructionTarget");
            target.transform.position = new Vector3(1000f, 0f, 1000f);
            var targetBody = target.AddComponent<Rigidbody>();
            targetBody.isKinematic = true;
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "TestOverpass";
            obstacle.transform.position = target.transform.position + new Vector3(0f, 9f, -2f);
            obstacle.transform.localScale = new Vector3(8f, 12f, 8f);
            var profile = ScriptableObject.CreateInstance<ChaseCameraProfile>();
            profile.overheadDamping = 100f;
            profile.overheadRotationDamping = 100f;
            profile.positionDamping = 100f;
            profile.rotationDamping = 100f;
            var cameraObject = new GameObject("ObstructionTestCamera");
            cameraObject.transform.position = target.transform.position;
            cameraObject.AddComponent<UnityEngine.Camera>();
            var cameraController = cameraObject.AddComponent<ChaseCameraController>();
            cameraController.Configure(target.transform, targetBody, profile, 1 << obstacle.layer);

            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(cameraObject.transform.position.y - target.transform.position.y, Is.LessThan(profile.overheadHeight - 4f), "Overhead view should pull in before the overpass.");

            cameraController.ToggleViewMode();
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(cameraObject.transform.position.z - target.transform.position.z, Is.GreaterThan(-profile.distance + 3f), "Chase view should pull in before the overpass.");
            Assert.That(obstacle.GetComponent<Collider>().bounds.Contains(cameraObject.transform.position), Is.False);

            Object.Destroy(cameraObject);
            Object.Destroy(obstacle);
            Object.Destroy(target);
            Object.Destroy(profile);
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
            public float DriftStrengthValue { get; set; }
            public bool BrakeValue { get; set; }
            public float Steering => SteeringValue;
            public float DriftStrength => DriftStrengthValue;
            public bool Brake => BrakeValue;
            public bool ConsumeResetRequest() => false;
        }
    }
}
