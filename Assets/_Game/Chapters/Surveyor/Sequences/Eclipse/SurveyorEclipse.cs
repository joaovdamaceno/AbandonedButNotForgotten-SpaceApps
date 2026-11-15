using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum EclipsePhase { Inactive, AwaitingOrder, InTransit, Viewing, Complete }

    /// <summary>SUR-011. One received order, one SequenceDirector clock and an immutable optical centre.</summary>
    public sealed class SurveyorEclipse : MonoBehaviour, IDisposable
    {
        public const string StepId = "Eclipse", NextStepId = "LunarNight";
        public const string SkyRigId = "surveyor.eclipse.sky", GroundRigId = "surveyor.eclipse.ground";
        public const float MirrorLimitDegrees = 35;
        ChapterDirector owner; SessionClock clock; InputRouter input; InteractionService service;
        SurveyorCommandDelay queue; SurveyorCommandInteraction action; SequenceDirector sequence;
        SequenceDefinition data; SurveyorSiteBindings site; SurveyorEclipseImage image;
        Camera camera; Transform skyRig, groundRig;
        Vector3 opticalPosition; Quaternion groundRotation, azimuthRest, elevationRest;
        IDisposable inputLease; uint queueGeneration; int generation;
        bool bound, running, ownsQueue; float startedAt;
        public event Action Changed;
        public EclipsePhase Phase { get; private set; }
        public bool Running => running;
        public bool IsPaused => owner != null && owner.IsPaused;
        public bool ReducedMotion { get; set; }
        public float SequenceTime => sequence != null ? sequence.TimeSeconds : 0;
        public SequenceDirector Director => sequence;
        public Camera TelevisionCamera => camera;
        public Vector3 OpticalPosition => opticalPosition;
        public bool CanConfirm => running && !IsPaused && Phase == EclipsePhase.AwaitingOrder && action.CanConfirm;
        public float PreparationProgress => action != null ? action.PreparationProgress : 0;
        public bool NeedsRelease => action != null && action.NeedsRelease;
        public float Enlargement => Phase == EclipsePhase.Viewing && !ReducedMotion
            ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,9,SequenceTime))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(21,22,SequenceTime)))
            : Phase == EclipsePhase.Viewing && SequenceTime >= 9 && SequenceTime < 21 ? 1 : 0;

        public void Configure(ChapterDirector chapter, SessionClock session, InputRouter router, InteractionService interactions,
            SurveyorCommandDelay delay, SurveyorCommandInteraction command, SequenceDirector director, SequenceDefinition track,
            SurveyorSiteBindings bindings, SurveyorEclipseImage skyImage, Transform sky, Transform ground)
        {
            Dispose(); owner=chapter; clock=session; input=router; service=interactions; queue=delay; action=command;
            sequence=director; data=track; site=bindings; image=skyImage; skyRig=sky; groundRig=ground;
            if(owner==null || clock==null || input==null || service==null || queue==null || action==null || sequence==null ||
                data==null || site==null || image==null || skyRig==null || groundRig==null || site.CameraDirector==null)
                throw new ArgumentException("Eclipse references missing.");
            if(data.Validate().Count>0 || Mathf.Abs(data.TotalDurationSeconds-26)>.001f)
                throw new ArgumentException("Eclipse needs a valid 26-second sequence.");
            if(!site.TryGetTarget("SUR.Trench",out _) || site.MirrorAzimuth==null || site.MirrorElevation==null)
                throw new ArgumentException("Eclipse requires the surface and mirror anchors.");
            camera=site.CameraDirector.TargetCamera;
            azimuthRest=site.MirrorAzimuth.localRotation; elevationRest=site.MirrorElevation.localRotation;
            queue.Configure(owner,clock); action.Configure(queue,service,input);
            sequence.enabled=false; sequence.Initialize(site.CameraDirector,input,clock);
            owner.StepEntered+=OnStep; owner.ChapterUnloading+=OnUnload; queue.Sent+=OnSent; input.InteractDown+=OnFreshPress;
            sequence.Finished+=OnFinished; bound=true;
            if(owner.CurrentStepId==StepId) Enter();
        }
        void Enter()
        {
            Stop(); running=true; Phase=EclipsePhase.AwaitingOrder;
            opticalPosition=camera.transform.position;
            site.TryGetTarget("SUR.Trench",out var trench);
            groundRotation=Quaternion.LookRotation(trench.position-opticalPosition,Vector3.up);
            groundRig.SetPositionAndRotation(opticalPosition,groundRotation);
            // SP-184: camera axis tilted 23.5 degrees; +35-degree mirror limit. Earth lies near the top of the wide image.
            var direction=new Vector3(-.62f,0,.78f).normalized;
            var skyRotation=Quaternion.LookRotation(Quaternion.AngleAxis(-58.5f,Vector3.Cross(Vector3.up,direction))*direction,Vector3.up);
            skyRig.SetPositionAndRotation(opticalPosition,skyRotation);
            site.CameraDirector.RegisterRig(SkyRigId,skyRig); site.CameraDirector.RegisterRig(GroundRigId,groundRig);
            image.Place(opticalPosition,skyRotation*Quaternion.Euler(-7.5f,0,0)); image.Evaluate(0); image.Show(true);
            inputLease=input.Acquire(InputContext.Panel,"SUR-011.Order");
            queue.BeginState(StepId); queueGeneration=queue.Generation; ownsQueue=true;
            int token=generation;
            action.Offer(new InteractionCandidate { Id="eclipse.frame", VerbKey="sur.eclipse.send", ObjectKey="sur.eclipse.earth",
                Available=true, Priority=100, HoldSeconds=.8f },()=>()=>Receive(token));
            Changed?.Invoke();
        }
        void Receive(int token)
        {
            if(token!=generation || !running || queue.Generation!=queueGeneration || owner.CurrentStepId!=StepId) return;
            startedAt=clock.ActiveSeconds; Phase=EclipsePhase.Viewing; sequence.Play(data); Changed?.Invoke();
        }
        public bool BeginConfirmation() => CanConfirm && action.BeginConfirmation();
        public void ReleaseConfirmation() { if(Phase==EclipsePhase.AwaitingOrder) action.ReleaseConfirmation(); }
        void OnSent(SurveyorCommandReceipt receipt)
        { if(running && receipt.Generation==queueGeneration && receipt.CommandId=="eclipse.frame") { Phase=EclipsePhase.InTransit; Changed?.Invoke(); } }
        void OnFreshPress()
        { if(running && Phase==EclipsePhase.AwaitingOrder && action.NeedsRelease && !action.IsHolding) { service.NotifyInteractReleased(); action.BeginConfirmation(); } }
        void Update() => Tick(clock!=null ? clock.ActiveDeltaSeconds : 0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || IsPaused || Phase!=EclipsePhase.Viewing || activeDeltaSeconds<=0) return;
            sequence.Tick(Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-startedAt)));
            if(!running) return;
            float t=SequenceTime;
            float up=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,4,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,26,t)));
            site.MirrorAzimuth.localRotation=azimuthRest*Quaternion.Euler(0,-38*up,0);
            site.MirrorElevation.localRotation=elevationRest*Quaternion.Euler(-MirrorLimitDegrees*up,0,0);
            image.Evaluate(t); Changed?.Invoke();
        }
        void OnFinished(SequenceResult result)
        {
            if(!running || result.SequenceId!=data.SequenceId || result.Reason!=SequenceExitReason.Completed) return;
            Phase=EclipsePhase.Complete;
            owner.CompleteStep(StepId,NextStepId);
        }
        void OnStep(string id) { if(id==StepId) Enter(); else if(running) Stop(); }
        void OnUnload(string id) => Stop();
        public void Stop()
        {
            bool wasRunning=running;
            generation++; running=false;
            if(sequence!=null && sequence.Current==data) sequence.Cancel();
            if(ownsQueue && queue!=null && queue.Generation==queueGeneration) { action.ClearAction(); queue.EndState(); }
            ownsQueue=false; inputLease?.Dispose(); inputLease=null;
            if(site!=null) { site.MirrorAzimuth.localRotation=azimuthRest; site.MirrorElevation.localRotation=elevationRest; }
            if(wasRunning && camera!=null && !site.CameraDirector.IsControlled)
                site.CameraDirector.Apply(new CameraPose(opticalPosition,groundRotation,25.3f));
            if(image!=null) image.Show(false);
            if(Phase!=EclipsePhase.Complete) Phase=EclipsePhase.Inactive;
            Changed?.Invoke();
        }
        public void Dispose()
        {
            Stop(); if(!bound) return; bound=false;
            owner.StepEntered-=OnStep; owner.ChapterUnloading-=OnUnload; queue.Sent-=OnSent; input.InteractDown-=OnFreshPress;
            sequence.Finished-=OnFinished; site.CameraDirector.UnregisterRig(SkyRigId); site.CameraDirector.UnregisterRig(GroundRigId);
        }
        void OnDisable() => Dispose();
    }
}
