using NUnit.Framework;
using Sideways.Challenges;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Traffic;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class ChallengeTrafficTests
    {
        [Test]
        public void DestroySignsChallengeCountsEachSignOnlyOnce()
        {
            var definition = CreateChallenge(ChallengeType.DestroyRoadSigns, 2, 60f);
            var model = new ChallengeModel();
            model.Start(definition);

            Assert.That(model.RecordRoadSign("sign-a"), Is.True);
            Assert.That(model.RecordRoadSign("sign-a"), Is.False);
            Assert.That(model.Snapshot.Progress, Is.EqualTo(1));
            Assert.That(model.RecordRoadSign("sign-b"), Is.True);
            Assert.That(model.State, Is.EqualTo(ChallengeState.Succeeded));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void ChallengeTimeoutFailsWithoutEndingAnythingElse()
        {
            var definition = CreateChallenge(ChallengeType.DestroyRoadSigns, 10, 1f);
            var model = new ChallengeModel();
            model.Start(definition);
            model.Tick(1.1f);
            Assert.That(model.State, Is.EqualTo(ChallengeState.Failed));
            Assert.That(model.Snapshot.TimeRemaining, Is.Zero);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void DriftChallengeUsesOnlyPositiveDriftProgress()
        {
            var definition = CreateChallenge(ChallengeType.EarnDriftScore, 100, 60f);
            var model = new ChallengeModel();
            model.Start(definition);
            Assert.That(model.RecordDriftScore(40), Is.True);
            Assert.That(model.RecordDriftScore(0), Is.False);
            Assert.That(model.RecordRoadSign("sign-a"), Is.False);
            model.RecordDriftScore(60);
            Assert.That(model.State, Is.EqualTo(ChallengeState.Succeeded));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void CancelledChallengeCanBeRestartedFresh()
        {
            var definition = CreateChallenge(ChallengeType.DestroyRoadSigns, 2, 60f);
            var model = new ChallengeModel();
            model.Start(definition);
            model.RecordRoadSign("sign-a");
            model.Cancel();
            Assert.That(model.State, Is.EqualTo(ChallengeState.Cancelled));
            model.Start(definition);
            Assert.That(model.State, Is.EqualTo(ChallengeState.Active));
            Assert.That(model.Snapshot.Progress, Is.Zero);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void BankedBonusDoesNotAlterActiveCombo()
        {
            var scoring = ScriptableObject.CreateInstance<ScoringProfile>();
            var model = new DriftScoringModel(scoring);
            Assert.That(model.AwardBankedBonus(250), Is.True);
            Assert.That(model.Snapshot.BankedScore, Is.EqualTo(250));
            Assert.That(model.Snapshot.ActiveComboScore, Is.Zero);
            Assert.That(model.AwardBankedBonus(0), Is.False);
            Object.DestroyImmediate(scoring);
        }

        [Test]
        public void TrafficSpawnRejectsNearAndForwardPositions()
        {
            var player = new GameObject("SpawnRulePlayer");
            var spawnerObject = new GameObject("SpawnRuleSpawner");
            var profile = ScriptableObject.CreateInstance<TrafficProfile>();
            var spawner = spawnerObject.AddComponent<TrafficSpawner>();
            spawner.Configure(null, null, profile, player.transform);
            player.transform.forward = Vector3.forward;

            Assert.That(spawner.IsSpawnPositionAllowed(Vector3.forward * 20f), Is.False);
            Assert.That(spawner.IsSpawnPositionAllowed(Vector3.forward * 90f), Is.False);
            Assert.That(spawner.IsSpawnPositionAllowed(Vector3.right * 90f), Is.True);
            Assert.That(spawner.IsSpawnPositionAllowed(Vector3.right * 190f), Is.False);

            Object.DestroyImmediate(player);
            Object.DestroyImmediate(spawnerObject);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void DefaultTrafficProfileIsValid()
        {
            var profile = ScriptableObject.CreateInstance<TrafficProfile>();
            Assert.That(profile.IsValid(out var error), Is.True, error);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void InvalidTrafficSpacingIsRejected()
        {
            var profile = ScriptableObject.CreateInstance<TrafficProfile>();
            profile.minimumVehicleSpacing = 0f;

            Assert.That(profile.IsValid(out _), Is.False);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void JunctionClaimAllowsOnlyOneTrafficVehicleAtATime()
        {
            TrafficJunctionCoordinator.Clear();
            Assert.That(TrafficJunctionCoordinator.TryClaim("G_C", 10), Is.True);
            Assert.That(TrafficJunctionCoordinator.TryClaim("G_C", 20), Is.False);
            TrafficJunctionCoordinator.Release("G_C", 10);
            Assert.That(TrafficJunctionCoordinator.TryClaim("G_C", 20), Is.True);
            TrafficJunctionCoordinator.Clear();
        }

        [Test]
        public void InvalidTrafficDamageSettingsAreRejected()
        {
            var profile = ScriptableObject.CreateInstance<TrafficProfile>();
            profile.destructionImpactSpeed = 0f;

            Assert.That(profile.IsValid(out _), Is.False);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void SeverePlayerImpactTemporarilyWrecksTrafficVehicle()
        {
            var profile = ScriptableObject.CreateInstance<TrafficProfile>();
            profile.destructionImpactSpeed = 10f;
            var trafficObject = new GameObject("DamageableTraffic");
            var body = trafficObject.AddComponent<Rigidbody>();
            var vehicle = trafficObject.AddComponent<TrafficVehicle>();
            vehicle.Configure(body, null, profile, null);
            var eventRaised = false;
            System.Action<TrafficVehicleDestroyedEvent> handler = destroyed => eventRaised = destroyed.VehicleId == "DamageableTraffic";
            TrafficEvents.VehicleDestroyed += handler;

            Assert.That(vehicle.DestroyByImpact(9f), Is.False);
            Assert.That(vehicle.DestroyByImpact(12f), Is.True);
            Assert.That(vehicle.IsWrecked, Is.True);
            Assert.That(trafficObject.activeSelf, Is.False);
            Assert.That(eventRaised, Is.True);

            TrafficEvents.VehicleDestroyed -= handler;
            Object.DestroyImmediate(trafficObject);
            Object.DestroyImmediate(profile);
        }

        private static ChallengeDefinition CreateChallenge(ChallengeType type, int target, float duration)
        {
            var definition = ScriptableObject.CreateInstance<ChallengeDefinition>();
            definition.Configure("test", "Test Challenge", type, duration, target, 100);
            return definition;
        }
    }
}
