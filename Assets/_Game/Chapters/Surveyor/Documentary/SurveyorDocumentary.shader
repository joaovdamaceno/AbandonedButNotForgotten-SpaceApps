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
                if(_Instrument>.5) uv=.5+p*(1+dot(p,p)*.029*_Strength)*.5;
                float3 col=tex2D(_MainTex,uv).rgb;float luminance=dot(col,float3(.2126,.7152,.0722));
                float vignette=1-dot(p*.43,p*.43)*.25*_Strength;
                float noise=(hash(floor(i.uv*float2(1280,720))+floor(_StoryTime*24))-.5);
                if(_Instrument>.5) {
                    float2 dx=float2(_MainTex_TexelSize.x*1.5,0), dy=float2(0,_MainTex_TexelSize.y*1.5);
                    float3 soft=(tex2D(_MainTex,uv+dx).rgb+tex2D(_MainTex,uv-dx).rgb+tex2D(_MainTex,uv+dy).rgb+tex2D(_MainTex,uv-dy).rgb)*.25;
                    float y=pow(saturate(lerp(luminance,dot(soft,float3(.2126,.7152,.0722)),.24*_Strength)*1.32-.012),.88);
                    float scan=1-(.5+.5*cos(i.uv.y*270*6.2831853))*.16*_Strength;
                    col=y*float3(.87,1,.83)*scan+noise*.022*_Strength;
                    float2 corner=max(abs(uv-.5)-.459,0);
                    float mask=(1-smoothstep(.032,.043,length(corner)))*step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                    float glass=.018*(1-smoothstep(.0,.09,abs(uv.y-(.91-uv.x*.14))));
                    col=(col+glass)*mask;
                } else {
                    col=lerp(luminance.xxx,col,.83); col=col*float3(1.035,1.015,.98);col+=noise*.006*_Strength*smoothstep(.015,.15,luminance);
                }
                return float4(max(0,col)*vignette*_Fade,1);
            }
            ENDHLSL
        }
    }
}
