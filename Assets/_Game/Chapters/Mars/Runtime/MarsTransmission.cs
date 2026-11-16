using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum TransmissionPhase
    {
        Idle = 0,
        Antenna = 1,
        ChoosingMemory = 2,
        Done = 3,
    }

    /// <summary>Opções da escolha final: a foto do vale do jogador ou o registro científico do Sol.</summary>
    public enum MemoryOption
    {
        ValleyPhoto = 0,
        ScienceRecord = 1,
    }

    /// <summary>
    /// MAR-008 · M29–M30: preparar a transmissão e escolher a memória.
    /// <para>
    /// M29: <b>E sustentado</b> orienta apenas a antena (articulação) em direção à Terra, sem nunca girar o chassi e sem dirigir a
    /// estação nenhuma: funciona de qualquer lugar, inclusive ao lado do mirante. O giro é gradual (nunca instantâneo) e o prompt
    /// de transmissão tem prioridade maior que qualquer registro opcional. A interface mostra uma preparação condensada, não a
    /// comunicação literal enviada pelo rover (chave <c>transmissionNoteKey</c>). Envia <c>mars.antenna.prepared</c>.
    /// </para>
    /// <para>
    /// M30: pede o contexto de PAINEL do <see cref="InputRouter"/> (as setas só existem aí, e o rover não anda). As setas alternam
    /// entre a foto do vale feita pelo jogador (selecionada por padrão) e o registro científico do Sol; <b>E confirma uma vez</b>.
    /// O E que concluiu a antena nunca confirma: é preciso soltar e pressionar de novo. Não há tempo limite: nada escolhe a
    /// foto sozinho. A escolhida fica em <see cref="SelectedRecord"/> para o memorial; envia <c>mars.memory.confirmed</c>.
    /// </para>
    /// Dados em <see cref="FinalSurveyData"/>; textos PT/EN em <c>MarsFinalSurveyTexts.json</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsTransmission : MonoBehaviour
    {
        public const string AntennaStepId = "M29";
        public const string MemoryStepId = "M30";
        public const string AntennaCandidateId = "mars.transmission.antenna";
        public const string ConfirmCandidateId = "mars.memory.confirm";

        // Maior que a das capturas (10): a transmissão prevalece sobre qualquer registro opcional.
        const int AntennaPriority = 20;
        const int ConfirmPriority = 30;

        [SerializeField] FinalSurveyData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField, Tooltip("Articulação da antena de alto ganho. O chassi nunca gira.")] Transform antennaGimbal;
        [SerializeField, Tooltip("Direção da Terra, ponto de referência para a antena.")] Transform earthTarget;

        readonly HashSet<string> raised = new HashSet<string>();
        readonly HeldWork work = new HeldWork();
        readonly List<MemoryOption> options = new List<MemoryOption>();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        IEvidenceStore evidence;
        InputRouter router;
        IDisposable panelLease;
        Quaternion gimbalStart;
        bool gimbalCaptured;
        bool bound;
        bool holding;
        bool antennaCandidate;
        bool confirmCandidate;
        bool confirmAvailable;
        bool confirmArmed;
        float lastHold = -1f;

        public TransmissionPhase Phase { get; private set; }
        public bool IsHolding => holding;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public FinalSurveyData Data => data;
        public int SelectionIndex { get; private set; }
        public IReadOnlyList<MemoryOption> Options => options;
        public MemoryOption? Selected => options.Count > 0 ? options[SelectionIndex] : (MemoryOption?)null;

        /// <summary>A memória confirmada para o memorial (a foto do vale ou o registro científico); nula até confirmar.</summary>
        public EvidenceRecord SelectedRecord { get; private set; }

        public event Action<TransmissionPhase> PhaseChanged;
        public event Action<MemoryOption> SelectionChanged;
        public event Action<EvidenceRecord> MemoryConfirmed;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            InputRouter inputRouter, IEvidenceStore evidenceStore, Transform gimbal, Transform earth)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            router = inputRouter;
            evidence = evidenceStore;
            antennaGimbal = gimbal;
            earthTarget = earth;
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

            if (router != null)
                router.NavigateDown += OnNavigate;

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

            if (router != null)
                router.NavigateDown -= OnNavigate;

            if (flow != null)
            {
                flow.StepStarted -= OnStepStarted;
                flow.StepEnded -= OnStepEnded;
            }

            ClearCandidates();
            holding = false;
            ReleasePanel();
        }

        // ---------- Etapas ----------

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            if (binding.StepId == AntennaStepId)
            {
                ResetActivity();
                work.Begin(data.antennaHoldSeconds);
                CaptureGimbal();
                SetPhase(TransmissionPhase.Antenna);
            }
            else if (binding.StepId == MemoryStepId)
            {
                if (Phase != TransmissionPhase.Antenna || !raised.Contains(MarsChapterFlow.Events.AntennaPrepared))
                    ResetActivity(); // entrou em M30 sem passar por M29 (reinício, teste)

                BeginChoosing();
            }
            else if (Phase != TransmissionPhase.Idle && IsBeforeAntenna(binding.StepId))
            {
                ResetActivity(); // reinício do capítulo: sem escolha, sem painel, sem estado residual
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId == AntennaStepId)
            {
                ClearCandidates();
                holding = false;
                if (!raised.Contains(MarsChapterFlow.Events.AntennaPrepared))
                    ResetActivity();
            }
            else if (stepId == MemoryStepId)
            {
                ClearCandidates();
                ReleasePanel();
                if (!raised.Contains(MarsChapterFlow.Events.MemoryConfirmed))
                    ResetActivity();
                else
                    SetPhase(TransmissionPhase.Done);
            }
        }

        bool IsBeforeAntenna(string stepId)
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
                else if (steps[i].StepId == AntennaStepId)
                    self = i;
            }

            return current >= 0 && self >= 0 && current < self;
        }

        /// <summary>Zera tudo: antena, escolha, painel e eventos (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidates();
            holding = false;
            raised.Clear();
            work.Reset();
            options.Clear();
            SelectionIndex = 0;
            SelectedRecord = null;
            gimbalCaptured = false;
            ReleasePanel();
            SetPhase(TransmissionPhase.Idle);
        }

        void CaptureGimbal()
        {
            if (antennaGimbal != null)
            {
                gimbalStart = antennaGimbal.rotation;
                gimbalCaptured = true;
            }
        }

        void ReleasePanel()
        {
            panelLease?.Dispose();
            panelLease = null;
        }

        void BeginChoosing()
        {
            options.Clear();
            if (evidence != null && evidence.Contains(data.valleyPhotoId))
                options.Add(MemoryOption.ValleyPhoto);

            if (evidence != null && evidence.Contains(data.sunComparisonId))
                options.Add(MemoryOption.ScienceRecord);

            confirmArmed = false; // o E que concluiu a antena só vale de novo depois de solto
            SelectionIndex = 0; // a foto do vale vem primeiro e é a padrão: sem tempo limite
            SetPhase(TransmissionPhase.ChoosingMemory);
            if (router != null)
                panelLease = router.Acquire(InputContext.Panel, "mars.memory"); // as setas só existem no painel e o rover não anda

            if (options.Count == 0)
            {
                // Sem nenhuma imagem guardada (limite de fotos, câmera ausente): não trava a etapa.
                Debug.LogWarning("[MAR-008] nenhuma memória disponível para escolher; seguindo sem escolha.");
                ReleasePanel();
                SetPhase(TransmissionPhase.Done);
                Raise(MarsChapterFlow.Events.MemoryConfirmed);
            }
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

            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            if (Phase == TransmissionPhase.Antenna && antennaGimbal != null && earthTarget != null && gimbalCaptured)
            {
                // A articulação segue o trabalho real de E, com velocidade máxima: nunca um giro instantâneo.
                var direction = earthTarget.position - antennaGimbal.position;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    var goal = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    var wanted = Quaternion.Slerp(gimbalStart, goal, work.Fraction);
                    antennaGimbal.rotation = Quaternion.RotateTowards(antennaGimbal.rotation, wanted, data.antennaDegreesPerSecond * dt);
                }
            }

            RefreshCandidates();
        }

        void RefreshCandidates()
        {
            if (interaction == null || data == null)
                return;

            if (Phase == TransmissionPhase.Antenna)
            {
                ClearConfirm();
                if (holding)
                    return;

                var hold = work.Remaining;
                if (antennaCandidate && Mathf.Approximately(lastHold, hold))
                    return;

                antennaCandidate = true;
                lastHold = hold;
                interaction.SetCandidate(new InteractionCandidate
                {
                    Id = AntennaCandidateId, VerbKey = data.antennaVerbKey, ObjectKey = data.antennaObjectKey,
                    Available = true, DistanceMeters = 0f, HoldSeconds = hold, Priority = AntennaPriority,
                });
            }
            else if (Phase == TransmissionPhase.ChoosingMemory && options.Count > 0)
            {
                ClearAntenna();
                // O E que concluiu a antena não confirma: só depois de SOLTO o prompt aparece (prompt e execução coincidem).
                if (!confirmArmed && (router == null || !router.IsInteractPhysicallyHeld))
                    confirmArmed = true;

                if (confirmCandidate && confirmAvailable == confirmArmed)
                    return;

                // Zero: confirma ao pressionar (uma só vez).
                confirmCandidate = true;
                confirmAvailable = confirmArmed;
                interaction.SetCandidate(new InteractionCandidate
                {
                    Id = ConfirmCandidateId, VerbKey = data.confirmVerbKey, ObjectKey = data.confirmObjectKey,
                    Available = confirmArmed, DistanceMeters = 0f, HoldSeconds = 0f, Priority = ConfirmPriority,
                });
            }
            else
            {
                ClearCandidates();
            }
        }

        void ClearAntenna()
        {
            if (!antennaCandidate)
                return;

            antennaCandidate = false;
            lastHold = -1f;
            if (interaction != null)
                interaction.ClearCandidate(AntennaCandidateId);
        }

        void ClearConfirm()
        {
            if (!confirmCandidate)
                return;

            confirmCandidate = false;
            if (interaction != null)
                interaction.ClearCandidate(ConfirmCandidateId);
        }

        void ClearCandidates()
        {
            ClearAntenna();
            ClearConfirm();
        }

        // ---------- Entrada ----------

        void OnNavigate(Vector2Int direction)
        {
            if (Phase != TransmissionPhase.ChoosingMemory || options.Count < 2 || direction.x == 0)
                return;

            SelectionIndex = (SelectionIndex + (direction.x > 0 ? 1 : options.Count - 1)) % options.Count;
            SelectionChanged?.Invoke(options[SelectionIndex]);
        }

        void OnHoldStarted(string id)
        {
            if (id != AntennaCandidateId || Phase != TransmissionPhase.Antenna)
                return;

            holding = true;
            work.Started(interaction.TryGetActive(out var active) && active.Id == id ? active.HoldSeconds : (float?)null);
        }

        void OnHoldProgress(string id, float progress)
        {
            if (holding && id == AntennaCandidateId && Phase == TransmissionPhase.Antenna)
                work.Progress(progress);
        }

        void OnHoldCanceled(string id)
        {
            if (id != AntennaCandidateId)
                return;

            holding = false;
            work.Canceled();
            RefreshCandidates();
        }

        void OnHoldCompleted(string id)
        {
            if (id == AntennaCandidateId && Phase == TransmissionPhase.Antenna)
            {
                holding = false;
                ClearCandidates();
                work.Progress(1f);
                Raise(MarsChapterFlow.Events.AntennaPrepared);
            }
            else if (id == ConfirmCandidateId && Phase == TransmissionPhase.ChoosingMemory && options.Count > 0)
            {
                ClearCandidates(); // o alvo confirma uma vez
                var chosen = options[SelectionIndex];
                var evidenceId = chosen == MemoryOption.ValleyPhoto ? data.valleyPhotoId : data.sunComparisonId;
                if (evidence != null && evidence.TryGet(evidenceId, out var record))
                    SelectedRecord = record;

                ReleasePanel();
                SetPhase(TransmissionPhase.Done);
                MemoryConfirmed?.Invoke(SelectedRecord);
                Raise(MarsChapterFlow.Events.MemoryConfirmed);
            }
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        void SetPhase(TransmissionPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            if (data == null)
                ContractValidation.Report(errors, nameof(MarsTransmission), "falta FinalSurveyData.");
            else
                errors.AddRange(data.Validate());

            return errors;
        }

        /// <summary>Validação completa, com a antena, a Terra e o router ligados.</summary>
        public List<string> ValidateWiring()
        {
            var errors = ValidateSetup();
            const string owner = nameof(MarsTransmission);
            if (antennaGimbal == null) ContractValidation.Report(errors, owner, "falta a articulação da antena.");
            if (earthTarget == null) ContractValidation.Report(errors, owner, "falta a referência da Terra.");
            if (router == null) ContractValidation.Report(errors, owner, "falta o InputRouter (setas da escolha da memória).");
            return errors;
        }
    }
}
