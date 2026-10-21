using System;
using UnityEngine;

namespace LoongRacing
{
    [CreateAssetMenu(menuName = "NeonRush/Track Route")]
    public sealed class TrackRoute : ScriptableObject
    {
        [SerializeField] private string displayName = "霓虹港湾";
        [SerializeField] private float halfWidth = 9.5f;
        [SerializeField] private Vector3[] points;
        [SerializeField] private float[] distances;
        [SerializeField] private float[] checkpoints;

        public string DisplayName => displayName;
        public float HalfWidth => halfWidth;
        public float Length => distances == null || distances.Length == 0 ? 0f : distances[distances.Length - 1];
        public int CheckpointCount => checkpoints == null ? 0 : checkpoints.Length;
        public int SampleCount => points == null ? 0 : points.Length;
        public float CheckpointDistance(int index) => checkpoints[index];

        // Samples include the repeated first point at the end of the closed route.
        public void Configure(string title, float width, Vector3[] samples, float[] gates)
        {
            if (samples == null || samples.Length < 4) throw new ArgumentException("A route requires at least four samples.");
            if (gates == null || gates.Length < 2) throw new ArgumentException("A route requires ordered checkpoints and a finish.");
            displayName = title;
            halfWidth = width;
            points = (Vector3[])samples.Clone();
            distances = new float[points.Length];
            for (int i = 1; i < points.Length; i++) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            checkpoints = (float[])gates.Clone();
            for (int i = 0; i < checkpoints.Length; i++)
                if (checkpoints[i] <= (i == 0 ? 0f : checkpoints[i - 1]) || checkpoints[i] > Length + 0.01f)
                    throw new ArgumentException("Checkpoint distances must increase within the route length.");
        }

        public void Sample(float distance, out Vector3 position, out Vector3 forward)
        {
            if (Length <= 0f) { position = Vector3.zero; forward = Vector3.forward; return; }
            distance = Mathf.Repeat(distance, Length);
            int index = Array.BinarySearch(distances, distance);
            if (index < 0) index = ~index - 1;
            index = Mathf.Clamp(index, 0, points.Length - 2);
            float t = Mathf.InverseLerp(distances[index], distances[index + 1], distance);
            position = Vector3.Lerp(points[index], points[index + 1], t);
            forward = (points[index + 1] - points[index]).normalized;
        }

        public float FindNearest(Vector3 position, out Vector3 nearest, out Vector3 forward, out float distanceAlong)
        {
            float best = float.PositiveInfinity;
            nearest = Vector3.zero; forward = Vector3.forward; distanceAlong = 0f;
            for (int i = 0; i < SampleCount - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector3.Dot(position - points[i], delta) / Mathf.Max(0.001f, delta.sqrMagnitude));
                Vector3 candidate = points[i] + delta * t;
                float squared = (position - candidate).sqrMagnitude;
                if (squared >= best) continue;
                best = squared;
                nearest = candidate; forward = delta.normalized;
                distanceAlong = Mathf.Lerp(distances[i], distances[i + 1], t);
            }
            return Mathf.Sqrt(best);
        }
    }
}
