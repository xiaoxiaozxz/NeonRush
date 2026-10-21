using System;
using UnityEngine;

namespace LoongRacing
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(ArcadeKartController))]
    public sealed class KartRouteProgress : MonoBehaviour
    {
        [SerializeField] private TrackRoute route;
        [SerializeField] private float startingDistance = 8f;
        private ArcadeKartController kart;
        private Vector3 previousPosition;
        private float safeDistance = 8f;
        private float outsideTime, wrongWayTime, recoveryAllowedAt;
        private float noticeUntil;
        private bool subscribed;

        public TrackRoute Route => route;
        public int NextCheckpointIndex { get; private set; }
        public int CompletedCircuits { get; private set; }
        public int RecoveryCount { get; private set; }
        public bool IsWrongWay => wrongWayTime > 0.8f;
        public float DistanceAlong { get; private set; }
        public float SafeDistance => safeDistance;
        public bool CollectCheckpoints { get; private set; } = true;
        public string Notice { get; private set; }
        public bool HasNotice => Time.time < noticeUntil;
        public event Action ProgressChanged;
        public event Action CircuitCompleted;

        public void ConfigureStart(float distance) => startingDistance = distance;
        public void SetCheckpointCollection(bool enabled)
        {
            CollectCheckpoints = enabled;
            if (kart != null && kart.Body != null) SyncPosition();
        }

        public void Configure(TrackRoute track)
        {
            route = track;
            if (Application.isPlaying && isActiveAndEnabled && kart != null && kart.Body != null) RestartProgress();
        }

        private void Awake() => kart = GetComponent<ArcadeKartController>();
        private void OnEnable()
        {
            if (kart == null) kart = GetComponent<ArcadeKartController>();
            kart.Respawned += SyncPosition;
            kart.PracticeReset += RestartProgress;
            subscribed = true;
            RestartProgress();
        }

        private void OnDisable()
        {
            if (!subscribed || kart == null) return;
            kart.Respawned -= SyncPosition;
            kart.PracticeReset -= RestartProgress;
            subscribed = false;
        }

        private void SyncPosition()
        {
            previousPosition = kart.Body.position;
            outsideTime = wrongWayTime = 0f;
        }

        private void RestartProgress()
        {
            NextCheckpointIndex = CompletedCircuits = RecoveryCount = 0;
            safeDistance = startingDistance;
            DistanceAlong = 0f;
            Notice = null;
            noticeUntil = 0f;
            recoveryAllowedAt = Time.time + 0.5f;
            SyncPosition();
            ProgressChanged?.Invoke();
        }

        // Swept plane crossing catches fast cars without relying on trigger overlap.
        // Only the next gate is eligible; teleports, reverse, side and high crossings fail.
        public bool EvaluateMovement(Vector3 from, Vector3 to)
        {
            if (!CollectCheckpoints || route == null || route.CheckpointCount == 0 || (to - from).sqrMagnitude > 144f) return false;
            route.Sample(route.CheckpointDistance(NextCheckpointIndex), out Vector3 center, out Vector3 forward);
            forward.y = 0f; forward.Normalize();
            float a = Vector3.Dot(from - center, forward);
            float b = Vector3.Dot(to - center, forward);
            if (a >= 0f || b < 0f || b - a < 0.001f) return false;
            Vector3 crossing = Vector3.Lerp(from, to, -a / (b - a));
            float lateral = Mathf.Abs(Vector3.Dot(crossing - center, Vector3.Cross(Vector3.up, forward)));
            if (lateral > route.HalfWidth || crossing.y < center.y - 0.3f || crossing.y > center.y + 7f) return false;
            NextCheckpointIndex++;
            if (NextCheckpointIndex == route.CheckpointCount)
            {
                CompletedCircuits++;
                NextCheckpointIndex = 0;
                safeDistance = 0f;
                Notice = "本圈全部检查点已通过";
                noticeUntil = Time.time + 3f;
                CircuitCompleted?.Invoke();
            }
            else
            {
                // A recovery starts beyond the last accepted gate, facing the route.
                safeDistance = route.CheckpointDistance(NextCheckpointIndex - 1) + 4f;
                Notice = "检查点 " + NextCheckpointIndex + " / " + route.CheckpointCount + " 已通过";
                noticeUntil = Time.time + 1.5f;
            }
            ProgressChanged?.Invoke();
            return true;
        }

        private void FixedUpdate()
        {
            if (route == null || route.Length <= 0f) return;
            Vector3 position = kart.Body.position;
            bool continuous = (position - previousPosition).sqrMagnitude <= 144f;
            EvaluateMovement(previousPosition, position);
            previousPosition = position;
            float offset = route.FindNearest(position, out Vector3 nearest, out Vector3 tangent, out float along);
            DistanceAlong = along;
            float nextDistance = route.CheckpointDistance(NextCheckpointIndex);
            float lastDistance = NextCheckpointIndex == 0 ? 0f : route.CheckpointDistance(NextCheckpointIndex - 1);
            bool inExpectedSection = along >= lastDistance && along <= nextDistance - 5f;
            if (continuous && inExpectedSection && kart.IsGrounded && offset < route.HalfWidth - 1.8f
                && Mathf.Abs(position.y - nearest.y) < 1.6f && Vector3.Dot(transform.forward, tangent) > 0.25f)
                safeDistance = Mathf.Max(safeDistance, along);

            wrongWayTime = kart.SpeedKph > 18f && Vector3.Dot(kart.Body.linearVelocity.normalized, tangent) < -0.25f
                ? wrongWayTime + Time.fixedDeltaTime : 0f;
            bool outside = offset > route.HalfWidth + 10f || position.y < nearest.y - 7f;
            outsideTime = outside ? outsideTime + Time.fixedDeltaTime : 0f;
            if (Time.time >= recoveryAllowedAt && (outsideTime > 1.2f || position.y < -12f)) Recover();
        }

        public void Recover()
        {
            if (!kart.DrivingEnabled) return;
            if (route == null) { kart.RequestRecovery(); return; }
            route.Sample(safeDistance, out Vector3 position, out Vector3 forward);
            forward.y = 0f;
            // Centre lane is clear; optional jumps sit off-centre and are never rescue poses.
            position += Vector3.up * 0.7f;
            kart.RecoverToPose(position, Quaternion.LookRotation(forward, Vector3.up));
            RecoveryCount++;
            recoveryAllowedAt = Time.time + 1f;
            Notice = "已救援，检查点与集气保留，计时继续";
            noticeUntil = Time.time + 2.5f;
            ProgressChanged?.Invoke();
        }
    }
}
