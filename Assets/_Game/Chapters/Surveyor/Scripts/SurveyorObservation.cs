using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum ObservationPhase { Inactive, Framing, InTransit, Slewing, Ready, Result, Complete }

    /// <summary>SUR-008. Desired pointing is local UI; only a received command moves the mirror.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorObservation : MonoBehaviour, IDisposable
    {
        public const string StepId = "Observe", NextStepId = "Sample";
        public const float CaptureHoldSeconds = 1.5f;
        const string ResultAction = "surveyor.observation.continue";
        ChapterDirector owner; SessionClock clock; InputRouter input; InteractionService service;
        SurveyorCommandDelay queue; SurveyorCommandInteraction action; SurveyorSiteBindings site;
        SurveyorTelevisionOutput output; Camera camera; SurveyorObservationTargets data;
        Transform[] anchors; Quaternion initialRotation, azimuthRest, elevationRest;
        Vector3 opticalPosition; Vector2 actual, desired, committed; float committedFov, enteredAt, idleAt;
        IDisposable inputLease; SubtitleCue cue; uint queueGeneration; int generation;
        bool running, bound, ownsQueue, offered, detail, reviewing, rearmAfterContext; string offeredKind;
        int resultIndex; readonly EvidenceRecord[] records = new EvidenceRecord[3];

        public event Action Changed, PictureRecorded;
        public event Action<SubtitleCue> CueEntered, CueExited;
        public Func<Sprite> CaptureOverride { get; set; }
        public ObservationPhase Phase { get; private set; }
        public bool Running => running;
        public bool IsPaused => owner != null && owner.IsPaused;
        public bool ActComplete => owner != null && owner.CurrentStepId == NextStepId;
        public int TargetIndex { get; private set; }
        public int RecordCount { get; private set; }
        public int CaptureAllocations { get; private set; }
        public float LookSensitivity { get; set; } = 1f;
        public bool PointerOverImage { get; set; }
        public Vector2 DesiredAngles => desired;
        public Vector2 ActualAngles => actual;
        public Camera TelevisionCamera => camera;
        public SurveyorObservationTargets Definition => data;
        public SurveyorObservationTargets.Target Target => data.Targets[TargetIndex];
        public EvidenceRecord Result => resultIndex >= 0 && resultIndex < RecordCount ? records[resultIndex] : null;
        public bool Reviewing => reviewing;
        public bool DetailLens => detail;
        public string SubtitleKey => cue?.TextKey;
        public float IdleSeconds => running ? Mathf.Max(0, clock.ActiveSeconds - idleAt) : 0;
        public bool CanAim => running && !IsPaused && (Phase == ObservationPhase.Framing || Phase == ObservationPhase.Ready);
        public bool CanChangeLens => CanAim && queue.CanSend && !action.IsHolding;
        public bool MirrorSettled => AngularError(actual, committed) < .04f && Mathf.Abs(camera.fieldOfView - committedFov) < .02f;
        public bool Aligned => running && MirrorSettled && InsideFrame(TargetViewport, data.FrameHalfSize);
        public Vector3 TargetViewport => camera.WorldToViewportPoint(anchors[TargetIndex].position);
        public Vector3 DesiredViewport => camera.WorldToViewportPoint(opticalPosition+Rotation(desired)*Vector3.forward*10);
        public bool CanPrimary => running && !IsPaused && (Phase == ObservationPhase.Result ? service.CanBeginHold(ResultAction) : action.CanConfirm);
        public string PrimaryKey => Phase == ObservationPhase.Result ? reviewing ? "obs.back" : RecordCount == 3 ? "obs.finish" : "obs.next" : offeredKind == "photo" ? "obs.capture" : "obs.send";

        public void Configure(ChapterDirector chapter, SessionClock session, InputRouter router, InteractionService interactions,
            SurveyorCommandDelay delay, SurveyorCommandInteraction command, SurveyorSiteBindings bindings,
            Camera televisionCamera, SurveyorTelevisionOutput image, SurveyorObservationTargets targets)
        {
            Dispose(); owner=chapter; clock=session; input=router; service=interactions; queue=delay; action=command;
            site=bindings; camera=televisionCamera; output=image; data=targets;
            if (owner==null || clock==null || input==null || service==null || queue==null || action==null || site==null || camera==null || output==null || data==null)
                throw new ArgumentException("Observation references missing.");
            var errors=data.Validate(); anchors=new Transform[3];
            for (int i=0;i<Mathf.Min(3,data.Targets.Count);i++) if(!site.TryGetTarget(data.Targets[i].anchorId,out anchors[i])) errors.Add("Observation anchor missing.");
            if(errors.Count>0) throw new ArgumentException(string.Join("\n",errors));
            opticalPosition=camera.transform.position; initialRotation=camera.transform.rotation;
            azimuthRest=site.MirrorAzimuth.localRotation; elevationRest=site.MirrorElevation.localRotation;
            queue.Configure(owner,clock); action.Configure(queue,service,input); Bind();
            if(owner.CurrentStepId==StepId) Enter();
        }

        public static float AngularError(Vector2 a, Vector2 b) => new Vector2(Mathf.DeltaAngle(a.x,b.x),a.y-b.y).magnitude;
        public static bool InsideFrame(Vector3 viewport, Vector2 halfSize) => viewport.z > 0 &&
            Mathf.Abs(viewport.x-.5f) <= halfSize.x+.00001f && Mathf.Abs(viewport.y-.5f) <= halfSize.y+.00001f;
        public static Vector2 Angles(Vector3 direction) => new Vector2(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,
            Mathf.Asin(Mathf.Clamp(direction.normalized.y,-1,1))*Mathf.Rad2Deg);
        static Quaternion Rotation(Vector2 angles) => Quaternion.Euler(-angles.y,angles.x,0);

        void Enter()
        {
            Stop(); running=true; TargetIndex=RecordCount=0; Array.Clear(records,0,records.Length); reviewing=false;
            foreach(var target in data.Targets)
            {
                if(!owner.Evidence.TryGet(target.evidenceId,out var existing))break;
                records[RecordCount++]=existing;
            }
            TargetIndex=Mathf.Min(RecordCount,2);
            actual=committed=desired=Angles(initialRotation*Vector3.forward); committedFov=data.WideFovDegrees; detail=false;
            camera.transform.SetPositionAndRotation(opticalPosition,initialRotation); camera.fieldOfView=committedFov;
            inputLease=input.Acquire(InputContext.Gameplay,"SUR-008.Observe");
            queue.BeginState(StepId); queueGeneration=queue.Generation; ownsQueue=true;
            if(RecordCount==3){resultIndex=2;ShowResult();}else SelectTarget();
        }
        void SelectTarget()
        {
            ClearCue(); desired=Angles(anchors[TargetIndex].position-opticalPosition); enteredAt=idleAt=clock.ActiveSeconds;
            cue=SubtitleCue.Triggered(Target.subtitleKey,Target.subtitleKey,"surveyor.observe.target",data.ReadingSeconds);
            CueEntered?.Invoke(cue); Phase=ObservationPhase.Framing; RefreshAction(); Changed?.Invoke();
        }
        void SetContext(InputContext context) { inputLease?.Dispose(); inputLease=input.Acquire(context,"SUR-008."+Phase); }
        public void SetDesired(Vector2 angles)
        {
            if(!CanAim || !float.IsFinite(angles.x) || !float.IsFinite(angles.y)) return;
            desired=new Vector2(Mathf.Repeat(angles.x+180,360)-180,Mathf.Clamp(angles.y,-85,85));
            idleAt=clock.ActiveSeconds; RefreshAction(); Changed?.Invoke();
        }
        public bool AssistFrame()
        {
            if(!CanAim || !queue.CanSend) return false;
            desired=Angles(anchors[TargetIndex].position-opticalPosition);
            var operation=CaptureAimOperation(); action.ClearAction(); offered=false;
            bool sent=queue.TrySend("observe.aim",operation); if(!sent)RefreshAction(); return sent;
        }
        Action CaptureAimOperation()
        {
            var accepted=desired; int token=generation;
            return ()=>{if(!IsCurrent(token))return;committed=accepted;Phase=ObservationPhase.Slewing;Changed?.Invoke();};
        }
        public bool ChangeLens()
        {
            if(!CanChangeLens)return false;
            bool next=!detail; int token=generation; action.ClearAction(); offered=false;
            return queue.TrySend("observe.lens",()=>{if(!IsCurrent(token))return;detail=next;committedFov=detail?data.DetailFovDegrees:data.WideFovDegrees;Phase=ObservationPhase.Slewing;});
        }
        bool IsCurrent(int token) => running && token==generation && queue.Generation==queueGeneration && owner.CurrentStepId==StepId;
        void RefreshAction()
        {
            if(!CanAim || !queue.CanSend)return;
            bool changedAim=AngularError(desired,committed)>.15f;
            string kind=!changedAim && Aligned ? "photo" : "aim";
            Phase=kind=="photo"?ObservationPhase.Ready:ObservationPhase.Framing;
            if(offered && offeredKind==kind) { action.SetAvailable(kind=="aim"?changedAim:Aligned); return; }
            action.ClearAction(); offered=false; offeredKind=kind;
            int token=generation;
            offered=action.Offer(new InteractionCandidate { Id="observe."+kind, VerbKey=kind=="photo"?"obs.capture":"obs.send",
                ObjectKey=Target.nameKey, Anchor=anchors[TargetIndex], Available=kind=="photo"?Aligned:changedAim,
                Priority=100, HoldSeconds=kind=="photo"?CaptureHoldSeconds:0 },
                ()=>kind=="photo"?(Action)(()=>ReceivePhoto(token)):CaptureAimOperation());
        }
        void ReceivePhoto(int token)
        {
            if(!IsCurrent(token))return;
            if(!Aligned) { Phase=ObservationPhase.Framing; offered=false; RefreshAction(); return; }
            Sprite photo=CaptureOverride!=null?CaptureOverride():CapturePicture();
            if(photo==null)throw new InvalidOperationException("The TV did not produce a photograph.");
            var record=new EvidenceRecord(Target.evidenceId,EvidenceKind.Photo,Target.resultKey,"S3-03",EvidenceAuthorship.PlayerCapture,photo);
            if(!owner.Evidence.Add(record))throw new InvalidOperationException("Observation was already registered.");
            if(!IsCurrent(token))return; // Added listeners may synchronously unload/restart the chapter.
            records[RecordCount++]=record; resultIndex=RecordCount-1; reviewing=false;
            ShowResult(); PictureRecorded?.Invoke();
        }
        Sprite CapturePicture()
        {
            if(output.Texture==null)return null;
            output.RenderFrame(); var source=output.Texture; var previous=RenderTexture.active;
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGB24,false) { name=Target.evidenceId };
            try { RenderTexture.active=source; texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0); texture.Apply(false,false); }
            catch { if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture); throw; }
            finally { RenderTexture.active=previous; }
            CaptureAllocations++; return Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
        }
        void ShowResult()
        {
            action.ClearAction(); offered=false; Phase=ObservationPhase.Result; SetContext(InputContext.Panel);
            service.SetCandidate(new InteractionCandidate { Id=ResultAction,VerbKey=PrimaryKey,ObjectKey="obs.record",Available=true,Priority=110,HoldSeconds=0 });
            service.RequireRelease(); Changed?.Invoke();
        }
        public bool Review(int index)
        {
            if(!CanAim || !queue.CanSend || index<0 || index>=RecordCount)return false;
            resultIndex=index;reviewing=true;ShowResult();return true;
        }
        public bool BeginPrimary() => Phase==ObservationPhase.Result ? CanPrimary && service.BeginHold(ResultAction) : action.BeginConfirmation();
        public void ReleasePrimary() { if(Phase!=ObservationPhase.Result)action.ReleaseConfirmation(); }
        void OnHoldCompleted(string id)
        {
            if(id!=ResultAction || !running || Phase!=ObservationPhase.Result)return;
            service.ClearCandidate(ResultAction);
            if(reviewing) { reviewing=false;Phase=ObservationPhase.Framing;SetContext(InputContext.Gameplay);RefreshAction(); }
            else if(RecordCount==3) { Phase=ObservationPhase.Complete;owner.CompleteStep(StepId,NextStepId); }
            else { TargetIndex=RecordCount;Phase=ObservationPhase.Framing;SetContext(InputContext.Gameplay);SelectTarget(); }
            Changed?.Invoke();
        }
        void OnSent(SurveyorCommandReceipt receipt)
        {
            if(!running || receipt.Generation!=queueGeneration)return;
            offered=false;Phase=ObservationPhase.InTransit;idleAt=clock.ActiveSeconds;Changed?.Invoke();
        }
        void OnFreshPress()
        {
            if(!running || IsPaused || !rearmAfterContext)return;
            rearmAfterContext=false;
            if(Phase==ObservationPhase.Result && service.AwaitingRelease) { service.NotifyInteractReleased();service.BeginHold(ResultAction); }
            else if(CanAim && action.NeedsRelease && !action.IsHolding) { service.NotifyInteractReleased();action.BeginConfirmation(); }
        }
        void OnContextChanged(InputContext context)=>rearmAfterContext=true;
        void OnHelp()=>AssistFrame();
        void Update() => Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || IsPaused)return;
            // A release suppressed by the preceding context is still a real physical release.
            if(rearmAfterContext && !input.IsInteractPhysicallyHeld && service.HoldingId==null)
            { service.NotifyInteractReleased();rearmAfterContext=false; }
            if(cue!=null && clock.ActiveSeconds-enteredAt>=data.ReadingSeconds)ClearCue();
            float dt=Mathf.Clamp(activeDeltaSeconds,0,SessionClock.MaxFrameDeltaSeconds);
            if(Phase==ObservationPhase.Slewing && dt>0)
            {
                actual=new Vector2(Mathf.MoveTowardsAngle(actual.x,committed.x,data.SlewDegreesPerSecond*dt),Mathf.MoveTowards(actual.y,committed.y,data.SlewDegreesPerSecond*dt));
                camera.transform.rotation=Rotation(actual); camera.fieldOfView=Mathf.MoveTowards(camera.fieldOfView,committedFov,35*dt);
                var original=Angles(initialRotation*Vector3.forward);
                site.MirrorAzimuth.localRotation=azimuthRest*Quaternion.Euler(0,Mathf.DeltaAngle(original.x,actual.x)*.5f,0);
                site.MirrorElevation.localRotation=elevationRest*Quaternion.Euler(-(actual.y-original.y)*.5f,0,0);
                if(MirrorSettled) { Phase=ObservationPhase.Framing;RefreshAction(); }
            }
            if(CanAim)
            {
                Vector2 move=input.Move;
                if(move.sqrMagnitude>0)SetDesired(desired+move*dt*14);
                if(PointerOverImage && input.Look.sqrMagnitude>0)SetDesired(desired+input.Look*(.055f*LookSensitivity));
                RefreshAction();
            }
            Changed?.Invoke();
        }
        void ClearCue(){if(cue==null)return;var old=cue;cue=null;CueExited?.Invoke(old);}
        void OnStep(string id){if(id==StepId)Enter();else if(running)Stop();}
        void OnUnloading(string id)
        {
            Stop();Array.Clear(records,0,records.Length);RecordCount=0;
            if(camera!=null){camera.transform.SetPositionAndRotation(opticalPosition,initialRotation);camera.fieldOfView=data.WideFovDegrees;}
            if(site!=null){site.MirrorAzimuth.localRotation=azimuthRest;site.MirrorElevation.localRotation=elevationRest;}
        }
        public void Stop()
        {
            generation++;running=false;ClearCue();
            if(ownsQueue && queue!=null && queue.Generation==queueGeneration){action.ClearAction();queue.EndState();}
            ownsQueue=false;offered=false;service?.ClearCandidate(ResultAction);inputLease?.Dispose();inputLease=null;
            Phase=ActComplete?ObservationPhase.Complete:ObservationPhase.Inactive;Changed?.Invoke();
        }
        void Bind()
        {
            if(bound)return;bound=true;owner.StepEntered+=OnStep;owner.ChapterUnloading+=OnUnloading;
            service.HoldCompleted+=OnHoldCompleted;queue.Sent+=OnSent;input.HelpDown+=OnHelp;input.InteractDown+=OnFreshPress;
            input.ContextChanged+=OnContextChanged;
        }
        public void Dispose()
        {
            Stop();if(!bound)return;bound=false;owner.StepEntered-=OnStep;owner.ChapterUnloading-=OnUnloading;
            service.HoldCompleted-=OnHoldCompleted;queue.Sent-=OnSent;input.HelpDown-=OnHelp;input.InteractDown-=OnFreshPress;
            input.ContextChanged-=OnContextChanged;
        }
        void OnDisable()=>Dispose();
    }
}
