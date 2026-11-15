Shader "Abandoned/Surveyor Documentary"
{
    Properties { _MainTex("Picture",2D)="white" {} _Instrument("Monitor",Float)=0 _Strength("Treatment",Float)=1 _Fade("Fade",Float)=1 _StoryTime("Active time",Float)=0 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; float _Instrument,_Strength,_Fade,_StoryTime;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            float4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;float2 uv=i.uv;
                if(_Instrument>.5) uv=.5+p*(1+dot(p,p)*.007*_Strength)*.5;
                float3 col=tex2D(_MainTex,uv).rgb;float luminance=dot(col,float3(.2126,.7152,.0722));
                float vignette=1-dot(p*.43,p*.43)*.25*_Strength;
                float noise=(hash(floor(i.uv*float2(1280,720))+floor(_StoryTime*24))-.5);
                if(_Instrument>.5) {
                    float soft=dot(tex2D(_MainTex,uv+float2(_MainTex_TexelSize.x,0)).rgb,float3(.2126,.7152,.0722));
                    float y=pow(saturate(lerp(luminance,soft,.13*_Strength)*1.18+.017),.91);
                    float scan=1-(.5+.5*cos(i.uv.y*720*3.14159265))*.055*_Strength;
                    col=y*float3(.94,1,.91)*scan+noise*.013*_Strength;
                } else {
                    col=lerp(luminance.xxx,col,.83); col=col*float3(1.035,1.015,.98);col+=noise*.006*_Strength*smoothstep(.015,.15,luminance);
                }
                return float4(max(0,col)*vignette*_Fade,1);
            }
            ENDHLSL
        }
    }
}
