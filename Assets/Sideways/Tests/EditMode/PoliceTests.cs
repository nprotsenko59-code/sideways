using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Police;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class PoliceTests
    {
        private PoliceProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<PoliceProfile>();
            profile.lostSightDelay = 1f;
            profile.searchDuration = 10f;
            profile.escapedDisplayDuration = 1f;
            profile.minimumEscapeSpeed = 2f;
            profile.minimumEscapeDistance = 35f;
            profile.escapeProgressDecay = 2f;
            profile.predictionHorizon = 5f;
            profile.searchWaypointInterval = 3.5f;
            profile.searchRadius = 55f;
            profile.bustedDistance = 4f;
            profile.bustedMaximumPlayerSpeed = 5f;
            profile.bustedHoldDuration = 1f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void DefaultProfileIsValid()
        {
            Assert.That(profile.IsValid(out var error), Is.True, error);
        }

        [Test]
        public void VisionUsesRangeAndForwardCone()
        {
            Assert.That(PoliceMath.IsWithinVision(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 20f), 30f, 90f), Is.True);
            Assert.That(PoliceMath.IsWithinVision(Vector3.zero, Vector3.forward, new Vector3(20f, 0f, 0f), 30f, 90f), Is.False);
            Assert.That(PoliceMath.IsWithinVision(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 40f), 30f, 90f), Is.False);
        }

        [Test]
        public void SolidObstacleBlocksSensorSighting()
        {
            var police = new GameObject("PoliceSensorTest");
            var target = new GameObject("VisionTarget");
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                target.transform.position = new Vector3(0f, 0f, 20f);
                obstacle.name = "VisionOccluder";
                obstacle.layer = 8;
                obstacle.transform.position = new Vector3(0f, 1f, 10f);
                obstacle.transform.localScale = new Vector3(5f, 5f, 2f);
                profile.visionRange = 30f;
                profile.visionAngle = 90f;
                var sensor = police.AddComponent<PoliceVisionSensor>();
                sensor.Configure(target.transform, profile, 1 << 8);
                Physics.SyncTransforms();
                Assert.That(sensor.ScanNow(), Is.False);

                obstacle.SetActive(false);
                Physics.SyncTransforms();
                Assert.That(sensor.ScanNow(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(police);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(obstacle);
            }
        }

        [Test]
        public void RecklessDrivingMustBeVisibleToStartPursuit()
        {
            var model = new PolicePursuitModel(profile);
            model.Tick(false, true, new Vector3(9f, 0f, 4f), 12f, 20f, 1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Patrol));
            Assert.That(model.HasLastSeen, Is.False);

            model.Tick(true, true, new Vector3(9f, 0f, 4f), 12f, 20f, 0.1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Pursuit));
            Assert.That(model.LastSeenPosition, Is.EqualTo(new Vector3(9f, 0f, 4f)));
        }

        [Test]
        public void HiddenMovementDoesNotUpdateLastSeenAndMovingEscapeCompletes()
        {
            var model = BeginPursuit();
            var seen = model.LastSeenPosition;
            model.Tick(false, false, new Vector3(100f, 0f, 100f), 10f, 30f, 1.1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Search));
            Assert.That(model.LastSeenPosition, Is.EqualTo(seen));

            model.Tick(false, false, new Vector3(200f, 0f, 200f), 10f, 40f, 10.1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Escaped));
            Assert.That(model.LastSeenPosition, Is.EqualTo(seen));
        }

        [Test]
        public void StationaryHidingDoesNotCompleteEscape()
        {
            var model = BeginPursuit();
            model.Tick(false, false, Vector3.zero, 0f, 30f, 1.1f);
            model.Tick(false, false, Vector3.zero, 0f, 30f, 30f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Search));
        }

        [Test]
        public void VisualReacquisitionResumesPursuit()
        {
            var model = BeginPursuit();
            model.Tick(false, false, Vector3.zero, 8f, 20f, 1.1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Search));
            model.Tick(true, false, new Vector3(3f, 0f, 7f), 8f, 20f, 0.1f);
            Assert.That(model.State, Is.EqualTo(PoliceState.Pursuit));
            Assert.That(model.LastSeenPosition, Is.EqualTo(new Vector3(3f, 0f, 7f)));
        }

        [Test]
        public void HiddenPursuitPredictsPlayerTravelFromLastSeenVelocity()
        {
            var model = new PolicePursuitModel(profile);
            model.Tick(true, true, new Vector3(1f, 0f, 2f), new Vector3(0f, 0f, 10f), 10f, 20f, 0.1f);
            model.Tick(false, false, Vector3.zero, Vector3.zero, 10f, 60f, 1.1f);

            Assert.That(model.State, Is.EqualTo(PoliceState.Search));
            Assert.That(model.EstimatedPlayerPosition.x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(model.EstimatedPlayerPosition.z, Is.EqualTo(13f).Within(0.01f));
        }

        [Test]
        public void NearbyPoliceReversesEscapeProgress()
        {
            profile.minimumEscapeDistance = 50f;
            var model = BeginPursuit();
            model.Tick(false, false, Vector3.zero, 10f, 60f, 1.1f);
            model.Tick(false, false, Vector3.zero, 10f, 80f, 5f);
            var remainingAfterGetaway = model.SearchRemaining;

            model.Tick(false, false, Vector3.zero, 10f, 20f, 2f);

            Assert.That(model.State, Is.EqualTo(PoliceState.Search));
            Assert.That(model.SearchRemaining, Is.GreaterThan(remainingAfterGetaway));
        }

        [Test]
        public void SearchAdvancesAcrossRoadSweepWaypoints()
        {
            var model = BeginPursuit();
            model.Tick(false, false, Vector3.zero, 10f, 60f, 1.1f);
            Assert.That(model.SearchWaypointIndex, Is.Zero);

            model.Tick(false, false, Vector3.zero, 10f, 60f, 3.6f);
            Assert.That(model.SearchWaypointIndex, Is.EqualTo(1));
        }

        [Test]
        public void BustRequiresSustainedCloseSlowVisualContact()
        {
            var model = BeginPursuit();
            var brief = model.Tick(true, false, Vector3.zero, 2f, 3f, 0.5f);
            Assert.That(brief.Busted, Is.False);
            model.Tick(true, false, Vector3.zero, 9f, 3f, 0.1f);
            var first = model.Tick(true, false, Vector3.zero, 2f, 3f, 0.6f);
            var second = model.Tick(true, false, Vector3.zero, 2f, 3f, 0.5f);
            Assert.That(first.Busted, Is.False);
            Assert.That(second.Busted, Is.True);
            Assert.That(model.State, Is.EqualTo(PoliceState.Patrol));
        }

        private PolicePursuitModel BeginPursuit()
        {
            var model = new PolicePursuitModel(profile);
            model.Tick(true, true, new Vector3(1f, 0f, 2f), 10f, 20f, 0.1f);
            return model;
        }
    }
}
