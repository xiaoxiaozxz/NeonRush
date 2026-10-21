using UnityEngine;

namespace LoongRacing
{
    [CreateAssetMenu(menuName = "NeonRush/Kart Tuning", fileName = "ArcadeKartTuning")]
    public sealed class KartTuning : ScriptableObject
    {
        [Header("Speed (metres / second)")]
        [Min(1f)] public float maximumForwardSpeed = 48f;
        [Min(1f)] public float maximumReverseSpeed = 9f;
        [Min(1f)] public float acceleration = 18f;
        [Min(1f)] public float reverseAcceleration = 10f;
        [Min(1f)] public float braking = 32f;
        [Min(0f)] public float coastDeceleration = 3.2f;
        [Min(0f)] public float reverseHoldDelay = 0.35f;

        [Header("Steering and grip")]
        [Min(1f)] public float lowSpeedSteering = 100f;
        [Min(1f)] public float highSpeedSteering = 76f;
        [Min(0.1f)] public float steeringResponse = 10f;
        [Min(0f)] public float lateralGrip = 14f;
        [Min(0f)] public float extraGravity = 18f;

        [Header("Drift and energy")]
        [Min(1f)] public float minimumDriftSpeed = 12f;
        [Min(0.1f)] public float driftGrip = 4.2f;
        [Range(1f, 2f)] public float driftSteeringMultiplier = 1.16f;
        [Range(0f, 3f)] public float driftEntrySlip = 2f;
        [Range(20f, 60f)] public float driftTargetAngle = 48f;
        [Min(0f)] public float driftDeceleration = 2.4f;
        [Range(0f, 1f)] public float driftAccelerationMultiplier = 0.65f;
        [Min(0.05f)] public float gripRecoverySeconds = 0.28f;
        [Min(0f)] public float driftChargePerSecond = 68f;
        [Min(0f)] public float minimumChargeAngle = 8f;
        [Range(20f, 80f)] public float maximumDriftAngle = 67f;

        [Header("Boosts")]
        [Min(0.1f)] public float nitroDuration = 2.5f;
        [Min(0f)] public float nitroAcceleration = 24f;
        [Min(0f)] public float nitroSpeedBonus = 18f;
        [Min(0f)] public float nitroKick = 5f;
        [Min(0.05f)] public float quickBoostDuration = 0.55f;
        [Min(0f)] public float quickBoostAcceleration = 30f;
        [Min(0f)] public float quickBoostSpeedBonus = 10f;
        [Min(0f)] public float quickBoostKick = 3f;
        [Min(0.1f)] public float miniBoostWindow = 1.15f;
        [Min(0.1f)] public float airBoostWindow = 1.1f;
        [Min(0.1f)] public float landingBoostWindow = 0.9f;
        [Min(0.05f)] public float minimumValidDriftTime = 0.25f;
        [Min(0f)] public float overspeedDeceleration = 9f;

        [Header("Camera")]
        [Range(40f, 90f)] public float cameraBaseFieldOfView = 58f;
        [Range(0f, 25f)] public float cameraSpeedFieldOfView = 12f;

        private void OnValidate()
        {
            maximumForwardSpeed = Mathf.Max(1f, maximumForwardSpeed);
            maximumReverseSpeed = Mathf.Max(1f, maximumReverseSpeed);
            acceleration = Mathf.Max(1f, acceleration);
            braking = Mathf.Max(1f, braking);
        }
    }
}
