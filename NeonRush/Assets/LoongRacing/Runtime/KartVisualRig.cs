using UnityEngine;

namespace LoongRacing
{
    [DefaultExecutionOrder(20), DisallowMultipleComponent, RequireComponent(typeof(ArcadeKartController))]
    public sealed class KartVisualRig : MonoBehaviour
    {
        [SerializeField] private Transform[] wheelSpins;
        [SerializeField] private Transform[] exhausts;
        [SerializeField] private Transform[] rearContacts;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private int tailMaterialIndex = -1;
        [SerializeField] private float wheelRadius = 0.254f;
        private ArcadeKartController kart;
        private MaterialPropertyBlock block;
        private float wheelAngle;
        public Transform[] Exhausts => exhausts;
        public Transform[] RearContacts => rearContacts;
        public float WheelAngle => wheelAngle;
        public bool BrakeLightsOn { get; private set; }

        public void Configure(Transform[] wheels, Transform[] jets, Transform[] contacts, Renderer body, int tailIndex, float radius)
        {
            wheelSpins=wheels;exhausts=jets;rearContacts=contacts;bodyRenderer=body;
            tailMaterialIndex=tailIndex;wheelRadius=radius;
        }
        private void Awake() { kart=GetComponent<ArcadeKartController>();block=new MaterialPropertyBlock(); }
        private void LateUpdate()
        {
            if(kart==null || Time.timeScale<=0f)return;
            wheelAngle=Mathf.Repeat(wheelAngle+kart.ForwardSpeed/Mathf.Max(.05f,wheelRadius)*Mathf.Rad2Deg*Time.deltaTime,360f);
            if(wheelSpins!=null)foreach(var wheel in wheelSpins)
                if(wheel!=null)wheel.localRotation=Quaternion.Euler(wheelAngle,0f,0f);
            BrakeLightsOn=kart.Throttle<-.05f && kart.ForwardSpeed>.5f;
            if(bodyRenderer==null || tailMaterialIndex<0)return;
            bodyRenderer.GetPropertyBlock(block,tailMaterialIndex);
            block.SetColor("_EmissionColor",new Color(1f,.002f,.04f)*(BrakeLightsOn?2.5f:1.1f));
            bodyRenderer.SetPropertyBlock(block,tailMaterialIndex);
        }
    }
}
