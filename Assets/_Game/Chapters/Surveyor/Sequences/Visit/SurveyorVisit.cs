using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorVisit : MonoBehaviour, IDisposable
    {
        public const string StepId="ImaginedVisit", ReceiptStep="HistoricalReceipt", EndStep="End";
        public const string LandingRig="surveyor.visit.landing", HumanRig="surveyor.visit.human";
        public const string ArchiveEvent="surveyor.visit.archive", MemorialEvent="surveyor.visit.memorial";
        ChapterDirector owner;SessionClock clock;InputRouter input;SequenceDirector sequence;SequenceDefinition track;
        SurveyorEnding ending;SurveyorVisitActors actors;CameraDirector cameras;Transform landingRig,humanRig;
        IDisposable returnInput;bool bound,running,began,awaitingReturn,lastHeld,cameraWasEnabled;float startedAt;
        public event Action Changed;
        public bool Running=>running;
        public bool AwaitingReturn=>awaitingReturn;
        public bool CanClickReturn=>awaitingReturn && !input.IsInteractPhysicallyHeld;
        public float TimeSeconds=>began?sequence.TimeSeconds:0;
        public bool ReducedMotion {get;set;}
        public bool ShowWorld=>began && TimeSeconds>=5 && TimeSeconds<31;
        public bool ShowArchive=>began && TimeSeconds>=32 && TimeSeconds<44;
        public bool ShowMemorial=>began && TimeSeconds>=44;
        public SurveyorEnding Ending=>ending;
        public void Configure(ChapterDirector chapter,SessionClock session,InputRouter router,SequenceDirector director,
            SequenceDefinition film,SurveyorEnding result,SurveyorVisitActors cast,CameraDirector camera,Transform landing,Transform human)
        {
            Dispose();owner=chapter;clock=session;input=router;sequence=director;track=film;ending=result;actors=cast;cameras=camera;landingRig=landing;humanRig=human;
            if(owner==null||clock==null||input==null||sequence==null||track==null||ending==null||actors==null||cameras==null||landingRig==null||humanRig==null)
                throw new ArgumentException("Visit references missing.");
            if(track.Validate().Count>0 || ending.Validate().Count>0 || Mathf.Abs(track.TotalDurationSeconds-50)>.001f)throw new ArgumentException("Visit needs a valid 50-second film and credited image.");
            sequence.enabled=false;sequence.Initialize(cameras,input,clock);
            owner.StepEntered+=Step;owner.ChapterUnloading+=Unloading;sequence.EventRaised+=Marker;sequence.Finished+=Finished;input.InteractDown+=FreshReturn;bound=true;
            if(owner.CurrentStepId==StepId)Enter();
        }
        void Enter()
        {
            Stop();running=began=true;awaitingReturn=false;
            var position=cameras.TargetCamera.transform.position;landingRig.position=humanRig.position=position;
            cameras.RegisterRig(LandingRig,landingRig);cameras.RegisterRig(HumanRig,humanRig);
            cameraWasEnabled=cameras.TargetCamera.enabled;cameras.TargetCamera.enabled=true;startedAt=clock.ActiveSeconds;
            sequence.Play(track);actors.Evaluate(0,ReducedMotion);Changed?.Invoke();
        }
        void Step(string id)
        {if(id==StepId)Enter();else if(running && id!=ReceiptStep && id!=EndStep)Stop();}
        void Marker(string id)
        {
            if(!running)return;
            if(id==ArchiveEvent)
            {
                owner.Evidence.Add(new EvidenceRecord("surveyor.apollo12.archive",EvidenceKind.Photo,"SUR-S29","S3-08",EvidenceAuthorship.Archive,null,ending.CreditKey));
                owner.CompleteStep(StepId,ReceiptStep);
            }
            else if(id==MemorialEvent)owner.CompleteStep(ReceiptStep,EndStep);
        }
        void Update()
        {
            Tick(clock!=null?clock.ActiveDeltaSeconds:0);
            if(awaitingReturn && lastHeld!=input.IsInteractPhysicallyHeld){lastHeld=input.IsInteractPhysicallyHeld;Changed?.Invoke();}
        }
        public void Tick(float activeDeltaSeconds)
        {
            if(!running||owner.IsPaused||activeDeltaSeconds<=0)return;
            sequence.Tick(Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-startedAt)));
            if(!running)return;
            actors.Evaluate(TimeSeconds,ReducedMotion);cameras.TargetCamera.enabled=TimeSeconds<31;Changed?.Invoke();
        }
        void Finished(SequenceResult result)
        {
            if(!running||result.SequenceId!=track.SequenceId||result.Reason!=SequenceExitReason.Completed)return;
            running=false;actors.Hide();cameras.TargetCamera.enabled=false;owner.CompleteStep(EndStep);
            awaitingReturn=true;lastHeld=input.IsInteractPhysicallyHeld;returnInput=input.Acquire(InputContext.Panel,"SUR-013.Return");Changed?.Invoke();
        }
        void FreshReturn(){if(awaitingReturn)Return();} // InputRouter only delivers a fresh, rearmed edge.
        public bool ReturnFromButton(){if(!CanClickReturn)return false;return Return();}
        bool Return()
        {
            if(!awaitingReturn)return false;
            return owner.ReturnToDossier(); // ChapterUnloading releases our lease and restores the source camera.
        }
        void Unloading(string id)=>Stop();
        public void Stop()
        {
            bool owned=running||awaitingReturn;running=awaitingReturn=began=false;
            if(sequence!=null&&sequence.Current==track)sequence.Cancel();
            returnInput?.Dispose();returnInput=null;
            if(actors!=null)actors.Hide();
            if(cameras!=null){cameras.UnregisterRig(LandingRig);cameras.UnregisterRig(HumanRig);if(owned)cameras.TargetCamera.enabled=cameraWasEnabled;}
            Changed?.Invoke();
        }
        public void Dispose()
        {
            Stop();if(!bound)return;bound=false;
            owner.StepEntered-=Step;owner.ChapterUnloading-=Unloading;sequence.EventRaised-=Marker;sequence.Finished-=Finished;input.InteractDown-=FreshReturn;
        }
        void OnDisable()=>Dispose();
    }
}
