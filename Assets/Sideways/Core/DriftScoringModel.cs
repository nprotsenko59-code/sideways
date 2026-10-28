using System;
using Sideways.Configuration;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Core
{
    public sealed class DriftScoringModel
    {
        private readonly ScoringProfile profile;
        private float bankedScore;
        private float activeComboScore;
        private float comboDriftTime;
        private float graceRemaining;
        private float collisionLockoutRemaining;
        private bool scoring;
        private bool comboGraceHeld;

        public DriftScoringModel(ScoringProfile scoringProfile)
        {
            profile = scoringProfile != null ? scoringProfile : throw new ArgumentNullException(nameof(scoringProfile));
        }

        public DriftScoreSnapshot Snapshot => new(
            Mathf.FloorToInt(bankedScore + activeComboScore),
            Mathf.FloorToInt(bankedScore),
            Mathf.FloorToInt(activeComboScore),
            CurrentMultiplier,
            comboDriftTime,
            graceRemaining,
            scoring);

        private float CurrentMultiplier => Mathf.Min(
            profile.maximumMultiplier,
            1f + Mathf.Floor(comboDriftTime / profile.multiplierStepSeconds) * profile.multiplierStep);

        public DriftScoreSnapshot Tick(VehicleTelemetry telemetry, float deltaTime)
        {
            var safeDelta = Mathf.Clamp(deltaTime, 0f, profile.maximumScoringDeltaTime);
            collisionLockoutRemaining = Mathf.Max(0f, collisionLockoutRemaining - safeDelta);
            scoring = collisionLockoutRemaining <= 0f && IsEligible(telemetry);

            if (scoring)
            {
                comboDriftTime += safeDelta;
                graceRemaining = profile.comboGraceDuration;
                activeComboScore += CalculatePointsPerSecond(telemetry) * CurrentMultiplier * safeDelta;
            }
            else if (activeComboScore > 0f && !comboGraceHeld)
            {
                graceRemaining = Mathf.Max(0f, graceRemaining - safeDelta);
                if (graceRemaining <= 0f) BankCombo();
            }

            return Snapshot;
        }

        public bool RegisterImpact(float relativeSpeed)
        {
            collisionLockoutRemaining = Mathf.Max(collisionLockoutRemaining, profile.collisionScoringLockout);
            scoring = false;
            if (relativeSpeed < profile.severeCollisionSpeed) return false;
            return CancelCombo();
        }

        public bool BankCombo()
        {
            if (activeComboScore <= 0f) return false;
            bankedScore += activeComboScore;
            ResetActiveCombo();
            return true;
        }

        public bool CancelCombo()
        {
            if (activeComboScore <= 0f) return false;
            ResetActiveCombo();
            return true;
        }

        public bool AwardBankedBonus(int points)
        {
            if (points <= 0) return false;
            bankedScore += points;
            return true;
        }

        public void SetComboGraceHeld(bool held)
        {
            comboGraceHeld = held && activeComboScore > 0f;
        }

        public bool IsEligible(VehicleTelemetry telemetry)
        {
            var slip = Mathf.Abs(telemetry.SlipAngle);
            return telemetry.Grounded &&
                   telemetry.Drifting &&
                   telemetry.Speed >= profile.minimumSpeed &&
                   slip >= profile.minimumSlipAngle &&
                   slip <= profile.maximumSlipAngle &&
                   telemetry.AngleFromUpright <= profile.maximumVehicleTilt &&
                   Mathf.Abs(telemetry.YawRateDegrees) <= profile.maximumYawRate;
        }

        private float CalculatePointsPerSecond(VehicleTelemetry telemetry)
        {
            var slip = Mathf.Abs(telemetry.SlipAngle);
            var angleQuality = slip <= profile.idealSlipAngle
                ? Mathf.InverseLerp(profile.minimumSlipAngle, profile.idealSlipAngle, slip)
                : Mathf.InverseLerp(profile.maximumSlipAngle, profile.idealSlipAngle, slip);
            var angleFactor = Mathf.Lerp(profile.minimumAngleFactor, 1f, angleQuality);
            var speedQuality = Mathf.InverseLerp(profile.minimumSpeed, profile.fullSpeedBonusAt, telemetry.Speed);
            var speedFactor = Mathf.Lerp(1f, profile.maximumSpeedMultiplier, speedQuality);
            return profile.basePointsPerSecond * angleFactor * speedFactor;
        }

        private void ResetActiveCombo()
        {
            activeComboScore = 0f;
            comboDriftTime = 0f;
            graceRemaining = 0f;
            scoring = false;
            comboGraceHeld = false;
        }
    }
}
