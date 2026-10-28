using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sideways.EnvironmentSystem;
using UnityEditor;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class RoadGraphTests
    {
        private const string GraphPath = "Assets/Sideways/Configuration/LAPrototypeRoadGraph.asset";
        private const string PortraitCoursePath = "Assets/Sideways/Configuration/PortraitDriftCourse.asset";

        [Test]
        public void NeighborhoodGraphIsValidAndConnected()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);

            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.IsValid(out var error), Is.True, error);
            Assert.That(graph.Nodes.Count, Is.GreaterThanOrEqualTo(18));
            Assert.That(graph.Edges.Count, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void NeighborhoodContainsEveryRequiredRoadClass()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var kinds = graph.Edges.Select(edge => edge.Kind).ToHashSet();

            Assert.That(kinds, Does.Contain(RoadKind.Street));
            Assert.That(kinds, Does.Contain(RoadKind.Boulevard));
            Assert.That(kinds, Does.Contain(RoadKind.Alley));
            Assert.That(kinds, Does.Contain(RoadKind.Scenic));
        }

        [Test]
        public void KeyAreasRetainASecondRouteWhenDirectLinksAreBlocked()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var route = new List<RoadGraphEdge>();

            Assert.That(graph.TryFindRoute("G_C", "B_SC", route), Is.True);
            Assert.That(graph.TryFindRoute("G_C", "B_SC", route, new HashSet<string> { "Grid_C_SS" }), Is.True);
            Assert.That(graph.TryFindRoute("G_C", "B_SC", route, new HashSet<string> { "BoulevardLink_C" }), Is.True);
        }

        [Test]
        public void GraphHasNoAccidentalDeadEnds()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            foreach (var node in graph.Nodes)
            {
                Assert.That(graph.GetDegree(node.Id) >= 2 || node.AllowsDeadEnd, Is.True, $"{node.Id} is an unmarked dead end.");
            }
        }

        [Test]
        public void PortraitCourseIsOneWideForwardDriftRun()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(PortraitCoursePath);
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.IsValid(out var error), Is.True, error);
            Assert.That(graph.GraphId, Is.EqualTo("portrait-drift-run"));
            Assert.That(graph.DriveableSize, Is.EqualTo(new Vector2(320f, 720f)));
            Assert.That(graph.Edges.Count, Is.EqualTo(7));
            Assert.That(graph.Edges, Has.All.Matches<RoadGraphEdge>(edge => edge.Width >= 22f));
            Assert.That(graph.Edges, Has.None.Matches<RoadGraphEdge>(edge =>
                (edge.Features & (RoadFeature.Ramp | RoadFeature.Rooftop | RoadFeature.Stunt | RoadFeature.Interchange)) != 0));
            Assert.That(graph.TryGetNode("COURSE_START", out var start), Is.True);
            Assert.That(graph.TryGetNode("COURSE_FINISH", out var finish), Is.True);
            Assert.That(start.AllowsDeadEnd, Is.True);
            Assert.That(finish.AllowsDeadEnd, Is.True);
            Assert.That(finish.Position.z, Is.GreaterThan(start.Position.z + 650f));
        }

        [Test]
        public void EveryOrdinaryRampHasAnAccessibleContinuationAtBothEnds()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var ramps = graph.Edges.Where(edge => (edge.Features & RoadFeature.Ramp) != 0 && (edge.Features & RoadFeature.Stunt) == 0);
            foreach (var ramp in ramps)
            {
                foreach (var access in new[] { RoadAccess.Player, RoadAccess.Traffic, RoadAccess.Police })
                {
                    if (!ramp.Supports(access)) continue;
                    foreach (var endpoint in new[] { ramp.FromNodeId, ramp.ToNodeId })
                    {
                        var continuations = graph.Edges.Count(edge => edge.Id != ramp.Id && edge.Supports(access) && (edge.FromNodeId == endpoint || edge.ToNodeId == endpoint));
                        Assert.That(continuations, Is.GreaterThan(0), $"{ramp.Id} ends at {endpoint} for {access} without a permitted continuation.");
                    }
                }
            }
        }

        [Test]
        public void ExpansionPreservesLegacyAnchorsAndAddsLayeredRoutes()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var legacyIds = new[] { "G_NW", "G_C", "G_SE", "B_W", "B_SC", "O_W", "O_N", "O_E" };
            foreach (var id in legacyIds) Assert.That(graph.TryGetNode(id, out _), Is.True, $"Legacy node {id} must remain stable.");
            Assert.That(graph.DriveableSize, Is.EqualTo(new Vector2(900f, 720f)));
            Assert.That(graph.TryGetNode("U_C", out var upper), Is.True);
            Assert.That(graph.TryGetNode("G_C", out var lower), Is.True);
            Assert.That(upper.Position.y, Is.GreaterThan(lower.Position.y + 6f));
        }

        [Test]
        public void ExpresswayMazeAddsSignatureInterchangeCanalAndEscapeRoutes()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var signature = graph.Edges.Where(edge => (edge.Features & RoadFeature.Signature) != 0).ToArray();
            var interchange = graph.Edges.Where(edge => (edge.Features & RoadFeature.Interchange) != 0).ToArray();
            var escape = graph.Edges.Where(edge => (edge.Features & RoadFeature.EscapeRoute) != 0).ToArray();
            var route = new List<RoadGraphEdge>();

            Assert.That(signature.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(signature, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.All)));
            Assert.That(interchange.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(interchange, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic)));
            Assert.That(escape, Is.Not.Empty);
            Assert.That(escape, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Player) && edge.Supports(RoadAccess.Police)));
            Assert.That(escape, Has.None.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic)));
            Assert.That(graph.TryFindRoute("L_W", "C_S", route, RoadAccess.Traffic), Is.True);
            Assert.That(route, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic)));
        }

        [Test]
        public void ExpresswayMazeMatchesTheApprovedTopDownSchema()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            Assert.That(graph.GraphId, Is.EqualTo("sunset-basin-schema"));
            Assert.That(graph.TryGetNode("B_SC", out var start), Is.True);
            Assert.That(start.Position, Is.EqualTo(new Vector3(0f, 0f, -330f)));
            Assert.That(graph.TryGetNode("L_N", out var loopNorth), Is.True);
            Assert.That(loopNorth.Position.x, Is.LessThan(-200f));
            Assert.That(loopNorth.Position.z, Is.GreaterThan(250f));
            Assert.That(graph.TryGetNode("J_N", out var interchangeNorth), Is.True);
            Assert.That(interchangeNorth.Position.x, Is.GreaterThan(250f));
            Assert.That(interchangeNorth.Position.z, Is.GreaterThan(200f));
            Assert.That(graph.TryGetNode("O_NE", out var hillsideExit), Is.True);
            Assert.That(hillsideExit.Position.x, Is.GreaterThan(350f));
            Assert.That(hillsideExit.Position.z, Is.GreaterThan(250f));
            Assert.That(graph.TryGetNode("ESC_W", out var escapeWest), Is.True);
            Assert.That(escapeWest.Position.x, Is.LessThan(-380f));
            Assert.That(escapeWest.Position.z, Is.LessThan(-200f));
            Assert.That(graph.Edges.Count(edge => (edge.Features & RoadFeature.MainRoute) != 0), Is.EqualTo(12));
            Assert.That(graph.Edges.Count(edge => (edge.Features & RoadFeature.ServiceRoute) != 0), Is.GreaterThanOrEqualTo(13));
            Assert.That(graph.Edges.Count(edge => (edge.Features & RoadFeature.Shortcut) != 0), Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void PrimaryDriftRoutesUseRoundedCenterlines()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var curvedRoads = graph.Edges.Where(edge =>
                edge.Centerline.Count > 4 &&
                (edge.Features & (RoadFeature.MainRoute | RoadFeature.Signature | RoadFeature.Interchange)) != 0 &&
                edge.Centerline.All(point => Mathf.Abs(point.y - edge.Centerline[0].y) < 0.01f)).ToArray();

            Assert.That(curvedRoads.Length, Is.GreaterThanOrEqualTo(12));
            foreach (var edge in curvedRoads)
            {
                for (var point = 1; point < edge.Centerline.Count - 1; point++)
                {
                    var incoming = Vector3.ProjectOnPlane(edge.Centerline[point] - edge.Centerline[point - 1], Vector3.up);
                    var outgoing = Vector3.ProjectOnPlane(edge.Centerline[point + 1] - edge.Centerline[point], Vector3.up);
                    if (incoming.sqrMagnitude < 0.01f || outgoing.sqrMagnitude < 0.01f) continue;
                    Assert.That(Vector3.Angle(incoming, outgoing), Is.LessThan(35f), $"{edge.Id} has a harsh turn at sample {point}.");
                }
            }
        }

        [Test]
        public void PublicRoadsProvideAWideDriftEnvelope()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var primaryRoads = graph.Edges.Where(edge =>
                (edge.Features & RoadFeature.Rooftop) == 0 &&
                edge.Kind != RoadKind.Alley).ToArray();

            Assert.That(primaryRoads, Is.Not.Empty);
            Assert.That(primaryRoads, Has.All.Matches<RoadGraphEdge>(edge => edge.Width >= 17f));
            Assert.That(
                graph.Edges.Where(edge => (edge.Features & RoadFeature.MainRoute) != 0),
                Has.All.Matches<RoadGraphEdge>(edge => edge.Width >= 18f));
        }

        [Test]
        public void AccessRulesKeepTrafficOffRooftopsAndStunts()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var route = new List<RoadGraphEdge>();

            Assert.That(graph.TryFindRoute("U_W", "U_E", route, RoadAccess.Traffic), Is.True);
            Assert.That(route, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic)));
            Assert.That(graph.TryFindRoute("R_W", "R_E", route, RoadAccess.Police), Is.True);
            Assert.That(graph.TryFindRoute("R_W", "R_E", route, RoadAccess.Traffic), Is.False);
            Assert.That(graph.TryFindRoute("P_LOW", "P_S", route, RoadAccess.Police), Is.True);
            Assert.That(graph.TryFindRoute("P_LOW", "P_S", route, RoadAccess.Traffic), Is.False);
            Assert.That(graph.TryFindRoute("D_W", "D_E", route, RoadAccess.Traffic), Is.False);
            Assert.That(graph.TryFindRoute("D_W", "D_E", route, RoadAccess.Player), Is.True);

            var stuntEdges = graph.Edges.Where(edge => (edge.Features & RoadFeature.Stunt) != 0).ToArray();
            Assert.That(stuntEdges, Is.Not.Empty);
            Assert.That(stuntEdges, Has.All.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Player)));
            Assert.That(stuntEdges, Has.None.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic) || edge.Supports(RoadAccess.Police)));

            var shortcuts = graph.Edges.Where(edge => (edge.Features & RoadFeature.Shortcut) != 0).ToArray();
            Assert.That(shortcuts, Is.Not.Empty);
            Assert.That(shortcuts, Has.None.Matches<RoadGraphEdge>(edge => edge.Supports(RoadAccess.Traffic)));
        }

        [Test]
        public void LayeredEdgesMeetEndpointHeightsAndGradeLimits()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            foreach (var edge in graph.Edges)
            {
                Assert.That(graph.TryGetNode(edge.FromNodeId, out var from), Is.True);
                Assert.That(graph.TryGetNode(edge.ToNodeId, out var to), Is.True);
                Assert.That(Vector3.Distance(edge.Centerline[0], from.Position), Is.LessThan(0.001f), $"{edge.Id} start height must meet its node.");
                Assert.That(Vector3.Distance(edge.Centerline[^1], to.Position), Is.LessThan(0.001f), $"{edge.Id} end height must meet its node.");

                var maximumGrade = (edge.Features & RoadFeature.Stunt) != 0
                    ? 25f
                    : (edge.Features & RoadFeature.Rooftop) != 0 ? 12f : 10f;
                for (var i = 1; i < edge.Centerline.Count; i++)
                {
                    var delta = edge.Centerline[i] - edge.Centerline[i - 1];
                    var horizontal = new Vector2(delta.x, delta.z).magnitude;
                    var grade = Mathf.Atan2(Mathf.Abs(delta.y), Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg;
                    Assert.That(grade, Is.LessThanOrEqualTo(maximumGrade + 0.01f), $"{edge.Id} segment {i} exceeds its grade limit.");
                }
            }
        }

        [Test]
        public void SchemaElevatedRoutesRemainSeparatedFromGroundNetwork()
        {
            var graph = AssetDatabase.LoadAssetAtPath<RoadGraphAsset>(GraphPath);
            var upper = graph.Edges.Single(edge => edge.Id == "Upper_Crosstown_E");
            var lower = graph.Edges.Single(edge => edge.Id == "Grid_N_CE");

            Assert.That(new[] { upper.FromNodeId, upper.ToNodeId }, Has.None.EqualTo(lower.FromNodeId));
            Assert.That(new[] { upper.FromNodeId, upper.ToNodeId }, Has.None.EqualTo(lower.ToNodeId));
            Assert.That(upper.Centerline, Has.All.Matches<Vector3>(point => point.y > 6.5f));
            Assert.That(lower.Centerline, Has.All.Matches<Vector3>(point => Mathf.Abs(point.y) < 0.01f));
        }

        [Test]
        public void WeightedRoutingPrefersFasterTravelTime()
        {
            var graph = ScriptableObject.CreateInstance<RoadGraphAsset>();
            var nodes = new[]
            {
                new RoadGraphNode("A", Vector3.zero),
                new RoadGraphNode("B", Vector3.right * 10f),
                new RoadGraphNode("C", Vector3.right * 20f)
            };
            var slow = new RoadGraphEdge("Slow", "A", "C", RoadKind.Street, 10f, 2f, Vector3.zero, Vector3.right * 20f);
            var fastA = new RoadGraphEdge("FastA", "A", "B", RoadKind.Street, 10f, 20f, Vector3.zero, Vector3.right * 10f);
            var fastB = new RoadGraphEdge("FastB", "B", "C", RoadKind.Street, 10f, 20f, Vector3.right * 10f, Vector3.right * 20f);
            graph.Configure("weighted-test", new Vector2(30f, 30f), nodes, new[] { slow, fastA, fastB });
            var route = new List<RoadGraphEdge>();

            Assert.That(graph.TryFindRoute("A", "C", route, RoadAccess.Traffic), Is.True);
            Assert.That(route.Select(edge => edge.Id), Is.EqualTo(new[] { "FastA", "FastB" }));
            Object.DestroyImmediate(graph);
        }
    }
}
