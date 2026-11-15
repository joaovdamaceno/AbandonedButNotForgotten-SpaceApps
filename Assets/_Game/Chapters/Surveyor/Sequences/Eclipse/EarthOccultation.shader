Shader "Abandoned/Surveyor/EarthOccultation"
{
    Properties { _Phase("Occultation", Range(0,1)) = 0 _Exposure("Exposure", Range(0,1)) = .8 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "EarthOccultation"
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Phase, _Exposure;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float r=length(p), aa=max(fwidth(r),.002);
                float earth=1-smoothstep(.57-aa,.57+aa,r);
                float2 sunCenter=lerp(float2(.82,.11),float2(.10,.09),_Phase);
                float s=length(p-sunCenter);
                float solarDisk=1-smoothstep(.159-aa,.159+aa,s);
                float a=atan2(p.y,p.x);
                // SP-184 figs 3-97/98: irregular beads, stronger northern arc and an eastern gap.
                float beads=.58+.22*sin(a*17+.8)+.13*sin(a*31-1.4);
                float arc=.35+.65*pow(saturate(.5+.5*sin(a)),3);
                float gap=smoothstep(.22,.85,abs(atan2(sin(a+.6),cos(a+.6))));
                float rim=exp(-pow((r-.575)/max(.009,aa),2))*beads*arc*gap*_Phase;
                float halo=exp(-pow((r-.58)/.029,2))*.035*_Phase*arc*gap;
                half3 color=half3(.62,.48,.31)*solarDisk*(1-earth);
                color+=half3(.38,.027,.008)*(rim+halo);
                return half4(saturate(color*_Exposure),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
