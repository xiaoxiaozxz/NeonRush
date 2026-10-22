using UnityEngine;
using UnityEngine.Rendering;

namespace LoongRacing
{
    [DefaultExecutionOrder(-250)]
    public sealed class NeonEnvironment : MonoBehaviour
    {
        private SphericalHarmonicsL2 previous;
        private void OnEnable()
        {
            previous=RenderSettings.ambientProbe;
            var fill=new SphericalHarmonicsL2();fill.AddAmbientLight(new Color(.10f,.12f,.20f));
            RenderSettings.ambientProbe=fill;
        }
        private void OnDisable() => RenderSettings.ambientProbe=previous;
    }
}
