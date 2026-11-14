using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum TelevisionPhase{Locked,AwaitingOrder,InTransit,Scanning,Contemplation,Complete}

    /// <summary>SUR-007: a command receives one image; only the shared sequence advances the scan and reading window.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorTelevision:MonoBehaviour,IDisposable
    {
        public const string StepId="FirstImage",NextStepId="Observe",ReadyEvent="surveyor.tv.imageReady";
        public const float HoldSeconds=2;
        ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService sharedService;SurveyorDescent descent;SurveyorDescentCameraGate gate;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SequenceDirector sequence;SequenceDefinition data;SurveyorTelevisionOutput output;
        IDisposable inputLease;bool bound,running,ownsQueue;int generation;uint queueGeneration;float beganSequenceAt;
        Action<SubtitleCue> cueIn,cueOut;Action<string> marker;Action<SequenceResult> finished;
        public event Action Changed;public event Action<SubtitleCue> CueEntered,CueExited;
        public bool Running=>running;public bool IsPaused=>owner!=null&&owner.IsPaused;
        public bool ActComplete=>owner!=null&&owner.CurrentStepId==NextStepId;
        public TelevisionPhase Phase{get;private set;}
        public float ScanSeconds{get;private set;}
        public float SequenceTime=>sequence!=null?sequence.TimeSeconds:0;
        public string SubtitleKey{get;private set;}
        public SurveyorTelevisionOutput Output=>output;
        public SequenceDefinition Definition=>data;
        public void Configure(ChapterDirector chapter,SessionClock session,InputRouter router,InteractionService service,
            SurveyorCommandDelay delay,SurveyorCommandInteraction interaction,SequenceDirector director,SequenceDefinition track,
            SurveyorDescent landed,SurveyorDescentCameraGate cameraGate,SurveyorTelevisionOutput image)
        {
            Dispose();owner=chapter;clock=session;input=router;sharedService=service;queue=delay;action=interaction;sequence=director;data=track;descent=landed;gate=cameraGate;output=image;
            if(owner==null||clock==null||input==null||queue==null||action==null||sequence==null||data==null||descent==null||gate==null||output==null)throw new ArgumentException("TV references missing.");
            var errors=data.Validate();ScanSeconds=MarkerTime(ReadyEvent);
            if(ScanSeconds<=0 || data.TotalDurationSeconds<ScanSeconds+6)errors.Add("TV needs a scan and at least six seconds of contemplation.");
            if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            queue.Configure(owner,clock);action.Configure(queue,service,input);sequence.enabled=false;sequence.Initialize(sequence.CameraDirector,null,clock);
            Bind();if(owner.CurrentStepId==StepId)Enter();
        }
        float MarkerTime(string id){float start=0;foreach(var shot in data.Shots){foreach(var e in shot.Events)if(e.eventId==id)return start+e.timeSeconds;start+=shot.DurationSeconds;}return -1;}
        void Enter()
        {
            Stop(true);running=true;Phase=TelevisionPhase.Locked;SubtitleKey=null;
            inputLease=input.Acquire(InputContext.Panel,"SUR-007.FirstImage");queue.BeginState(StepId);queueGeneration=queue.Generation;ownsQueue=true;
            int token=generation;
            cueIn=c=>{if(token!=generation)return;SubtitleKey=c.TextKey;CueEntered?.Invoke(c);Changed?.Invoke();};
            cueOut=c=>{if(token!=generation)return;if(SubtitleKey==c.TextKey)SubtitleKey=null;CueExited?.Invoke(c);Changed?.Invoke();};
            marker=id=>{if(token==generation&&id==ReadyEvent){Phase=TelevisionPhase.Contemplation;output.SetProgress(1);Changed?.Invoke();}};
            finished=r=>{if(token==generation&&r.Reason==SequenceExitReason.Completed&&running){Phase=TelevisionPhase.Complete;owner.CompleteStep(StepId,NextStepId);}};
            sequence.CueEntered+=cueIn;sequence.CueExited+=cueOut;sequence.EventRaised+=marker;sequence.Finished+=finished;
            OfferWhenLanded();Changed?.Invoke();
        }
        void OfferWhenLanded()
        {
            if(!running||Phase!=TelevisionPhase.Locked||!descent.CanActivateCamera)return;
            int token=generation;
            if(action.Offer(new InteractionCandidate{Id="tv.firstImage",VerbKey="tv.receive",ObjectKey="tv.instrument",Available=true,Priority=100,HoldSeconds=HoldSeconds},()=>()=>Receive(token)))Phase=TelevisionPhase.AwaitingOrder;
        }
        void OnSent(SurveyorCommandReceipt receipt)
        {if(running&&receipt.Generation==queueGeneration&&receipt.CommandId=="tv.firstImage"){Phase=TelevisionPhase.InTransit;Changed?.Invoke();}}
        void Receive(int token)
        {
            if(token!=generation||!running||owner.CurrentStepId!=StepId||queue.Generation!=queueGeneration||!descent.CanActivateCamera)return;
            if(!gate.TryActivate()||!output.Begin())throw new InvalidOperationException("The settled TV could not allocate its image output.");
            beganSequenceAt=clock.ActiveSeconds;Phase=TelevisionPhase.Scanning;sequence.Play(data,false);Changed?.Invoke();
        }
        void Update()=>Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running||IsPaused)return;OfferWhenLanded();
            if((Phase==TelevisionPhase.Scanning||Phase==TelevisionPhase.Contemplation)&&activeDeltaSeconds>0)
            {
                float dt=Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-beganSequenceAt));
                sequence.Tick(dt);if(running)output.SetProgress(Mathf.Clamp01(sequence.TimeSeconds/ScanSeconds));
            }
            Changed?.Invoke();
        }
        void OnStep(string id){if(id==StepId)Enter();else if(running)Stop(false);}
        void OnUnloading(string id)=>Stop(true);void OnPaused(bool paused)=>Changed?.Invoke();
        void Bind(){if(bound||owner==null)return;bound=true;owner.StepEntered+=OnStep;owner.ChapterUnloading+=OnUnloading;owner.PausedChanged+=OnPaused;queue.Sent+=OnSent;descent.Changed+=OnDescentChanged;input.InteractDown+=OnFreshPress;}
        void OnFreshPress()
        {
            // Router emits this only for a new, unsuppressed press. A release while its previous context
            // suppressed E has no logical InteractUp; rearm the shared service at this proven fresh edge.
            if(running&&Phase==TelevisionPhase.AwaitingOrder&&action.NeedsRelease&&!action.IsHolding)
            {sharedService.NotifyInteractReleased();action.BeginConfirmation();}
        }
        void OnDescentChanged(){OfferWhenLanded();Changed?.Invoke();}
        public void Stop(bool releaseImage=true)
        {
            generation++;running=false;SubtitleKey=null;
            if(sequence!=null){sequence.CueEntered-=cueIn;sequence.CueExited-=cueOut;sequence.EventRaised-=marker;sequence.Finished-=finished;if(sequence.Current==data)sequence.Cancel();}
            if(ownsQueue&&queue!=null&&queue.Generation==queueGeneration){action.ClearAction();queue.EndState();}ownsQueue=false;
            inputLease?.Dispose();inputLease=null;if(releaseImage&&output!=null){output.Release();Phase=TelevisionPhase.Locked;}Changed?.Invoke();
        }
        public void Dispose()
        {
            Stop();if(!bound)return;bound=false;owner.StepEntered-=OnStep;owner.ChapterUnloading-=OnUnloading;owner.PausedChanged-=OnPaused;queue.Sent-=OnSent;descent.Changed-=OnDescentChanged;input.InteractDown-=OnFreshPress;
        }
        void OnEnable()=>Bind();void OnDisable()=>Dispose();
    }
}
