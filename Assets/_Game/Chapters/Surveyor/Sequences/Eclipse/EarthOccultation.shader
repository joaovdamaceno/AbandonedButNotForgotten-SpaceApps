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
                float2 sunCenter=lerp(float2(.82,.11),float2(-.82,.09),_Phase);
                float s=length(p-sunCenter);
                float solarDisk=1-smoothstep(.159-aa,.159+aa,s);
                float a=atan2(p.y,p.x);
                // SP-184 figs 3-97/98: irregular beads, stronger northern arc and an eastern gap.
                float beads=.62+.20*sin(a*17+.8)+.10*sin(a*31-1.4);
                float north=.35+.65*pow(saturate(.5+.5*sin(a)),3);
                float towardSun=.35+.65*saturate(dot(normalize(p+float2(.0001,0)),normalize(sunCenter)));
                float arc=north*towardSun;
                float gap=smoothstep(.22,.85,abs(atan2(sin(a+.6),cos(a+.6))));
                float hidden=1-smoothstep(.41,.73,length(sunCenter));
                float rim=exp(-pow((r-.575)/max(.008,aa),2))*beads*arc*gap*hidden;
                float halo=exp(-pow((r-.58)/.035,2))*.075*hidden*arc*gap;
                half3 color=half3(1,.91,.73)*solarDisk*(1-earth);
                color+=lerp(half3(.46,.025,.007),half3(.85,.19,.027),towardSun)*(rim+halo);
                return half4(saturate(color*_Exposure),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
