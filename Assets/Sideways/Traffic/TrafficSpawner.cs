using System.Collections.Generic;
using Sideways.Configuration;
using Sideways.EnvironmentSystem;
using UnityEngine;

namespace Sideways.Traffic
{
    public sealed class TrafficSpawner : MonoBehaviour
    {
        [SerializeField] private bool trafficEnabled = true;
        [SerializeField] private GameObject trafficPrefab;
        [SerializeField] private RoadGraphAsset roadGraph;
        [SerializeField] private TrafficProfile profile;
        [SerializeField] private Transform player;

        private readonly List<TrafficVehicle> vehicles = new();
        private float nextInactiveRetryTime;

        public bool TrafficEnabled { get => trafficEnabled; set => trafficEnabled = value; }
        public int ActiveCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < vehicles.Count; i++) if (vehicles[i] != null && vehicles[i].gameObject.activeSelf) count++;
                return count;
            }
        }

        private void Start()
        {
            if (!trafficEnabled || trafficPrefab == null || roadGraph == null || profile == null) return;
            for (var i = 0; i < profile.maximumVehicles; i++) SpawnVehicle();
        }

        private void Update()
        {
            if (!trafficEnabled || player == null) return;
            for (var i = 0; i < vehicles.Count; i++)
            {
                if (!vehicles[i].gameObject.activeSelf)
                {
                    if (Time.time >= nextInactiveRetryTime && vehicles[i].CanRespawn) TryPlace(vehicles[i]);
                    continue;
                }
                if (Vector3.Distance(vehicles[i].transform.position, player.position) > profile.despawnDistance)
                {
                    TryPlace(vehicles[i]);
                }
            }
            if (Time.time >= nextInactiveRetryTime) nextInactiveRetryTime = Time.time + 2f;
        }

        public void Configure(GameObject prefab, RoadGraphAsset graph, TrafficProfile trafficProfile, Transform playerTransform)
        {
            trafficPrefab = prefab;
            roadGraph = graph;
            profile = trafficProfile;
            player = playerTransform;
        }

        public bool IsSpawnPositionAllowed(Vector3 position)
        {
            if (player == null || profile == null) return false;
            var toSpawn = Vector3.ProjectOnPlane(position - player.position, Vector3.up);
            var distance = toSpawn.magnitude;
            if (distance < profile.minimumSpawnDistance || distance > profile.maximumSpawnDistance) return false;
            if (distance <= profile.forwardSpawnExclusionDistance && Vector3.Angle(player.forward, toSpawn) <= profile.forwardSpawnExclusionAngle * 0.5f) return false;
            var spacingSquared = profile.minimumVehicleSpacing * profile.minimumVehicleSpacing;
            for (var i = 0; i < vehicles.Count; i++)
            {
                var vehicle = vehicles[i];
                if (vehicle == null || !vehicle.gameObject.activeSelf) continue;
                if ((vehicle.transform.position - position).sqrMagnitude < spacingSquared) return false;
            }
            return true;
        }

        private void SpawnVehicle()
        {
            var gameObject = Instantiate(trafficPrefab, transform);
            gameObject.name = $"TrafficCar_{vehicles.Count + 1:00}";
            var vehicle = gameObject.GetComponent<TrafficVehicle>();
            vehicles.Add(vehicle);
            if (!TryPlace(vehicle)) gameObject.SetActive(false);
        }

        private bool TryPlace(TrafficVehicle vehicle)
        {
            for (var attempt = 0; attempt < profile.spawnAttempts; attempt++)
            {
                var edge = roadGraph.Edges[Random.Range(0, roadGraph.Edges.Count)];
                if (!edge.Supports(RoadAccess.Traffic)) continue;
                var segment = Random.Range(0, edge.Centerline.Count - 1);
                var t = Random.Range(0.15f, 0.85f);
                var position = Vector3.Lerp(edge.Centerline[segment], edge.Centerline[segment + 1], t);
                if (!IsSpawnPositionAllowed(position)) continue;
                vehicle.gameObject.SetActive(true);
                vehicle.PlaceOnEdge(edge, Random.value >= 0.5f, segment, t);
                return true;
            }
            vehicle.gameObject.SetActive(false);
            return false;
        }
    }
}
