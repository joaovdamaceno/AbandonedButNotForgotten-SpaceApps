Shader "Opportunity/Mars Surface Web"
{
 Properties
 {
  _BaseMap("Surface",2D)="white"{} _NormalMap("Relief",2D)="bump"{} _WearMask("Abrasion",2D)="black"{}
  _BaseColor("Tint",Color)=(.8,.6,.4,1) _Accent("Interior",Color)=(.7,.65,.5,1)
  _Scale("Meters to texture",Float)=.35 _Rock("Stratification",Range(0,1))=0 _GroundMix("Terrain geology",Float)=0 _GravelMap("Terrain fragments",2D)="white"{}
  _Smoothness("Smoothness",Range(0,1))=.12 _NormalStrength("Relief",Range(0,2))=.8 _Abrasion("Technical surface",Float)=0 _Inspection("Inspection",Float)=0 _Fabric("Woven fabric",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags{"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   // Forward+ does not populate the per-object main-light attenuation used by Forward.
   // Compile its light-loop variant so URP selects the correct main-light path.
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
   TEXTURE2D(_GravelMap);SAMPLER(sampler_GravelMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_Accent;
   float _Scale,_Rock,_Smoothness,_NormalStrength,_Abrasion,_Inspection,_GroundMix,_Fabric;
   CBUFFER_END
   struct A{float4 pos:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   V vert(A a){V v;v.world=TransformObjectToWorld(a.pos.xyz);v.pos=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normal);v.uv=a.uv;return v;}
   half4 frag(V v):SV_Target
   {
    half3 geometricNormal=normalize(v.normal),w=abs(geometricNormal);w*=w;w*=w;w/=max(dot(w,half3(1,1,1)),.001h);
    float3 p=v.world*_Scale;
    half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;
    float macro=noise(v.world.xz*.035+float2(17,9));
    float2 driftUV=float2(p.z*1.173-p.x*.671,p.x*1.173+p.z*.671)+float2(13.4,8.7);
    half3 alternate=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,driftUV).rgb;
    tex=lerp(tex,alternate,_GroundMix*smoothstep(.2,.8,macro)*.5);
    half3 gravel=SAMPLE_TEXTURE2D(_GravelMap,sampler_GravelMap,v.world.xz*.56).rgb;
    float patch=smoothstep(.53,.76,macro+saturate(1-geometricNormal.y)) * _GroundMix;
    tex=lerp(tex,gravel,patch*.3);
    half gray=dot(tex,half3(.3,.59,.11));tex=lerp(tex,gray.xxx,.38h);
    half3 albedo=pow(max(tex,half3(.008,.008,.008)),.68h)*_BaseColor.rgb*1.15h*lerp(.72h,1.14h,macro);
    float strata=sin((v.world.y+macro*.1)*12)*.5+.5;
    albedo*=1-_Rock*.075*(1-strata);
    // One dominant projection supplies relief; the three colour projections keep slopes seamless.
    float2 normalUV=w.y>=max(w.x,w.z)?p.xz:(w.x>w.z?p.zy:p.xy);
    half3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,normalUV),_NormalStrength);
    half3 relief=w.y>=max(w.x,w.z)?half3(detail.x,0,detail.y):(w.x>w.z?half3(0,detail.y,detail.x):half3(detail.x,detail.y,0));
    half3 n=normalize(geometricNormal+relief*.23h);
    albedo*=1+_Fabric*.05*sin(v.uv.x*420)*sin(v.uv.y*220);
    InputData input=(InputData)0;input.positionWS=v.world;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.world);
    input.shadowCoord=TransformWorldToShadowCoord(v.world);input.bakedGI=SampleSH(n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.pos);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.alpha=1;surface.smoothness=_Smoothness;surface.occlusion=1;surface.normalTS=half3(0,0,1);
    return half4(UniversalFragmentPBR(input,surface).rgb,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
