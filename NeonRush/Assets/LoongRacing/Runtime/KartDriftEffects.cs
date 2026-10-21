using UnityEngine;

namespace LoongRacing
{
    [RequireComponent(typeof(ArcadeKartController)), DisallowMultipleComponent]
    public sealed class KartDriftEffects : MonoBehaviour
    {
        private ArcadeKartController kart;
        private TrailRenderer[] tracks;
        private Material material;
        private KartVisualRig rig;
        [SerializeField] private Material tyreMaterial;
        public void Configure(Material preset) => tyreMaterial = preset;

        private void Awake()
        {
            kart = GetComponent<ArcadeKartController>();
            rig = GetComponent<KartVisualRig>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;
            material = tyreMaterial != null ? new Material(tyreMaterial) : new Material(shader);
            material.name = "Runtime Tyre Marks";
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            tracks = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Rear Tyre Mark " + i);
                go.transform.SetParent(transform, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = 3f;
                trail.startWidth = 0.19f;
                trail.endWidth = 0.13f;
                trail.minVertexDistance = 0.14f;
                trail.startColor = new Color(0.025f, 0.035f, 0.04f, 0.72f);
                trail.endColor = new Color(0.025f, 0.035f, 0.04f, 0f);
                trail.alignment = LineAlignment.TransformZ;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                tracks[i] = trail;
            }
            kart.Respawned += Clear;
        }

        private void LateUpdate()
        {
            if (tracks == null) return;
            for (int i = 0; i < tracks.Length; i++)
            {
                var trail = tracks[i];
                Vector3 wheel = rig != null && rig.RearContacts != null && rig.RearContacts.Length > i && rig.RearContacts[i] != null
                    ? rig.RearContacts[i].position : transform.TransformPoint(new Vector3(i == 0 ? -0.74f : 0.74f, 0f, -1.04f));
                bool ground = Physics.Raycast(wheel + Vector3.up * 0.2f, Vector3.down, out var hit, 1.2f,
                    ~(1 << 2), QueryTriggerInteraction.Ignore);
                trail.transform.position = ground ? hit.point + hit.normal * 0.018f : wheel;
                trail.transform.rotation = Quaternion.LookRotation(ground ? hit.normal : Vector3.up, transform.forward);
                trail.emitting = Time.timeScale > 0f && kart.IsDrifting && kart.IsGrounded && ground && Mathf.Abs(kart.DriftAngle) > 6f;
            }
        }

        private void Clear() { if (tracks != null) foreach (var track in tracks) track.Clear(); }
        private void OnDestroy()
        {
            if (kart != null) kart.Respawned -= Clear;
            if (material != null) Destroy(material);
        }
    }
}
