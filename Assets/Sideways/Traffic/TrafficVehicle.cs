using Sideways.Configuration;
using Sideways.EnvironmentSystem;
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

        public Vector3 Velocity => velocity;

        private void Awake() => body ??= GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            if (body == null || edge == null || profile == null) return;
            var target = GetLanePoint(waypointIndex);
            target.y = body.position.y;
            var delta = target - body.position;
            if (delta.sqrMagnitude <= profile.waypointTolerance * profile.waypointTolerance)
            {
                if (!AdvanceWaypoint()) return;
                target = GetLanePoint(waypointIndex);
                target.y = body.position.y;
                delta = target - body.position;
            }

            var direction = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            var blocked = Physics.SphereCast(body.position + Vector3.up * 0.6f, 0.8f, transform.forward, out _, profile.obstacleLookAhead, (1 << 6) | (1 << 8) | (1 << 9) | (1 << 10), QueryTriggerInteraction.Ignore);
            var desiredSpeed = blocked ? 0f : Mathf.Clamp(edge.SpeedLimitMps * 0.75f, profile.minimumSpeed, profile.maximumSpeed);
            speed = Mathf.MoveTowards(speed, desiredSpeed, profile.maximumSpeed * Time.fixedDeltaTime);
            var rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction, Vector3.up), profile.turnRate * Time.fixedDeltaTime);
            velocity = rotation * Vector3.forward * speed;
            body.MoveRotation(rotation);
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer == 6) nearMiss?.NotifyCollision();
        }

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
            edge = startEdge;
            forward = travelForward;
            segmentIndex = Mathf.Clamp(segmentIndex, 0, edge.Centerline.Count - 2);
            var a = edge.Centerline[segmentIndex];
            var b = edge.Centerline[segmentIndex + 1];
            var direction = forward ? (b - a).normalized : (a - b).normalized;
            var center = Vector3.Lerp(a, b, segmentT);
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            body.position = center + right * profile.laneOffset + Vector3.up * 0.65f;
            body.rotation = Quaternion.LookRotation(direction, Vector3.up);
            waypointIndex = forward ? segmentIndex + 1 : segmentIndex;
            speed = Random.Range(profile.minimumSpeed, profile.maximumSpeed);
            velocity = direction * speed;
            gameObject.SetActive(true);
        }

        private bool AdvanceWaypoint()
        {
            waypointIndex += forward ? 1 : -1;
            if (waypointIndex >= 0 && waypointIndex < edge.Centerline.Count) return true;

            var nodeId = forward ? edge.ToNodeId : edge.FromNodeId;
            RoadGraphEdge next = null;
            for (var i = 0; i < roadGraph.Edges.Count; i++)
            {
                var candidate = roadGraph.Edges[i];
                if (candidate == edge) continue;
                if (candidate.FromNodeId == nodeId || (candidate.Bidirectional && candidate.ToNodeId == nodeId))
                {
                    next = candidate;
                    break;
                }
            }
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
            return point + Vector3.Cross(Vector3.up, direction).normalized * profile.laneOffset;
        }
    }
}
