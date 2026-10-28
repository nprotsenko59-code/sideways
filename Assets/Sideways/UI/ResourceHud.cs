using Sideways.Collectibles;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class ResourceHud : MonoBehaviour
    {
        [SerializeField] private RideResourceController resources;
        [SerializeField] private Text coinLabel;
        [SerializeField] private Text fuelLabel;
        [SerializeField] private Image fuelFill;

        private bool subscribed;
        private int lastCoins = int.MinValue;
        private int lastFuel = int.MinValue;
        private bool lastEmpty;

        private void OnEnable() => Subscribe();

        private void Start() => Render(resources != null ? resources.Snapshot : default);

        private void OnDisable() => Unsubscribe();

        public void Configure(RideResourceController resourceController, Text coins, Text fuel, Image fill)
        {
            Unsubscribe();
            resources = resourceController;
            coinLabel = coins;
            fuelLabel = fuel;
            fuelFill = fill;
            Subscribe();
            Render(resources != null ? resources.Snapshot : default);
        }

        private void Subscribe()
        {
            if (subscribed || resources == null) return;
            resources.ResourcesUpdated += Render;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || resources == null) return;
            resources.ResourcesUpdated -= Render;
            subscribed = false;
        }

        private void Render(RideResourceSnapshot snapshot)
        {
            if (coinLabel != null && snapshot.Coins != lastCoins)
            {
                coinLabel.text = $"COINS  {snapshot.Coins}";
                lastCoins = snapshot.Coins;
            }
            var roundedFuel = Mathf.CeilToInt(snapshot.Fuel);
            if (fuelLabel != null && (roundedFuel != lastFuel || snapshot.IsEmpty != lastEmpty))
            {
                fuelLabel.text = snapshot.IsEmpty ? "FUEL  EMPTY" : $"FUEL  {roundedFuel}";
                lastFuel = roundedFuel;
                lastEmpty = snapshot.IsEmpty;
            }
            if (fuelFill == null) return;
            fuelFill.fillAmount = snapshot.FuelNormalized;
            fuelFill.color = snapshot.FuelNormalized <= 0.2f
                ? new Color(0.92f, 0.18f, 0.08f, 1f)
                : new Color(0.20f, 0.82f, 0.36f, 1f);
        }
    }
}
