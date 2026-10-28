using Sideways.Configuration;
using Sideways.EnvironmentSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Sideways.Traffic
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private RoadGraphAsset roadGraph;
        [SerializeField] private TrafficProfile profile;
        [SerializeField] private NearMissEncounter nearMiss;

        private RoadGraphEdge edge;
        private bool forward;
        private int waypointIndex;
        private float speed;
        private Vector3 velocity;
        private string claimedJunctionId;
        private Vector3 claimedJunctionPosition;
        private float respawnAvailableTime;
        private bool wrecked;
        private readonly List<RoadGraphEdge> junctionChoices = new();

        public Vector3 Velocity => velocity;
        public bool IsWrecked => wrecked;
        public bool CanRespawn => !wrecked || Time.time >= respawnAvailableTime;

        private void Awake() => body ??= GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            if (body == null || edge == null || profile == null) return;
            ReleaseClearedJunction();
            var target = GetLanePoint(waypointIndex);
            var delta = target - body.position;
            if (delta.sqrMagnitude <= profile.waypointTolerance * profile.waypointTolerance)
            {
                if (!AdvanceWaypoint()) return;
                target = GetLanePoint(waypointIndex);
                delta = target - body.position;
            }

            var direction = delta.normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            var blocked = Physics.SphereCast(body.position + Vector3.up * 0.6f, 0.8f, transform.forward, out _, profile.obstacleLookAhead, (1 << 6) | (1 << 8) | (1 << 9) | (1 << 10), QueryTriggerInteraction.Ignore);
            if (!HasJunctionPriority()) blocked = true;
            var desiredSpeed = blocked ? 0f : Mathf.Clamp(edge.SpeedLimitMps * 0.75f, profile.minimumSpeed, profile.maximumSpeed);
            speed = Mathf.MoveTowards(speed, desiredSpeed, profile.maximumSpeed * Time.fixedDeltaTime);
            var rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction, Vector3.up), profile.turnRate * Time.fixedDeltaTime);
            velocity = rotation * Vector3.forward * speed;
            body.MoveRotation(rotation);
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer != 6) return;
            nearMiss?.NotifyCollision();
            var impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed >= profile.destructionImpactSpeed) DestroyByImpact(impactSpeed);
        }

        private void OnDisable() => ReleaseJunction();

        public void Configure(Rigidbody rigidbody, RoadGraphAsset graph, TrafficProfile trafficProfile, NearMissEncounter encounter)
        {
            body = rigidbody;
            roadGraph = graph;
            profile = trafficProfile;
            nearMiss = encounter;
            nearMiss?.Configure(this, profile);
        }

        public void PlaceOnEdge(RoadGraphEdge startEdge, bool travelForward, int segmentIndex, float segmentT)
        {
            ReleaseJunction();
            edge = startEdge;
            forward = travelForward;
            segmentIndex = Mathf.Clamp(segmentIndex, 0, edge.Centerline.Count - 2);
            var a = edge.Centerline[segmentIndex];
            var b = edge.Centerline[segmentIndex + 1];
            var direction = forward ? (b - a).normalized : (a - b).normalized;
            var center = Vector3.Lerp(a, b, segmentT);
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var roadUp = Vector3.Cross(direction, right).normalized;
            body.position = center + right * profile.laneOffset + roadUp * 0.65f;
            body.rotation = Quaternion.LookRotation(direction, roadUp);
            waypointIndex = forward ? segmentIndex + 1 : segmentIndex;
            speed = Random.Range(profile.minimumSpeed, profile.maximumSpeed);
            velocity = direction * speed;
            wrecked = false;
            gameObject.SetActive(true);
        }

        public bool DestroyByImpact(float impactSpeed)
        {
            if (wrecked || profile == null || impactSpeed < profile.destructionImpactSpeed) return false;
            wrecked = true;
            respawnAvailableTime = Time.time + profile.wreckRespawnDelay;
            velocity = Vector3.zero;
            speed = 0f;
            ReleaseJunction();
            TrafficEvents.RaiseVehicleDestroyed(new TrafficVehicleDestroyedEvent(name, impactSpeed, transform.position));
            gameObject.SetActive(false);
            return true;
        }

        private bool AdvanceWaypoint()
        {
            waypointIndex += forward ? 1 : -1;
            if (waypointIndex >= 0 && waypointIndex < edge.Centerline.Count) return true;

            var nodeId = forward ? edge.ToNodeId : edge.FromNodeId;
            junctionChoices.Clear();
            for (var i = 0; i < roadGraph.Edges.Count; i++)
            {
                var candidate = roadGraph.Edges[i];
                if (candidate == edge || !candidate.Supports(RoadAccess.Traffic)) continue;
                if (candidate.FromNodeId == nodeId || (candidate.Bidirectional && candidate.ToNodeId == nodeId))
                {
                    junctionChoices.Add(candidate);
                }
            }
            var next = junctionChoices.Count > 0 ? junctionChoices[Random.Range(0, junctionChoices.Count)] : null;
            if (next == null) return false;
            edge = next;
            forward = edge.FromNodeId == nodeId;
            waypointIndex = forward ? 1 : edge.Centerline.Count - 2;
            return true;
        }

        private Vector3 GetLanePoint(int index)
        {
            index = Mathf.Clamp(index, 0, edge.Centerline.Count - 1);
            var point = edge.Centerline[index];
            var adjacent = forward
                ? edge.Centerline[Mathf.Max(0, index - 1)]
                : edge.Centerline[Mathf.Min(edge.Centerline.Count - 1, index + 1)];
            var direction = forward ? (point - adjacent).normalized : (point - adjacent).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var roadUp = Vector3.Cross(direction, right).normalized;
            return point + right * profile.laneOffset + roadUp * 0.65f;
        }

        private bool HasJunctionPriority()
        {
            var finalWaypoint = forward ? edge.Centerline.Count - 1 : 0;
            if (waypointIndex != finalWaypoint) return true;
            var junctionId = forward ? edge.ToNodeId : edge.FromNodeId;
            var junctionPosition = GetLanePoint(finalWaypoint);
            if ((body.position - junctionPosition).sqrMagnitude > profile.intersectionYieldDistance * profile.intersectionYieldDistance) return true;
            if (!TrafficJunctionCoordinator.TryClaim(junctionId, GetHashCode())) return false;
            claimedJunctionId = junctionId;
            claimedJunctionPosition = junctionPosition;
            return true;
        }

        private void ReleaseClearedJunction()
        {
            if (string.IsNullOrEmpty(claimedJunctionId)) return;
            if ((body.position - claimedJunctionPosition).sqrMagnitude <= profile.intersectionYieldDistance * profile.intersectionYieldDistance) return;
            ReleaseJunction();
        }

        private void ReleaseJunction()
        {
            if (string.IsNullOrEmpty(claimedJunctionId)) return;
            TrafficJunctionCoordinator.Release(claimedJunctionId, GetHashCode());
            claimedJunctionId = null;
        }
    }
}
