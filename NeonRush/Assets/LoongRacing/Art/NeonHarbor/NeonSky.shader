Shader "NeonRush/Neon Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (.045,.085,.30,1)
        _Horizon("Horizon", Color) = (.22,.035,.16,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _Zenith, _Horizon;
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;
                #if UNITY_REVERSED_Z
                    o.position.z=0;
                #else
                    o.position.z=o.position.w;
                #endif
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float h=saturate(d.y);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.30));
                float cloud=pow(saturate(sin(d.x*12+d.z*17+d.y*61)*.5+.5),10)*exp(-abs(d.y-.16)*16);
                color+=float3(.10,.025,.085)*cloud;
                float2 uv=d.xz/max(.12,d.y+1.1)*170;
                float star=step(.997,hash(floor(uv)))*pow(saturate(1-length(frac(uv)-.5)*2),7)*smoothstep(.2,.65,h);
                return half4(color+star*.55,1);
            }
            ENDHLSL
        }
    }
}
