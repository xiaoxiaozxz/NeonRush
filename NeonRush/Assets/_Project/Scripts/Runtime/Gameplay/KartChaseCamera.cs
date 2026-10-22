using UnityEngine;

namespace LoongRacing
{
    [RequireComponent(typeof(Camera))]
    public sealed class KartChaseCamera : MonoBehaviour
    {
        [SerializeField] private ArcadeKartController target;
        [SerializeField] private float cameraHeight = 3.6f;
        [SerializeField] private float cameraDistance = 7.8f;
        [SerializeField] private float lookHeight = .65f;
        private Camera view;
        private Quaternion heading;
        private bool snapped;

        public void Configure(ArcadeKartController kart) { target = kart; }
        public void ConfigureFraming(float height, float distance, float lookAtHeight)
        { cameraHeight=height;cameraDistance=distance;lookHeight=lookAtHeight; }
        private void Awake() { view = GetComponent<Camera>(); }

        private void Start() => SnapToTarget();

        public void SnapToTarget()
        {
            if (target == null) return;
            if (view == null) view = GetComponent<Camera>();
            heading = target.transform.rotation;
            transform.position = DesiredPosition();
            transform.rotation = Quaternion.LookRotation(LookAtPosition() - transform.position, Vector3.up);
            view.fieldOfView = target.Tuning.cameraBaseFieldOfView;
            snapped = true;
        }

        private Vector3 LookAtPosition() => target.transform.position + Vector3.up * lookHeight
            + heading * Vector3.forward * (3f + target.SpeedKph / 3.6f * 0.06f);

        private Vector3 DesiredPosition()
        {
            float speed = target.SpeedKph / 3.6f;
            return target.transform.position + heading * new Vector3(0f, cameraHeight, -cameraDistance - speed * 0.035f - (target.IsBoosting ? 0.7f : 0f));
        }

        private void LateUpdate()
        {
            if (target == null || Time.timeScale == 0f) return;
            if (!snapped) SnapToTarget();
            float dt = Time.deltaTime;
            heading = Quaternion.Slerp(heading, target.transform.rotation, 1f - Mathf.Exp(-6f * dt));
            Vector3 desired = DesiredPosition();
            Vector3 anchor = target.transform.position + Vector3.up * 1.1f;
            Vector3 offset = desired - anchor;
            if (Physics.SphereCast(anchor, 0.3f, offset.normalized, out RaycastHit hit, offset.magnitude,
                    ~(1 << 2), QueryTriggerInteraction.Ignore))
                desired = anchor + offset.normalized * Mathf.Max(0.6f, hit.distance - 0.18f);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-14f * dt));
            transform.rotation = Quaternion.LookRotation(LookAtPosition() - transform.position, Vector3.up);
            float speedRatio = Mathf.Clamp01(target.SpeedKph / (target.Tuning.maximumForwardSpeed * 3.6f));
            float fov = target.Tuning.cameraBaseFieldOfView + target.Tuning.cameraSpeedFieldOfView * speedRatio;
            if (target.IsBoosting) fov += target.IsNitroActive ? 5f : 3f;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, 1f - Mathf.Exp(-4f * dt));
        }
    }
}
