using UnityEngine;

namespace Sideways.EnvironmentSystem
{
    public sealed class RoadGraphComponent : MonoBehaviour
    {
        [SerializeField] private RoadGraphAsset graph;

        public RoadGraphAsset Graph => graph;

        public void Configure(RoadGraphAsset roadGraph) => graph = roadGraph;

        private void Awake()
        {
            // The terrain is the continuous driving surface. Road boxes only mark
            // where recovery is allowed; keeping them solid creates internal faces
            // at segment joins that fast-moving vehicles can hit like invisible walls.
            var roadVolumes = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < roadVolumes.Length; i++)
            {
                roadVolumes[i].isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (graph == null) return;
            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                Gizmos.color = edge.Kind switch
                {
                    RoadKind.Boulevard => new Color(1f, 0.75f, 0.1f),
                    RoadKind.Alley => new Color(0.2f, 0.8f, 1f),
                    RoadKind.Scenic => new Color(0.3f, 1f, 0.45f),
                    _ => Color.white
                };
                for (var point = 1; point < edge.Centerline.Count; point++)
                {
                    Gizmos.DrawLine(edge.Centerline[point - 1] + Vector3.up, edge.Centerline[point] + Vector3.up);
                }
            }
        }
    }
}
