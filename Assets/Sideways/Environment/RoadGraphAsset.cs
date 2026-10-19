using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sideways.EnvironmentSystem
{
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
        public List<Vector3> Centerline = new();
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
            if (route == null) throw new ArgumentNullException(nameof(route));
            route.Clear();
            if (!TryGetNode(startNodeId, out _) || !TryGetNode(goalNodeId, out _)) return false;
            if (startNodeId == goalNodeId) return true;

            var frontier = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            var previousNode = new Dictionary<string, string>(StringComparer.Ordinal);
            var previousEdge = new Dictionary<string, RoadGraphEdge>(StringComparer.Ordinal);
            frontier.Enqueue(startNodeId);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (blockedEdgeIds != null && blockedEdgeIds.Contains(edge.Id)) continue;

                    string next = null;
                    if (edge.FromNodeId == current) next = edge.ToNodeId;
                    else if (edge.Bidirectional && edge.ToNodeId == current) next = edge.FromNodeId;
                    if (next == null || !visited.Add(next)) continue;

                    previousNode[next] = current;
                    previousEdge[next] = edge;
                    if (next == goalNodeId)
                    {
                        ReconstructRoute(startNodeId, goalNodeId, previousNode, previousEdge, route);
                        return true;
                    }
                    frontier.Enqueue(next);
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
