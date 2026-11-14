using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum ShieldSurveyPhase
    {
        Idle = 0,
        Approaching = 1,
        Photographing = 2,
        Done = 3,
    }

    /// <summary>Trabalho de E com pausa: o que já foi feito fica quando se solta a tecla. Usado pelas atividades de M16–M19.</summary>
    internal sealed class HeldWork
    {
        float total;
        float banked;
        float current = 1f;

        public float Work { get; private set; }
        public float Total => total;
        public float Remaining => Mathf.Max(0.01f, total - banked);
        public float Fraction => total > 0f ? Mathf.Clamp01(Work / total) : 0f;

        public void Begin(float totalSeconds)
        {
            total = totalSeconds;
            banked = 0f;
            Work = 0f;
            current = Remaining;
        }

        /// <summary>Começou um hold. <paramref name="candidateHoldSeconds"/> é a duração realmente pedida ao serviço, quando conhecida.</summary>
        public void Started(float? candidateHoldSeconds = null) => current = candidateHoldSeconds.HasValue ? Mathf.Max(0.01f, candidateHoldSeconds.Value) : Remaining;

        public void Progress(float progress01) => Work = Mathf.Min(total, banked + progress01 * current);

        // Soltar E pausa: o próximo hold pede só o restante.
        public void Canceled() => banked = Work;

        public void Reset()
        {
            total = 0f;
            banked = 0f;
            Work = 0f;
        }
    }

    /// <summary>
    /// MAR-006 · M16–M17: o escudo descartado. M16: chegar à estação do campo envia <c>mars.shield.reached</c> e para o rover
    /// enquanto a fala é lida. M17: <b>segurar E por 4 s</b> fotografa o escudo, que já está enquadrado (a câmera sugerida é a
    /// âncora do campo; sem mira). Registra a foto do jogador e a imagem REAL de arquivo (PIA07402, NASA/JPL/Cornell) com
    /// crédito, que nunca é apresentada como do jogador, e envia <c>mars.shield.photographed</c> uma só vez.
    /// <para>
    /// O escudo é hardware descartado: explica o pouso. Não é a Heat Shield Rock (ver <see cref="MeteoriteActivity"/>): verbo,
    /// objeto, evidência e imagem são outros, e um resultado nunca conclui o outro. Soltar E, pausar ou perder foco conserva o
    /// trabalho. Parâmetros em <see cref="HeatShieldResultsData"/>; textos PT/EN em <c>MarsShieldTexts.json</c>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShieldSurveyActivity : MonoBehaviour
    {
        public const string ReachStepId = "M16";
        public const string PhotoStepId = "M17";
        public const string PhotoCandidateId = "mars.shield.photograph";
        const int CandidatePriority = 10;

        [SerializeField] HeatShieldResultsData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField, Tooltip("Âncoras do campo (MAR-015). Ligadas na montagem; o prefab de interação não as carrega.")] ShieldFieldAnchors anchors;
        [SerializeField] Transform operatorTransform;

        readonly HashSet<string> raised = new HashSet<string>();
        readonly HeldWork work = new HeldWork();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        IEvidenceStore evidence;
        bool bound;
        bool holding;
        bool engaged;
        bool candidateRegistered;
        bool lastAvailable;
        float lastHold = -1f;

        public ShieldSurveyPhase Phase { get; private set; }
        public bool IsHolding => holding;
        public bool IsEngaged => engaged;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public bool Photographed { get; private set; }
        public HeatShieldResultsData Data => data;

        /// <summary>Pose sugerida da câmera durante a foto (âncora do campo); nula fora de M17.</summary>
        public Transform CurrentView { get; private set; }

        public event Action<ShieldSurveyPhase> PhaseChanged;
        public event Action<bool> EngagedChanged;
        public event Action<Transform> ViewChanged;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            ShieldFieldAnchors fieldAnchors, Transform operatorPosition, IEvidenceStore evidenceStore = null)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            anchors = fieldAnchors;
            operatorTransform = operatorPosition;
            evidence = evidenceStore;
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
            SetEngaged(false);
        }

        // ---------- Etapas ----------

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            // Reinício do capítulo: voltou para uma etapa ANTES desta atividade. Nenhum estado residual pode sobrar.
            if (Phase != ShieldSurveyPhase.Idle && binding.StepId != ReachStepId && IsBeforeReach(binding.StepId))
            {
                ResetActivity();
                return;
            }

            if (binding.StepId == ReachStepId)
            {
                ResetActivity();
                SetPhase(ShieldSurveyPhase.Approaching);
            }
            else if (binding.StepId == PhotoStepId)
            {
                if (Phase != ShieldSurveyPhase.Approaching)
                    ResetActivity(); // entrou em M17 sem passar por M16 (reinício, teste)

                work.Begin(data.shieldPhotoHoldSeconds);
                SetView(anchors != null ? anchors.ShieldCameraAnchor : null);
                SetPhase(ShieldSurveyPhase.Photographing);
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != ReachStepId && stepId != PhotoStepId)
                return;

            ClearCandidate();
            holding = false;
            var completed = stepId == ReachStepId ? raised.Contains(MarsChapterFlow.Events.ShieldReached) : Phase == ShieldSurveyPhase.Done;
            if (!completed)
                ResetActivity();
            else if (stepId == PhotoStepId)
            {
                SetEngaged(false);
                SetView(null);
            }
        }

        /// <summary>Zera o trabalho e os eventos já enviados (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            work.Reset();
            Photographed = false;
            SetEngaged(false);
            SetView(null);
            SetPhase(ShieldSurveyPhase.Idle);
        }

        bool IsBeforeReach(string stepId)
        {
            if (flow == null)
                return false;

            var steps = flow.Steps;
            var current = -1;
            var reach = -1;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepId == stepId)
                    current = i;
                else if (steps[i].StepId == ReachStepId)
                    reach = i;
            }

            return current >= 0 && reach >= 0 && current < reach;
        }

        // ---------- Quadro ----------

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO. Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (data == null)
                return;

            if (Phase == ShieldSurveyPhase.Approaching && operatorTransform != null && anchors != null && anchors.ShieldStation != null
                && Horizontal(anchors.ShieldStation.position) <= data.reachMeters)
            {
                // A fala sobre o escudo ocorre com o rover parado: o fluxo segura a etapa até ela terminar.
                SetEngaged(true);
                Raise(MarsChapterFlow.Events.ShieldReached);
            }

            RefreshCandidate();
        }

        float Horizontal(Vector3 world)
        {
            var delta = world - operatorTransform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        void RefreshCandidate()
        {
            if (interaction == null || data == null)
                return;

            if (Phase != ShieldSurveyPhase.Photographing)
            {
                ClearCandidate();
                return;
            }

            if (holding)
                return;

            var distance = 0f;
            var inRange = true;
            if (operatorTransform != null && anchors != null && anchors.ShieldPhotoTarget != null)
            {
                distance = Horizontal(anchors.ShieldPhotoTarget.position);
                inRange = Horizontal(anchors.ShieldStation.position) <= data.promptMeters;
            }

            var hold = work.Remaining;
            if (candidateRegistered && lastAvailable == inRange && Mathf.Approximately(lastHold, hold))
                return;

            candidateRegistered = true;
            lastAvailable = inRange;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = PhotoCandidateId, Anchor = anchors != null ? anchors.ShieldPhotoTarget : null, VerbKey = data.shieldVerbKey,
                ObjectKey = data.shieldObjectKey, Available = inRange, DistanceMeters = distance, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            lastHold = -1f;
            if (interaction != null)
                interaction.ClearCandidate(PhotoCandidateId);
        }

        // ---------- Eventos do InteractionService ----------

        void OnHoldStarted(string id)
        {
            if (id == PhotoCandidateId && Phase == ShieldSurveyPhase.Photographing)
            {
                holding = true;
                work.Started();
            }
        }

        void OnHoldProgress(string id, float progress)
        {
            if (holding && id == PhotoCandidateId && Phase == ShieldSurveyPhase.Photographing)
                work.Progress(progress);
        }

        void OnHoldCanceled(string id)
        {
            if (id != PhotoCandidateId)
                return;

            holding = false;
            work.Canceled();
        }

        void OnHoldCompleted(string id)
        {
            if (id != PhotoCandidateId || Phase != ShieldSurveyPhase.Photographing)
                return;

            holding = false;
            ClearCandidate();
            work.Progress(1f);
            Photographed = true;
            SetPhase(ShieldSurveyPhase.Done);
            TryAdd(data.ShieldPhoto());
            TryAdd(data.ShieldArchive()); // imagem real creditada, junto do resultado
            Raise(MarsChapterFlow.Events.ShieldPhotographed);
        }

        // O EvidenceStore recusa (lança) acima de 3 fotos do jogador: a etapa nunca pode travar por isso.
        void TryAdd(EvidenceRecord record)
        {
            if (evidence == null)
                return;

            try
            {
                evidence.Add(record);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogWarning($"[MAR-006] {record.EvidenceId} não foi guardada: {e.Message}");
            }
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        void SetPhase(ShieldSurveyPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        void SetEngaged(bool value)
        {
            if (engaged == value)
                return;

            engaged = value;
            EngagedChanged?.Invoke(value);
        }

        void SetView(Transform view)
        {
            if (CurrentView == view)
                return;

            CurrentView = view;
            ViewChanged?.Invoke(view);
        }

        /// <summary>Dados e referências (as âncoras são ligadas na montagem). Vazio = pronto.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(ShieldSurveyActivity);
            if (data == null)
                ContractValidation.Report(errors, owner, "falta HeatShieldResultsData.");
            else
                errors.AddRange(data.Validate());

            return errors;
        }

        /// <summary>Validação completa, com as âncoras do campo ligadas.</summary>
        public List<string> ValidateWiring()
        {
            var errors = ValidateSetup();
            if (anchors == null)
                ContractValidation.Report(errors, nameof(ShieldSurveyActivity), "faltam as âncoras do campo (ShieldFieldAnchors).");
            else
                errors.AddRange(anchors.ValidateSetup());

            return errors;
        }
    }
}
