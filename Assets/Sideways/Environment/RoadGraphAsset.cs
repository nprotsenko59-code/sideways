using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sideways.EnvironmentSystem
{
    [Flags]
    public enum RoadAccess
    {
        None = 0,
        Player = 1 << 0,
        Traffic = 1 << 1,
        Police = 1 << 2,
        All = Player | Traffic | Police
    }

    [Flags]
    public enum RoadFeature
    {
        Standard = 0,
        Elevated = 1 << 0,
        Ramp = 1 << 1,
        Rooftop = 1 << 2,
        Stunt = 1 << 3,
        Shortcut = 1 << 4,
        EscapeRoute = 1 << 5,
        Signature = 1 << 6,
        Interchange = 1 << 7,
        MainRoute = 1 << 8,
        ServiceRoute = 1 << 9,
        ComboRoute = 1 << 10
    }

    public enum RoadKind
    {
        Street,
        Boulevard,
        Alley,
        Scenic
    }

    public enum RoadNodeRole
    {
        Junction,
        Landmark,
        BoundaryLoop,
        CulDeSac
    }

    [Serializable]
    public sealed class RoadGraphNode
    {
        public RoadGraphNode(string id, Vector3 position, RoadNodeRole role = RoadNodeRole.Junction, bool allowsDeadEnd = false)
        {
            Id = id;
            Position = position;
            Role = role;
            AllowsDeadEnd = allowsDeadEnd;
        }

        public string Id;
        public Vector3 Position;
        public RoadNodeRole Role;
        public bool AllowsDeadEnd;
    }

    [Serializable]
    public sealed class RoadGraphEdge
    {
        public RoadGraphEdge(string id, string fromNodeId, string toNodeId, RoadKind kind, float width, float speedLimitMps, params Vector3[] centerline)
        {
            Id = id;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Kind = kind;
            Width = width;
            SpeedLimitMps = speedLimitMps;
            Bidirectional = true;
            Centerline = new List<Vector3>(centerline);
        }

        public string Id;
        public string FromNodeId;
        public string ToNodeId;
        public RoadKind Kind;
        public bool Bidirectional = true;
        public float Width = 14f;
        public float SpeedLimitMps = 16f;
        public RoadAccess Access = RoadAccess.All;
        public RoadFeature Features = RoadFeature.Standard;
        public List<Vector3> Centerline = new();

        public float Length
        {
            get
            {
                var length = 0f;
                if (Centerline == null) return length;
                for (var i = 1; i < Centerline.Count; i++) length += Vector3.Distance(Centerline[i - 1], Centerline[i]);
                return length;
            }
        }

        public float TravelTime => Length / Mathf.Max(0.1f, SpeedLimitMps);

        public bool Supports(RoadAccess requiredAccess) => requiredAccess == RoadAccess.None || (Access & requiredAccess) == requiredAccess;
    }

    [CreateAssetMenu(menuName = "Sideways/Road Graph", fileName = "RoadGraph")]
    public sealed class RoadGraphAsset : ScriptableObject
    {
        [SerializeField] private string graphId = "sunset-basin";
        [SerializeField] private Vector2 driveableSize = new(520f, 420f);
        [SerializeField] private List<RoadGraphNode> nodes = new();
        [SerializeField] private List<RoadGraphEdge> edges = new();

        public string GraphId => graphId;
        public Vector2 DriveableSize => driveableSize;
        public IReadOnlyList<RoadGraphNode> Nodes => nodes;
        public IReadOnlyList<RoadGraphEdge> Edges => edges;

        public void Configure(string id, Vector2 size, IEnumerable<RoadGraphNode> graphNodes, IEnumerable<RoadGraphEdge> graphEdges)
        {
            graphId = id;
            driveableSize = size;
            nodes = new List<RoadGraphNode>(graphNodes);
            edges = new List<RoadGraphEdge>(graphEdges);
        }

        public bool TryGetNode(string nodeId, out RoadGraphNode node)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (!string.Equals(nodes[i].Id, nodeId, StringComparison.Ordinal)) continue;
                node = nodes[i];
                return true;
            }

            node = null;
            return false;
        }

        public RoadGraphNode FindNearestNode(Vector3 position)
        {
            return FindNearestNode(position, RoadAccess.None);
        }

        public RoadGraphNode FindNearestNode(Vector3 position, RoadAccess requiredAccess)
        {
            RoadGraphNode nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (requiredAccess != RoadAccess.None && !HasAccessibleEdge(nodes[i].Id, requiredAccess)) continue;
                var distance = (nodes[i].Position - position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = nodes[i];
            }
            return nearest;
        }

        public int GetDegree(string nodeId)
        {
            var degree = 0;
            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i].FromNodeId == nodeId || edges[i].ToNodeId == nodeId) degree++;
            }
            return degree;
        }

        public bool TryFindRoute(string startNodeId, string goalNodeId, List<RoadGraphEdge> route, ISet<string> blockedEdgeIds = null)
        {
            return TryFindRoute(startNodeId, goalNodeId, route, RoadAccess.None, blockedEdgeIds);
        }

        public bool TryFindRoute(
            string startNodeId,
            string goalNodeId,
            List<RoadGraphEdge> route,
            RoadAccess requiredAccess,
            ISet<string> blockedEdgeIds = null)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            route.Clear();
            if (!TryGetNode(startNodeId, out _) || !TryGetNode(goalNodeId, out _)) return false;
            if (startNodeId == goalNodeId) return true;

            var frontier = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            var distances = new Dictionary<string, float>(StringComparer.Ordinal) { [startNodeId] = 0f };
            var previousNode = new Dictionary<string, string>(StringComparer.Ordinal);
            var previousEdge = new Dictionary<string, RoadGraphEdge>(StringComparer.Ordinal);

            while (frontier.Count > 0)
            {
                string current = null;
                var currentDistance = float.PositiveInfinity;
                foreach (var candidate in frontier)
                {
                    var candidateDistance = distances[candidate];
                    if (candidateDistance >= currentDistance) continue;
                    current = candidate;
                    currentDistance = candidateDistance;
                }
                frontier.Remove(current);
                if (current == goalNodeId)
                {
                    ReconstructRoute(startNodeId, goalNodeId, previousNode, previousEdge, route);
                    return true;
                }

                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (blockedEdgeIds != null && blockedEdgeIds.Contains(edge.Id)) continue;
                    if (!edge.Supports(requiredAccess)) continue;

                    string next = null;
                    if (edge.FromNodeId == current) next = edge.ToNodeId;
                    else if (edge.Bidirectional && edge.ToNodeId == current) next = edge.FromNodeId;
                    if (next == null) continue;

                    var nextDistance = currentDistance + edge.TravelTime;
                    if (distances.TryGetValue(next, out var knownDistance) && nextDistance >= knownDistance) continue;
                    distances[next] = nextDistance;
                    previousNode[next] = current;
                    previousEdge[next] = edge;
                    frontier.Add(next);
                }
            }

            return false;
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(graphId) || driveableSize.x <= 0f || driveableSize.y <= 0f)
            {
                error = "Graph identity and driveable dimensions are required.";
                return false;
            }

            if (nodes.Count < 2 || edges.Count < 2)
            {
                error = "A road graph requires at least two nodes and two edges.";
                return false;
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(nodes[i].Id) || !nodeIds.Add(nodes[i].Id))
                {
                    error = $"Node {i} has a missing or duplicate id.";
                    return false;
                }
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (string.IsNullOrWhiteSpace(edge.Id) || !edgeIds.Add(edge.Id))
                {
                    error = $"Edge {i} has a missing or duplicate id.";
                    return false;
                }
                if (!TryGetNode(edge.FromNodeId, out var from) || !TryGetNode(edge.ToNodeId, out var to) || edge.FromNodeId == edge.ToNodeId)
                {
                    error = $"Edge {edge.Id} has invalid endpoints.";
                    return false;
                }
                if (edge.Width < 4f || edge.SpeedLimitMps <= 0f || edge.Centerline == null || edge.Centerline.Count < 2)
                {
                    error = $"Edge {edge.Id} has invalid road geometry.";
                    return false;
                }
                if (Vector3.Distance(edge.Centerline[0], from.Position) > 0.25f || Vector3.Distance(edge.Centerline[^1], to.Position) > 0.25f)
                {
                    error = $"Edge {edge.Id} centerline does not meet its endpoint nodes.";
                    return false;
                }

                if (edge.Access == RoadAccess.None)
                {
                    error = $"Edge {edge.Id} must allow at least one road user.";
                    return false;
                }

                var maximumGrade = (edge.Features & RoadFeature.Stunt) != 0
                    ? 25f
                    : (edge.Features & RoadFeature.Rooftop) != 0 ? 12f : 10f;
                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    var delta = edge.Centerline[point] - edge.Centerline[point - 1];
                    var horizontal = new Vector2(delta.x, delta.z).magnitude;
                    var grade = Mathf.Atan2(Mathf.Abs(delta.y), Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg;
                    if (grade <= maximumGrade + 0.01f) continue;
                    error = $"Edge {edge.Id} exceeds its {maximumGrade:0.#} degree grade limit.";
                    return false;
                }
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                if (GetDegree(nodes[i].Id) >= 2 || nodes[i].AllowsDeadEnd) continue;
                error = $"Node {nodes[i].Id} is an accidental dead end.";
                return false;
            }

            var route = new List<RoadGraphEdge>();
            for (var i = 1; i < nodes.Count; i++)
            {
                if (TryFindRoute(nodes[0].Id, nodes[i].Id, route)) continue;
                error = $"Node {nodes[i].Id} is disconnected from the road network.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool HasAccessibleEdge(string nodeId, RoadAccess requiredAccess)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (!edge.Supports(requiredAccess)) continue;
                if (edge.FromNodeId == nodeId || edge.ToNodeId == nodeId) return true;
            }
            return false;
        }

        private static void ReconstructRoute(
            string startNodeId,
            string goalNodeId,
            IReadOnlyDictionary<string, string> previousNode,
            IReadOnlyDictionary<string, RoadGraphEdge> previousEdge,
            List<RoadGraphEdge> route)
        {
            var current = goalNodeId;
            while (current != startNodeId)
            {
                route.Add(previousEdge[current]);
                current = previousNode[current];
            }
            route.Reverse();
        }
    }
}
