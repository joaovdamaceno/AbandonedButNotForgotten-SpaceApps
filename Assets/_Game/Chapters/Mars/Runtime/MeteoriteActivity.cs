using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum MeteoritePhase
    {
        Idle = 0,
        Approaching = 1,
        Measuring = 2,
        Result = 3,
        Done = 4,
    }

    /// <summary>
    /// MAR-006 · M18–M19: a Heat Shield Rock, uma rocha espacial independente do escudo. M18: chegar à estação envia
    /// <c>mars.meteorite.reached</c> e para o rover durante a fala. M19: <b>segurar E por 6 s</b> mede (APXS e Mössbauer:
    /// ferro e níquel, qualitativo, sem números inventados); o resultado fica <b>4 s</b> na tela com o registro visual e a imagem
    /// REAL de arquivo creditada, e só então chega <c>mars.meteorite.photographed</c> (a foto é o registro do que o jogador
    /// mediu: sem mira escondida nem segundo E). Ordem dos eventos: medido → fotografado, uma vez cada.
    /// <para>
    /// A RAT não se aplica ao metal: <see cref="IsRatBlockedAt"/> e <see cref="RatBlocked"/> dizem onde ela é vetada (a RAT só
    /// oferece E em M11–M12 e nunca num meteorito, e este componente nunca registra candidato de RAT). O meteorito não é
    /// explicado como pedaço do escudo: textos e evidências próprios (MS14). Soltar E, pausar ou perder foco conserva o trabalho.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeteoriteActivity : MonoBehaviour
    {
        public const string ReachStepId = "M18";
        public const string MeasureStepId = "M19";
        public const string MeasureCandidateId = "mars.meteorite.measure";
        const int CandidatePriority = 10;

        [SerializeField] HeatShieldResultsData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField] ShieldFieldAnchors anchors;
        [SerializeField] Transform operatorTransform;
        [SerializeField, Tooltip("Raio, em metros, em volta do meteorito onde a RAT é vetada.")] float ratBlockRadiusMeters = 1.5f;

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
        float resultElapsed;

        public MeteoritePhase Phase { get; private set; }
        public bool IsHolding => holding;
        public bool IsEngaged => engaged;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public bool Measured { get; private set; }
        public bool Photographed { get; private set; }
        public HeatShieldResultsData Data => data;

        /// <summary>A RAT é vetada no metal durante toda a sequência do meteorito (M18–M19).</summary>
        public bool RatBlocked => Phase != MeteoritePhase.Idle && Phase != MeteoritePhase.Done;

        public Transform CurrentView { get; private set; }

        public event Action<MeteoritePhase> PhaseChanged;
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

        /// <summary>O ponto (por exemplo, o alvo de uma ferramenta) está sobre o meteorito? A RAT nunca opera aí.</summary>
        public bool IsRatBlockedAt(Vector3 worldPoint)
        {
            if (anchors == null || anchors.MeteoriteMeasurePoint == null)
                return false;

            var delta = worldPoint - anchors.MeteoriteMeasurePoint.position;
            delta.y = 0f;
            return delta.magnitude <= ratBlockRadiusMeters;
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
            if (Phase != MeteoritePhase.Idle && binding.StepId != ReachStepId && IsBeforeReach(binding.StepId))
            {
                ResetActivity();
                return;
            }

            if (binding.StepId == ReachStepId)
            {
                ResetActivity();
                SetPhase(MeteoritePhase.Approaching);
            }
            else if (binding.StepId == MeasureStepId)
            {
                if (Phase != MeteoritePhase.Approaching)
                    ResetActivity();

                work.Begin(data.meteoriteMeasureHoldSeconds);
                SetView(anchors != null ? anchors.MeteoriteCameraAnchor : null);
                SetPhase(MeteoritePhase.Measuring);
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != ReachStepId && stepId != MeasureStepId)
                return;

            ClearCandidate();
            holding = false;
            var completed = stepId == ReachStepId ? raised.Contains(MarsChapterFlow.Events.MeteoriteReached) : Phase == MeteoritePhase.Done;
            if (!completed)
                ResetActivity();
            else if (stepId == MeasureStepId)
            {
                SetEngaged(false);
                SetView(null);
            }
        }

        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            work.Reset();
            resultElapsed = 0f;
            Measured = Photographed = false;
            SetEngaged(false);
            SetView(null);
            SetPhase(MeteoritePhase.Idle);
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

        public void Tick(float activeDeltaSeconds)
        {
            if (data == null)
                return;

            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            if (Phase == MeteoritePhase.Approaching && operatorTransform != null && anchors != null && anchors.MeteoriteStation != null
                && Horizontal(anchors.MeteoriteStation.position) <= data.reachMeters)
            {
                SetEngaged(true);
                Raise(MarsChapterFlow.Events.MeteoriteReached);
            }
            else if (Phase == MeteoritePhase.Result)
            {
                resultElapsed += dt;
                if (resultElapsed >= data.meteoriteResultSeconds - 1e-3f)
                {
                    Photographed = true;
                    SetPhase(MeteoritePhase.Done);
                    TryAdd(data.MeteoritePhoto());
                    TryAdd(data.MeteoriteArchive()); // imagem real creditada, junto do registro visual
                    Raise(MarsChapterFlow.Events.MeteoritePhotographed);
                }
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

            if (Phase != MeteoritePhase.Measuring)
            {
                ClearCandidate();
                return;
            }

            if (holding)
                return;

            var distance = 0f;
            var inRange = true;
            if (operatorTransform != null && anchors != null && anchors.MeteoriteMeasurePoint != null)
            {
                distance = Horizontal(anchors.MeteoriteMeasurePoint.position);
                inRange = Horizontal(anchors.MeteoriteStation.position) <= data.promptMeters;
            }

            var hold = work.Remaining;
            if (candidateRegistered && lastAvailable == inRange && Mathf.Approximately(lastHold, hold))
                return;

            candidateRegistered = true;
            lastAvailable = inRange;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = MeasureCandidateId, Anchor = anchors != null ? anchors.MeteoriteMeasurePoint : null, VerbKey = data.meteoriteVerbKey,
                ObjectKey = data.meteoriteObjectKey, Available = inRange, DistanceMeters = distance, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            lastHold = -1f;
            if (interaction != null)
                interaction.ClearCandidate(MeasureCandidateId);
        }

        // ---------- Eventos do InteractionService ----------

        void OnHoldStarted(string id)
        {
            if (id == MeasureCandidateId && Phase == MeteoritePhase.Measuring)
            {
                holding = true;
                work.Started();
            }
        }

        void OnHoldProgress(string id, float progress)
        {
            if (holding && id == MeasureCandidateId && Phase == MeteoritePhase.Measuring)
                work.Progress(progress);
        }

        void OnHoldCanceled(string id)
        {
            if (id != MeasureCandidateId)
                return;

            holding = false;
            work.Canceled();
        }

        void OnHoldCompleted(string id)
        {
            if (id != MeasureCandidateId || Phase != MeteoritePhase.Measuring)
                return;

            holding = false;
            ClearCandidate();
            work.Progress(1f);
            Measured = true;
            resultElapsed = 0f;
            SetPhase(MeteoritePhase.Result);
            TryAdd(data.MeteoriteComposition());
            Raise(MarsChapterFlow.Events.MeteoriteMeasured); // abre a legenda do resultado (4 s de leitura mínima)
        }

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

        void SetPhase(MeteoritePhase phase)
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

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MeteoriteActivity);
            if (data == null)
                ContractValidation.Report(errors, owner, "falta HeatShieldResultsData.");
            else
                errors.AddRange(data.Validate());

            ContractValidation.RequirePositive(errors, owner, nameof(ratBlockRadiusMeters), ratBlockRadiusMeters);
            return errors;
        }

        public List<string> ValidateWiring()
        {
            var errors = ValidateSetup();
            if (anchors == null)
                ContractValidation.Report(errors, nameof(MeteoriteActivity), "faltam as âncoras do campo (ShieldFieldAnchors).");
            else
                errors.AddRange(anchors.ValidateSetup());

            return errors;
        }
    }
}
