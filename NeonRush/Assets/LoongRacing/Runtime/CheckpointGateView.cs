using UnityEngine;

namespace LoongRacing
{
    public sealed class CheckpointGateView : MonoBehaviour
    {
        [SerializeField] private KartRouteProgress progress;
        [SerializeField] private int index;
        [SerializeField] private Renderer[] strips;
        private MaterialPropertyBlock block;

        public void Configure(KartRouteProgress tracker, int gateIndex, Renderer[] lights)
        { progress = tracker; index = gateIndex; strips = lights; }
        private void OnEnable() { if (progress != null) { progress.ProgressChanged += Refresh; Refresh(); } }
        private void OnDisable() { if (progress != null) progress.ProgressChanged -= Refresh; }
        private void Refresh()
        {
            if (block == null) block = new MaterialPropertyBlock();
            Color color = index == progress.NextCheckpointIndex ? new Color(1f, 0.74f, 0.12f)
                : index < progress.NextCheckpointIndex ? new Color(0.1f, 0.95f, 0.65f) : new Color(0.16f, 0.38f, 0.45f);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 0.6f);
            foreach (var strip in strips) if (strip != null) strip.SetPropertyBlock(block);
        }
    }
}
