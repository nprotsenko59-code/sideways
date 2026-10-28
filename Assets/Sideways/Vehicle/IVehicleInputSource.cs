namespace Sideways.Vehicle
{
    public interface IVehicleInputSource
    {
        float Steering { get; }
        float DriftStrength { get; }
        bool Brake { get; }
        bool ConsumeResetRequest();
    }
}
