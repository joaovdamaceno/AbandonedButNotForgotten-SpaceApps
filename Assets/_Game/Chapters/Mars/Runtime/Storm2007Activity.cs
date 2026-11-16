using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum StormPhase
    {
        Idle = 0,
        Dimming = 1,
        Conserving = 2,
        Weeks = 3,
        Clearing = 4,
        Done = 5,
    }

    /// <summary>Grupos que o jogador desliga para conservar energia (rodas, braço e instrumentos).</summary>
    public enum StormGroup
    {
        Wheels = 0,
        Arm = 1,
        Instruments = 2,
    }

    /// <summary>Estado do medidor GERAÇÃO SOLAR · Wh/sol: só os marcos publicados têm número.</summary>
    public enum StormMeterState
    {
        Before = 0,
        Low = 1,
        Recovering = 2,
    }

    /// <summary>
    /// MAR-007 · M21: a tempestade de 2007, para conservar e voltar (25 s de referência: 6 + 8 + 6 + 5).
    /// <para>
    /// 0–6 s o horizonte apaga (<see cref="DustLevel"/> sobe); 6–14 s <b>segurar E por 8 s</b> desliga rodas, braço e
    /// instrumentos, nessa ordem, e só ao completar chega <c>mars.storm.conserved</c>; 14–20 s é a elipse de semanas, com
    /// comunicações reduzidas (não rádio morto para sempre); 20–25 s o céu clareia pela passagem do tempo, a máquina
    /// retoma e chega <c>mars.storm.skyCleared</c>. O jogador conserva, não afasta a tempestade: sem E nada se conclui e
    /// nada pune. Não há tornado, peça arrancada nem derrota.
    /// </para>
    /// <para>
    /// Ciência (MS17–MS19): o medidor só mostra os marcos publicados, mais de 700 Wh/sol antes e 128 Wh/sol em julho de 2007,
    /// e uma retomada SEM número; o ponteiro é esquemático. Geração por sol não é bateria, potência nem curva completa.
    /// Soltar E, pausar ou perder foco conserva o trabalho; todo o tempo é ativo (a pausa congela a elipse também).
    /// Quem faz a arte lê <see cref="DustLevel"/>; quem monta liga <see cref="GroupChanged"/> ao rover e aos instrumentos.
    /// Dados em <see cref="Storm2007Data"/>; textos PT/EN em <c>MarsStormTexts.json</c>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Storm2007Activity : MonoBehaviour
    {
        public const string StepId = "M21";
        public const string ConserveCandidateId = "mars.storm.conserve";
        const int CandidatePriority = 10;

        [SerializeField] Storm2007Data data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;

        readonly HashSet<string> raised = new HashSet<string>();
        readonly HeldWork work = new HeldWork();
        readonly bool[] groupEnabled = { true, true, true };
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        bool bound;
        bool holding;
        bool candidateRegistered;
        float lastHold = -1f;
        float phaseElapsed;

        public StormPhase Phase { get; private set; }

        /// <summary>0 = céu limpo, 1 = escuro total. Só visual; não é um dado científico.</summary>
        public float DustLevel { get; private set; }

        public float SkyClearness => 1f - DustLevel;

        /// <summary>Posição esquemática do ponteiro (0–1). Não representa valores intermediários.</summary>
        public float MeterNeedle { get; private set; } = 1f;

        public StormMeterState MeterState { get; private set; }

        /// <summary>Chave do texto do medidor: só os marcos publicados e a retomada sem número.</summary>
        public string MeterLabelKey => MeterState == StormMeterState.Before ? data.meterBeforeKey
            : MeterState == StormMeterState.Low ? data.meterLowKey : data.meterRecoveringKey;

        /// <summary>O valor publicado em Wh/sol quando o estado tem número; nulo na retomada (nenhum número intermediário).</summary>
        public int? PublishedWhPerSol => MeterState == StormMeterState.Before ? data.beforeWhPerSol
            : MeterState == StormMeterState.Low ? data.lowWhPerSol : (int?)null;

        public bool CommsReduced { get; private set; }
        public bool IsHolding => holding;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public bool Conserved { get; private set; }
        public Storm2007Data Data => data;

        public bool IsGroupEnabled(StormGroup group) => groupEnabled[(int)group];

        public event Action<StormPhase> PhaseChanged;
        public event Action<StormGroup, bool> GroupChanged;
        public event Action<float> DustLevelChanged;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            if (isActiveAndEnabled)
                Bind();
        }

        void OnEnable()
        {
            if (interaction == null && interactionService != null)
                interaction = interactionService;

            Bind();
        }

        void OnDisable() => Unbind();

        void Bind()
        {
            if (bound)
                return;

            bound = true;
            if (interaction != null)
            {
                interaction.HoldStarted += OnHoldStarted;
                interaction.HoldProgress += OnHoldProgress;
                interaction.HoldCompleted += OnHoldCompleted;
                interaction.HoldCanceled += OnHoldCanceled;
            }

            if (flow != null)
            {
                flow.StepStarted += OnStepStarted;
                flow.StepEnded += OnStepEnded;
                if (flow.CurrentBinding != null)
                    OnStepStarted(flow.CurrentBinding);
            }
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            if (interaction != null)
            {
                interaction.HoldStarted -= OnHoldStarted;
                interaction.HoldProgress -= OnHoldProgress;
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.HoldCanceled -= OnHoldCanceled;
            }

            if (flow != null)
            {
                flow.StepStarted -= OnStepStarted;
                flow.StepEnded -= OnStepEnded;
            }

            ClearCandidate();
            holding = false;
            RestoreAll();
        }

        // ---------- Etapa ----------

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            if (binding.StepId == StepId)
            {
                ResetActivity();
                work.Begin(data.conserveHoldSeconds);
                SetPhase(data.dimSeconds > 0f ? StormPhase.Dimming : StormPhase.Conserving);
            }
            else if (Phase != StormPhase.Idle && IsBeforeStorm(binding.StepId))
            {
                ResetActivity(); // reinício do capítulo: nenhuma poeira nem grupo desligado pode sobrar
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != StepId)
                return;

            ClearCandidate();
            holding = false;
            if (Phase != StormPhase.Done)
                ResetActivity();
        }

        bool IsBeforeStorm(string stepId)
        {
            if (flow == null)
                return false;

            var steps = flow.Steps;
            var current = -1;
            var storm = -1;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepId == stepId)
                    current = i;
                else if (steps[i].StepId == StepId)
                    storm = i;
            }

            return current >= 0 && storm >= 0 && current < storm;
        }

        /// <summary>Volta ao céu limpo, grupos ligados e eventos zerados (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            work.Reset();
            phaseElapsed = 0f;
            Conserved = false;
            CommsReduced = false;
            MeterState = StormMeterState.Before;
            MeterNeedle = data != null ? data.needleBefore : 1f;
            RestoreAll();
            SetDust(0f);
            SetPhase(StormPhase.Idle);
        }

        void RestoreAll()
        {
            for (var i = 0; i < groupEnabled.Length; i++)
                SetGroup((StormGroup)i, true);
        }

        // ---------- Quadro ----------

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa, inclusive na elipse). Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (data == null)
                return;

            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            switch (Phase)
            {
                case StormPhase.Dimming:
                    phaseElapsed += dt;
                    SetDust(data.dustPeak * Smooth(phaseElapsed / Mathf.Max(0.01f, data.dimSeconds)));
                    if (phaseElapsed >= data.dimSeconds - 1e-3f)
                        SetPhase(StormPhase.Conserving);

                    break;

                case StormPhase.Conserving:
                    // A tempestade não piora além do pico e não pune a espera: sem E, nada se conclui.
                    SetDust(data.dustPeak);
                    break;

                case StormPhase.Weeks:
                    phaseElapsed += dt;
                    var t = Smooth(phaseElapsed / Mathf.Max(0.01f, data.weeksSeconds));
                    MeterNeedle = Mathf.Lerp(data.needleBefore, data.needleLow, t);
                    if (phaseElapsed >= data.weeksSeconds * 0.5f)
                        MeterState = StormMeterState.Low; // chegou a julho de 2007: o marco publicado de 128 Wh/sol

                    if (phaseElapsed >= data.weeksSeconds - 1e-3f)
                    {
                        MeterNeedle = data.needleLow;
                        phaseElapsed = 0f; // o clareamento tem o seu próprio relógio de 5 s
                        SetPhase(StormPhase.Clearing);
                    }

                    break;

                case StormPhase.Clearing:
                    phaseElapsed += dt;
                    var c = Smooth(phaseElapsed / Mathf.Max(0.01f, data.clearSeconds));
                    SetDust(Mathf.Lerp(data.dustPeak, data.dustAfterClearing, c));
                    MeterNeedle = Mathf.Lerp(data.needleLow, data.needleRecovered, c);
                    MeterState = StormMeterState.Recovering; // retomada cautelosa: sem número
                    if (phaseElapsed >= data.clearSeconds - 1e-3f)
                    {
                        SetDust(data.dustAfterClearing);
                        MeterNeedle = data.needleRecovered;
                        RestoreAll(); // a máquina retoma: rodas, braço e instrumentos voltam
                        CommsReduced = false;
                        SetPhase(StormPhase.Done);
                        Raise(MarsChapterFlow.Events.StormSkyCleared);
                    }

                    break;
            }

            RefreshCandidate();
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---------- Candidato de E ----------

        void RefreshCandidate()
        {
            if (interaction == null || data == null)
                return;

            if (Phase != StormPhase.Conserving)
            {
                ClearCandidate();
                return;
            }

            if (holding)
                return;

            var hold = work.Remaining;
            if (candidateRegistered && Mathf.Approximately(lastHold, hold))
                return;

            candidateRegistered = true;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = ConserveCandidateId, Anchor = null, VerbKey = data.conserveVerbKey, ObjectKey = data.conserveObjectKey,
                Available = true, DistanceMeters = 0f, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            lastHold = -1f;
            if (interaction != null)
                interaction.ClearCandidate(ConserveCandidateId);
        }

        // ---------- Eventos do InteractionService ----------

        void OnHoldStarted(string id)
        {
            if (id == ConserveCandidateId && Phase == StormPhase.Conserving)
            {
                holding = true;
                // A duração real do hold é a do candidato ativo (pode ter sido registrada antes de um cancelamento no mesmo quadro).
                work.Started(interaction.TryGetActive(out var active) && active.Id == id ? active.HoldSeconds : (float?)null);
            }
        }

        void OnHoldProgress(string id, float progress)
        {
            if (!holding || id != ConserveCandidateId || Phase != StormPhase.Conserving)
                return;

            work.Progress(progress);
            var fraction = work.Fraction;
            if (fraction >= data.wheelsOffAt) SetGroup(StormGroup.Wheels, false);
            if (fraction >= data.armOffAt) SetGroup(StormGroup.Arm, false);
            if (fraction >= data.instrumentsOffAt) SetGroup(StormGroup.Instruments, false);
        }

        void OnHoldCanceled(string id)
        {
            if (id != ConserveCandidateId)
                return;

            // Soltar E pausa: o que já foi desligado fica e o próximo hold pede só o restante. O candidato é atualizado
            // já, para um novo E no mesmo quadro não usar a duração antiga.
            holding = false;
            work.Canceled();
            RefreshCandidate();
        }

        void OnHoldCompleted(string id)
        {
            if (id != ConserveCandidateId || Phase != StormPhase.Conserving)
                return;

            holding = false;
            ClearCandidate();
            work.Progress(1f);
            SetGroup(StormGroup.Wheels, false);
            SetGroup(StormGroup.Arm, false);
            SetGroup(StormGroup.Instruments, false);
            Conserved = true;
            CommsReduced = true;
            phaseElapsed = 0f;
            SetPhase(StormPhase.Weeks);
            Raise(MarsChapterFlow.Events.StormConserved); // abre a legenda "atividades suspensas… a espera durou semanas"
        }

        // ---------- Utilitários ----------

        void SetGroup(StormGroup group, bool enabled)
        {
            if (groupEnabled[(int)group] == enabled)
                return;

            groupEnabled[(int)group] = enabled;
            GroupChanged?.Invoke(group, enabled);
        }

        void SetDust(float value)
        {
            if (Mathf.Approximately(DustLevel, value))
                return;

            DustLevel = value;
            DustLevelChanged?.Invoke(value);
        }

        void SetPhase(StormPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        /// <summary>Dados e referências. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            if (data == null)
                ContractValidation.Report(errors, nameof(Storm2007Activity), "falta Storm2007Data.");
            else
                errors.AddRange(data.Validate());

            return errors;
        }
    }
}
