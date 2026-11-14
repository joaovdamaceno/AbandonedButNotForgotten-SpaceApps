using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum CompositionPhase
    {
        Idle = 0,
        BlockedByRat = 1,
        ApxsApproach = 2,
        ApxsMeasure = 3,
        ApxsRetract = 4,
        RotateToMossbauer = 5,
        MossbauerApproach = 6,
        MossbauerMeasure = 7,
        MossbauerRetract = 8,
        RotateToStow = 9,
        Result = 10,
        Done = 11,
    }

    /// <summary>
    /// MAR-005 · M13: APXS e Mössbauer medem a MESMA cavidade que a RAT abriu. APXS (6 s: aproximar, <b>4 s de E</b>, recuar,
    /// girar) informa o ELEMENTO (enxofre abundante) e envia <c>mars.apxs.measured</c>; Mössbauer (6 s, igual) identifica o
    /// MINERAL (jarosita) e envia <c>mars.mossbauer.measured</c>; 6 s de leitura e recolhimento fecham com
    /// <c>mars.composition.done</c>. A RAT prepara, os espectrômetros medem: sem a RAT concluída (cavidade no fim) a
    /// atividade fica bloqueada e nenhum E é oferecido.
    /// <para>
    /// A ferramenta só se move pela normal da cavidade, sobre o centro do disco (<see cref="RatCavitySurface"/>): nenhum
    /// ponto se espalha pela rocha. Antes de girar ela recua pelo menos <c>retractClearanceMeters</c> (nunca menos de 10 cm),
    /// e nunca fica abaixo da superfície, então não há interseção na entrada nem na saída.
    /// </para>
    /// <para>
    /// Resultados qualitativos, sem espectro numérico. Cada medição vira uma evidência de <c>Composition</c> distinta
    /// (elemento × mineral); juntas alimentam a descoberta "evidências de água ANTIGA", uma <c>Observation</c>: nunca água atual
    /// nem vida (MS10, MS11). Soltar E, pausar ou perder foco conserva o trabalho. Dados em
    /// <see cref="CompositionResultsData"/>; textos PT/EN em <c>MarsCompositionTexts.json</c>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsCompositionActivity : MonoBehaviour
    {
        public const string StepId = "M13";
        public const string ApxsCandidateId = "mars.composition.apxs";
        public const string MossbauerCandidateId = "mars.composition.mossbauer";
        const int CandidatePriority = 10;

        [SerializeField] CompositionResultsData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField, Tooltip("Cavidade da RAT: âncora única das medições. Ligada na montagem (MAR-009).")] RatCavitySurface cavity;

        [Header("Ferramenta (prefab)")]
        [SerializeField, Tooltip("Pivô na ponta que mede. Move-se só pela normal da cavidade.")] Transform tool;
        [SerializeField, Tooltip("Parte que gira com a ferramenta recuada.")] Transform turret;
        [SerializeField] GameObject apxsHead;
        [SerializeField] GameObject mossbauerHead;

        readonly HashSet<string> raised = new HashSet<string>();
        readonly HeldWork work = new HeldWork();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        IEvidenceStore evidence;
        bool bound;
        bool holding;
        bool engaged;
        bool candidateRegistered;
        string candidateId;
        float lastHold = -1f;
        float phaseElapsed;
        float turretDegrees;

        public CompositionPhase Phase { get; private set; }
        public bool IsHolding => holding;
        public bool IsEngaged => engaged;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public bool ApxsMeasured { get; private set; }
        public bool MossbauerMeasured { get; private set; }
        public CompositionResultsData Data => data;
        public Transform Tool => tool;

        /// <summary>Distância atual da ponta à cavidade, pela normal (metros). Nunca negativa.</summary>
        public float ToolOffsetMeters { get; private set; }

        /// <summary>Ferramenta ativa agora: APXS ou Mössbauer (a que está sob a ponta).</summary>
        public bool ApxsActive { get; private set; } = true;

        /// <summary>A RAT terminou: a cavidade chegou ao fim do progresso. Só então se mede.</summary>
        public bool RatCompleted => cavity != null && cavity.Progress01 >= 0.999f;

        public event Action<CompositionPhase> PhaseChanged;
        public event Action<bool> EngagedChanged;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            RatCavitySurface ratCavity, IEvidenceStore evidenceStore = null)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            cavity = ratCavity;
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

        // ---------- Etapa ----------

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            if (binding.StepId == StepId)
            {
                ResetActivity();
                SetEngaged(true); // o rover continua parado do começo da RAT ao fim das medições
                if (!RatCompleted)
                {
                    SetPhase(CompositionPhase.BlockedByRat); // não se mede antes de concluir a RAT
                    return;
                }

                BeginApproach(true);
            }
            else if (Phase != CompositionPhase.Idle && IsBeforeStep(binding.StepId))
            {
                ResetActivity(); // reinício do capítulo: nenhum estado residual
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != StepId)
                return;

            ClearCandidate();
            holding = false;
            if (Phase == CompositionPhase.Done)
                SetEngaged(false);
            else
                ResetActivity();
        }

        bool IsBeforeStep(string stepId)
        {
            if (flow == null)
                return false;

            var steps = flow.Steps;
            var current = -1;
            var self = -1;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepId == stepId)
                    current = i;
                else if (steps[i].StepId == StepId)
                    self = i;
            }

            return current >= 0 && self >= 0 && current < self;
        }

        /// <summary>Zera medições, ferramenta e eventos (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            work.Reset();
            phaseElapsed = 0f;
            ApxsMeasured = MossbauerMeasured = false;
            ApxsActive = true;
            turretDegrees = 0f;
            ToolOffsetMeters = data != null ? data.retractClearanceMeters : 0.12f;
            SetEngaged(false);
            SetPhase(CompositionPhase.Idle);
            ApplyPose();
        }

        void BeginApproach(bool apxs)
        {
            ApxsActive = apxs;
            phaseElapsed = 0f;
            ToolOffsetMeters = data.retractClearanceMeters;
            SetPhase(apxs ? CompositionPhase.ApxsApproach : CompositionPhase.MossbauerApproach);
            ApplyPose();
        }

        // ---------- Quadro ----------

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (data == null)
                return;

            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            switch (Phase)
            {
                case CompositionPhase.ApxsApproach:
                case CompositionPhase.MossbauerApproach:
                    phaseElapsed += dt;
                    ToolOffsetMeters = Mathf.Lerp(data.retractClearanceMeters, data.contactStandoffMeters, Smooth(phaseElapsed / data.approachSeconds));
                    if (phaseElapsed >= data.approachSeconds - 1e-3f)
                    {
                        ToolOffsetMeters = data.contactStandoffMeters;
                        work.Begin(ApxsActive ? data.apxsHoldSeconds : data.mossbauerHoldSeconds);
                        SetPhase(ApxsActive ? CompositionPhase.ApxsMeasure : CompositionPhase.MossbauerMeasure);
                    }

                    break;

                case CompositionPhase.ApxsRetract:
                case CompositionPhase.MossbauerRetract:
                    phaseElapsed += dt;
                    ToolOffsetMeters = Mathf.Lerp(data.contactStandoffMeters, data.retractClearanceMeters, Smooth(phaseElapsed / data.retractSeconds));
                    if (phaseElapsed >= data.retractSeconds - 1e-3f)
                    {
                        ToolOffsetMeters = data.retractClearanceMeters; // só agora a folga permite girar
                        phaseElapsed = 0f;
                        SetPhase(Phase == CompositionPhase.ApxsRetract ? CompositionPhase.RotateToMossbauer : CompositionPhase.RotateToStow);
                    }

                    break;

                case CompositionPhase.RotateToMossbauer:
                case CompositionPhase.RotateToStow:
                    phaseElapsed += dt;
                    var t = Smooth(phaseElapsed / data.rotateSeconds);
                    turretDegrees = Mathf.Lerp(0f, 180f, t) + (Phase == CompositionPhase.RotateToStow ? 180f : 0f);
                    if (t >= 0.5f)
                        ApxsActive = Phase == CompositionPhase.RotateToStow; // a troca de ferramenta ocorre com ela recuada

                    if (phaseElapsed >= data.rotateSeconds - 1e-3f)
                    {
                        if (Phase == CompositionPhase.RotateToMossbauer)
                        {
                            ApxsActive = false;
                            BeginApproach(false);
                        }
                        else
                        {
                            ApxsActive = true;
                            turretDegrees = 0f;
                            phaseElapsed = 0f;
                            SetPhase(CompositionPhase.Result);
                        }
                    }

                    break;

                case CompositionPhase.Result:
                    phaseElapsed += dt;
                    if (phaseElapsed >= data.finalSeconds - 1e-3f)
                    {
                        SetPhase(CompositionPhase.Done);
                        TryAdd(data.DiscoveryRecord());
                        Raise(MarsChapterFlow.Events.CompositionDone);
                    }

                    break;
            }

            ApplyPose();
            RefreshCandidate();
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // A ferramenta só anda pela normal, sobre o centro do disco: nenhum ponto se espalha pela rocha.
        void ApplyPose()
        {
            if (apxsHead != null)
                apxsHead.SetActive(ApxsActive);

            if (mossbauerHead != null)
                mossbauerHead.SetActive(!ApxsActive);

            if (tool == null || cavity == null)
                return;

            var center = cavity.transform.position;
            tool.SetPositionAndRotation(center + cavity.Normal * Mathf.Max(0f, ToolOffsetMeters), cavity.transform.rotation);
            if (turret != null)
                turret.localRotation = Quaternion.Euler(0f, turretDegrees, 0f);
        }

        // ---------- Candidato de E ----------

        void RefreshCandidate()
        {
            if (interaction == null || data == null)
                return;

            string wanted = null;
            string verb = null;
            if (Phase == CompositionPhase.ApxsMeasure)
            {
                wanted = ApxsCandidateId;
                verb = data.apxsVerbKey;
            }
            else if (Phase == CompositionPhase.MossbauerMeasure)
            {
                wanted = MossbauerCandidateId;
                verb = data.mossbauerVerbKey;
            }

            if (wanted == null || !RatCompleted)
            {
                ClearCandidate();
                return;
            }

            if (holding)
                return;

            var hold = work.Remaining;
            if (candidateRegistered && candidateId == wanted && Mathf.Approximately(lastHold, hold))
                return;

            if (candidateRegistered && candidateId != wanted)
                ClearCandidate();

            candidateRegistered = true;
            candidateId = wanted;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = wanted, Anchor = cavity != null ? cavity.transform : null, VerbKey = verb, ObjectKey = data.cavityObjectKey,
                Available = true, DistanceMeters = 0f, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            lastHold = -1f;
            if (interaction != null && candidateId != null)
                interaction.ClearCandidate(candidateId);

            candidateId = null;
        }

        // ---------- Eventos do InteractionService ----------

        bool IsMine(string id) =>
            (id == ApxsCandidateId && Phase == CompositionPhase.ApxsMeasure) || (id == MossbauerCandidateId && Phase == CompositionPhase.MossbauerMeasure);

        void OnHoldStarted(string id)
        {
            if (!IsMine(id))
                return;

            holding = true;
            work.Started(interaction.TryGetActive(out var active) && active.Id == id ? active.HoldSeconds : (float?)null);
        }

        void OnHoldProgress(string id, float progress)
        {
            if (holding && IsMine(id))
                work.Progress(progress);
        }

        void OnHoldCanceled(string id)
        {
            if (id != ApxsCandidateId && id != MossbauerCandidateId)
                return;

            holding = false;
            work.Canceled();
            RefreshCandidate(); // um novo E no mesmo quadro precisa da duração restante
        }

        void OnHoldCompleted(string id)
        {
            if (!IsMine(id))
                return;

            holding = false;
            ClearCandidate();
            work.Progress(1f);
            phaseElapsed = 0f;
            if (id == ApxsCandidateId)
            {
                ApxsMeasured = true;
                SetPhase(CompositionPhase.ApxsRetract);
                TryAdd(data.ApxsRecord());
                Raise(MarsChapterFlow.Events.ApxsMeasured); // elemento: enxofre abundante
            }
            else
            {
                MossbauerMeasured = true;
                SetPhase(CompositionPhase.MossbauerRetract);
                TryAdd(data.MossbauerRecord());
                Raise(MarsChapterFlow.Events.MossbauerMeasured); // mineral: jarosita
            }
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
                Debug.LogWarning($"[MAR-005] {record.EvidenceId} não foi guardada: {e.Message}");
            }
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        void SetPhase(CompositionPhase phase)
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

        /// <summary>Dados e ferramenta. A cavidade é ligada na montagem. Vazio = pronto.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsCompositionActivity);
            if (data == null)
                ContractValidation.Report(errors, owner, "falta CompositionResultsData.");
            else
                errors.AddRange(data.Validate());

            if (tool == null)
                ContractValidation.Report(errors, owner, "falta a ferramenta (pivô na ponta).");

            if (apxsHead == null || mossbauerHead == null)
                ContractValidation.Report(errors, owner, "faltam as cabeças do APXS e do Mössbauer.");

            return errors;
        }

        /// <summary>Validação completa, com a cavidade da RAT ligada.</summary>
        public List<string> ValidateWiring()
        {
            var errors = ValidateSetup();
            if (cavity == null)
                ContractValidation.Report(errors, nameof(MarsCompositionActivity), "falta a cavidade da RAT (RatCavitySurface).");

            return errors;
        }
    }
}
