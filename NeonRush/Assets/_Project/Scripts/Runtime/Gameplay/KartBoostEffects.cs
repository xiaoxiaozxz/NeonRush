using UnityEngine;

namespace LoongRacing
{
    [RequireComponent(typeof(ArcadeKartController))]
    public sealed class KartBoostEffects : MonoBehaviour
    {
        [SerializeField] private Material plumeMaterial;
        private ArcadeKartController kart;
        private Transform[] plumes;
        private Material material;
        private KartVisualRig rig;
        public void Configure(Material preset) => plumeMaterial = preset;

        private void Awake()
        {
            kart = GetComponent<ArcadeKartController>();
            rig = GetComponent<KartVisualRig>();
            if (plumeMaterial == null) return;
            material = new Material(plumeMaterial);
            plumes = new Transform[2];
            for (int i = 0; i < plumes.Length; i++)
            {
                var go = GameObject.CreatePrimitive(rig != null ? PrimitiveType.Capsule : PrimitiveType.Cube);
                go.name = "Boost Plume " + i;
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                go.layer = 2;
                go.transform.SetParent(transform, false);
                go.GetComponent<Renderer>().sharedMaterial = material;
                plumes[i] = go.transform;
                go.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (plumes == null) return;
            float length = (kart.IsNitroActive ? 2f : 1.15f) * (1f + 0.08f * Mathf.Sin(Time.time * 65f));
            material.color = kart.IsNitroActive ? new Color(0.03f, 0.78f, 1f) : new Color(0.38f, 1f, 0.85f);
            for (int i = 0; i < plumes.Length; i++)
            {
                plumes[i].gameObject.SetActive(kart.IsBoosting);
                if (rig != null && rig.Exhausts != null && rig.Exhausts.Length > i && rig.Exhausts[i] != null)
                {
                    plumes[i].position = rig.Exhausts[i].position-transform.forward*length*.5f;
                    plumes[i].rotation = transform.rotation*Quaternion.Euler(90f,0f,0f);
                    plumes[i].localScale = new Vector3(.18f,length*.5f,.18f);
                }
                else
                {
                    plumes[i].localPosition = new Vector3(i == 0 ? -0.59f : 0.59f, -0.06f, -1.6f - length * 0.5f);
                    plumes[i].localScale = new Vector3(0.23f, 0.2f, length);
                }
            }
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
