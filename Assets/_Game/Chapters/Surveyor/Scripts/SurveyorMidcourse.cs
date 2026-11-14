using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum MidcoursePhase { Inactive, Travel, Guidance, Burn, Handoff }

    /// <summary>SUR-005. Sequências comuns por fase; orientação é interativa e nunca termina por cronômetro.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorMidcourse : MonoBehaviour, IDisposable
    {
        public const string StepId = "Cruise", NextStepId = "Descent";
        public const string HandoffEvent = "surveyor.cruise.handoff";
        public const float HoldSeconds = 3;
        public const float TargetDiameter = .35f;
        public const float MapAspect = .45f;
        public static readonly Vector2 TargetCenter = new Vector2(.72f,.225f);
        public static readonly Vector2 InitialAim = TargetCenter + new Vector2(-.14f,.04f);

        ChapterDirector owner;
        InputRouter router;
        SessionClock clock;
        SurveyorCommandDelay queue;
        SurveyorCommandInteraction action;
        SequenceDirector sequence;
        SequenceDefinition source, travel, guidance, arrival;
        IDisposable inputLease;
        Action<SequenceResult> finished;
        Action<SubtitleCue> entered, exited;
        Action<string> marked;
        bool bound, offered, running, widened;
        bool ownsCommandScope;
        uint commandGeneration;
        int generation;
        float lastProgressActive, lastPreparation, armAt, phaseBeganActive;
        Vector2 desired, committed, beforeBurn;
        string subtitleKey;

        public event Action Changed;
        public event Action<SubtitleCue> CueEntered, CueExited;
        public MidcoursePhase Phase { get; private set; }
        public bool Running => running;
        public bool ActComplete => owner != null && owner.CurrentStepId == NextStepId;
        public bool IsPaused => owner != null && owner.IsPaused;
        public bool CanAim => running && Phase == MidcoursePhase.Guidance && !IsPaused;
        public bool Aligned => Vector2.Distance(desired,TargetCenter) <= Tolerance;
        public float Tolerance => widened ? .12f : .075f;
        public Vector2 DesiredAim => desired;
        public Vector2 CommittedAim => committed;
        public Vector2 ActualAim => Phase == MidcoursePhase.Burn ? Vector2.Lerp(beforeBurn,TargetCenter,Mathf.SmoothStep(0,1,BurnProgress)) :
            Phase == MidcoursePhase.Handoff || ActComplete ? TargetCenter : InitialAim;
        public float TravelProgress => Phase == MidcoursePhase.Travel ? Mathf.Clamp01(sequence.TimeSeconds/source.Shots[0].DurationSeconds) : 1;
        public float BurnProgress => Phase == MidcoursePhase.Burn ? Mathf.Clamp01(sequence.TimeSeconds/source.Shots[2].DurationSeconds) :
            Phase == MidcoursePhase.Handoff || ActComplete ? 1 : 0;
        public float HandoffProgress => Phase == MidcoursePhase.Handoff ? Mathf.Clamp01((sequence.TimeSeconds-source.Shots[2].DurationSeconds)/source.Shots[3].DurationSeconds) : ActComplete ? 1 : 0;
        public float IdleSeconds => CanAim ? Mathf.Max(0,clock.ActiveSeconds-lastProgressActive) : 0;
        public int HelpLevel => widened ? 2 : IdleSeconds >= 8 ? 1 : 0;
        public string SubtitleKey => subtitleKey;
        public float ArmAtSeconds => armAt;
        public SequenceDefinition Definition => source;

        public void Configure(ChapterDirector chapter, InputRouter input, SessionClock session, InteractionService service,
            SurveyorCommandDelay delay, SurveyorCommandInteraction interaction, SequenceDirector director, SequenceDefinition definition)
        {
            Detach(); owner=chapter; router=input; clock=session; queue=delay; action=interaction; sequence=director; source=definition;
            if (owner==null || router==null || clock==null || queue==null || action==null || sequence==null || source==null)
                throw new ArgumentException("Referências do meio de curso incompletas.");
            var errors=source.Validate();
            if (source.Shots.Count!=4 || source.Cues.Count!=3) errors.Add("Cruise precisa de quatro planos e SUR-S04–S06.");
            if (errors.Count>0) throw new ArgumentException(string.Join("\n",errors));
            queue.Configure(owner,clock); action.Configure(queue,service,router);
            sequence.enabled=false; sequence.Initialize(sequence.CameraDirector,null,clock);
            travel=Slice("travel",0,1); guidance=Slice("guidance",1,1); arrival=Slice("arrival",2,2);
            if(guidance.Cues.Count!=1)throw new ArgumentException("SUR-S05 precisa estar no plano de orientação.");
            // Reservar leitura de S05 enquanto se alinha, prepara e espera o sinal, sem espera extra após a queima.
            armAt=Mathf.Max(0,guidance.Cues[0].EndSeconds-HoldSeconds-SurveyorCommandDelay.OneWayDelaySeconds);
            Bind(); if (owner.CurrentStepId==StepId) Enter();
        }
        SequenceDefinition Slice(string id,int first,int count)
        {
            var shots=new List<ShotDefinition>(); var cues=new List<SubtitleCue>(); float offset=0,duration=0;
            for(int i=0;i<first;i++) offset+=source.Shots[i].DurationSeconds;
            for(int i=first;i<first+count;i++){shots.Add(source.Shots[i]);duration+=source.Shots[i].DurationSeconds;}
            foreach(var cue in source.Cues)
                if(!cue.IsTriggered && cue.StartSeconds>=offset && cue.EndSeconds<=offset+duration+.001f)
                    cues.Add(new SubtitleCue(cue.CueId,cue.TextKey,cue.StartSeconds-offset,cue.EndSeconds-offset,cue.Position,cue.Priority));
            var data=SequenceDefinition.Create("surveyor.cruise.runtime."+id,shots,cues);
            data.hideFlags=HideFlags.DontSave; return data;
        }
        void Enter()
        {
            Stop(); running=true; offered=false; widened=false; desired=InitialAim; committed=InitialAim; beforeBurn=InitialAim;
            lastPreparation=0; int token=generation;
            entered=cue=>{if(token!=generation)return;subtitleKey=cue.TextKey;CueEntered?.Invoke(cue);Changed?.Invoke();};
            exited=cue=>{if(token!=generation)return;if(subtitleKey==cue.TextKey)subtitleKey=null;CueExited?.Invoke(cue);Changed?.Invoke();};
            marked=id=>{if(token==generation && id==HandoffEvent){Phase=MidcoursePhase.Handoff;Changed?.Invoke();}};
            finished=result=>{if(token==generation)OnFinished(result);};
            sequence.CueEntered+=entered; sequence.CueExited+=exited; sequence.EventRaised+=marked; sequence.Finished+=finished;
            SetPhase(MidcoursePhase.Travel,InputContext.Cinematic); sequence.Play(travel,false);
        }
        void SetPhase(MidcoursePhase phase,InputContext context)
        {
            Phase=phase; phaseBeganActive=clock.ActiveSeconds;
            inputLease?.Dispose(); inputLease=router.Acquire(context,"SUR-005."+phase); Changed?.Invoke();
        }
        void OnFinished(SequenceResult result)
        {
            if(!running || result.Reason!=SequenceExitReason.Completed)return;
            if(Phase==MidcoursePhase.Travel)
            {
                lastProgressActive=clock.ActiveSeconds; queue.BeginState(StepId);
                commandGeneration=queue.Generation; ownsCommandScope=true;
                SetPhase(MidcoursePhase.Guidance,InputContext.Gameplay); sequence.Play(guidance,false);
            }
            else if(Phase==MidcoursePhase.Handoff) owner.CompleteStep(StepId,NextStepId);
        }
        void Update() => Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(!running || IsPaused || !(activeDeltaSeconds>0))return;
            activeDeltaSeconds=Mathf.Min(activeDeltaSeconds,Mathf.Max(0,clock.ActiveSeconds-phaseBeganActive));
            if(Phase==MidcoursePhase.Guidance)
            {
                Vector2 direction=router.Move;
                if(direction.sqrMagnitude>0) SetDesired(desired+new Vector2(direction.x,-direction.y)*activeDeltaSeconds*.24f);
                if(!offered && sequence.TimeSeconds>=armAt) Offer();
                if(IdleSeconds>=15) widened=true;
                action.SetAvailable(Aligned && sequence.TimeSeconds>=armAt);
                float currentPreparation=action.PreparationProgress;
                if(currentPreparation>lastPreparation+.0001f)lastProgressActive=clock.ActiveSeconds;
                lastPreparation=currentPreparation;
                // Maintém câmera e UI em espera, sem disparar término ou concluir o objetivo sozinho.
                activeDeltaSeconds=Mathf.Min(activeDeltaSeconds,Mathf.Max(0,guidance.TotalDurationSeconds-.001f-sequence.TimeSeconds));
            }
            sequence.Tick(activeDeltaSeconds); Changed?.Invoke();
        }
        void Offer()
        {
            int token=generation;
            offered=action.Offer(new InteractionCandidate { Id="midcourse.correct",VerbKey="mid.send",ObjectKey="mid.destination",
                HoldSeconds=HoldSeconds,Available=Aligned,Priority=100 },()=>
            {
                Vector2 accepted=desired; committed=accepted;
                return ()=>
                {
                    if(token!=generation || !running || Phase!=MidcoursePhase.Guidance)return;
                    beforeBurn=InitialAim; SetPhase(MidcoursePhase.Burn,InputContext.Cinematic); sequence.Play(arrival,false);
                };
            });
        }
        public void SetDesired(Vector2 position)
        {
            if(!CanAim || float.IsNaN(position.x) || float.IsNaN(position.y))return;
            position=new Vector2(Mathf.Clamp(position.x,.06f,.94f),Mathf.Clamp(position.y,.03f,MapAspect-.03f));
            float previous=Vector2.Distance(desired,TargetCenter); desired=position;
            if(Vector2.Distance(desired,TargetCenter)<previous-.001f) lastProgressActive=clock.ActiveSeconds;
            action.SetAvailable(Aligned && sequence.TimeSeconds>=armAt); Changed?.Invoke();
        }
        public void Assist() { if(CanAim) SetDesired(TargetCenter); }
        void OnStep(string id) { if(id==StepId)Enter(); else Stop(); }
        void OnUnloading(string id)=>Stop();
        void OnPaused(bool value)=>Changed?.Invoke();
        void Bind()
        {
            if(bound || owner==null)return;bound=true;
            owner.StepEntered+=OnStep;owner.ChapterUnloading+=OnUnloading;owner.PausedChanged+=OnPaused;router.HelpDown+=Assist;
        }
        public void Stop()
        {
            generation++;running=false;
            if(sequence!=null)
            {
                sequence.CueEntered-=entered;sequence.CueExited-=exited;sequence.EventRaised-=marked;sequence.Finished-=finished;
                if(sequence.Current==travel || sequence.Current==guidance || sequence.Current==arrival)sequence.Cancel();
            }
            if(ownsCommandScope && queue!=null && queue.Generation==commandGeneration)
            { if(action!=null)action.ClearAction(); queue.EndState(); }
            ownsCommandScope=false;
            inputLease?.Dispose();inputLease=null;subtitleKey=null;Phase=MidcoursePhase.Inactive;Changed?.Invoke();
        }
        void Detach()
        {
            Stop();
            if(bound){bound=false;owner.StepEntered-=OnStep;owner.ChapterUnloading-=OnUnloading;owner.PausedChanged-=OnPaused;router.HelpDown-=Assist;}
            DestroySequence(travel);DestroySequence(guidance);DestroySequence(arrival);travel=guidance=arrival=null;
        }
        static void DestroySequence(SequenceDefinition data)
        { if(data!=null){if(Application.isPlaying)Destroy(data);else DestroyImmediate(data);} }
        public void Dispose()=>Detach();
        void OnEnable()=>Bind();
        void OnDisable()=>Dispose();
    }
}
