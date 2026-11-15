Shader "Opportunity/Mars Surface"
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
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);TEXTURE2D(_WearMask);SAMPLER(sampler_WearMask);
   TEXTURE2D(_GravelMap);SAMPLER(sampler_GravelMap);
   CBUFFER_START(UnityPerMaterial)
   float4x4 _WorldToWork;float4 _WorkSize;float4 _BaseMap_ST,_BaseColor,_Accent;float _Scale,_Rock,_Smoothness,_NormalStrength,_Abrasion,_Inspection,_GroundMix,_Fabric;
   CBUFFER_END
   float _AtmosphericDensity,_Memory;float4 _AtmosphericColor;
   struct A{float4 pos:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   V vert(A a){V v;v.world=TransformObjectToWorld(a.pos.xyz);v.pos=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normal);v.uv=a.uv;return v;}
   half4 frag(V v):SV_Target
   {
    float3 n=normalize(v.normal),w=pow(abs(n),4);w/=max(dot(w,1),.001);
    float3 p=v.world*_Scale;
    half3 x=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb,y=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb,z=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb;
    half3 tex=x*w.x+y*w.y+z*w.z;
    float2 rotatedUV=float2(p.z*1.173-p.x*.671,p.x*1.173+p.z*.671)+float2(13.4,8.7);
    half3 alternate=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,rotatedUV).rgb;
    float tileBreak=smoothstep(.22,.77,noise(v.world.xz*.087+float2(23,11)));
    tex=lerp(tex,alternate,_GroundMix*tileBreak*.58);
    half3 gravel=SAMPLE_TEXTURE2D(_GravelMap,sampler_GravelMap,v.world.xz*.56).rgb;
    float groundPatch=smoothstep(.46,.69,noise(v.world.xz*.053)+saturate(1-n.y)*1.8)*_GroundMix;
    tex=lerp(tex,gravel,groundPatch*.32);
    float gray=dot(tex,float3(.3,.59,.11));tex=lerp(tex,gray.xxx,.38);
    half3 albedo=pow(max(tex,half3(.008,.008,.008)),.68)*_BaseColor.rgb*1.15;
    float macro=noise(v.world.xz*.035);albedo*=lerp(.72,1.14,macro);
    // Broad deposits and finer wind-aligned patches interrupt texture repetition.
    float deposit=noise(v.world.xz*.018+float2(17,9)),runnel=noise(float2(v.world.x*.11+v.world.z*.035,v.world.z*.025));
    float2 driftUV=float2(v.world.x*.27+v.world.z*.09,v.world.z*.31-v.world.x*.07);
    half3 fineSand=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,driftUV).rgb;
    half3 sediment=lerp(half3(.88,.77,.62),half3(1.12,1.03,.88),smoothstep(.26,.72,deposit));
    albedo=lerp(albedo,albedo*sediment*(.92+fineSand.r*.19),_GroundMix*(1-groundPatch));
    albedo*=1-_GroundMix*groundPatch*.13*smoothstep(.45,.72,runnel);
    float layerWarp=noise(v.world.xz*.21)*.13+noise(v.world.xz*.61)*.035;
    float strata=sin((v.world.y+layerWarp)*12)*.5+.5;
    float fracture=noise(float2(v.world.x*.52+v.world.z*.18,v.world.y*8));
    albedo*=1-_Rock*(.075*(1-strata)+.045*smoothstep(.65,.86,fracture));
    float3 nx=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.zy),_NormalStrength),ny=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.xz),_NormalStrength),nz=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,p.xy),_NormalStrength);
    float3 relief=float3(0,nx.y,nx.x)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z;
    float3 geometricNormal=n;n=normalize(n+relief*.23);albedo*=1+_Fabric*.05*sin(v.uv.x*420)*sin(v.uv.y*220);albedo*=1-_Fabric*.22*smoothstep(.988,1,cos(v.uv.x*18.84956));
    float rippleEnvelope=smoothstep(.21,.75,noise(v.world.xz*.024+7));
    float rippleSlope=cos(v.world.x*.9+v.world.z*.14+noise(v.world.xz*.05)*2)*.045*rippleEnvelope*_GroundMix*(1-groundPatch);
    n=normalize(n+float3(rippleSlope,0,rippleSlope*.16));
    float2 workUV=mul(_WorldToWork,float4(v.world,1)).xz/max(_WorkSize.xy,float2(.001,.001))+.5;
    float workEdge=smoothstep(0,.07,min(min(workUV.x,1-workUV.x),min(workUV.y,1-workUV.y)));
    float inWork=workEdge*smoothstep(.1,.5,geometricNormal.y);
    float wear=SAMPLE_TEXTURE2D(_WearMask,sampler_WearMask,workUV).r*_Abrasion*inWork;
    float vein=exp(-pow((workUV.x-.47-sin(workUV.y*15)*.009)/.007,2));
    float pores=step(.82,noise(workUV*45))*smoothstep(.27,.09,distance(workUV,float2(.53,.49)));
    half3 interior=lerp(albedo*1.1,_Accent.rgb*(.92+gray*.32),.78)*(1-pores*.4)+vein*half3(.09,.08,.055);
    n=normalize(lerp(n,geometricNormal,smoothstep(.02,.65,wear)*.93));
    albedo=lerp(albedo,interior,smoothstep(.08,.7,wear));
    float ripple=sin(v.uv.x*75+sin(v.uv.y*18)-_Time.y*3)*.5+.5;
    albedo=lerp(albedo,lerp(half3(.12,.34,.4),half3(.55,.8,.8),ripple),wear*_Memory*.7);
    float chosen=_Inspection==1?vein:pores;albedo+=chosen*wear*(_Inspection>0?.15:0);
    InputData input=(InputData)0;input.positionWS=v.world;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.world);
    input.shadowCoord=TransformWorldToShadowCoord(v.world);input.bakedGI=SampleSH(n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.pos);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.alpha=1;surface.metallic=0;surface.smoothness=lerp(_Smoothness,.31,smoothstep(.04,.7,wear));surface.occlusion=1;surface.normalTS=half3(0,0,1);
    half3 color=UniversalFragmentPBR(input,surface).rgb;
    return half4(color,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}

