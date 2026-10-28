using Sideways.Configuration;
using Sideways.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sideways.Vehicle
{
    public sealed class VehicleDriftEffects : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private Transform bodyVisual;

        private readonly Transform[] frontWheelPivots = new Transform[2];
        private readonly ParticleSystem[] smokeSystems = new ParticleSystem[2];
        private readonly TrailRenderer[] skidTrails = new TrailRenderer[2];
        private Material wheelMaterial;
        private Material smokeMaterial;
        private Material skidMaterial;
        private Quaternion bodyBaseRotation;
        private Vector3 previousPosition;
        private float previousSpeed;
        private bool initialized;

        private void Awake()
        {
            vehicle ??= GetComponent<VehicleController>();
            bodyVisual ??= transform.Find("Visual");
            if (bodyVisual != null) bodyBaseRotation = bodyVisual.localRotation;
            BuildRuntimePresentation();
            previousPosition = transform.position;
            initialized = true;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            previousSpeed = vehicle != null ? vehicle.Telemetry.Speed : 0f;
            GameplayEvents.RoadReset += HandleRoadReset;
            GameplayEvents.RideEnded += HandleRideEnded;
        }

        private void OnDisable()
        {
            GameplayEvents.RoadReset -= HandleRoadReset;
            GameplayEvents.RideEnded -= HandleRideEnded;
            StopAndClearEffects();
            if (bodyVisual != null) bodyVisual.localRotation = bodyBaseRotation;
        }

        private void OnDestroy()
        {
            Destroy(wheelMaterial);
            Destroy(smokeMaterial);
            Destroy(skidMaterial);
        }

        public void Configure(VehicleController controller, Transform visual)
        {
            vehicle = controller;
            bodyVisual = visual;
        }

        private void HandleRoadReset() => StopAndClearEffects();

        private void HandleRideEnded(RideEndReason reason) => StopAndClearEffects();

        private void LateUpdate()
        {
            if (!initialized || vehicle == null || vehicle.HandlingProfile == null) return;
            var telemetry = vehicle.Telemetry;
            var profile = vehicle.HandlingProfile;
            var response = 1f - Mathf.Exp(-profile.visualResponse * Time.deltaTime);

            for (var i = 0; i < frontWheelPivots.Length; i++)
            {
                if (frontWheelPivots[i] != null)
                {
                    var target = Quaternion.Euler(0f, telemetry.SteeringAngleDegrees, 0f);
                    frontWheelPivots[i].localRotation = Quaternion.Slerp(frontWheelPivots[i].localRotation, target, response);
                }
            }

            if (bodyVisual != null)
            {
                var acceleration = Time.deltaTime > 0f ? (telemetry.Speed - previousSpeed) / Time.deltaTime : 0f;
                var roll = -Mathf.Clamp(telemetry.YawRateDegrees / Mathf.Max(1f, profile.maximumYawRate), -1f, 1f) * profile.maximumBodyRoll;
                var pitch = Mathf.Clamp(-acceleration * 0.22f, -profile.maximumBodyPitch, profile.maximumBodyPitch);
                var target = bodyBaseRotation * Quaternion.Euler(pitch, 0f, roll);
                bodyVisual.localRotation = Quaternion.Slerp(bodyVisual.localRotation, target, response);
            }

            var teleported = (transform.position - previousPosition).sqrMagnitude > 25f;
            if (teleported || !telemetry.Grounded)
            {
                StopAndClearEffects();
            }
            else
            {
                UpdateGroundEffects(telemetry, profile);
            }

            previousPosition = transform.position;
            previousSpeed = telemetry.Speed;
        }

        private void UpdateGroundEffects(VehicleTelemetry telemetry, VehicleHandlingProfile profile)
        {
            var speedFactor = Mathf.InverseLerp(profile.effectMinimumSpeed, profile.driftMinimumSpeed + 4f, telemetry.Speed);
            var rearSlip = Mathf.Abs(telemetry.RearSlipAngle);
            var skidFactor = Mathf.InverseLerp(profile.skidSlipAngle, profile.fullDriftIntensityAngle, rearSlip) * speedFactor;
            var smokeFactor = Mathf.InverseLerp(profile.smokeSlipAngle, profile.fullDriftIntensityAngle, rearSlip) * speedFactor;
            var skid = skidFactor > 0.05f && telemetry.RearGripUtilization > 0.45f;

            for (var i = 0; i < skidTrails.Length; i++)
            {
                if (skidTrails[i] != null) skidTrails[i].emitting = skid;
                if (smokeSystems[i] == null) continue;
                var emission = smokeSystems[i].emission;
                emission.rateOverTime = smokeFactor * 32f;
                if (smokeFactor > 0.02f && !smokeSystems[i].isPlaying) smokeSystems[i].Play();
                if (smokeFactor <= 0.02f && smokeSystems[i].isPlaying) smokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void StopAndClearEffects()
        {
            for (var i = 0; i < skidTrails.Length; i++)
            {
                if (skidTrails[i] != null)
                {
                    skidTrails[i].emitting = false;
                    skidTrails[i].Clear();
                }

                if (smokeSystems[i] != null)
                {
                    smokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void BuildRuntimePresentation()
        {
            var root = new GameObject("RuntimeVehiclePresentation").transform;
            root.SetParent(transform, false);
            wheelMaterial = CreateMaterial("VehicleWheelRuntime", new Color(0.035f, 0.04f, 0.05f, 1f), false);
            smokeMaterial = CreateMaterial("VehicleSmokeRuntime", new Color(0.62f, 0.65f, 0.68f, 0.42f), true);
            skidMaterial = CreateMaterial("VehicleSkidRuntime", new Color(0.035f, 0.035f, 0.04f, 0.9f), false);

            CreateWheel(root, "FrontLeftWheel", new Vector3(-0.95f, 0.26f, 1.25f), 0);
            CreateWheel(root, "FrontRightWheel", new Vector3(0.95f, 0.26f, 1.25f), 1);
            CreateWheel(root, "RearLeftWheel", new Vector3(-0.95f, 0.26f, -1.35f), -1);
            CreateWheel(root, "RearRightWheel", new Vector3(0.95f, 0.26f, -1.35f), -1);
            CreateRearEffect(root, "Left", new Vector3(-0.87f, 0.04f, -1.35f), 0);
            CreateRearEffect(root, "Right", new Vector3(0.87f, 0.04f, -1.35f), 1);
        }

        private void CreateWheel(Transform parent, string name, Vector3 position, int frontIndex)
        {
            var pivot = new GameObject(name + "Pivot").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.layer = gameObject.layer;
            wheel.transform.SetParent(pivot, false);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.34f, 0.12f, 0.34f);
            var collider = wheel.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            wheel.GetComponent<Renderer>().sharedMaterial = wheelMaterial;
            if (frontIndex >= 0) frontWheelPivots[frontIndex] = pivot;
        }

        private void CreateRearEffect(Transform parent, string side, Vector3 position, int index)
        {
            var contact = new GameObject("Rear" + side + "Contact").transform;
            contact.SetParent(parent, false);
            contact.localPosition = position;

            var trail = contact.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = skidMaterial;
            trail.time = 4f;
            trail.startWidth = 0.17f;
            trail.endWidth = 0.11f;
            trail.minVertexDistance = 0.08f;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            skidTrails[index] = trail;

            var smokeObject = new GameObject("Rear" + side + "Smoke");
            smokeObject.transform.SetParent(contact, false);
            smokeObject.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            var smoke = smokeObject.AddComponent<ParticleSystem>();
            var main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 0.7f;
            main.startSize = 0.28f;
            main.startColor = new Color(0.68f, 0.7f, 0.72f, 0.4f);
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = smoke.emission;
            emission.rateOverTime = 0f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.08f;
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smokeSystems[index] = smoke;
        }

        private static Material CreateMaterial(string name, Color color, bool transparent)
        {
            var shader = Shader.Find(transparent ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find(transparent ? "Sprites/Default" : "Unlit/Color");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }
    }
}
