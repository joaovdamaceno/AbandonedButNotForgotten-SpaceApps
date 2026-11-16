Shader "Opportunity/Atmospheric Perspective"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline"} ZWrite Off Cull Off ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   float _AtmosphericDensity;float4 _AtmosphericColor;float _DustLevel;
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 uv=i.texcoord;half4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
    if(_AtmosphericDensity<.00001)return source;
    // Use normalized depth coordinates. URP 17.6 SampleSceneDepth converts UVs
    // through the global _ScreenSize before texelFetch; that size can differ from
    // this attachment with Web render scaling and an offscreen photograph.
    float2 depthTexel=max(_CameraDepthTexture_TexelSize.xy,float2(.000001,.000001));
    float2 depthUV=clamp(uv,depthTexel*.5,1-depthTexel*.5);
    float raw=SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture,sampler_PointClamp,depthUV,0).r;
    #if UNITY_REVERSED_Z
    if(raw<.000001)return source;
    #else
    if(raw>.999999)return source;
    #endif
    // LinearEyeDepth consumes the untouched device-depth value and Unity's
    // platform-specific _ZBufferParams. Avoid mixing inverse-VP reconstruction
    // with GLES [-1,+1] clip conversion in a RenderGraph fullscreen pass.
    float eyeDepth=LinearEyeDepth(raw,_ZBufferParams);
    if(unity_OrthoParams.w>.5)eyeDepth=LinearEyeDepthOrtho(raw,_ProjectionParams.y,_ProjectionParams.z);
    if(!(eyeDepth>0)||eyeDepth>_ProjectionParams.z*1.01)return source;
    float2 screenUV=DYNAMIC_SCALING_REMOVE_SCALEBIAS(uv);
    float2 projectionScale=max(abs(float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11)),float2(.0001,.0001));
    float2 viewSlope=(screenUV*2-1)/projectionScale;
    float rayLength=unity_OrthoParams.w>.5?1:sqrt(1+dot(viewSlope,viewSlope));
    float distanceWS=eyeDepth*rayLength;
    float density=_AtmosphericDensity;
    float transmission=exp(-pow(max(0,distanceWS*density),1.35));
    half3 haze=_AtmosphericColor.rgb;
    half3 safeColor=max(source.rgb,0);safeColor=min(safeColor,32);
    return half4(lerp(haze,safeColor,transmission),source.a);
   }
   ENDHLSL
  }
 }
}

