using System;
using System.Collections.Generic;
using Sideways.Configuration;
using Sideways.EnvironmentSystem;
using UnityEngine;

namespace Sideways.Police
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PoliceVehicleController : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private RoadGraphAsset roadGraph;
        [SerializeField] private PoliceProfile profile;
        [SerializeField] private string[] patrolNodeIds = { "G_NE", "G_E", "G_SE", "B_SE", "G_SC", "G_C", "G_NC" };
        [SerializeField] private bool operational = true;

        private readonly List<RoadGraphEdge> routeEdges = new();
        private readonly List<Vector3> waypoints = new();
        private readonly List<RoadGraphNode> searchNodes = new();
        private PoliceState state;
        private string currentGoalNodeId;
        private int waypointIndex;
        private int patrolIndex;
        private float speed;
        private float nextReplanTime;
        private bool hasDestination;
        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        public PoliceState State => state;
        public int WaypointCount => waypoints.Count;
        public bool IsOperational => operational;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            EnsurePresentationCache();
            ApplyOperationalPresentation();
        }

        private void FixedUpdate()
        {
            if (!operational || body == null || roadGraph == null || profile == null) return;

            if (state == PoliceState.Patrol && (!hasDestination || waypointIndex >= waypoints.Count))
            {
                RequestNextPatrolNode();
            }

            if (!hasDestination || waypointIndex >= waypoints.Count)
            {
                speed = Mathf.MoveTowards(speed, 0f, profile.acceleration * Time.fixedDeltaTime);
                return;
            }

            var target = waypoints[waypointIndex];
            var delta = target - body.position;
            if (delta.sqrMagnitude <= profile.waypointTolerance * profile.waypointTolerance)
            {
                waypointIndex++;
                if (waypointIndex >= waypoints.Count)
                {
                    hasDestination = false;
                    return;
                }
                target = waypoints[waypointIndex];
                delta = target - body.position;
            }

            var desiredDirection = delta.normalized;
            if (desiredDirection.sqrMagnitude < 0.001f) return;
            var desiredRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            var rotation = Quaternion.RotateTowards(body.rotation, desiredRotation, profile.turnRate * Time.fixedDeltaTime);
            var desiredSpeed = state == PoliceState.Pursuit ? profile.pursuitSpeed : state == PoliceState.Search ? profile.searchSpeed : profile.patrolSpeed;
            speed = Mathf.MoveTowards(speed, desiredSpeed, profile.acceleration * Time.fixedDeltaTime);
            var alignment = Mathf.Clamp01((Vector3.Dot(rotation * Vector3.forward, desiredDirection) + 0.2f) / 1.2f);
            body.MoveRotation(rotation);
            body.MovePosition(body.position + (rotation * Vector3.forward) * (speed * alignment * Time.fixedDeltaTime));
        }

        public void Configure(Rigidbody rigidbody, RoadGraphAsset graph, PoliceProfile policeProfile, string[] patrolNodes = null)
        {
            body = rigidbody;
            roadGraph = graph;
            profile = policeProfile;
            if (patrolNodes != null && patrolNodes.Length > 0) patrolNodeIds = patrolNodes;
        }

        public void SetState(PoliceState newState)
        {
            if (state == newState) return;
            state = newState;
            hasDestination = false;
            nextReplanTime = 0f;
        }

        public void SetOperational(bool value)
        {
            if (operational == value) return;
            operational = value;
            speed = 0f;
            hasDestination = false;
            nextReplanTime = 0f;
            EnsurePresentationCache();
            ApplyOperationalPresentation();
        }

        public void SetDestination(Vector3 destination, bool force = false)
        {
            if (!force && Time.time < nextReplanTime) return;
            nextReplanTime = Time.time + profile.routeReplanInterval;
            BuildRoute(destination, force);
        }

        public void SetSearchDestination(Vector3 estimatedPosition, int searchIndex)
        {
            if (roadGraph == null || profile == null) return;
            var anchor = roadGraph.FindNearestNode(estimatedPosition, RoadAccess.Police);
            if (anchor == null) return;

            searchNodes.Clear();
            var radiusSquared = profile.searchRadius * profile.searchRadius;
            for (var i = 0; i < roadGraph.Nodes.Count; i++)
            {
                var node = roadGraph.Nodes[i];
                if ((node.Position - anchor.Position).sqrMagnitude <= radiusSquared && roadGraph.FindNearestNode(node.Position, RoadAccess.Police) == node) searchNodes.Add(node);
            }

            searchNodes.Sort((left, right) =>
            {
                var leftDistance = (left.Position - anchor.Position).sqrMagnitude;
                var rightDistance = (right.Position - anchor.Position).sqrMagnitude;
                var distanceComparison = leftDistance.CompareTo(rightDistance);
                return distanceComparison != 0 ? distanceComparison : string.CompareOrdinal(left.Id, right.Id);
            });

            var target = searchNodes.Count > 0 ? searchNodes[Mathf.Abs(searchIndex) % searchNodes.Count] : anchor;
            SetDestination(target.Position, true);
        }

        private void RequestNextPatrolNode()
        {
            if (patrolNodeIds == null || patrolNodeIds.Length == 0) return;
            var nodeId = patrolNodeIds[patrolIndex++ % patrolNodeIds.Length];
            if (roadGraph.TryGetNode(nodeId, out var node)) SetDestination(node.Position, true);
        }

        private void BuildRoute(Vector3 destination, bool force)
        {
            var start = roadGraph.FindNearestNode(body.position, RoadAccess.Police);
            var goal = roadGraph.FindNearestNode(destination, RoadAccess.Police);
            if (!force && goal != null && goal.Id == currentGoalNodeId && hasDestination && waypointIndex < waypoints.Count) return;

            waypoints.Clear();
            waypointIndex = 0;
            currentGoalNodeId = goal?.Id;
            if (start == null || goal == null || !roadGraph.TryFindRoute(start.Id, goal.Id, routeEdges, RoadAccess.Police))
            {
                waypoints.Add(destination);
                hasDestination = true;
                return;
            }

            var currentNodeId = start.Id;
            for (var edgeIndex = 0; edgeIndex < routeEdges.Count; edgeIndex++)
            {
                var edge = routeEdges[edgeIndex];
                var forward = string.Equals(edge.FromNodeId, currentNodeId, StringComparison.Ordinal);
                if (forward)
                {
                    for (var i = 1; i < edge.Centerline.Count; i++) waypoints.Add(edge.Centerline[i] + Vector3.up * 0.65f);
                    currentNodeId = edge.ToNodeId;
                }
                else
                {
                    for (var i = edge.Centerline.Count - 2; i >= 0; i--) waypoints.Add(edge.Centerline[i] + Vector3.up * 0.65f);
                    currentNodeId = edge.FromNodeId;
                }
            }

            if (waypoints.Count == 0) waypoints.Add(goal.Position + Vector3.up * 0.65f);
            hasDestination = true;
        }

        private void EnsurePresentationCache()
        {
            cachedRenderers ??= GetComponentsInChildren<Renderer>(true);
            cachedColliders ??= GetComponentsInChildren<Collider>(true);
        }

        private void ApplyOperationalPresentation()
        {
            if (cachedRenderers != null)
            {
                for (var i = 0; i < cachedRenderers.Length; i++) cachedRenderers[i].enabled = operational;
            }
            if (cachedColliders != null)
            {
                for (var i = 0; i < cachedColliders.Length; i++) cachedColliders[i].enabled = operational;
            }
        }
    }
}
