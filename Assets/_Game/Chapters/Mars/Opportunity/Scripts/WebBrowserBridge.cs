using System.Runtime.InteropServices;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    [DefaultExecutionOrder(-90)]
    public sealed class WebBrowserBridge:MonoBehaviour
    {
        public ChapterDirector chapter;
        public WebQualityController quality;
        public static WebBrowserBridge Current{get;private set;}
        bool suspendedByBrowser,diagnostics;float nextSnapshot;
        bool Browser=>Application.platform==RuntimePlatform.WebGLPlayer&&!Application.isEditor;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void OpportunityBrowser_Register(string name);
        [DllImport("__Internal")]static extern void OpportunityBrowser_ResumeAudio();
        [DllImport("__Internal")]static extern void OpportunityBrowser_Fullscreen();
        [DllImport("__Internal")]static extern void OpportunityBrowser_ResumeOverlay(int visible);
        [DllImport("__Internal")]static extern void OpportunityBrowser_Snapshot(string snapshot);
        [DllImport("__Internal")]static extern int OpportunityBrowser_IsQA();
#endif
        void Awake()
        {
            Current=this;
#if UNITY_WEBGL && !UNITY_EDITOR
            gameObject.name="OpportunityBrowserBridge";OpportunityBrowser_Register(gameObject.name);diagnostics=OpportunityBrowser_IsQA()!=0;
#endif
        }
        void OnApplicationFocus(bool focus){if(Browser&&!focus)SuspendForBrowser();}
        void OnApplicationPause(bool pause){if(Browser&&pause)SuspendForBrowser();}
        public void BrowserFocusLost(string unused){SuspendForBrowser();}
        void SuspendForBrowser()
        {
            if(!chapter||chapter.Stage==ChapterStage.ChapterEnd)return;
            if(!chapter.Paused){suspendedByBrowser=true;chapter.Pause(true);}
#if UNITY_WEBGL && !UNITY_EDITOR
            OpportunityBrowser_ResumeOverlay(1);
#endif
        }
        public void BrowserResume(string unused){RequestResume();}
        public void BrowserPause(string unused){RequestPause();}
        public void RequestResume()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpportunityBrowser_ResumeAudio();OpportunityBrowser_ResumeOverlay(0);
#endif
            if(suspendedByBrowser&&chapter){suspendedByBrowser=false;chapter.Pause(false);}
            else if(chapter&&!chapter.Paused)chapter.SetCursor();
        }
        public void RequestPause(){if(chapter&&!chapter.Paused)chapter.Pause(true);}
        public void RequestFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpportunityBrowser_Fullscreen();
#else
            Screen.fullScreen=!Screen.fullScreen;
#endif
        }
        public void BrowserSetQuality(string value)
        {
            if(!quality)return;
            if(value=="auto")quality.SetAutomatic(true);
            else if(int.TryParse(value,out int tier))quality.SetTier(tier);
        }
        void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(Time.realtimeSinceStartup<nextSnapshot)return;nextSnapshot=Time.realtimeSinceStartup+(diagnostics?.2f:1);
            var snapshot=new BrowserSnapshot{stage=chapter?chapter.Stage.ToString():"Dossier",paused=chapter&&chapter.Paused,
                elapsed=chapter?chapter.Elapsed:0,tier=quality?quality.Tier:1,automatic=quality&&quality.Automatic,
                sceneScale=quality?quality.SceneScale:1,frameMs=quality?quality.AverageFrameMilliseconds:0,
                managedBytes=System.GC.GetTotalMemory(false),width=Screen.width,height=Screen.height};
            if(diagnostics&&chapter)
            {
                snapshot.sessionSeconds=chapter.SessionSeconds;
                var rover=chapter.rover;
                if(rover)snapshot.rover=new RoverSnapshot{x=rover.transform.position.x,y=rover.transform.position.y,z=rover.transform.position.z,yaw=rover.transform.eulerAngles.y,speed=rover.Speed,controllable=rover.Controllable};
                var target=chapter.ActiveTarget;
                if(target)snapshot.target=new TargetSnapshot{x=target.position.x,y=target.position.y,z=target.position.z,distance=chapter.TargetDistance};
                var presentation=chapter.interactions?chapter.interactions.Current:default;
                snapshot.interaction=new InteractionSnapshot{id=presentation.Id,available=presentation.Available};
                var field=chapter.investigation;
                if(field)snapshot.field=new FieldSnapshot{study=field.Study.ToString(),sample=field.Sample,result=field.Result,aimX=field.AimX,aimY=field.AimY,stable=field.Stable,dwell=field.Dwell,berries=field.BerriesCompared,shield=field.ShieldExamined,meteorite=field.MeteoriteIdentified,sunClear=field.ClearSunMeasured,sunDust=field.DustSunMeasured};
                var science=chapter.science;
                if(science)snapshot.science=new ScienceSnapshot{active=science.Active,phase=science.Phase.ToString(),pressure=science.Pressure,progress=science.Progress,temperature=science.Temperature,stowed=science.ToolStowed,deployed=science.ToolDeployed,observations=science.Observations};
                var journey=chapter.journey;
                if(journey)snapshot.journey=new JourneySnapshot{traverse=journey.TraverseStep,panoramas=journey.PanoramaCount,frames=journey.photography?journey.photography.Frames.Count:0,photoActive=journey.photography&&journey.photography.Active};
            }
            OpportunityBrowser_Snapshot(JsonUtility.ToJson(snapshot));
#endif
        }
        [System.Serializable]sealed class BrowserSnapshot
        {public string stage;public bool paused,automatic;public float elapsed,sceneScale,frameMs,sessionSeconds;public int tier,width,height;public long managedBytes;
            public RoverSnapshot rover;public TargetSnapshot target;public InteractionSnapshot interaction;public FieldSnapshot field;public ScienceSnapshot science;public JourneySnapshot journey;}
        [System.Serializable]sealed class RoverSnapshot{public float x,y,z,yaw,speed;public bool controllable;}
        [System.Serializable]sealed class TargetSnapshot{public float x,y,z,distance;}
        [System.Serializable]sealed class InteractionSnapshot{public string id;public bool available;}
        [System.Serializable]sealed class FieldSnapshot{public string study;public int sample;public bool result,stable,berries,shield,meteorite,sunClear,sunDust;public float aimX,aimY,dwell;}
        [System.Serializable]sealed class ScienceSnapshot{public bool active,stowed,deployed;public string phase;public float pressure,progress,temperature;public int observations;}
        [System.Serializable]sealed class JourneySnapshot{public int traverse,panoramas,frames;public bool photoActive;}
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
