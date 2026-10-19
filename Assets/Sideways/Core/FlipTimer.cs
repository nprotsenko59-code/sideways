namespace Sideways.Core
{
    public sealed class FlipTimer
    {
        private readonly float angleThreshold;
        private readonly float duration;
        private float elapsed;

        public FlipTimer(float angleThreshold, float duration)
        {
            this.angleThreshold = angleThreshold;
            this.duration = duration;
        }

        public float Elapsed => elapsed;

        public bool Tick(float angleFromUpright, float deltaTime)
        {
            if (angleFromUpright <= angleThreshold)
            {
                elapsed = 0f;
                return false;
            }

            elapsed += deltaTime;
            return elapsed >= duration;
        }

        public void Reset() => elapsed = 0f;
    }
}
