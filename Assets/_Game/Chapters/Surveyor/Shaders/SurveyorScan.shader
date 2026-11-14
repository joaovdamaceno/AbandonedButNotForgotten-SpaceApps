Shader "Abandoned/Surveyor/Scan"
{
    Properties { _MainTex ("Lunar image", 2D) = "black" {} _Progress ("Scan progress", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_TexelSize;float _Progress;
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if(_MainTex_TexelSize.y<0)uv.y=1-uv.y;
                #endif
                float top=1-i.uv.y;
                float2 sampleUv=float2(uv.x,(floor(uv.y*200)+.5)/200);
                float luminance=dot(tex2D(_MainTex,sampleUv).rgb,float3(.2126,.7152,.0722));
                float rowFade=lerp(.95,1,step(.25,frac(uv.y*200)));
                float value=saturate((luminance-.018)*1.15)*rowFade;
                float revealed=step(top,_Progress)*step(.00001,_Progress);
                float beam=(1-step(1.0/200,abs(top-_Progress)))*step(.00001,_Progress)*(1-step(.99999,_Progress));
                float gray=lerp(.012,value,revealed)+beam*.16;
                return fixed4(gray,gray,gray,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
