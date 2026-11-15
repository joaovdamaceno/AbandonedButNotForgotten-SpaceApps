using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum SoilPhase { Inactive, Digging, Press, PressRetreat, Impact, Falling, ImpactRetreat, Comparison, Archive, Montage, Complete }

    /// <summary>Two different soil operations on the same arm. Only holds do work; reading uses active time.</summary>
    public sealed class SurveyorSoilTests : MonoBehaviour,IDisposable
    {
        public const string StepId="Sample",NextStep="Eclipse",PressId="sur.soil.press",ImpactId="sur.soil.impact";
        public const float PressSeconds=4,ImpactSeconds=2,RetreatSeconds=3.2f;
        ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService service;SurveyorScoop scoop;
        SurveyorTelevisionOutput output;Camera camera;SurveyorSoilResults data;Transform pressMark,impactMark,montageMarks;
        IDisposable lease;bool bound,running,offered;float stageTime,saved,holdStart,cueTime,entryTime;Vector3 lastTip;SubtitleCue cue;
        [SerializeField] Renderer[] opticalOccluders=Array.Empty<Renderer>();
        UnityEngine.Rendering.ShadowCastingMode[] originalShadows;
        public void SetOpticalOccluders(Renderer[] renderers)=>opticalOccluders=renderers??Array.Empty<Renderer>();
        public event Action Changed;public event Action<SubtitleCue> CueEntered,CueExited;
        public SoilPhase Phase{get;private set;}
        public bool Running=>running;
        public float Preparation=>Phase==SoilPhase.Press?Mathf.Clamp01(saved/PressSeconds):Phase==SoilPhase.Impact?Mathf.Clamp01(saved/ImpactSeconds):0;
        public float Resistance=>Mathf.Pow(Preparation,.65f);
        public bool CanAct=>running && !owner.IsPaused && offered && service.CanBeginHold(ActionId);
        public string ActionId=>Phase==SoilPhase.Press?PressId:ImpactId;
        public bool NeedsRelease=>offered && service.AwaitingRelease;
        public Texture2D Before{get;private set;}
        public Texture2D After{get;private set;}
        public SurveyorSoilResults Results=>data;
        public Func<Texture2D> CaptureOverride{get;set;}
        public bool HasBothCaptures=>Before!=null && After!=null;
        public void Configure(ChapterDirector chapter,SessionClock session,InputRouter router,InteractionService interactions,
            SurveyorScoop arm,SurveyorTelevisionOutput source,Camera optics,SurveyorSoilResults result,Transform press,Transform impact,Transform montage)
        {
            Dispose();owner=chapter;clock=session;input=router;service=interactions;scoop=arm;output=source;camera=optics;data=result;pressMark=press;impactMark=impact;montageMarks=montage;
            if(owner==null||clock==null||input==null||service==null||scoop==null||data==null||pressMark==null||impactMark==null||montageMarks==null)throw new ArgumentException("Soil references missing.");
            owner.StepEntered+=Step;owner.ChapterUnloading+=Unload;scoop.Completed+=Dug;
            service.HoldStarted+=Started;service.HoldProgress+=Progress;service.HoldCanceled+=Canceled;service.HoldCompleted+=Held;bound=true;
            if(owner.CurrentStepId==StepId)Enter();
        }
        void Step(string id){if(id==StepId)Enter();else Stop();}
        void Unload(string id)=>Stop();
        void Enter()
        {
            Stop();running=true;Phase=SoilPhase.Digging;entryTime=clock.ActiveSeconds;
            // First-person instrument view: keep chassis shadows, hide the frame crossing the optical ray.
            originalShadows=new UnityEngine.Rendering.ShadowCastingMode[opticalOccluders.Length];
            for(int i=0;i<opticalOccluders.Length;i++)if(opticalOccluders[i]!=null){originalShadows[i]=opticalOccluders[i].shadowCastingMode;opticalOccluders[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;}
            pressMark.gameObject.SetActive(false);impactMark.gameObject.SetActive(false);montageMarks.gameObject.SetActive(false);
            // Keep the optical centre, point at the same excavated area. Capture before the scoop begins moving.
            if(camera!=null){camera.transform.rotation=Quaternion.LookRotation(scoop.TrenchCenter.position-camera.transform.position,Vector3.up);camera.fieldOfView=25.3f;}
            Before=Capture();Changed?.Invoke();
        }
        Texture2D Capture()
        {
            if(CaptureOverride!=null)return CaptureOverride();
            if(output==null||output.Source==null||camera==null)return null;
            camera.Render();output.RenderFrame();var previous=RenderTexture.active;var source=output.Texture;
            if(source==null)return null;
            var texture=new Texture2D(640,480,TextureFormat.RGB24,false){name="Surveyor soil comparison"};
            try{RenderTexture.active=source;texture.ReadPixels(new Rect(0,0,640,480),0,0);texture.Apply(false,false);}finally{RenderTexture.active=previous;}
            return texture;
        }
        void Dug(){if(running&&Phase==SoilPhase.Digging)Begin(SoilPhase.Press);}
        void Begin(SoilPhase next)
        {
            ClearOffer();Phase=next;stageTime=saved=0;
            if(next==SoilPhase.Press||next==SoilPhase.Impact)
            {
                lease?.Dispose();lease=input.Acquire(InputContext.Panel,"SUR-010.Soil");
                Offer();Raise(next==SoilPhase.Press?"SUR-S16":"SUR-S17",5);
            }
            else if(next==SoilPhase.Comparison){After=Capture();Raise("SUR-S18",6);}
            else if(next==SoilPhase.Archive)Raise("SUR-S19",6);
            montageMarks.gameObject.SetActive(next==SoilPhase.Montage);Changed?.Invoke();
        }
        void Offer()
        {
            float total=Phase==SoilPhase.Press?PressSeconds:ImpactSeconds;
            service.SetCandidate(new InteractionCandidate{Id=ActionId,VerbKey=Phase==SoilPhase.Press?"soil.press":"soil.impact",ObjectKey="soil.sample",Available=true,HoldSeconds=Mathf.Max(0,total-saved),Priority=110});
            offered=true;if(input.IsInteractPhysicallyHeld)service.RequireRelease();
        }
        void ClearOffer(){if(offered){offered=false;service.ClearCandidate(ActionId);}}
        void Started(string id){if(offered&&id==ActionId)holdStart=saved;}
        void Progress(string id,float p)
        {
            if(!offered||id!=ActionId)return;
            float total=Phase==SoilPhase.Press?PressSeconds:ImpactSeconds;saved=Mathf.Max(saved,Mathf.Lerp(holdStart,total,p));
            PoseWork();Changed?.Invoke();
        }
        void Canceled(string id){if(offered&&id==ActionId){ClearOffer();Offer();Changed?.Invoke();}}
        void Held(string id)
        {
            if(!offered||id!=ActionId)return;
            saved=Phase==SoilPhase.Press?PressSeconds:ImpactSeconds;PoseWork();
            Begin(Phase==SoilPhase.Press?SoilPhase.PressRetreat:SoilPhase.Falling);
        }
        Vector3 Point(bool impact)=>new Vector3(.08f,.015f,impact?-.38f:.38f);
        void PoseWork()
        {
            bool impact=Phase==SoilPhase.Impact;var point=Point(impact);
            point.y+=impact?.12f:.14f*(1-Mathf.SmoothStep(0,1,Preparation));
            scoop.PositionForSoilTest(point);
            if(!impact){pressMark.gameObject.SetActive(true);pressMark.localScale=Vector3.one*Mathf.Lerp(.25f,1,Preparation);}
            lastTip=point;
        }
        void Update()=>Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running||owner.IsPaused||activeDeltaSeconds<=0)return;
            float dt=Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-entryTime));stageTime+=dt;
            if(cue!=null){cueTime-=dt;if(cueTime<=0)ClearCue();}
            if(Phase==SoilPhase.PressRetreat||Phase==SoilPhase.ImpactRetreat)
            {
                var p=Point(Phase==SoilPhase.ImpactRetreat);var lifted=p+Vector3.up*.10f;
                var rest=scoop.Trajectory.Rest;Vector3 tool=stageTime<1?Vector3.Lerp(p,lifted,Mathf.SmoothStep(0,1,stageTime)):
                    stageTime<2?Vector3.Lerp(lifted,new Vector3(rest.x,Mathf.Max(lifted.y,rest.y),rest.z),Mathf.SmoothStep(0,1,stageTime-1)):
                    Vector3.Lerp(new Vector3(rest.x,Mathf.Max(lifted.y,rest.y),rest.z),rest,Mathf.SmoothStep(0,1,(stageTime-2)/1.2f));
                scoop.PositionForSoilTest(tool);
                if(stageTime>=RetreatSeconds&&cue==null)Begin(Phase==SoilPhase.PressRetreat?SoilPhase.Impact:SoilPhase.Comparison);
            }
            else if(Phase==SoilPhase.Falling)
            {
                var p=Point(true);p.y+=stageTime<.4f?.12f*(1-Mathf.Pow(stageTime/.4f,2)):.02f*Mathf.Sin(Mathf.Clamp01((stageTime-.4f)/.4f)*Mathf.PI);
                scoop.PositionForSoilTest(p);
                if(stageTime>=.4f)impactMark.gameObject.SetActive(true);
                if(stageTime>=.8f)Begin(SoilPhase.ImpactRetreat);
            }
            else if(Phase==SoilPhase.Comparison&&stageTime>=8)Begin(SoilPhase.Archive);
            else if(Phase==SoilPhase.Archive&&stageTime>=6)Begin(SoilPhase.Montage);
            else if(Phase==SoilPhase.Montage&&stageTime>=data.MontageSeconds)
            {
                owner.Evidence.Add(new EvidenceRecord("surveyor.soil.result",EvidenceKind.Observation,"SUR-S19","S3-02",EvidenceAuthorship.Reconstruction));
                Phase=SoilPhase.Complete;running=false;lease?.Dispose();lease=null;owner.CompleteStep(StepId,NextStep);
            }
            Changed?.Invoke();
        }
        void Raise(string id,float seconds){ClearCue();string trigger=id=="SUR-S16"?"bearing":id=="SUR-S17"?"impact":id=="SUR-S18"?"result":"comparison";cue=SubtitleCue.Triggered(id,id,"surveyor.scoop."+trigger,seconds);cueTime=seconds;CueEntered?.Invoke(cue);}
        void ClearCue(){if(cue==null)return;var old=cue;cue=null;CueExited?.Invoke(old);}
        static void Release(Texture2D t){if(t==null)return;if(Application.isPlaying)Destroy(t);else DestroyImmediate(t);}
        public void Stop()
        {
            running=false;ClearOffer();ClearCue();lease?.Dispose();lease=null;Release(Before);Release(After);Before=After=null;
            if(originalShadows!=null){for(int i=0;i<opticalOccluders.Length;i++)if(opticalOccluders[i]!=null)opticalOccluders[i].shadowCastingMode=originalShadows[i];originalShadows=null;}
            if(montageMarks!=null)montageMarks.gameObject.SetActive(false);Phase=SoilPhase.Inactive;
        }
        public void Dispose()
        {
            Stop();if(!bound)return;bound=false;owner.StepEntered-=Step;owner.ChapterUnloading-=Unload;scoop.Completed-=Dug;
            service.HoldStarted-=Started;service.HoldProgress-=Progress;service.HoldCanceled-=Canceled;service.HoldCompleted-=Held;
        }
        void OnDisable()=>Dispose();
    }
}
