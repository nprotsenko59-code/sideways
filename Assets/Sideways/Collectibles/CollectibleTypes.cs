namespace Sideways.Collectibles
{
    public enum CollectibleKind
    {
        Coin,
        FuelCanister
    }

    public readonly struct RideResourceSnapshot
    {
        public RideResourceSnapshot(float fuel, float maximumFuel, int coins)
        {
            Fuel = fuel;
            MaximumFuel = maximumFuel;
            Coins = coins;
        }

        public float Fuel { get; }
        public float MaximumFuel { get; }
        public int Coins { get; }
        public float FuelNormalized => MaximumFuel > 0f ? Fuel / MaximumFuel : 0f;
        public bool IsEmpty => Fuel <= 0f;
    }

    public readonly struct CollectiblePickup
    {
        public CollectiblePickup(CollectibleKind kind, float amount, RideResourceSnapshot resources)
        {
            Kind = kind;
            Amount = amount;
            Resources = resources;
        }

        public CollectibleKind Kind { get; }
        public float Amount { get; }
        public RideResourceSnapshot Resources { get; }
    }
}
