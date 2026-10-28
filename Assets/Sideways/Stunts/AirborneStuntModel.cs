using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Stunts
{
    public readonly struct StuntUpdate
    {
        public StuntUpdate(bool began, bool ended, bool comboHold, bool cleanLanding, int bonus, float airtime)
        {
            Began = began;
            Ended = ended;
            ComboHold = comboHold;
            CleanLanding = cleanLanding;
            Bonus = bonus;
            Airtime = airtime;
        }

        public bool Began { get; }
        public bool Ended { get; }
        public bool ComboHold { get; }
        public bool CleanLanding { get; }
        public int Bonus { get; }
        public float Airtime { get; }
    }

    public sealed class AirborneStuntModel
    {
        private readonly StuntProfile profile;
        private bool armed;
        private bool airborne;
        private float armedTime;
        private float airtime;
        private float cooldown;
        private float lastVerticalSpeed;

        public AirborneStuntModel(StuntProfile stuntProfile)
        {
            profile = stuntProfile;
        }

        public bool IsAirborneStunt => airborne;
        public float Airtime => airtime;

        public bool Arm(float speed)
        {
            if (profile == null || cooldown > 0f || armed || airborne || speed < profile.minimumLaunchSpeed) return false;
            armed = true;
            armedTime = 0f;
            return true;
        }

        public StuntUpdate Tick(bool grounded, float speed, float tilt, float verticalSpeed, float deltaTime)
        {
            var dt = Mathf.Max(0f, deltaTime);
            cooldown = Mathf.Max(0f, cooldown - dt);

            if (armed && !airborne)
            {
                armedTime += dt;
                if (!grounded)
                {
                    armed = false;
                    airborne = true;
                    airtime = 0f;
                    lastVerticalSpeed = verticalSpeed;
                    return new StuntUpdate(true, false, true, false, 0, 0f);
                }
                if (armedTime > profile.launchWindow) armed = false;
            }

            if (!airborne) return default;
            if (!grounded)
            {
                airtime += dt;
                lastVerticalSpeed = verticalSpeed;
                return new StuntUpdate(false, false, airtime <= profile.maximumCreditedAirtime, false, 0, airtime);
            }

            var creditedAirtime = Mathf.Min(airtime, profile.maximumCreditedAirtime);
            var clean = airtime >= profile.minimumAirtime &&
                        speed >= profile.minimumLandingSpeed &&
                        tilt <= profile.maximumLandingTilt &&
                        Mathf.Max(0f, -lastVerticalSpeed) <= profile.maximumDownwardLandingSpeed;
            var bonus = clean ? profile.cleanLandingPoints + Mathf.FloorToInt(creditedAirtime * profile.pointsPerAirborneSecond) : 0;
            var completedAirtime = airtime;
            airborne = false;
            airtime = 0f;
            cooldown = profile.rearmDelay;
            return new StuntUpdate(false, true, false, clean, bonus, completedAirtime);
        }
    }
}
