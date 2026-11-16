using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class NarrativeDirector : MonoBehaviour
    {
        public ChapterDirector chapter;
        public FilmSequence[] sequences;
        public Transform mast=>chapter.rover.Rig.Mast;
        public Transform assemblyBay,memorial;public ReflectionProbe assemblyProbe;
        public AbrasionSurface montageSurface;
        public ParticleSystem montageDust;
        float filmCut;
        [SerializeField] Abandoned.SpaceApps.NarrationPlayer narration;
        Abandoned.SpaceApps.NarrationTimeMap narrationClock;
        string spoken;
        public Abandoned.SpaceApps.NarrationPlayer Narration => narration;
        public bool HasPendingNarration => speaking != null || (narration != null && narration.IsBusy);
        void Speak(string key)
        {
            if(key==spoken)return;spoken=key;
            if (narration == null) return;
            narration.SetMuted(!GameSettings.Voice);
            narration.Present(key);
        }
        public void PresentToast(string key) { if (narration != null && narration.Catalog != null && narration.Catalog.Duration(key) > 0f) Speak(key); }
        public float SpeechDuration(string key) => narration != null && narration.Catalog != null ? narration.Catalog.Duration(key) : 0f;
        /// <summary>Plays narration lines one after another during free play (caption shown as a toast).</summary>
        Coroutine speaking;
        public void SpeakSequence(params string[] keys){if(speaking!=null)StopCoroutine(speaking);speaking=StartCoroutine(SpeakLines(keys));}
        System.Collections.IEnumerator SpeakLines(string[] keys)
        {
            foreach(var key in keys)
            {
                spoken=null;Speak(key);
                float seconds=Mathf.Max(SpeechDuration(key),Mathf.Max(4.5f,Localization.Get(key).Length/15f));
                chapter.hud.Toast(key,seconds+.6f);
                float active=0f;
                while(active<seconds+.5f||(narration!=null&&narration.IsBusy&&narration.CurrentKey==key)){if(!chapter.Paused)active+=Time.deltaTime;yield return null;}
            }
            speaking=null;
        }
        void Update()
        {
            if(narration==null)return;
            narration.SetMuted(!GameSettings.Voice);
            // MissionAudio applies the master through AudioListener.volume.
            narration.SetVolume(1f);
            narration.SetPaused(chapter&&chapter.Paused);
            narration.Tick(chapter&&!chapter.Paused?Time.deltaTime:0f);
            if(chapter&&chapter.sound)chapter.sound.NarrationActive=narration.IsBusy;
        }
        public float ToAuthoredTime(float actualSeconds) => narrationClock != null ? narrationClock.ToAuthored(actualSeconds) : ChapterDirector.UnnarratedFilmTime(chapter.Stage, actualSeconds);
        void FitNarrationClock(ChapterStage stage)
        {
            narrationClock=null;if(!current||current.beats==null||current.beats.Length==0)return;
            narrationClock=new Abandoned.SpaceApps.NarrationTimeMap();
            for(int i=0;i<current.beats.Length;)
            {
                var first=current.beats[i];int last=i;
                while(last+1<current.beats.Length&&current.beats[last+1].bodyKey==first.bodyKey)last++;
                float end=last+1<current.beats.Length?current.beats[last+1].at:current.duration;
                float baseline=ChapterDirector.UnnarratedRealTime(stage,end)-ChapterDirector.UnnarratedRealTime(stage,first.at);
                narrationClock.AddWindow(end,baseline,SpeechDuration(first.bodyKey));i=last+1;
            }
        }
        public float YearsStormStart {get{foreach(var sequence in sequences)if(sequence.stage==ChapterStage.TimeSkip)foreach(var b in sequence.beats)if(b.bodyKey=="v7.years.approach")return b.at;return 15;}}
        public bool MemorialActive=>(current&&current.stage==ChapterStage.Farewell)||chapter.Stage==ChapterStage.ChapterEnd;
        public float PictureBlack
        {
            get
            {
                if(current&&current.stage==ChapterStage.CommunicationLost)return Mathf.InverseLerp(10.7f,12,chapter.FilmTime);
                if(!current||current.stage!=ChapterStage.Farewell)return 0;
                float t=chapter.FilmTime;
                if(t<.7f)return 1;
                if(t<1.6f)return 1-Mathf.InverseLerp(.7f,1.6f,t);
                return Mathf.InverseLerp(15.3f,16,t);
            }
        }
        FilmSequence current;FilmBeat beat;int beatIndex=-1,lastImpact=-1;bool photoFired,pressureReleased;
        Vector3 toolRest;Quaternion toolRotation,mastRest;bool initialized;float wear=-1,initialStorm;int radioBeat=-1;
        public string TitleKey=>beat?.titleKey;
        public string BodyKey=>beat?.bodyKey;
        public bool ToolOperating=>beat!=null&&beat.action==FilmAction.Work&&BeatTime/BeatLength>.28f&&BeatTime/BeatLength<.62f;
        public float BeatTime=>beat==null?0:chapter.FilmTime-beat.at;
        public float BeatLength=>beat?.duration??1;
        public float CaptionOpacity
        {
            get
            {
                if(!current||beat==null||string.IsNullOrWhiteSpace(beat.bodyKey))return 0;
                float begin=beat.at,end=beat.at+beat.duration;
                for(int i=beatIndex-1;i>=0&&current.beats[i].bodyKey==beat.bodyKey;i--)begin=current.beats[i].at;
                for(int i=beatIndex+1;i<current.beats.Length&&current.beats[i].bodyKey==beat.bodyKey;i++)end=current.beats[i].at+current.beats[i].duration;
                if(beat.captionDuration>0)end=Mathf.Min(end,beat.at+beat.captionDuration);
                return Mathf.Clamp01((chapter.FilmTime-begin)/.35f)*Mathf.Clamp01((end-chapter.FilmTime)/.45f);
            }
        }
        /// <summary>Start and end of the first and last beat whose caption is one of <paramref name="keys"/>.</summary>
        public bool CaptionWindow(out float start,out float end,params string[] keys)
        {
            start=end=0;if(!current)return false;bool found=false;
            foreach(var b in current.beats)if(System.Array.IndexOf(keys,b.bodyKey)>=0){if(!found){start=b.at;found=true;}end=b.at+b.duration;}
            return found;
        }
        public float Power{get;private set;}=1;
        public bool HasSequence=>current!=null;
        public float SequenceDuration=>current?current.duration:0;
        void Initialize(){if(initialized)return;toolRest=chapter.rover.tool.localPosition;toolRotation=chapter.rover.tool.localRotation;if(mast)mastRest=mast.localRotation;initialized=true;}
        public void Enter(ChapterStage stage)
        {
            if(speaking!=null){StopCoroutine(speaking);speaking=null;}Speak(null);initialStorm=chapter.environment.Storm;chapter.cameras.Shake=0;chapter.environment.StoryLighting=false;radioBeat=-1;Initialize();if(stage!=ChapterStage.Landing)chapter.split?.ShowSpirit(false,Vector3.zero,0);current=null;beat=null;beatIndex=lastImpact=-1;Power=1;photoFired=pressureReleased=false;
            if((stage==ChapterStage.TimeSkip||stage==ChapterStage.HeatShieldArrival)&&montageSurface){montageSurface.ResetSurface();filmCut=0;}
            if(chapter.investigation.entryCapsule)chapter.investigation.entryCapsule.SetActive(false);if(chapter.investigation.entryBridle)chapter.investigation.entryBridle.gameObject.SetActive(false);
            if(assemblyBay)assemblyBay.gameObject.SetActive(false);chapter.environment.Indoors=false;
            if(memorial)memorial.gameObject.SetActive(false);chapter.environment.Memorial=false;chapter.sound.CinematicEnvelope=1;chapter.sound.QuietEnvironment=false;
            chapter.rover.GetComponent<RoverPresentationRig>()?.Fold(0);
            chapter.science.abrasionDust.Stop();chapter.sound.Tool(0);
            if(montageDust)montageDust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            chapter.rover.CinematicControl=false;chapter.rover.tool.localPosition=toolRest;chapter.rover.tool.localRotation=toolRotation;
            foreach(var s in sequences)if(s.stage==stage){current=s;break;}
            FitNarrationClock(stage);
            if(stage==ChapterStage.Activation||stage==ChapterStage.Discovery)chapter.sound.Radio(true);
            if(stage==ChapterStage.ChapterEnd){chapter.environment.Memorial=true;chapter.sound.QuietEnvironment=true;}
            if(current){chapter.rover.CinematicControl=stage!=ChapterStage.Intro&&stage!=ChapterStage.Landing;Evaluate(0);}
            else if(mast)mast.localRotation=mastRest;
        }
        public void Evaluate(float time)
        {
            if(!current)return;
            beat=current.At(time,out int index);float u=Mathf.Clamp01((time-beat.at)/Mathf.Max(.1f,beat.duration));
            bool changed=index!=beatIndex;
            if(changed)
            {
                beatIndex=index;photoFired=false;
                if(!string.IsNullOrWhiteSpace(beat.bodyKey))Speak(beat.bodyKey);
                if(current.stage!=ChapterStage.Intro&&current.stage!=ChapterStage.Transition)
                {
                    chapter.environment.Region(beat.valley,false);chapter.environment.Travel(beat.travel);chapter.environment.Hardware(beat.hardware);
                    if(current.stage!=ChapterStage.CommunicationLost&&current.stage!=ChapterStage.Landing&&current.stage!=ChapterStage.Activation&&Vector3.Distance(chapter.rover.transform.position,beat.from)>18)chapter.rover.Teleport(Ground(beat.from),beat.yaw);
                }
                if(!string.IsNullOrEmpty(beat.music))chapter.sound.Cue(beat.music);
                if(current.stage==ChapterStage.TimeSkip&&beat.powerFrom>=.3f&&beat.powerTo<.3f)chapter.sound.Warn();
            }
            if(current.stage!=ChapterStage.Landing)chapter.split?.ShowSpirit(beat.spirit,beat.spiritAt,beat.spiritYaw,beat.spiritTilt,beat.spiritSink);
            if(current.stage==ChapterStage.Intro)
            {
                chapter.environment.Indoors=beat.assembly;if(assemblyBay)assemblyBay.gameObject.SetActive(beat.assembly);chapter.rover.gameObject.SetActive(beat.assembly);
                if(beat.assembly){chapter.rover.CinematicControl=true;chapter.rover.SetFilmPose(beat.from,beat.yaw,0);if(mast)mast.localRotation=mastRest*Quaternion.Euler(0,Mathf.Lerp(beat.mastFrom,beat.mastTo,Mathf.SmoothStep(0,1,u)),0);}
                return;
            }
            if(current.stage==ChapterStage.Farewell)
            {
                bool portrait=true;chapter.environment.Memorial=portrait;chapter.sound.QuietEnvironment=portrait;
                if(memorial){memorial.gameObject.SetActive(time>=.7f&&time<16);memorial.GetComponentInChildren<RoverSurfaceWear>(true)?.SetDust(.6f);}
                chapter.sound.CinematicEnvelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,2,time));
                if(time>=2&&time<6){int call=Mathf.FloorToInt((time-2)/4);if(call!=radioBeat){radioBeat=call;chapter.sound.Radio(false);}}
            }
            else if(current.stage==ChapterStage.CommunicationLost)chapter.sound.CinematicEnvelope=1-Mathf.InverseLerp(10.7f,12,time);
            Power=Mathf.Lerp(beat.powerFrom,beat.powerTo,u);
            if(current.stage!=ChapterStage.Intro&&current.stage!=ChapterStage.Transition)
                chapter.environment.SetStorm(current.stage==ChapterStage.CommunicationLost?Mathf.Max(initialStorm,Mathf.Lerp(beat.stormFrom,beat.stormTo,Mathf.SmoothStep(0,1,u))):Mathf.Lerp(beat.stormFrom,beat.stormTo,Mathf.SmoothStep(0,1,u)),true);
            if(Mathf.Abs(wear-beat.wear)>.025f){wear=beat.wear;chapter.rover.GetComponent<RoverSurfaceWear>()?.SetDust(wear);}
            chapter.rover.DeploymentSupport=current.stage==ChapterStage.Landing||current.stage==ChapterStage.Activation;
            DirectLight(time);
            if(current.stage==ChapterStage.Landing){Entry(CompactLandingClock.SourceTime(time));chapter.split?.Tick(time);return;}
            
            if(current.stage==ChapterStage.Intro||current.stage==ChapterStage.Transition)return;
            var rover=chapter.rover;rover.CinematicControl=true;
            float movement=Mathf.Lerp(u,Mathf.SmoothStep(0,1,u),beat.movementEase);Vector3 p=Vector3.Lerp(beat.from,beat.to,movement);float speed=beat.action==FilmAction.Drive?Vector3.Distance(beat.from,beat.to)/Mathf.Max(1,beat.duration):0;
            if(current.stage==ChapterStage.CommunicationLost)p=chapter.FinalPosition+Quaternion.Euler(0,chapter.FinalYaw,0)*(p-chapter.shelterTarget.position);
            if(current.stage==ChapterStage.MeteoriteDiscovery)p=chapter.HardwarePosition;
            Vector3 ground=Ground(p);
            if(current.stage==ChapterStage.Activation)
            {
                float distance=Vector3.Distance(chapter.spawn2004.position,p);
                var landerRig=chapter.lander.GetComponent<AirbagLanderRig>();
                float ramp=Mathf.Clamp01((distance-landerRig.apothem)/Mathf.Sqrt(landerRig.apothem*landerRig.apothem+landerRig.closedRise*landerRig.closedRise));
                ground.y+=Mathf.Lerp(landerRig.deckHeight+.025f,0,ramp);
            }
            rover.SetFilmPose(ground,current.stage==ChapterStage.MeteoriteDiscovery?chapter.HardwareYaw:Mathf.LerpAngle(beat.yaw,beat.endYaw,u)+(current.stage==ChapterStage.CommunicationLost?chapter.FinalYaw:0),speed);
            if(mast)mast.localRotation=mastRest*Quaternion.Euler(0,Mathf.LerpAngle(beat.mastFrom,beat.mastTo,Mathf.SmoothStep(0,1,u)),0);
            if(beat.action==FilmAction.Work)
            {
                float withdrawn=u<.28f?1-u/.28f:u>.62f?(u-.62f)/.38f:0;
                var surface=beat.secondaryWork?montageSurface:chapter.science.surface;var specimen=surface.GetComponent<Collider>();
                if(beat.secondaryWork){float cut=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.62f,u));if(cut<filmCut){surface.ResetSurface();filmCut=0;}if(cut>filmCut){surface.Brush(new Vector2(.4f,.5f),(cut-filmCut)*6,.52f);if(cut>=1)surface.CommitCollision();}filmCut=cut;}
                Vector3 target=surface.WorldPoint(new Vector2(.4f,.5f))+surface.transform.up*.035f;
                rover.tool.position=InstrumentTrajectory.Position(target,rover.tool.parent.TransformPoint(toolRest),specimen.bounds,withdrawn);
                rover.tool.rotation=InstrumentTrajectory.Rotation(surface.transform.rotation,rover.tool.parent.rotation*toolRotation,withdrawn);
                if(u>.28f&&u<.62f){rover.toolRotor.Rotate(Vector3.up,Time.deltaTime*700);chapter.sound.Tool(.3f);var dust=beat.secondaryWork?montageDust:chapter.science.abrasionDust;if(!dust.isPlaying)dust.Play();dust.transform.position=surface.WorldPoint(new Vector2(.4f,.5f));var emission=dust.emission;emission.rateOverTime=45;}else {chapter.sound.Tool(0);chapter.science.abrasionDust.Stop();if(montageDust)montageDust.Stop();}
            }
            else {rover.tool.localPosition=Vector3.Lerp(rover.tool.localPosition,toolRest,Time.deltaTime*4);chapter.sound.Tool(0);chapter.science.abrasionDust.Stop();if(montageDust)montageDust.Stop();}
            if(beat.photograph&&!photoFired&&u>.55f){photoFired=true;chapter.sound.Photograph();}
        }
        Vector3 Ground(Vector3 p)
        {
            return chapter.rover.TerrainPoint(p);
        }
        void DirectLight(float time)
        {
            var env=chapter.environment;var stage=current.stage;env.StoryLighting=stage==ChapterStage.Landing||stage==ChapterStage.Discovery||stage==ChapterStage.HeatShieldArrival||stage==ChapterStage.MeteoriteDiscovery||stage==ChapterStage.TimeSkip;
            env.StoryExposure=0;
            if(stage==ChapterStage.Landing)env.StorySun=new Vector3(35,140,0);
            else if(stage==ChapterStage.Discovery){env.StorySun=new Vector3(18,-57,0);env.StoryExposure=.06f;}
            else if(stage==ChapterStage.HeatShieldArrival){env.StorySun=new Vector3(25,-39,0);env.StoryExposure=.03f;}
            else if(stage==ChapterStage.MeteoriteDiscovery)env.StorySun=new Vector3(25,-39,0);
            else if(stage==ChapterStage.TimeSkip){if(beat!=null&&beat.spirit){env.StorySun=new Vector3(9,-72,0);env.StoryExposure=.05f;}else{env.StorySun=time<14?new Vector3(17,-71,0):new Vector3(19,-64,0);env.StoryExposure=Mathf.Lerp(-.03f,.04f,Mathf.InverseLerp(20,25,time));}}
        }
        void Entry(float time)
        {
            var d=chapter;var study=d.investigation;var rig=d.lander.GetComponent<AirbagLanderRig>();var e=d.spawn2004.position;
            Vector3 upper=e+EntryFlight.Upper(time),payload=e+EntryFlight.Payload(time,rig.landingClearance);
            study.entryCapsule.SetActive(time<26);study.entryCapsule.transform.SetPositionAndRotation(upper,Quaternion.Euler(8,35,-4));
            study.entryShield.transform.localPosition=new Vector3(Mathf.Max(0,time-6)*.7f,-1.3f-Mathf.Max(0,time-6)*2.8f,0);
            study.entryParachute.SetActive(time>=6);study.entryParachute.transform.localScale=Vector3.one*EntryFlight.Ease(6,7.2f,time);
            var emission=study.entryBrake.emission;emission.rateOverTime=time>14&&time<18?22:0;
            d.lander.gameObject.SetActive(true);d.lander.SetPositionAndRotation(payload,EntryFlight.Rotation(time));rig.Pose(EntryFlight.Deflation(time),EntryFlight.Opening(time));
            d.airbagRoot.localScale=Vector3.one*EntryFlight.Inflation(time);
            study.entryBridle.gameObject.SetActive(time>=8&&time<18);
            var cable=study.entryBridle.GetComponent<LineRenderer>();cable.SetPosition(0,upper-Vector3.up);cable.SetPosition(1,d.lander.TransformPoint(Vector3.up*(rig.deckHeight+rig.closedRise)));
            // The payload exists inside its carrier throughout descent, without a reveal-time teleport.
            d.rover.gameObject.SetActive(true);d.rover.CinematicControl=true;
            d.rover.SetFilmPose(d.lander.TransformPoint(Vector3.up*(rig.deckHeight+.035f)),d.lander.eulerAngles.y,0);d.rover.transform.rotation=d.lander.rotation;
            d.rover.GetComponent<RoverPresentationRig>()?.Fold(1-EntryFlight.Deployed(time));
            int impact=EntryFlight.Impact(time);
            if(time<38&&impact>=0&&lastImpact!=impact){lastImpact=impact;d.landingDust.transform.position=Ground(payload)+Vector3.up*.03f;d.landingDust.Emit(impact==0?30:14);d.sound.Impact(1-impact*.25f);}
            if(time>=38&&time<40&&!pressureReleased){pressureReleased=true;d.sound.ReleasePressure();}
            float since=impact<0?100:time-(impact==0?28.4f:impact==1?32.6f:35.8f);d.cameras.Shake=time<38?.48f*Mathf.Exp(-since*7)/(impact+1):0;
        }
        public void Landing(float time){Entry(time+18);}
        void LateUpdate()
        {
            if(chapter.Paused||!mast||chapter.rover.CinematicControl)return;
            float angle=0;
            if(chapter.journey.photography.Active)angle=Mathf.DeltaAngle(chapter.rover.transform.eulerAngles.y,chapter.cameras.view.transform.eulerAngles.y);
            else if(chapter.rover.Controllable&&chapter.ActiveTarget){Vector3 delta=chapter.ActiveTarget.position-chapter.rover.transform.position;if(delta.sqrMagnitude>.0001f)angle=Mathf.DeltaAngle(chapter.rover.transform.eulerAngles.y,Quaternion.LookRotation(delta).eulerAngles.y)*.35f;}
            mast.localRotation=Quaternion.Slerp(mast.localRotation,mastRest*Quaternion.Euler(0,Mathf.Clamp(angle,-110,110),0),Time.deltaTime*2.5f);
        }
    }
}
