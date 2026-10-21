using System;
using UnityEngine;

namespace LoongRacing
{
    public enum KartBoostKind { None, Nitro, Mini, Air, Landing }

    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeKartController : MonoBehaviour
    {
        [SerializeField] private KartTuning tuning;
        [SerializeField] private Transform visualBody;
        [SerializeField] private Transform[] frontWheelPivots;
        private Rigidbody body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float throttleInput;
        private float steerInput;
        private float reverseHold;
        private bool ownsTuning;
        private bool wasMoving;
        private bool driftHeld;
        private bool driftReleaseRequired;
        private float recoveryTime;
        private float chargeBlockedUntil;
        private float validDriftTime;
        private float miniReadyUntil, landingReadyUntil, takeoffTime;
        private float nitroUntil, burstUntil, burstRequestUntil;
        private bool hasGroundSample, previousGrounded, flightEligible, airBoostUsed;
        private KartBoostKind burstKind;

        public KartTuning Tuning => tuning;
        public bool DrivingEnabled { get; private set; } = true;
        public Rigidbody Body => body;
        public float SpeedKph => body == null ? 0f : Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f;
        public float ForwardSpeed => body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);
        public float Steering { get; private set; }
        public float Throttle => throttleInput;
        public event Action Respawned;
        public event Action PracticeReset;
        public event Action NitroChargeAdded;
        public event Action<KartBoostKind> BoostStarted;
        public bool IsDrifting { get; private set; }
        public float DriftAngle { get; private set; }
        public float DriftEnergy { get; private set; }
        public int NitroCharges { get; private set; }
        public const int MaximumNitroCharges = 2;
        public bool IsNitroActive => nitroUntil > Time.time;
        public bool IsBurstActive => burstUntil > Time.time;
        public bool IsBoosting => IsNitroActive || IsBurstActive;
        public float NitroTimeRemaining => Mathf.Max(0f, nitroUntil - Time.time);
        public float BurstTimeRemaining => Mathf.Max(0f, burstUntil - Time.time);
        public KartBoostKind ActiveBurst => IsBurstActive ? burstKind : KartBoostKind.None;
        public bool MiniBoostReady => IsGrounded && !IsDrifting && miniReadyUntil > Time.time;
        public bool AirBoostReady => !IsGrounded && flightEligible && !airBoostUsed
            && Time.time - takeoffTime >= 0.1f && Time.time - takeoffTime <= tuning.airBoostWindow;
        public bool LandingBoostReady => IsGrounded && landingReadyUntil > Time.time;
        public KartBoostKind ReadyQuickBoost => AirBoostReady ? KartBoostKind.Air : LandingBoostReady
            ? KartBoostKind.Landing : MiniBoostReady ? KartBoostKind.Mini : KartBoostKind.None;
        public float QuickBoostWindowRemaining => AirBoostReady ? tuning.airBoostWindow - (Time.time - takeoffTime)
            : LandingBoostReady ? landingReadyUntil - Time.time : MiniBoostReady ? miniReadyUntil - Time.time : 0f;
        public float CurrentMaximumSpeed => tuning.maximumForwardSpeed
            + Mathf.Max(IsNitroActive ? tuning.nitroSpeedBonus : 0f, IsBurstActive ? tuning.quickBoostSpeedBonus : 0f)
            + (IsNitroActive && IsBurstActive ? 4f : 0f);
        public bool IsGrounded { get; private set; }
        public bool IsReversing => ForwardSpeed < -0.6f;
        public float DrivingTime { get; private set; }
        public float DistanceMetres { get; private set; }
        public float TopSpeedKph { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<KartTuning>();
                ownsTuning = true;
            }
            body.mass = 250f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, -0.25f, 0f);
            body.maxAngularVelocity = 4f;
            body.maxLinearVelocity = 90f;
            RememberSpawn();
        }

        public void Configure(KartTuning settings, Transform model, Transform[] steeringWheels)
        {
            if (ownsTuning && tuning != null) Destroy(tuning);
            ownsTuning = false;
            tuning = settings;
            visualBody = model;
            frontWheelPivots = steeringWheels;
        }

        // Shared driver interface: keyboard now, AI or replay input in later stages.
        public void SetDriveInput(float throttle, float steering)
        {
            if (!DrivingEnabled) { throttleInput = steerInput = 0f; return; }
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steering, -1f, 1f);
        }

        public void SetDriftInput(bool held)
        {
            driftHeld = held && DrivingEnabled;
            if (!held) driftReleaseRequired = false;
        }

        private void EndDrift(bool allowMini = false)
        {
            if (!IsDrifting) return;
            IsDrifting = false;
            driftReleaseRequired = true;
            recoveryTime = tuning.gripRecoverySeconds;
            if (allowMini && validDriftTime >= tuning.minimumValidDriftTime)
                miniReadyUntil = Time.time + tuning.miniBoostWindow;
            validDriftTime = 0f;
        }

        public void QueueQuickBoost() { if (DrivingEnabled && Time.timeScale > 0f) burstRequestUntil = Time.time + 0.14f; }
        public void CancelBufferedActions() => burstRequestUntil = 0f;

        public void SetDrivingEnabled(bool enabled)
        {
            DrivingEnabled = enabled;
            if (enabled) return;
            SetDriveInput(0f, 0f);
            SetDriftInput(false);
            EndDrift();
            Steering = 0f;
            CancelBufferedActions();
        }

        public bool TryUseNitro()
        {
            if (!DrivingEnabled || Time.timeScale <= 0f || NitroCharges <= 0 || IsNitroActive || ForwardSpeed < 1f) return false;
            NitroCharges--;
            nitroUntil = Time.time + tuning.nitroDuration;
            ApplyBoostKick(tuning.nitroKick);
            BoostStarted?.Invoke(KartBoostKind.Nitro);
            return true;
        }

        public bool TryUseQuickBoost()
        {
            if (!DrivingEnabled || Time.timeScale <= 0f || IsBurstActive || ForwardSpeed < 1f) return false;
            var kind = ReadyQuickBoost;
            if (kind == KartBoostKind.None) return false;
            if (kind == KartBoostKind.Air) airBoostUsed = true;
            if (kind == KartBoostKind.Landing) landingReadyUntil = 0f;
            if (kind == KartBoostKind.Mini) miniReadyUntil = 0f;
            burstKind = kind;
            burstUntil = Time.time + tuning.quickBoostDuration;
            burstRequestUntil = 0f;
            ApplyBoostKick(tuning.quickBoostKick);
            BoostStarted?.Invoke(kind);
            return true;
        }

        private Vector3 BoostDirection()
        {
            Vector3 travel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            return IsDrifting && travel.sqrMagnitude > 1f ? travel.normalized : transform.forward;
        }

        private void ApplyBoostKick(float kick)
        {
            float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            body.AddForce(BoostDirection() * Mathf.Min(kick, Mathf.Max(0f, CurrentMaximumSpeed - speed)), ForceMode.VelocityChange);
        }

        private void UpdateFlight()
        {
            if (!hasGroundSample) { previousGrounded = IsGrounded; hasGroundSample = true; return; }
            if (previousGrounded && !IsGrounded)
            {
                takeoffTime = Time.time;
                flightEligible = SpeedKph > 28f;
                airBoostUsed = false;
                miniReadyUntil = landingReadyUntil = 0f;
            }
            if (!previousGrounded && IsGrounded)
            {
                if (flightEligible && Time.time - takeoffTime >= 0.14f)
                    landingReadyUntil = Time.time + tuning.landingBoostWindow;
                flightEligible = false;
            }
            previousGrounded = IsGrounded;
        }

        private void ApplyBoostForces(float dt)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float speed = horizontal.magnitude;
            if (IsBoosting && throttleInput >= -0.05f)
            {
                float acceleration = (IsNitroActive ? tuning.nitroAcceleration : 0f)
                    + (IsBurstActive ? tuning.quickBoostAcceleration * (IsNitroActive ? 0.55f : 1f) : 0f);
                float available = Mathf.Max(0f, (CurrentMaximumSpeed - speed) / dt);
                body.AddForce(BoostDirection() * Mathf.Min(acceleration, available), ForceMode.Acceleration);
            }
            if (IsGrounded && speed > CurrentMaximumSpeed)
                body.AddForce(-horizontal.normalized * Mathf.Min(tuning.overspeedDeceleration,
                    (speed - CurrentMaximumSpeed) / dt), ForceMode.Acceleration);
        }

        private void UpdateDrift(float forwardSpeed, float sidewaysSpeed, float dt)
        {
            DriftAngle = Mathf.Atan2(-sidewaysSpeed, Mathf.Max(0.1f, forwardSpeed)) * Mathf.Rad2Deg;
            recoveryTime = Mathf.Max(0f, recoveryTime - dt);
            bool eligible = IsGrounded && forwardSpeed >= tuning.minimumDriftSpeed * (IsDrifting ? 0.6f : 1f) && throttleInput >= -0.05f
                && Time.time >= chargeBlockedUntil;
            if (IsDrifting && (!driftHeld || !eligible || Mathf.Abs(DriftAngle) > tuning.maximumDriftAngle))
                EndDrift(!driftHeld && eligible && Mathf.Abs(DriftAngle) < tuning.maximumDriftAngle);
            if (!IsDrifting && eligible && driftHeld && !driftReleaseRequired && Mathf.Abs(Steering) > 0.25f)
            {
                IsDrifting = true;
                validDriftTime = 0f;
                miniReadyUntil = 0f;
                body.AddForce(-transform.right * Mathf.Sign(Steering) * tuning.driftEntrySlip, ForceMode.VelocityChange);
            }
            if (!IsDrifting) return;
            if (Mathf.Abs(DriftAngle) >= tuning.minimumChargeAngle) validDriftTime += dt;
            if (NitroCharges >= MaximumNitroCharges) return;
            float angleFactor = Mathf.InverseLerp(tuning.minimumChargeAngle, 34f, Mathf.Abs(DriftAngle));
            float speedFactor = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(forwardSpeed / tuning.maximumForwardSpeed));
            DriftEnergy += tuning.driftChargePerSecond * angleFactor * speedFactor * dt;
            if (DriftEnergy >= 100f)
            {
                NitroCharges++;
                DriftEnergy = NitroCharges >= MaximumNitroCharges ? 0f : DriftEnergy - 100f;
                NitroChargeAdded?.Invoke();
            }
        }

        public void RememberSpawn()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        public void ResetToSpawn()
        {
            RecoverToPose(spawnPosition, spawnRotation, false);
            DrivingTime = DistanceMetres = TopSpeedKph = 0f;
            wasMoving = false;
            PracticeReset?.Invoke();
        }

        public void RequestRecovery()
        {
            var progress = GetComponent<KartRouteProgress>();
            if (progress != null && progress.Route != null) progress.Recover();
            else RecoverToPose(spawnPosition, spawnRotation);
        }

        public void RecoverToPose(Vector3 position, Quaternion rotation, bool preserveEnergy = true)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            SetDriveInput(0f, 0f);
            Steering = 0f;
            reverseHold = 0f;
            driftHeld = false;
            driftReleaseRequired = false;
            IsDrifting = false;
            DriftAngle = recoveryTime = 0f;
            if (!preserveEnergy) { DriftEnergy = 0f; NitroCharges = 0; }
            chargeBlockedUntil = 0f;
            validDriftTime = miniReadyUntil = landingReadyUntil = takeoffTime = 0f;
            nitroUntil = burstUntil = burstRequestUntil = 0f;
            flightEligible = airBoostUsed = hasGroundSample = previousGrounded = false;
            burstKind = KartBoostKind.None;
            IsGrounded = false;
            Physics.SyncTransforms();
            body.WakeUp();
            var follow = FindFirstObjectByType<KartChaseCamera>();
            if (follow != null) follow.SnapToTarget();
            Respawned?.Invoke();
        }

        private void FixedUpdate()
        {
            if (tuning == null) return;
            float dt = Time.fixedDeltaTime;
            IsGrounded = Physics.SphereCast(body.position + Vector3.up * 0.1f, 0.18f, Vector3.down,
                out RaycastHit ground, 0.64f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && Vector3.Dot(ground.normal, Vector3.up) > 0.5f;
            UpdateFlight();
            if (!DrivingEnabled)
            {
                body.linearVelocity = Vector3.up * body.linearVelocity.y;
                body.angularVelocity = Vector3.zero;
                return;
            }
            Steering = Mathf.Lerp(Steering, steerInput, 1f - Mathf.Exp(-tuning.steeringResponse * dt));
            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float sidewaysSpeed = Vector3.Dot(body.linearVelocity, transform.right);
            UpdateDrift(forwardSpeed, sidewaysSpeed, dt);
            if (burstRequestUntil > Time.time) TryUseQuickBoost();

            if (IsGrounded)
            {
                float speed = Mathf.Abs(forwardSpeed);
                float steeringRate = Mathf.Lerp(tuning.lowSpeedSteering, tuning.highSpeedSteering,
                    Mathf.InverseLerp(12f, tuning.maximumForwardSpeed, speed));
                if (IsDrifting) steeringRate *= tuning.driftSteeringMultiplier;
                float movingFactor = Mathf.Clamp01(speed / 7f);
                float direction = forwardSpeed < -0.1f ? -1f : 1f;
                float yawRate = Steering * steeringRate * movingFactor * direction * Mathf.Deg2Rad;
                float driveAcceleration = 0f;
                if (throttleInput > 0.05f)
                {
                    reverseHold = 0f;
                    driveAcceleration = forwardSpeed < -0.5f ? tuning.braking :
                        tuning.acceleration * throttleInput * Mathf.Clamp01(1f - Mathf.Pow(Mathf.Max(0f, forwardSpeed) / tuning.maximumForwardSpeed, 2f));
                }
                else if (throttleInput < -0.05f)
                {
                    if (forwardSpeed > 0.5f)
                    {
                        reverseHold = 0f;
                        driveAcceleration = -Mathf.Min(tuning.braking * -throttleInput, forwardSpeed / dt);
                    }
                    else
                    {
                        reverseHold += dt;
                        driveAcceleration = reverseHold >= tuning.reverseHoldDelay
                            ? -tuning.reverseAcceleration * -throttleInput * Mathf.Clamp01(1f - Mathf.Max(0f, -forwardSpeed) / tuning.maximumReverseSpeed)
                            : -forwardSpeed / dt;
                    }
                }
                else
                {
                    reverseHold = 0f;
                    driveAcceleration = -Mathf.Sign(forwardSpeed) * Mathf.Min(Mathf.Abs(forwardSpeed) / dt, tuning.coastDeceleration);
                }

                float grip = IsDrifting ? tuning.driftGrip : Mathf.Lerp(tuning.lateralGrip, tuning.driftGrip,
                    recoveryTime / Mathf.Max(0.05f, tuning.gripRecoverySeconds));
                float gripAcceleration = -sidewaysSpeed * (1f - Mathf.Exp(-grip * dt)) / dt;
                Vector3 driveDirection = transform.forward;
                Vector3 lateralForce = transform.right * gripAcceleration;
                if (IsDrifting)
                {
                    Vector3 travel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                    driveDirection = travel.normalized;
                    // Rotate the travel direction without letting lateral grip erase speed.
                    // The explicit drift drag controls speed loss instead of an accidental spin-stop.
                    float targetAngle = Mathf.Sign(Steering) * Mathf.Lerp(18f, tuning.driftTargetAngle, Mathf.Abs(Steering));
                    float travelYaw = yawRate + (DriftAngle - targetAngle) * Mathf.Deg2Rad * tuning.driftGrip;
                    // Keep entry momentum pointing down the road while the body swings into drift.
                    if (Mathf.Abs(Steering) > 0.2f && Mathf.Sign(DriftAngle) == Mathf.Sign(Steering))
                        travelYaw = Mathf.Sign(yawRate) * Mathf.Clamp(travelYaw * Mathf.Sign(yawRate), 0f, 3f);
                    lateralForce = Vector3.Cross(Vector3.up, driveDirection) * (Mathf.Clamp(travelYaw, -3f, 3f) * travel.magnitude);
                    driveAcceleration = throttleInput > 0.05f
                        ? tuning.acceleration * throttleInput * tuning.driftAccelerationMultiplier
                            * Mathf.Clamp01(1f - Mathf.Pow(travel.magnitude / tuning.maximumForwardSpeed, 2f))
                        : -tuning.coastDeceleration;
                    driveAcceleration -= tuning.driftDeceleration;
                }
                body.AddForce(driveDirection * driveAcceleration + lateralForce, ForceMode.Acceleration);

                body.angularVelocity = Vector3.up * yawRate;
            }
            else
            {
                // Ground throttle has no traction in the air; explicit boosts are applied below.
                body.angularVelocity = Vector3.Lerp(body.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-3f * dt));
            }

            ApplyBoostForces(dt);
            body.AddForce(Vector3.down * tuning.extraGravity, ForceMode.Acceleration);
            float speedKph = SpeedKph;
            wasMoving |= speedKph > 1f;
            if (wasMoving) DrivingTime += dt;
            DistanceMetres += speedKph / 3.6f * dt;
            TopSpeedKph = Mathf.Max(TopSpeedKph, speedKph);
            if (body.position.y < -12f) RequestRecovery();
        }

        private void LateUpdate()
        {
            if (visualBody != null)
            {
                Quaternion lean = Quaternion.Euler(-throttleInput * 1.3f, 0f, -Steering * Mathf.Clamp01(SpeedKph / 90f) * (IsDrifting ? 7f : 4f));
                visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, lean, 1f - Mathf.Exp(-9f * Time.deltaTime));
            }
            if (frontWheelPivots == null) return;
            foreach (Transform wheel in frontWheelPivots)
                if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, Steering * 24f, 0f);
        }

        private void OnDestroy()
        {
            if (ownsTuning && tuning != null) Destroy(tuning);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;
            var contact = collision.GetContact(0);
            if (Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > 0.6f) return;
            if (Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)) < 3f) return;
            EndDrift();
            chargeBlockedUntil = Time.time + 0.45f;
            miniReadyUntil = landingReadyUntil = burstUntil = burstRequestUntil = 0f;
        }
    }
}
