using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum CutoffOrigin { None, Player, GroundTeam }

    /// <summary>SUR-006. Contatos históricos independentes do input; fila e sequência usam o tempo ativo comum.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorDescent : MonoBehaviour, IDisposable
    {
        public const string StepId="Descent", NextStepId="FirstImage", IndicatorId="Surveyor.DescentIndicator";
        public const string OfferEvent="surveyor.descent.offer", DeadlineEvent="surveyor.descent.deadline", HardDeadlineEvent="surveyor.descent.graceEnd";
        public const string Contact1="surveyor.descent.contact1", Contact2="surveyor.descent.contact2", Contact3="surveyor.descent.contact3";
        public const string SettledEvent="surveyor.descent.settled", QuietEvent="surveyor.descent.quiet", ReadyEvent="surveyor.descent.ready";
        public const string CutoffEligibleEvent="surveyor.descent.cutoffEligible";
        public const string TeamTrigger="surveyor.descent.team", PlayerTrigger="surveyor.descent.player", ImageTrigger="surveyor.descent.image";
        public const float HoldSeconds=3;

        ChapterDirector owner;SessionClock clock;InputRouter input;SequenceDirector sequence;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SequenceDefinition data;Transform indicator;
        IDisposable inputLease;
        readonly List<SubtitleCue> activeCues=new List<SubtitleCue>();
        Action<SubtitleCue> cueIn,cueOut;Action<string> marked;Action<SequenceResult> finished;
        bool bound,running,grace,settled,readyTriggered,ownsQueue;
        int generation;uint queueGeneration;float enteredActiveSeconds,lastImpactTime=-100,receiptReadUntil;
        CutoffOrigin preparedOrigin;
        string subtitleKey;
        public event Action Changed;
        public event Action<int> GroundContact;
        public event Action<SubtitleCue> CueEntered,CueExited;
        public bool Running=>running;
        public bool IsPaused=>owner!=null && owner.IsPaused;
        public bool ActComplete=>owner!=null && owner.CurrentStepId==NextStepId;
        public bool Settled=>settled;
        public bool CanActivateCamera=>settled && EnginesCutOff && owner!=null && owner.State==AppState.Playing && owner.CurrentStepId==NextStepId && !owner.IsPaused;
        public bool CutoffSent{get;private set;}
        public bool CutoffReceived{get;private set;}
        public bool EnginesCutOff{get;private set;}
        public CutoffOrigin Origin{get;private set;}
        public int ContactCount{get;private set;}
        public float TimeSeconds=>sequence!=null?sequence.TimeSeconds:0;
        public float HeightMeters=>indicator!=null?Mathf.Max(0,indicator.localPosition.y):0;
        public float ImpactPulse=>running?Mathf.Clamp01(1-(TimeSeconds-lastImpactTime)/.65f):0;
        public float OfferAt{get;private set;}
        public float DeadlineAt{get;private set;}
        public float GraceEndAt{get;private set;}
        public float SettledAt{get;private set;}
        public float QuietAt{get;private set;}
        public float ReadyAt{get;private set;}
        public float CutoffEligibleAt{get;private set;}
        public string SubtitleKey=>subtitleKey;
        public SequenceDefinition Definition=>data;

        public void Configure(ChapterDirector chapter,SessionClock session,InputRouter router,InteractionService service,
            SurveyorCommandDelay delay,SurveyorCommandInteraction interaction,SequenceDirector director,SequenceDefinition track,Transform altitudeIndicator)
        {
            Dispose();owner=chapter;clock=session;input=router;queue=delay;action=interaction;sequence=director;data=track;indicator=altitudeIndicator;
            if(owner==null || clock==null || input==null || queue==null || action==null || sequence==null || data==null || indicator==null)
                throw new ArgumentException("Referências do pouso incompletas.");
            var errors=data.Validate();OfferAt=Marker(OfferEvent);DeadlineAt=Marker(DeadlineEvent);GraceEndAt=Marker(HardDeadlineEvent);
            SettledAt=Marker(SettledEvent);QuietAt=Marker(QuietEvent);ReadyAt=Marker(ReadyEvent);
            CutoffEligibleAt=Marker(CutoffEligibleEvent);
            if(DeadlineAt-OfferAt<15 || GraceEndAt<DeadlineAt || SettledAt<GraceEndAt+SurveyorCommandDelay.OneWayDelaySeconds || QuietAt<SettledAt+1.5f || ReadyAt<QuietAt)
                errors.Add("Janela acessível, trânsito ou pausa de assentamento inválidos.");
            if(!(Marker(Contact1)>OfferAt && Marker(Contact2)>Marker(Contact1) && Marker(Contact3)>Marker(Contact2) && Marker(Contact3)<=SettledAt))errors.Add("Contatos fora de ordem.");
            if(!(CutoffEligibleAt>Marker(Contact2) && CutoffEligibleAt<Marker(Contact3)))errors.Add("O corte deve ocorrer entre o segundo e o terceiro contato.");
            RequireCue(TeamTrigger,errors);RequireCue(PlayerTrigger,errors);RequireCue(ImageTrigger,errors);
            if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            queue.Configure(owner,clock);action.Configure(queue,service,input);
            sequence.enabled=false;sequence.Initialize(sequence.CameraDirector,null,clock);sequence.RegisterActor(IndicatorId,indicator);
            Bind();if(owner.CurrentStepId==StepId)Enter();
        }
        void RequireCue(string trigger,List<string> errors){if(CueFor(trigger)==null)errors.Add("Legenda por gatilho ausente: "+trigger);}
        SubtitleCue CueFor(string trigger){foreach(var cue in data.Cues)if(cue.TriggerId==trigger)return cue;return null;}
        float Marker(string id)
        {float start=0;foreach(var shot in data.Shots){foreach(var e in shot.Events)if(e.eventId==id)return start+e.timeSeconds;start+=shot.DurationSeconds;}return -1;}

        void Enter()
        {
            Stop();running=true;grace=settled=readyTriggered=false;CutoffSent=CutoffReceived=EnginesCutOff=false;Origin=preparedOrigin=CutoffOrigin.None;
            ContactCount=0;lastImpactTime=-100;receiptReadUntil=0;enteredActiveSeconds=clock.ActiveSeconds;int token=generation;
            inputLease=input.Acquire(InputContext.Panel,"SUR-006.Descent");queue.BeginState(StepId);queueGeneration=queue.Generation;ownsQueue=true;
            cueIn=cue=>{if(token!=generation)return;activeCues.Add(cue);SelectSubtitle();CueEntered?.Invoke(cue);Changed?.Invoke();};
            cueOut=cue=>{if(token!=generation)return;activeCues.Remove(cue);SelectSubtitle();CueExited?.Invoke(cue);Changed?.Invoke();};
            marked=id=>{if(token==generation)OnMarker(id);};finished=result=>{if(token==generation)OnFinished(result);};
            sequence.CueEntered+=cueIn;sequence.CueExited+=cueOut;sequence.EventRaised+=marked;sequence.Finished+=finished;
            sequence.Play(data,false);Changed?.Invoke();
        }
        void SelectSubtitle()
        {
            SubtitleCue best=null;foreach(var cue in activeCues)if(best==null || cue.Priority>=best.Priority)best=cue;
            subtitleKey=best?.TextKey;
        }
        void OnMarker(string id)
        {
            if(!running)return;
            if(id==OfferEvent)
            {
                int token=generation;
                action.Offer(new InteractionCandidate{Id="descent.cutoff",VerbKey="descent.send",ObjectKey="descent.engines",HoldSeconds=HoldSeconds,Available=true,Priority=100},()=>
                {preparedOrigin=CutoffOrigin.Player;return ()=>ReceiveCutoff(token);});
            }
            else if(id==DeadlineEvent && !CutoffSent)
            {if(action.IsHolding)grace=true;else SendTeamCutoff();}
            else if(id==HardDeadlineEvent && !CutoffSent)SendTeamCutoff();
            else if(id==CutoffEligibleEvent && CutoffReceived)EnginesCutOff=true;
            else if(id==Contact1 || id==Contact2 || id==Contact3)
            {
                int number=id==Contact1?1:id==Contact2?2:3;
                if(number>ContactCount){ContactCount=number;lastImpactTime=TimeSeconds;GroundContact?.Invoke(number);}
            }
            else if(id==SettledEvent)settled=ContactCount==3;
            Changed?.Invoke();
        }
        void OnSent(SurveyorCommandReceipt receipt)
        {
            if(!running || !ownsQueue || receipt.Generation!=queueGeneration || receipt.CommandId!="descent.cutoff")return;
            CutoffSent=true;Origin=preparedOrigin;Changed?.Invoke();
        }
        void SendTeamCutoff()
        {
            if(!running || CutoffSent || !queue.CanSend)return;
            action.ClearAction();preparedOrigin=CutoffOrigin.GroundTeam;int token=generation;
            queue.TrySend("descent.cutoff",()=>ReceiveCutoff(token));
        }
        void ReceiveCutoff(int token)
        {
            if(token!=generation || !running || CutoffReceived || owner.CurrentStepId!=StepId || queue.Generation!=queueGeneration)return;
            CutoffReceived=true;EnginesCutOff=TimeSeconds>=CutoffEligibleAt;
            string trigger=Origin==CutoffOrigin.GroundTeam?TeamTrigger:PlayerTrigger;
            receiptReadUntil=TimeSeconds+CueFor(trigger).MinimumReadSeconds;sequence.Trigger(trigger);Changed?.Invoke();
        }
        void Update()=>Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || IsPaused || !(activeDeltaSeconds>0))return;
            activeDeltaSeconds=Mathf.Min(activeDeltaSeconds,SessionClock.MaxFrameDeltaSeconds);
            activeDeltaSeconds=Mathf.Min(activeDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-enteredActiveSeconds));
            if(grace && !CutoffSent && !action.IsHolding)SendTeamCutoff();
            if(!readyTriggered && settled && EnginesCutOff && TimeSeconds>=ReadyAt-.0001f && TimeSeconds>=receiptReadUntil-.0001f)
            {readyTriggered=true;sequence.Trigger(ImageTrigger);}
            if(!readyTriggered)activeDeltaSeconds=Mathf.Min(activeDeltaSeconds,Mathf.Max(0,Mathf.Max(ReadyAt,receiptReadUntil)-TimeSeconds));
            sequence.Tick(activeDeltaSeconds);Changed?.Invoke();
        }
        void OnFinished(SequenceResult result)
        {if(running && result.Reason==SequenceExitReason.Completed && settled && EnginesCutOff && readyTriggered)owner.CompleteStep(StepId,NextStepId);}
        void OnStep(string id){if(id==StepId)Enter();else Stop();}
        void OnUnloading(string id){settled=false;CutoffReceived=EnginesCutOff=false;Stop();}void OnPaused(bool value)=>Changed?.Invoke();
        void Bind()
        {
            if(bound || owner==null)return;bound=true;owner.StepEntered+=OnStep;owner.ChapterUnloading+=OnUnloading;owner.PausedChanged+=OnPaused;queue.Sent+=OnSent;
        }
        public void Stop()
        {
            generation++;running=false;
            if(sequence!=null){sequence.CueEntered-=cueIn;sequence.CueExited-=cueOut;sequence.EventRaised-=marked;sequence.Finished-=finished;if(sequence.Current==data)sequence.Cancel();}
            if(ownsQueue && queue!=null && queue.Generation==queueGeneration){action.ClearAction();queue.EndState();}ownsQueue=false;
            inputLease?.Dispose();inputLease=null;activeCues.Clear();subtitleKey=null;Changed?.Invoke();
        }
        public void Dispose()
        {
            Stop();if(!bound)return;bound=false;owner.StepEntered-=OnStep;owner.ChapterUnloading-=OnUnloading;owner.PausedChanged-=OnPaused;queue.Sent-=OnSent;
            sequence.UnregisterActor(IndicatorId);
        }
        void OnEnable()=>Bind();void OnDisable()=>Dispose();
    }
}
