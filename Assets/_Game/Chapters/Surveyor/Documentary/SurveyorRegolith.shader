Shader "Abandoned/Surveyor Regolith"
{
    Properties { _BaseMap("Soil detail",2D)="white" {} _BaseColor("Albedo",Color)=(.63,.62,.59,1) _BumpScale("Grain relief",Float)=.6 _UseCutColor("Fresh cut contrast",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor; float _BumpScale, _UseCutColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float exposed:TEXCOORD2; };
            Varyings vert(Attributes a) { Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.exposed=a.color.r*_UseCutColor;return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y); }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float macro=noise(p*.11)+.35*noise(p*.41+13);
                float3 grain=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p*.35).rgb;
                float3 detail=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(p.y,-p.x)*3.7+float2(.27,.62)).rgb;
                float3 albedo=_BaseColor.rgb*(.30+.12*macro+.16*grain.r+.09*detail.r);
                albedo*=lerp(1,.58,saturate(i.exposed));
                float nx=(detail.r-.42)*.28*_BumpScale, nz=(grain.r-.42)*.22*_BumpScale;
                float3 normal=normalize(i.normalWS+float3(nx,0,nz));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float shade=saturate(dot(normal,sun.direction));
                float3 color=albedo*(SampleSH(normal)+sun.color*shade*sun.shadowAttenuation);
                return half4(color,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
