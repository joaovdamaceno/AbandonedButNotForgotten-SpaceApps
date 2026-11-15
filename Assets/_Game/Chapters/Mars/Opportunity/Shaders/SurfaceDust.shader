Shader "Opportunity/Surface Dust"
{
 Properties{_BaseMap("Shape",2D)="white"{} _BaseColor("Tint",Color)=(.68,.52,.36,.4)}
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   float4 _BaseColor,_AtmosphericColor;float _DustLevel,_AtmosphericDensity;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;float3 world:TEXCOORD1;float4 screen:TEXCOORD2;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.screen=ComputeScreenPos(o.p);o.uv=i.uv;o.c=i.c;return o;}
   half4 frag(V i):SV_Target
   {
    float shape=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;
    float2 screen=i.screen.xy/i.screen.w;float depth=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
    float particleDepth=-TransformWorldToView(i.world).z;
    float edge=saturate((depth-particleDepth)/.45);
    float nearFade=saturate((distance(i.world,_WorldSpaceCameraPos)-.35)/1.2);
    float alpha=min(.22,shape*i.c.a*_BaseColor.a)*edge*nearFade;
    float transmission=exp(-pow(max(0,distance(i.world,_WorldSpaceCameraPos)*_AtmosphericDensity),1.35));
    half3 color=lerp(i.c.rgb*_BaseColor.rgb,_AtmosphericColor.rgb,max(1-transmission,saturate(_DustLevel*.65)));
    return half4(min(color,.75),alpha*sqrt(transmission));
   }
   ENDHLSL
  }
 }
}

