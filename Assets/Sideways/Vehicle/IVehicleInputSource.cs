namespace Sideways.Vehicle
{
    public interface IVehicleInputSource
    {
        float Steering { get; }
        bool ConsumeResetRequest();
    }
}
