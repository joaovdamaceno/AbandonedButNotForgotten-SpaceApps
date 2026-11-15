Shader "Opportunity/Planet Atmosphere"
{
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha One ZWrite Off Cull Back
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float3 _SunDirection;
   struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);o.p=TransformWorldToHClip(o.w);return o;}
   half4 frag(V i):SV_Target{float3 n=normalize(i.n),v=normalize(_WorldSpaceCameraPos-i.w);float edge=pow(1-saturate(dot(n,v)),5);float light=saturate(dot(n,normalize(_SunDirection))+.13);return half4(.65,.32,.16,edge*light*.22);}
   ENDHLSL
  }
 }
}
