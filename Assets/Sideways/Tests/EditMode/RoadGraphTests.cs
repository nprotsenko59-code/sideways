using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sideways.EnvironmentSystem;
using UnityEditor;

namespace Sideways.Tests
{
    public sealed class RoadGraphTests
    {
        private const string GraphPath = "Assets/Sideways/Configuration/LAPrototypeRoadGraph.asset";

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
    }
}
