Shader "Opportunity/Dust Veil"
{
 Properties{_BaseColor("Dust",Color)=(.53,.39,.28,.2)}
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _BaseColor,_AtmosphericColor;float _DustLevel;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 frag(V i):SV_Target{float cloud=noise(i.uv*float2(9,4)-float2(_Time.y*.05,0))*.7+noise(i.uv*18+_Time.y*.03)*.3;float edge=pow(saturate(sin(i.uv.y*3.14159)),.7)*smoothstep(0,.12,i.uv.x)*smoothstep(1,.88,i.uv.x);return half4(_AtmosphericColor.rgb*1.025,_BaseColor.a*edge*smoothstep(.18,.7,cloud)*_DustLevel);}
   ENDHLSL
  }
 }
}

