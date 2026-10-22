using Sideways.Challenges;
using Sideways.Collectibles;
using Sideways.UI;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Core
{
    public sealed class GameFeedbackController : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private VehicleCollisionSensor collisionSensor;
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private RideResourceController resources;
        [SerializeField] private ChallengeController challenges;
        [SerializeField] private PlayerPreferencesController preferences;

        private AudioSource engineSource;
        private AudioSource effectsSource;
        private AudioClip pickupClip;
        private AudioClip rewardClip;
        private AudioClip impactClip;
        private AudioClip resetClip;
        private float nextHapticTime;
        private bool subscribed;

        private void Awake()
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            effectsSource = gameObject.AddComponent<AudioSource>();
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 0f;
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            engineSource.clip = CreateTone("EngineLoop", 78f, 1f, 0.12f, false);
            pickupClip = CreateTone("Pickup", 660f, 0.12f, 0.26f, true);
            rewardClip = CreateTone("Reward", 440f, 0.3f, 0.28f, true);
            impactClip = CreateNoise("Impact", 0.16f, 0.34f);
            resetClip = CreateTone("Reset", 180f, 0.2f, 0.22f, true);
            engineSource.Play();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (engineSource == null || vehicle == null) return;
            var speed = vehicle.Telemetry.Speed;
            engineSource.pitch = Mathf.Lerp(0.72f, 1.55f, Mathf.Clamp01(speed / 30f));
            engineSource.volume = Mathf.Lerp(0.05f, 0.22f, Mathf.Clamp01(speed / 24f));
        }

        public void Configure(
            VehicleController vehicleController,
            VehicleCollisionSensor sensor,
            DriftScoreController scoreController,
            RideResourceController resourceController,
            ChallengeController challengeController,
            PlayerPreferencesController preferencesController)
        {
            Unsubscribe();
            vehicle = vehicleController;
            collisionSensor = sensor;
            scoring = scoreController;
            resources = resourceController;
            challenges = challengeController;
            preferences = preferencesController;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (collisionSensor != null) collisionSensor.ImpactReported += HandleImpact;
            if (scoring != null) scoring.ComboBanked += HandleComboBanked;
            if (resources != null) resources.PickupCollected += HandlePickup;
            if (challenges != null) challenges.RewardEarned += HandleReward;
            RoadSignEvents.SignDestroyed += HandleSignDestroyed;
            GameplayEvents.RoadReset += HandleRoadReset;
            GameplayEvents.RideEnded += HandleRideEnded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (collisionSensor != null) collisionSensor.ImpactReported -= HandleImpact;
            if (scoring != null) scoring.ComboBanked -= HandleComboBanked;
            if (resources != null) resources.PickupCollected -= HandlePickup;
            if (challenges != null) challenges.RewardEarned -= HandleReward;
            RoadSignEvents.SignDestroyed -= HandleSignDestroyed;
            GameplayEvents.RoadReset -= HandleRoadReset;
            GameplayEvents.RideEnded -= HandleRideEnded;
            subscribed = false;
        }

        private void HandleImpact(VehicleImpact impact)
        {
            if (impact.RelativeSpeed < 3f) return;
            Play(impactClip, Mathf.Clamp01(impact.RelativeSpeed / 14f));
            Vibrate();
        }

        private void HandlePickup(CollectiblePickup pickup)
        {
            Play(pickupClip, pickup.Kind == CollectibleKind.FuelCanister ? 1f : 0.65f);
            Vibrate();
        }

        private void HandleComboBanked(int points) => Play(rewardClip, 0.75f);
        private void HandleReward(ChallengeReward reward) { Play(rewardClip, 1f); Vibrate(); }
        private void HandleSignDestroyed(string signId) => Play(impactClip, 0.42f);
        private void HandleRoadReset() => Play(resetClip, 0.8f);

        private void HandleRideEnded(RideEndReason reason)
        {
            engineSource?.Stop();
            Play(impactClip, 0.7f);
            Vibrate();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (effectsSource != null && clip != null) effectsSource.PlayOneShot(clip, volume);
        }

        private void Vibrate()
        {
            if (preferences != null && !preferences.Preferences.HapticsEnabled) return;
            if (Time.unscaledTime < nextHapticTime) return;
            nextHapticTime = Time.unscaledTime + 0.25f;
#if UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float amplitude, bool decay)
        {
            const int sampleRate = 24000;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = decay ? 1f - i / (float)sampleCount : 1f;
                samples[i] = (Mathf.Sin(2f * Mathf.PI * frequency * time) + Mathf.Sin(2f * Mathf.PI * frequency * 2f * time) * 0.25f) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoise(string name, float duration, float amplitude)
        {
            const int sampleRate = 24000;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(1729);
            for (var i = 0; i < sampleCount; i++)
            {
                var envelope = 1f - i / (float)sampleCount;
                samples[i] = ((float)random.NextDouble() * 2f - 1f) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
