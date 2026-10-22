using UnityEngine;
using UnityEngine.InputSystem;

namespace LoongRacing
{
    [RequireComponent(typeof(ArcadeKartController))]
    public sealed class KartKeyboardInput : MonoBehaviour
    {
        private ArcadeKartController kart;
        private void Awake() => kart = GetComponent<ArcadeKartController>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !kart.DrivingEnabled || Time.timeScale == 0f || !Application.isFocused)
            {
                kart.SetDriveInput(0f, 0f);
                kart.SetDriftInput(false);
                kart.CancelBufferedActions();
                return;
            }
            float forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            float backward = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
            float right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            float left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            kart.SetDriveInput(forward - backward, right - left);
            kart.SetDriftInput(keyboard.eKey.isPressed || keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            if (keyboard.rKey.wasPressedThisFrame) { kart.RequestRecovery(); return; }
            if (keyboard.qKey.wasPressedThisFrame) kart.TryUseNitro();
            if (keyboard.wKey.wasPressedThisFrame) kart.QueueQuickBoost();
        }

        private void OnDisable()
        {
            if (kart != null) kart.SetDriveInput(0f, 0f);
            if (kart != null) kart.SetDriftInput(false);
            if (kart != null) kart.CancelBufferedActions();
        }
    }
}
