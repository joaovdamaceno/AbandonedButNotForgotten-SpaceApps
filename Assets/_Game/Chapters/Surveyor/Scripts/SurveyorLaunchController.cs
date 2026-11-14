using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>SUR-003: somente o ato Launch. ChapterDirector é o proprietário das etapas; SequenceDirector é o relógio da montagem.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorLaunchController : MonoBehaviour
    {
        public const string StepId="Launch";
        public const string NextStepId="Cruise";
        public const string AscentEvent="surveyor.launch.ascent";
        public const string EnableCommandEvent="surveyor.launch.commandAvailable";
        public const string SendCommandEvent="surveyor.launch.send";
        public const string ExecuteCentaurEvent="surveyor.launch.centaur";
        public const float HoldSeconds=2.5f;

        [SerializeField] ChapterDirector owner;
        [SerializeField] InputRouter router;
        [SerializeField] InteractionService interactions;
        [SerializeField] SequenceDirector sequence;
        [SerializeField] SequenceDefinition launch;
        IActiveTimeSource activeTime;
        IDisposable inputLease;
        Action<SequenceResult> finishHandler;
        Action<SubtitleCue> enterHandler,exitHandler;
        Action<string> eventHandler;
        bool bound,running,prepared,sent,executed;
        int generation;
        float savedSeconds,attemptStart;
        string candidateId,subtitleKey;

        public event Action Changed;
        public event Action CentaurExecuted;
        public event Action<SubtitleCue> CueEntered;
        public event Action<SubtitleCue> CueExited;
        public event Action<string> AudioCueRequested;
        public bool Running=>running;
        public bool Prepared=>prepared;
        public bool Sent=>sent;
        public bool Executed=>executed;
        public bool ActComplete=>owner!=null && owner.CurrentStepId==NextStepId;
        public bool IsPaused=>owner!=null && owner.IsPaused;
        public bool IsHolding=>interactions!=null && candidateId!=null && interactions.HoldingId==candidateId;
        public bool NeedsRelease=>interactions!=null && interactions.AwaitingRelease;
        public float TimeSeconds=>sequence!=null?sequence.TimeSeconds:0;
        public float Progress=>Mathf.Clamp01(savedSeconds/HoldSeconds);
        public string SubtitleKey=>subtitleKey;
        public float ReadyAt { get; private set; }
        public float AscentAt { get; private set; }
        public float SendAt { get; private set; }
        public float ExecuteAt { get; private set; }
        public SequenceDefinition Definition=>launch;
        public bool CommandAvailable=>running && !prepared && TimeSeconds>=ReadyAt && !IsPaused;

        public void Configure(ChapterDirector chapter,InputRouter input,InteractionService service,SequenceDirector director,
            SequenceDefinition definition,IActiveTimeSource time)
        {
            Detach(); owner=chapter;router=input;interactions=service;sequence=director;launch=definition;activeTime=time;
            if(owner==null || router==null || interactions==null || sequence==null || launch==null || activeTime==null)
                throw new ArgumentException("Referências do lançamento incompletas.");
            var errors=launch.Validate();
            AscentAt=MarkerTime(launch,AscentEvent); ReadyAt=MarkerTime(launch,EnableCommandEvent); SendAt=MarkerTime(launch,SendCommandEvent); ExecuteAt=MarkerTime(launch,ExecuteCentaurEvent);
            if(!(AscentAt>=0 && ReadyAt>AscentAt && SendAt>ReadyAt && ExecuteAt>SendAt && ExecuteAt<launch.TotalDurationSeconds)) errors.Add("Marcas do lançamento fora de ordem.");
            if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            // O relógio continua sendo o comum. Só o alimentamos até a barreira de confirmação para nunca saltá-la.
            sequence.enabled=false;
            sequence.Initialize(sequence.CameraDirector,null,activeTime);
            Bind();
            if(owner.CurrentStepId==StepId)Enter();
        }

        static float MarkerTime(SequenceDefinition data,string id)
        {
            float start=0;
            foreach(var shot in data.Shots)
            {
                foreach(var e in shot.Events)if(e.eventId==id)return start+e.timeSeconds;
                start+=shot.DurationSeconds;
            }
            return -1;
        }

        void Bind()
        {
            if(bound || owner==null)return;
            bound=true;owner.StepEntered+=OnStep;owner.ChapterUnloading+=OnUnloading;owner.PausedChanged+=OnPaused;
            interactions.HoldStarted+=OnHoldStarted;interactions.HoldProgress+=OnHoldProgress;
            interactions.HoldCanceled+=OnHoldCanceled;interactions.HoldCompleted+=OnHoldCompleted;
        }

        void OnEnable(){if(Application.isPlaying)Bind();}
        void OnDisable()=>Detach();
        void OnStep(string id){if(id==StepId)Enter();else Stop();}
        void OnUnloading(string id)=>Stop();
        void OnPaused(bool value)=>Changed?.Invoke();

        void Enter()
        {
            Stop();running=true;savedSeconds=0;prepared=sent=executed=false;
            int token=generation;candidateId="surveyor.centaur."+token;
            inputLease=router.Acquire(InputContext.Panel,"SUR-003.Launch");
            enterHandler=cue=>{if(token!=generation)return;subtitleKey=cue.TextKey;CueEntered?.Invoke(cue);Changed?.Invoke();};
            exitHandler=cue=>{if(token!=generation)return;if(subtitleKey==cue.TextKey)subtitleKey=null;CueExited?.Invoke(cue);Changed?.Invoke();};
            eventHandler=id=>{if(token==generation)OnSequenceEvent(id);};
            finishHandler=result=>{if(token==generation)OnFinished(result);};
            sequence.CueEntered+=enterHandler;sequence.CueExited+=exitHandler;sequence.EventRaised+=eventHandler;sequence.Finished+=finishHandler;
            sequence.Play(launch,false); AudioCueRequested?.Invoke("surveyor.launch.bed"); Changed?.Invoke();
        }

        void Update(){if(activeTime!=null)Tick(activeTime.ActiveDeltaSeconds);}
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || IsPaused || !(activeDeltaSeconds>0))return;
            float allowed=prepared?activeDeltaSeconds:Mathf.Min(activeDeltaSeconds,Mathf.Max(0,SendAt-TimeSeconds));
            sequence.Tick(allowed);Changed?.Invoke();
        }

        void OnSequenceEvent(string id)
        {
            if(!running)return;
            if(id==EnableCommandEvent)RegisterCandidate();
            else if(id==SendCommandEvent)TrySend();
            else if(id==ExecuteCentaurEvent && prepared && sent && !executed)
            {executed=true;CentaurExecuted?.Invoke();}
            Changed?.Invoke();
        }

        void RegisterCandidate()
        {
            if(!running || prepared)return;
            interactions.SetCandidate(new InteractionCandidate { Id=candidateId,Anchor=transform,VerbKey="launch.confirm",ObjectKey="launch.centaur",
                Available=true,DistanceMeters=0,HoldSeconds=Mathf.Max(0,HoldSeconds-savedSeconds),Priority=100 });
            if(router.IsInteractPhysicallyHeld)interactions.RequireRelease();
        }

        public bool BeginConfirmation()
        {
            if(!CommandAvailable)return false;
            if(interactions.Mode==HoldMode.Toggle && IsHolding){interactions.CancelHold();return true;}
            return interactions.BeginHold(candidateId);
        }

        public void ReleaseConfirmation()
        {if(interactions!=null && interactions.Mode==HoldMode.Hold && IsHolding)interactions.CancelHold();}
        void OnHoldStarted(string id){if(running && id==candidateId){attemptStart=savedSeconds;Changed?.Invoke();}}
        void OnHoldProgress(string id,float p)
        {if(running && id==candidateId){savedSeconds=attemptStart+(HoldSeconds-attemptStart)*Mathf.Clamp01(p);Changed?.Invoke();}}
        void OnHoldCanceled(string id){if(running && id==candidateId){RegisterCandidate();Changed?.Invoke();}}
        void OnHoldCompleted(string id)
        {
            if(!running || id!=candidateId || prepared)return;
            savedSeconds=HoldSeconds;prepared=true;interactions.ClearCandidate(candidateId);
            if(TimeSeconds>=SendAt)TrySend();
            Changed?.Invoke();
        }

        void TrySend()
        {
            if(!running || !prepared || sent)return;
            sent=true;AudioCueRequested?.Invoke("surveyor.launch.release");Changed?.Invoke();
        }

        void OnFinished(SequenceResult result)
        {
            if(!running || result.Reason!=SequenceExitReason.Completed || !prepared || !executed || owner.CurrentStepId!=StepId)return;
            owner.CompleteStep(StepId,NextStepId);
        }

        public void Stop()
        {
            generation++;running=false;
            if(sequence!=null)
            {
                sequence.CueEntered-=enterHandler;sequence.CueExited-=exitHandler;
                sequence.EventRaised-=eventHandler;sequence.Finished-=finishHandler;
                if(sequence.Current==launch)sequence.Cancel();
            }
            if(interactions!=null && candidateId!=null)interactions.ClearCandidate(candidateId);
            inputLease?.Dispose();inputLease=null;candidateId=null;subtitleKey=null;
            AudioCueRequested?.Invoke("surveyor.launch.stop");Changed?.Invoke();
        }

        void Detach()
        {
            Stop();if(!bound)return;bound=false;
            owner.StepEntered-=OnStep;owner.ChapterUnloading-=OnUnloading;owner.PausedChanged-=OnPaused;
            interactions.HoldStarted-=OnHoldStarted;interactions.HoldProgress-=OnHoldProgress;
            interactions.HoldCanceled-=OnHoldCanceled;interactions.HoldCompleted-=OnHoldCompleted;
        }
    }
}
