Shader "Opportunity/Mars Sky"
{
 Properties{_Dust("Dust",Range(0,1))=0}
 SubShader
 {
  Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float _Dust;float3 _SunDirection;float4 _AtmosphericColor;
   struct A{float4 vertex:POSITION;};struct V{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
   V vert(A i){V o;o.pos=TransformObjectToHClip(i.vertex.xyz);o.dir=i.vertex.xyz;return o;}
   half4 frag(V i):SV_Target
   {
    float3 d=normalize(i.dir);float h=saturate(d.y),mu=saturate(dot(d,normalize(_SunDirection+float3(.00001,0,0))));
    float3 sky=lerp(_AtmosphericColor.rgb*1.08,float3(.24,.29,.33),pow(h,.55));
    float disc=smoothstep(.99993,.99997,mu),halo=pow(mu,28)*(1-_Dust*.8);
    sky+=float3(.14,.12,.09)*halo+float3(1.8,1.6,1.25)*disc*(1-_Dust*.94);
    sky=lerp(sky,lerp(_AtmosphericColor.rgb,_AtmosphericColor.rgb*.72,h),_Dust*.88);
    return half4(sky,1);
   }
   ENDHLSL
  }
 }
}


