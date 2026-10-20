using UnityEngine;

namespace Sideways.Challenges
{
    public sealed class BreakableRoadSign : MonoBehaviour
    {
        [SerializeField] private string signId;
        [SerializeField] private float minimumImpactSpeed = 1.5f;
        [SerializeField] private float respawnDelay = 12f;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider[] colliders;

        private bool destroyed;
        private float respawnAt;

        public string SignId => signId;
        public bool IsDestroyed => destroyed;

        private void Awake()
        {
            SetCollidersAsTriggers();
        }

        private void Update()
        {
            if (destroyed && Time.time >= respawnAt) SetDestroyed(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (destroyed || collision.gameObject.layer != 6 || collision.relativeVelocity.magnitude < minimumImpactSpeed) return;
            DestroySign();
        }

        private void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (destroyed || body == null || body.gameObject.layer != 6 || body.linearVelocity.magnitude < minimumImpactSpeed) return;
            DestroySign();
        }

        public void Configure(string id, float impactSpeed, float respawnSeconds, Renderer[] signRenderers, Collider[] signColliders)
        {
            signId = id;
            minimumImpactSpeed = impactSpeed;
            respawnDelay = respawnSeconds;
            renderers = signRenderers;
            colliders = signColliders;
            SetCollidersAsTriggers();
        }

        public bool DestroySign()
        {
            if (destroyed || string.IsNullOrWhiteSpace(signId)) return false;
            SetDestroyed(true);
            respawnAt = Time.time + respawnDelay;
            RoadSignEvents.RaiseDestroyed(signId);
            return true;
        }

        private void SetDestroyed(bool value)
        {
            destroyed = value;
            if (renderers != null)
            {
                for (var i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = !value;
            }
            if (colliders != null)
            {
                for (var i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = !value;
            }
        }

        private void SetCollidersAsTriggers()
        {
            if (colliders == null) return;
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].isTrigger = true;
            }
        }
    }
}
