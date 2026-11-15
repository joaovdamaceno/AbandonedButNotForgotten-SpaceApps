using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.Opportunity
{
    // Runs only in a browser. These profiles retain the same depth atmosphere as PC.
    [DefaultExecutionOrder(-100)]
    public sealed class WebQualityController:MonoBehaviour
    {
        public UniversalRenderPipelineAsset low,balanced;
        public ChapterDirector chapter;
        public bool Automatic{get;private set;}=true;
        public int Tier{get;private set;}=1;
        public float AverageFrameMilliseconds{get;private set;}
        public float SceneScale{get;private set;}=.9f;
        public float ParticleMultiplier=>Tier==0?.5f:.8f;
        public static WebQualityController Current{get;private set;}
        ParticleSystem[] particles=System.Array.Empty<ParticleSystem>();float[] rates=System.Array.Empty<float>();int[] capacities=System.Array.Empty<int>();
        UniversalRenderPipelineAsset runtimeLow,runtimeBalanced;float sampleTime,sampleSum,warmUntil,badTime,goodTime,nextChange;
        int samples;
        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Current=this;Automatic=PlayerPrefs.GetInt("ll.web.auto",1)!=0;Tier=Mathf.Clamp(PlayerPrefs.GetInt("ll.web.tier",1),0,1);
            if(low)runtimeLow=Instantiate(low);if(balanced)runtimeBalanced=Instantiate(balanced);
            Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            warmUntil=Time.realtimeSinceStartup+6;SetTierInternal(Tier);
#else
            enabled=false;
#endif
        }
        void Start(){RefreshParticles();}
        public void RefreshParticles()
        {
            if(!Application.isPlaying)return;
            particles=FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);rates=new float[particles.Length];capacities=new int[particles.Length];
            for(int i=0;i<particles.Length;i++){rates[i]=particles[i].emission.rateOverTimeMultiplier;capacities[i]=particles[i].main.maxParticles;}
            SetParticleBudget();
        }
        public void SetAutomatic(bool automatic)
        {Automatic=automatic;PlayerPrefs.SetInt("ll.web.auto",automatic?1:0);badTime=goodTime=0;nextChange=Time.realtimeSinceStartup+12;}
        public void SetTier(int tier)
        {SetAutomatic(false);SetTierInternal(Mathf.Clamp(tier,0,1));PlayerPrefs.SetInt("ll.web.tier",Tier);}
        void SetTierInternal(int tier)
        {
            Tier=tier;SceneScale=tier==0?.72f:.9f;
            var pipeline=Tier==0?runtimeLow:runtimeBalanced;
            if(pipeline){pipeline.renderScale=SceneScale;QualitySettings.renderPipeline=pipeline;}
            QualitySettings.lodBias=Tier==0?.65f:1;QualitySettings.shadowDistance=Tier==0?30:50;
            SetParticleBudget();badTime=goodTime=0;nextChange=Time.realtimeSinceStartup+12;
        }
        void SetParticleBudget()
        {
            float multiplier=ParticleMultiplier;
            for(int i=0;i<particles.Length;i++)if(particles[i])
            {var emission=particles[i].emission;emission.rateOverTimeMultiplier=rates[i]*multiplier;var main=particles[i].main;main.maxParticles=Mathf.Max(8,Mathf.RoundToInt(capacities[i]*multiplier));}
        }
        void Update()
        {
            float now=Time.realtimeSinceStartup,dt=Time.unscaledDeltaTime;
            if(now<warmUntil||dt<=0||chapter&&chapter.Paused||!Application.isFocused)return;
            // Clamp occasional loading stalls; sustained very slow rendering must still
            // trigger the low profile instead of being discarded as an outlier forever.
            dt=Mathf.Min(dt,.25f);
            sampleSum+=dt;sampleTime+=dt;samples++;if(sampleTime<2)return;
            AverageFrameMilliseconds=1000*sampleSum/Mathf.Max(1,samples);float span=sampleTime;sampleSum=sampleTime=0;samples=0;
            if(!Automatic)return;
            badTime=AverageFrameMilliseconds>35?badTime+span:0;
            goodTime=AverageFrameMilliseconds<23?goodTime+span:0;
            if(now<nextChange)return;
            // Resolution changes do not alter UI Toolkit's CSS size. Two-second windows
            // and a long cool-down avoid visible quality oscillation during cutscenes.
            if(badTime>=6)
            {
                var active=Tier==0?runtimeLow:runtimeBalanced;
                if(Tier==1&&SceneScale>.72f){SceneScale=.72f;if(active)active.renderScale=SceneScale;}
                else if(Tier==1)SetTierInternal(0);
                else if(SceneScale>.60f){SceneScale=.60f;if(active)active.renderScale=SceneScale;}
                badTime=goodTime=0;nextChange=now+15;
            }
            else if(goodTime>=24&&Tier==0){SetTierInternal(1);nextChange=now+25;}
        }
        void OnDestroy()
        {
            if(Current==this)Current=null;
            if(runtimeLow)Destroy(runtimeLow);if(runtimeBalanced)Destroy(runtimeBalanced);
        }
    }
}
