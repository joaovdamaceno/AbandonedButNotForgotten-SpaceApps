Shader "Opportunity/Sand Tracks"
{
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float _AtmosphericDensity;float4 _AtmosphericColor;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target
   {float edge=saturate(sin(i.uv.y*3.14159)*3);float tread=lerp(.25,1,step(.35,frac(i.uv.x*9+i.uv.y*.3)));float transmission=exp(-pow(max(0,distance(i.world,_WorldSpaceCameraPos)*_AtmosphericDensity),1.35));return half4(lerp(_AtmosphericColor.rgb,half3(.17,.12,.085),transmission),edge*tread*.23*transmission);}
   ENDHLSL
  }
 }
}
