Shader "Abandoned/Surveyor Exhaust"
{
    Properties { _BaseColor("Exhaust",Color)=(.40,.53,.64,.36) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; };
            V vert(A a) { V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color*_BaseColor;return o; }
            half4 frag(V i):SV_Target { return i.color; }
            ENDHLSL
        }
    }
}
