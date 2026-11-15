using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>SUR-012: the same optical point, two tracks, one active sequence clock.</summary>
    public sealed class SurveyorNight : MonoBehaviour, IDisposable
    {
        public const string NightStep="LunarNight", PassageStep="TimePassage", NextStep="ImaginedVisit";
        public const string RigId="surveyor.night.optics";
        ChapterDirector owner; SessionClock clock; SequenceDirector sequence; SequenceDefinition night, passage;
        CameraDirector cameras; Light sun; Transform optics; AudioSource[] instruments;
        Quaternion sunRest; float intensityRest; Color ambientRest;
        bool bound, running, transmissionEnded; float startedAt;
        public bool Running=>running;
        public bool ReducedMotion { get; set; }
        public bool IsPassage=>running && owner.CurrentStepId==PassageStep;
        public float TrackSeconds=>running?sequence.TimeSeconds:0;
        public float TotalSeconds=>IsPassage?night.TotalDurationSeconds+TrackSeconds:TrackSeconds;
        public float HudOpacity=>running && !IsPassage?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,12,TrackSeconds)):0;
        public bool TransmissionEnded=>transmissionEnded;
        public string CalendarKey=>!IsPassage?null:TrackSeconds<3?"sur.night.year1967":TrackSeconds<8?"sur.night.year1968":"sur.night.nov1969";
        public event Action Changed;

        public void Configure(ChapterDirector chapter, SessionClock session, InputRouter input, SequenceDirector director,
            CameraDirector cameraDirector, SequenceDefinition nightTrack, SequenceDefinition passageTrack,
            Light keyLight, Transform opticalRig, AudioSource[] instrumentSources=null)
        {
            Dispose(); owner=chapter; clock=session; sequence=director; cameras=cameraDirector; night=nightTrack; passage=passageTrack;
            sun=keyLight; optics=opticalRig; instruments=instrumentSources??Array.Empty<AudioSource>();
            if(owner==null || clock==null || input==null || sequence==null || cameras==null || cameras.TargetCamera==null ||
                sun==null || optics==null || night==null || passage==null) throw new ArgumentException("Night references missing.");
            if(night.Validate().Count>0 || passage.Validate().Count>0 || Mathf.Abs(night.TotalDurationSeconds+passage.TotalDurationSeconds-30)>.001f)
                throw new ArgumentException("Night and passage must form a valid 30-second edit.");
            sunRest=sun.transform.rotation; intensityRest=sun.intensity; ambientRest=RenderSettings.ambientLight;
            sequence.enabled=false; sequence.Initialize(cameras,input,clock);
            owner.StepEntered+=Step; owner.ChapterUnloading+=Unloading; sequence.Finished+=Finished; bound=true;
            if(owner.CurrentStepId==NightStep || owner.CurrentStepId==PassageStep) Step(owner.CurrentStepId);
        }
        void Step(string id)
        {
            if(id!=NightStep && id!=PassageStep) { if(running) Stop(true); return; }
            if(!running)
            {
                optics.SetPositionAndRotation(cameras.TargetCamera.transform.position,cameras.TargetCamera.transform.rotation);
                cameras.RegisterRig(RigId,optics); running=true; transmissionEnded=false;
            }
            if(id==PassageStep) { EndTransmission(); ReleasePlayerThumbnails(owner.Evidence); }
            startedAt=clock.ActiveSeconds;
            sequence.Play(id==NightStep?night:passage);
            ApplyEnvironment(); Changed?.Invoke();
        }
        void Update()=>Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || owner.IsPaused || activeDeltaSeconds<=0) return;
            sequence.Tick(Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-startedAt)));
            if(!running) return;
            if(!IsPassage && TrackSeconds>=11) EndTransmission();
            ApplyEnvironment(); Changed?.Invoke();
        }
        void ApplyEnvironment()
        {
            if(IsPassage)
            {
                float u=Mathf.Clamp01(TrackSeconds/passage.TotalDurationSeconds);
                float daylight=.5f-.5f*Mathf.Cos(u*4*Mathf.PI); // Four editorial light transitions, not an exact cycle count.
                float strength=ReducedMotion?.4f:1;
                sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(-6,23,daylight),145+u*80*strength,0);
                sun.intensity=Mathf.Lerp(.015f,intensityRest*strength,daylight);
                RenderSettings.ambientLight=Color.Lerp(ambientRest*.05f,ambientRest*strength,daylight);
            }
            else
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,night.TotalDurationSeconds,TrackSeconds));
                sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(23,-7,u),145,0);
                sun.intensity=intensityRest*(1-u);
                RenderSettings.ambientLight=Color.Lerp(ambientRest,ambientRest*.035f,u);
            }
        }
        void EndTransmission()
        {
            if(transmissionEnded) return; transmissionEnded=true;
            foreach(var source in instruments) if(source!=null) source.Stop();
        }
        // Keep discovery/provenance records; the next act uses an archival photograph, not these runtime textures.
        public static void ReleasePlayerThumbnails(EvidenceStore evidence)
        {
            var records=new List<EvidenceRecord>(); bool found=false;
            foreach(var record in evidence.Records)
            {
                bool release=record.Authorship==EvidenceAuthorship.PlayerCapture && record.Thumbnail!=null;
                found|=release;
                records.Add(release?new EvidenceRecord(record.EvidenceId,record.Kind,record.ResultKey,record.ScienceRef,
                    record.Authorship,null,record.CreditKey):record);
            }
            if(!found) return;
            evidence.Clear(); foreach(var record in records) evidence.Add(record);
        }
        void Finished(SequenceResult result)
        {
            if(!running || result.Reason!=SequenceExitReason.Completed) return;
            if(result.SequenceId==night.SequenceId && owner.CurrentStepId==NightStep) owner.CompleteStep(NightStep,PassageStep);
            else if(result.SequenceId==passage.SequenceId && owner.CurrentStepId==PassageStep)
            {
                // Release control before a visit consumer starts; later disposal must not overwrite its lighting.
                Stop(false); owner.CompleteStep(PassageStep,NextStep); Changed?.Invoke();
            }
        }
        void Unloading(string id)=>Stop(true);
        public void Stop(bool restore)
        {
            bool wasRunning=running; running=false;
            if(sequence!=null && (sequence.Current==night || sequence.Current==passage)) sequence.Cancel();
            if(wasRunning && restore) { sun.transform.rotation=sunRest; sun.intensity=intensityRest; RenderSettings.ambientLight=ambientRest; }
            if(cameras!=null) cameras.UnregisterRig(RigId);
            Changed?.Invoke();
        }
        public void Dispose()
        {
            Stop(true); if(!bound) return; bound=false;
            owner.StepEntered-=Step; owner.ChapterUnloading-=Unloading; sequence.Finished-=Finished;
        }
        void OnDisable()=>Dispose();
    }
}
