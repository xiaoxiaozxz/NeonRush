using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LoongRacing.Tests
{
    public sealed class KartDrivingTests
    {
        private GameObject root;
        private ArcadeKartController kart;
        private KartTuning tuning;
        private PhysicsMaterial friction;
        private Vector3 spawn;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            root = new GameObject("Kart Physics Test");
            root.AddComponent<AudioListener>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(500f, -0.5f, 500f);
            floor.transform.localScale = new Vector3(600f, 1f, 900f);
            var vehicle = new GameObject("Test Kart");
            vehicle.SetActive(false);
            vehicle.layer = 2;
            vehicle.transform.SetParent(root.transform);
            spawn = new Vector3(500f, 0.6f, 470f);
            vehicle.transform.position = spawn;
            var collider = vehicle.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.05f, 0f);
            collider.size = new Vector3(1.72f, 0.9f, 3.2f);
            friction = new PhysicsMaterial("Test Kart Grip")
            {
                dynamicFriction = 0f, staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            collider.sharedMaterial = friction;
            vehicle.AddComponent<Rigidbody>();
            kart = vehicle.AddComponent<ArcadeKartController>();
            tuning = ScriptableObject.CreateInstance<KartTuning>();
            kart.Configure(tuning, null, null);
            vehicle.SetActive(true);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(kart.IsGrounded, Is.True, "Vehicle must settle on the floor before testing.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(tuning);
            Object.Destroy(friction);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcceleratesBrakesReversesAndResets()
        {
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.5f);
            Assert.That(kart.SpeedKph, Is.GreaterThan(65f), "Forward throttle must accelerate the kart.");
            Assert.That(kart.transform.position.z, Is.GreaterThan(spawn.z + 10f));
            kart.SetDriveInput(-1f, 0f);
            yield return DriveFor(0.7f);
            Assert.That(kart.SpeedKph, Is.LessThan(18f), "Brake must significantly reduce forward speed.");
            yield return DriveFor(1.3f);
            Assert.That(kart.ForwardSpeed, Is.LessThan(-2f), "Holding brake after stopping must engage reverse.");
            kart.ResetToSpawn();
            Assert.That(Vector3.Distance(kart.Body.position, spawn), Is.LessThan(0.01f));
            Assert.That(kart.Body.linearVelocity.magnitude, Is.LessThan(0.01f));
            Assert.That(kart.DrivingTime, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SteeringRequiresMovementAndTurnsInBothDirections()
        {
            kart.SetDriveInput(0f, 1f);
            yield return DriveFor(0.4f);
            Assert.That(Quaternion.Angle(Quaternion.identity, kart.transform.rotation), Is.LessThan(2f), "Stationary steering must not spin the vehicle.");
            kart.SetDriveInput(1f, 1f);
            yield return DriveFor(1.2f);
            float rightHeading = Vector3.SignedAngle(Vector3.forward, kart.transform.forward, Vector3.up);
            Assert.That(rightHeading, Is.GreaterThan(15f), "Right steering must turn right while accelerating.");
            kart.ResetToSpawn();
            kart.SetDriveInput(1f, -1f);
            yield return DriveFor(1.2f);
            float leftHeading = Vector3.SignedAngle(Vector3.forward, kart.transform.forward, Vector3.up);
            Assert.That(leftHeading, Is.LessThan(-15f), "Left steering must turn left while accelerating.");
        }

        [UnityTest]
        public IEnumerator WallStopsVehicleWithoutTunnelling()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position = spawn + new Vector3(0f, 1f, 16f);
            wall.transform.localScale = new Vector3(30f, 3f, 0.5f);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(2.5f);
            Assert.That(kart.Body.position.z, Is.LessThan(wall.transform.position.z - 1f), "The collider must remain in front of the wall.");
            Assert.That(kart.SpeedKph, Is.LessThan(8f), "Throttle must not drive through a static wall.");
        }

        [UnityTest]
        public IEnumerator WallImpactKeepsNitroUntilItsOriginalExpiry()
        {
            yield return EarnCharges();
            kart.RecoverToPose(spawn,Quaternion.identity);
            kart.SetDriveInput(1f,0f);
            kart.Body.linearVelocity=Vector3.forward*30f;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position=spawn+new Vector3(0f,1f,11f);
            wall.transform.localScale=new Vector3(30f,3f,0.5f);
            Physics.SyncTransforms();
            Assert.That(kart.TryUseNitro(),Is.True);
            yield return DriveFor(0.45f);
            Assert.That(kart.Body.position.z,Is.LessThan(wall.transform.position.z-1f));
            Assert.That(kart.SpeedKph,Is.LessThan(8f),"The impact must physically stop the vehicle.");
            Assert.That(kart.IsNitroActive,Is.True,"A wall hit must retain the running nitro.");
            Assert.That(kart.NitroCharges,Is.EqualTo(1));
            Assert.That(kart.NitroTimeRemaining,Is.LessThan(tuning.nitroDuration-0.3f),"Collision must not restart the duration.");
            yield return DriveFor(kart.NitroTimeRemaining+0.1f);
            Assert.That(kart.IsNitroActive,Is.False);
            Assert.That(kart.NitroCharges,Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RecoveryPreservesPartialChargeAndInventoryButNewRunClearsBoth()
        {
            tuning.driftChargePerSecond=115f;
            kart.SetDriveInput(1f,0f);
            yield return DriveFor(1.4f);
            kart.SetDriveInput(1f,0.6f);kart.SetDriftInput(true);
            yield return DriveFor(1.8f);
            Assert.That(kart.NitroCharges,Is.EqualTo(1));
            Assert.That(kart.DriftEnergy,Is.GreaterThan(5f));
            float energy=kart.DriftEnergy;
            kart.RequestRecovery();
            Assert.That(kart.DriftEnergy,Is.EqualTo(energy).Within(0.001f));
            Assert.That(kart.NitroCharges,Is.EqualTo(1));
            Assert.That(kart.IsDrifting,Is.False);
            Assert.That(kart.Body.linearVelocity.magnitude,Is.LessThan(0.01f));
            kart.RequestRecovery();
            Assert.That(kart.DriftEnergy,Is.EqualTo(energy).Within(0.001f));
            Assert.That(kart.NitroCharges,Is.EqualTo(1),"Repeated recovery must neither erase nor duplicate inventory.");
            kart.ResetToSpawn();
            Assert.That(kart.NitroCharges,Is.Zero);
            Assert.That(kart.DriftEnergy,Is.Zero);
        }

        [UnityTest]
        public IEnumerator AirborneThrottleDoesNotCreateForwardAcceleration()
        {
            kart.Body.position = spawn + Vector3.up * 20f;
            kart.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            kart.SetDriveInput(1f, 1f);
            yield return DriveFor(0.35f);
            Assert.That(kart.IsGrounded, Is.False);
            Assert.That(Mathf.Abs(kart.Body.linearVelocity.z), Is.LessThan(0.1f), "Ground traction must not accelerate the car in mid-air.");
        }

        private static IEnumerator DriveFor(float seconds)
        {
            int ticks = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < ticks; i++) yield return new WaitForFixedUpdate();
        }

        private IEnumerator EarnCharges()
        {
            tuning.driftChargePerSecond = 1000f;
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.35f);
            kart.SetDriveInput(1f, 0.7f);
            kart.SetDriftInput(true);
            yield return DriveFor(0.9f);
            Assert.That(kart.NitroCharges, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LargerDriftAngleAndFasterDefaultCharge()
        {
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.4f);
            kart.SetDriveInput(1f, 0.55f);
            kart.SetDriftInput(true);
            yield return DriveFor(1.2f);
            Assert.That(kart.IsDrifting, Is.True);
            Assert.That(Mathf.Abs(kart.DriftAngle), Is.GreaterThan(27f), "Moderate steering must produce the requested larger sideslip.");
            Assert.That(kart.DriftEnergy + kart.NitroCharges * 100f, Is.GreaterThan(30f), "Default charge gain must be noticeably faster.");
        }

        [UnityTest]
        public IEnumerator NitroConsumesOneChargeCapsSpeedAndExpires()
        {
            Assert.That(kart.TryUseNitro(), Is.False, "An empty inventory cannot boost.");
            yield return EarnCharges();
            kart.SetDriftInput(false);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(0.5f);
            kart.Body.rotation = Quaternion.identity;
            kart.Body.linearVelocity = Vector3.forward * 30f;
            Physics.SyncTransforms();
            Time.timeScale = 0f;
            Assert.That(kart.TryUseNitro(), Is.False, "Pause must not consume a charge.");
            Assert.That(kart.NitroCharges, Is.EqualTo(2));
            Time.timeScale = 1f;
            Assert.That(kart.TryUseNitro(), Is.True);
            Assert.That(kart.NitroCharges, Is.EqualTo(1));
            Assert.That(kart.TryUseNitro(), Is.False, "Holding Q must not stack or consume a second charge.");
            yield return DriveFor(1.2f);
            Assert.That(kart.SpeedKph, Is.GreaterThan(tuning.maximumForwardSpeed * 3.6f + 10f));
            Assert.That(kart.SpeedKph / 3.6f, Is.LessThanOrEqualTo(kart.CurrentMaximumSpeed + 0.8f));
            yield return DriveFor(tuning.nitroDuration - 1.2f + 0.15f);
            Assert.That(kart.IsNitroActive, Is.False);
            yield return DriveFor(2.2f);
            Assert.That(kart.SpeedKph / 3.6f, Is.LessThanOrEqualTo(tuning.maximumForwardSpeed + 0.8f), "Boost speed must not persist permanently.");
        }

        [UnityTest]
        public IEnumerator BufferedWOnDriftReleaseTriggersOneMiniBoost()
        {
            Assert.That(kart.TryUseQuickBoost(), Is.False);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.4f);
            kart.SetDriftInput(true);
            kart.SetDriveInput(1f, 0.7f);
            yield return DriveFor(0.85f);
            kart.SetDriftInput(false);
            kart.SetDriveInput(1f, 0f);
            kart.QueueQuickBoost();
            yield return DriveFor(0.06f);
            Assert.That(kart.ActiveBurst, Is.EqualTo(KartBoostKind.Mini));
            Assert.That(kart.MiniBoostReady, Is.False);
            yield return DriveFor(tuning.quickBoostDuration + 0.1f);
            Assert.That(kart.TryUseQuickBoost(), Is.False, "A consumed mini-boost cannot repeat on the same exit.");
        }

        [UnityTest]
        public IEnumerator MiniBoostWindowExpires()
        {
            yield return EarnCharges();
            kart.SetDriftInput(false);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(0.05f);
            Assert.That(kart.MiniBoostReady, Is.True);
            yield return DriveFor(tuning.miniBoostWindow + 0.1f);
            Assert.That(kart.TryUseQuickBoost(), Is.False, "Late presses must not trigger expired mini-boosts.");
        }

        [UnityTest]
        public IEnumerator AirBoostAndLandingBoostAreSeparateSingleUseWindows()
        {
            kart.Body.linearVelocity = new Vector3(0f, 16f, 20f);
            yield return DriveFor(0.24f);
            Assert.That(kart.IsGrounded, Is.False);
            Assert.That(kart.AirBoostReady, Is.True);
            float verticalBefore = kart.Body.linearVelocity.y;
            float speedBefore = kart.SpeedKph;
            Assert.That(kart.TryUseQuickBoost(), Is.True);
            Assert.That(kart.ActiveBurst, Is.EqualTo(KartBoostKind.Air));
            yield return DriveFor(0.06f);
            Assert.That(kart.SpeedKph, Is.GreaterThan(speedBefore + 5f));
            Assert.That(kart.Body.linearVelocity.y, Is.LessThan(verticalBefore), "Air boost must not create another jump.");
            yield return DriveFor(tuning.quickBoostDuration + 0.05f);
            Assert.That(kart.IsGrounded, Is.False);
            Assert.That(kart.TryUseQuickBoost(), Is.False, "A flight permits only one air boost.");
            for (int i = 0; i < 100 && !kart.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.That(kart.LandingBoostReady, Is.True);
            Assert.That(kart.TryUseQuickBoost(), Is.True);
            Assert.That(kart.ActiveBurst, Is.EqualTo(KartBoostKind.Landing));
            Assert.That(kart.LandingBoostReady, Is.False);
        }

        [UnityTest]
        public IEnumerator RealKeyboardE_Q_WBindingsTriggerDriftNitroAndMini()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
            yield return null;
#endif
            Assert.That(Application.isFocused, Is.True, "The Game view must have focus for the real keyboard reader.");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var reader = kart.gameObject.AddComponent<KartKeyboardInput>();
            tuning.driftChargePerSecond = 1000f;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return DriveFor(1.4f);
                Assert.That(kart.SpeedKph, Is.GreaterThan(60f), "Keyboard must drive through the actual input reader.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.E, Key.RightArrow));
                yield return DriveFor(1.1f);
                Assert.That(kart.IsDrifting, Is.True, "E must enter drift.");
                Assert.That(kart.NitroCharges, Is.EqualTo(2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return DriveFor(0.06f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.Q, Key.W));
                yield return DriveFor(0.12f);
                Assert.That(kart.IsNitroActive, Is.True, "Q must release nitro.");
                Assert.That(kart.ActiveBurst, Is.EqualTo(KartBoostKind.Mini), "A fresh W press must release the mini-boost.");
                Assert.That(kart.NitroCharges, Is.EqualTo(1));
                yield return DriveFor(0.12f);
                Assert.That(kart.NitroCharges, Is.EqualTo(1), "A held key must not repeatedly spend charges.");
            }
            finally
            {
                Object.Destroy(reader);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator LowSpeedAndOrdinarySteeringDoNotChargeEnergy()
        {
            kart.SetDriveInput(0f, 1f);
            kart.SetDriftInput(true);
            yield return DriveFor(0.3f);
            Assert.That(kart.IsDrifting, Is.False, "Stationary input cannot enter drift.");
            kart.SetDriftInput(false);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.3f);
            kart.SetDriveInput(1f, 1f);
            yield return DriveFor(0.7f);
            Assert.That(kart.IsDrifting, Is.False);
            Assert.That(kart.DriftEnergy, Is.Zero, "Ordinary cornering must not collect drift energy.");
        }

        [UnityTest]
        public IEnumerator DriftCreatesSlipChargesAndRecoversOnRelease()
        {
            tuning.driftChargePerSecond = 180f;
            int readySignals = 0;
            kart.NitroChargeAdded += () => readySignals++;
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.4f);
            kart.SetDriftInput(true);
            kart.SetDriveInput(1f, 1f);
            yield return DriveFor(1.1f);
            Assert.That(kart.IsDrifting, Is.True, $"Drift ended early: speed={kart.SpeedKph:0.0}, angle={kart.DriftAngle:0.0}, forward={kart.ForwardSpeed:0.0}");
            Assert.That(Mathf.Abs(kart.DriftAngle), Is.GreaterThan(12f), "Drifting must create real physical sideslip.");
            Assert.That(kart.DriftEnergy + kart.NitroCharges * 100f, Is.GreaterThan(20f));
            yield return DriveFor(3.0f);
            Assert.That(kart.NitroCharges, Is.EqualTo(2), "Energy must fill the two-slot inventory.");
            Assert.That(readySignals, Is.EqualTo(2), "Each stored charge must emit exactly one ready cue.");
            Assert.That(kart.DriftEnergy, Is.Zero, "Full inventory must stop accumulating energy.");
            kart.SetDriftInput(false);
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(0.65f);
            Assert.That(kart.IsDrifting, Is.False);
            Assert.That(Mathf.Abs(kart.DriftAngle), Is.LessThan(6f), "Grip must recover after release.");
            kart.ResetToSpawn();
            Assert.That(kart.NitroCharges, Is.Zero);
            Assert.That(kart.DriftEnergy, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LeavingGroundStopsDriftAndEnergyGain()
        {
            kart.SetDriveInput(1f, 0f);
            yield return DriveFor(1.3f);
            kart.SetDriftInput(true);
            kart.SetDriveInput(1f, 1f);
            yield return DriveFor(0.5f);
            Assert.That(kart.IsDrifting, Is.True);
            float before = kart.DriftEnergy;
            kart.Body.position += Vector3.up * 20f;
            Physics.SyncTransforms();
            yield return DriveFor(0.3f);
            Assert.That(kart.IsGrounded, Is.False);
            Assert.That(kart.IsDrifting, Is.False);
            Assert.That(kart.DriftEnergy, Is.EqualTo(before).Within(0.001f));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AudioBankPlaysLoopsAndRespondsToSpeedAndMute()
        {
            var bank = UnityEditor.AssetDatabase.LoadAssetAtPath<KartAudioBank>("Assets/LoongRacing/Settings/KartAudioBank.asset");
            Assert.That(bank, Is.Not.Null);
            kart.gameObject.SetActive(false);
            var sound = kart.gameObject.AddComponent<KartAudioController>();
            sound.Configure(bank);
            kart.gameObject.SetActive(true);
            yield return null;
            float originalVolume = sound.MasterVolume;
            try
            {
                var sources = kart.GetComponentsInChildren<AudioSource>();
                Assert.That(sources.Length, Is.EqualTo(6));
                foreach (var source in sources)
                    if (source.loop)
                    {
                        Assert.That(source.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
                        Assert.That(source.isPlaying, Is.True);
                    }
                float idlePitch = sound.EnginePitch;
                kart.Body.linearVelocity = Vector3.forward * 30f;
                yield return DriveFor(0.3f);
                Assert.That(sound.EnginePitch, Is.GreaterThan(idlePitch + 0.2f));
                sound.SetMasterVolume(0f);
                yield return DriveFor(0.7f);
                Assert.That(sound.EngineVolume, Is.LessThan(0.001f));
                sound.SetMasterVolume(0.5f);
                yield return DriveFor(0.3f);
                Assert.That(sound.EngineVolume, Is.GreaterThan(0.02f));
            }
            finally { sound.SetMasterVolume(originalVolume); }
        }
#endif
    }
}
