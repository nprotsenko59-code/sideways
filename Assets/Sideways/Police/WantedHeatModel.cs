using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Police
{
    public sealed class WantedHeatModel
    {
        private readonly PoliceProfile profile;
        private float exposure;

        public WantedHeatModel(PoliceProfile policeProfile)
        {
            profile = policeProfile;
        }

        public int Level { get; private set; }
        public float Exposure => exposure;
        public int ActiveUnitCount => Level switch
        {
            >= 3 => profile.heatThreeUnits,
            2 => profile.heatTwoUnits,
            1 => profile.heatOneUnits,
            _ => 1
        };
        public bool RoadblockRequired => Level >= profile.roadblockHeatLevel;
        public int EscapeReward => Level * profile.escapeRewardPerHeat;

        public bool ReportInitialOffense()
        {
            if (Level > 0) return false;
            Level = 1;
            return true;
        }

        public bool Tick(bool recklessAndVisible, float deltaTime)
        {
            if (Level == 0 || !recklessAndVisible) return false;
            exposure += Mathf.Max(0f, deltaTime);
            var nextLevel = exposure >= profile.heatThreeExposure ? 3 : exposure >= profile.heatTwoExposure ? 2 : 1;
            if (nextLevel == Level) return false;
            Level = nextLevel;
            return true;
        }

        public float GetLevelProgress()
        {
            if (Level <= 0) return 0f;
            if (Level == 1) return Mathf.Clamp01(exposure / profile.heatTwoExposure);
            if (Level == 2) return Mathf.Clamp01((exposure - profile.heatTwoExposure) / (profile.heatThreeExposure - profile.heatTwoExposure));
            return 1f;
        }

        public void Reset()
        {
            exposure = 0f;
            Level = 0;
        }
    }
}
