using UnityEngine;

namespace Sideways.Collectibles
{
    public sealed class RoadCollectible : MonoBehaviour
    {
        [SerializeField] private CollectibleKind kind;
        [SerializeField] private RideResourceController resources;
        [SerializeField] private float amount;
        [SerializeField] private float respawnDelay = 20f;
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private float bobHeight = 0.18f;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider trigger;

        private bool available = true;
        private float respawnAt;
        private float baseHeight;

        public CollectibleKind Kind => kind;
        public bool IsAvailable => available;

        private void Awake()
        {
            baseHeight = transform.position.y;
            if (trigger != null) trigger.isTrigger = true;
        }

        private void Update()
        {
            if (!available)
            {
                if (Time.time >= respawnAt) SetAvailable(true);
                return;
            }

            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            var position = transform.position;
            position.y = baseHeight + Mathf.Sin(Time.time * 2.5f) * bobHeight;
            transform.position = position;
        }

        private void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (!available || body == null || body.gameObject.layer != 6 || resources == null) return;
            if (!resources.TryCollect(kind, amount)) return;
            SetAvailable(false);
            respawnAt = Time.time + respawnDelay;
        }

        public void Configure(CollectibleKind collectibleKind, RideResourceController resourceController, float pickupAmount, float delay, Renderer[] collectibleRenderers, Collider collectibleTrigger)
        {
            kind = collectibleKind;
            resources = resourceController;
            amount = pickupAmount;
            respawnDelay = delay;
            renderers = collectibleRenderers;
            trigger = collectibleTrigger;
            if (trigger != null) trigger.isTrigger = true;
        }

        private void SetAvailable(bool value)
        {
            available = value;
            if (renderers != null)
            {
                for (var i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = value;
            }
            if (trigger != null) trigger.enabled = value;
        }
    }
}
