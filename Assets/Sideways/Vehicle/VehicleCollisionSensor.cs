using System;
using UnityEngine;

namespace Sideways.Vehicle
{
    public sealed class VehicleCollisionSensor : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleMask;

        public event Action<VehicleImpact> ImpactReported;

        public void Configure(LayerMask obstacles) => obstacleMask = obstacles;

        private void OnCollisionEnter(Collision collision)
        {
            var otherLayer = collision.gameObject.layer;
            if ((obstacleMask.value & (1 << otherLayer)) == 0) return;

            var point = transform.position;
            var normal = Vector3.up;
            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            ImpactReported?.Invoke(new VehicleImpact(collision.relativeVelocity.magnitude, point, normal, otherLayer));
        }

        internal void ReportForTests(float relativeSpeed)
        {
            ImpactReported?.Invoke(new VehicleImpact(relativeSpeed, transform.position, Vector3.up, 0));
        }
    }
}
