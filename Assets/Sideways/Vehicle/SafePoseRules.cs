namespace Sideways.Vehicle
{
    public static class SafePoseRules
    {
        public static bool IsEligible(bool grounded, float angleFromUpright, float maximumTilt, bool roadBelow, bool obstacleFree)
        {
            return grounded && angleFromUpright <= maximumTilt && roadBelow && obstacleFree;
        }
    }
}
