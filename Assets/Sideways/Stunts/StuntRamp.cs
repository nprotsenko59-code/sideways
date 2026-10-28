using System;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Stunts
{
    public sealed class StuntRamp : MonoBehaviour
    {
        [SerializeField] private string rampId;

        public static event Action<string, VehicleController> RampEntered;

        public void Configure(string id) => rampId = id;

        private void OnTriggerEnter(Collider other)
        {
            var vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle != null) RampEntered?.Invoke(rampId, vehicle);
        }
    }
}
