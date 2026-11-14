using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum BlueberriesPhase
    {
        Idle = 0,
        Approaching = 1,
        ReadyToOpen = 2,
        Soil = 3,
        Rock = 4,
        Result = 5,
        Done = 6,
    }

    /// <summary>
    /// MAR-003 · M08–M09: observação guiada das esferas, sem mira nem caça de pixel.
    /// <para>
    /// M08: chegar a <c>reachDistanceMeters</c> do afloramento envia <c>mars.spheres.reached</c> e para o rover enquanto a
    /// fala C10 é lida (o prompt de E só existe depois, em M09). M09: o E <b>abre</b> a observação; é preciso <b>soltar</b> e pressionar de novo para operar. Segurar
    /// E por 4 s registra o solo (macrovisão MI já enquadrada) e por mais 4 s as esferas presas na rocha; só então o
    /// resultado mineralógico (Mössbauer, <b>separado</b> das imagens) aparece por 4 s e a etapa conclui. Soltar E, pausar ou
    /// perder foco pausa e conserva o trabalho (o hold seguinte pede só o restante). Nada conclui por tempo.
    /// </para>
    /// <para>
    /// Ciência: as imagens do MI mostram forma e distribuição (registros <c>Observation</c> de reconstrução, que não gastam
    /// as 3 fotos do jogador); só a medição Mössbauer identifica hematita (registro <c>Composition</c> à parte). Cor ou
    /// fotografia nunca são apresentadas como determinação mineral. Parâmetros e IDs em <see cref="BlueberriesActivityData"/>;
    /// textos em <c>MarsBlueberriesTexts.json</c>. Câmera e rover são de quem integra: este componente só expõe a macrovisão
    /// ativa (<see cref="ViewChanged"/>) e quando o rover deve ficar parado (<see cref="EngagedChanged"/>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlueberriesActivity : MonoBehaviour
    {
        public const string ReachStepId = "M08";
        public const string ObserveStepId = "M09";
        public const string OpenCandidateId = "mars.blueberries.open";
        public const string SoilCandidateId = "mars.blueberries.soil";
        public const string RockCandidateId = "mars.blueberries.rock";

        const int CandidatePriority = 10;

        [Header("Ligações (preenchidas pela integração; testes usam Initialize)")]
        [SerializeField] BlueberriesActivityData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField, Tooltip("Rover/câmera de quem opera. Sem ele a chegada não é detectada e o E vale em qualquer distância.")] Transform operatorTransform;

        [Header("Cena (prefab)")]
        [SerializeField, Tooltip("Ponto no chão junto ao afloramento: alvo da chegada e âncora do prompt.")] Transform outcropAnchor;
        [SerializeField, Tooltip("Pose da macrovisão do solo (olha para as esferas soltas).")] Transform soilView;
        [SerializeField, Tooltip("Pose da macrovisão da rocha (olha para as esferas presas).")] Transform rockView;
        [SerializeField, Tooltip("Marca no solo ao registrar: assentada na amostra, não um quadrado flutuante.")] GameObject soilMark;
        [SerializeField] GameObject rockMark;

        readonly HashSet<string> raised = new HashSet<string>();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        IEvidenceStore evidence;
        bool bound;
        bool holding;
        bool engaged;
        bool candidateRegistered;
        string candidateId;
        bool lastAvailable;
        float lastHold = -1f;
        float bankedSeconds;
        float currentHoldDuration;
        float resultElapsed;

        public BlueberriesPhase Phase { get; private set; }

        /// <summary>Segundos úteis de E no registro atual (solo ou rocha).</summary>
        public float WorkSeconds { get; private set; }

        public bool SoilRecorded { get; private set; }
        public bool RockRecorded { get; private set; }

        /// <summary>O Mössbauer já identificou o mineral (resultado separado das imagens).</summary>
        public bool MineralIdentified { get; private set; }

        public bool IsHolding => holding;
        public bool IsEngaged => engaged;
        public BlueberriesActivityData Data => data;
        public Transform OutcropAnchor => outcropAnchor;

        /// <summary>Macrovisão ativa (solo ou rocha); nula fora da observação. Quem controla a câmera interpola até ela.</summary>
        public Transform CurrentView { get; private set; }

        /// <summary>Fração 0–1 do registro em andamento.</summary>
        public float CurrentFraction
        {
            get
            {
                var duration = Phase == BlueberriesPhase.Soil ? data.soilHoldSeconds : Phase == BlueberriesPhase.Rock ? data.rockHoldSeconds : 0f;
                return duration > 0f ? Mathf.Clamp01(WorkSeconds / duration) : 0f;
            }
        }

        public event Action<BlueberriesPhase> PhaseChanged;
        public event Action<bool> EngagedChanged;
        public event Action<Transform> ViewChanged;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            Transform operatorPosition = null, IEvidenceStore evidenceStore = null)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
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

            // Reinício do capítulo: voltou para uma etapa ANTES desta atividade. Nenhum estado residual (marcas, vista,
            // registros) pode sobrar, nem de uma observação já concluída.
            if (Phase != BlueberriesPhase.Idle && binding.StepId != ReachStepId && binding.StepId != ObserveStepId && IsBeforeReach(binding.StepId))
            {
                ResetActivity();
                return;
            }

            if (binding.StepId == ReachStepId)
            {
                ResetActivity();
                SetPhase(BlueberriesPhase.Approaching);
            }
            else if (binding.StepId == ObserveStepId)
            {
                if (Phase != BlueberriesPhase.Approaching)
                    ResetActivity(); // entrou em M09 sem passar por M08 (reinício, teste)

                SetPhase(BlueberriesPhase.ReadyToOpen);
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != ReachStepId && stepId != ObserveStepId)
                return;

            ClearCandidate();
            holding = false;
            var completed = stepId == ReachStepId ? raised.Contains(MarsChapterFlow.Events.SpheresReached) : Phase == BlueberriesPhase.Done;
            if (!completed)
                ResetActivity();
        }

        /// <summary>Zera o progresso, as marcas e os eventos já enviados (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            WorkSeconds = 0f;
            bankedSeconds = 0f;
            currentHoldDuration = 0f;
            resultElapsed = 0f;
            SoilRecorded = RockRecorded = MineralIdentified = false;
            SetEngaged(false);
            SetView(null);
            ShowMark(soilMark, false);
            ShowMark(rockMark, false);
            SetPhase(BlueberriesPhase.Idle);
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

        /// <summary>Avança com tempo ATIVO (zero na pausa). Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            switch (Phase)
            {
                case BlueberriesPhase.Approaching:
                    if (operatorTransform != null && outcropAnchor != null && DistanceToOutcrop() <= data.reachDistanceMeters)
                    {
                        // A fala sobre as esferas (C10) ocorre antes da análise, com o rover parado: o fluxo segura a etapa até ela terminar.
                        SetEngaged(true);
                        Raise(MarsChapterFlow.Events.SpheresReached);
                    }

                    break;

                case BlueberriesPhase.Result:
                    resultElapsed += dt;
                    if (resultElapsed >= data.resultSeconds - 1e-3f)
                    {
                        SetPhase(BlueberriesPhase.Done);
                        SetEngaged(false);
                        SetView(null);
                        Raise(MarsChapterFlow.Events.SpheresDone);
                    }

                    break;
            }

            RefreshCandidate();
        }

        float DistanceToOutcrop()
        {
            var delta = outcropAnchor.position - operatorTransform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        // ---------- Candidato de E (a mesma função decide o prompt e a execução) ----------

        void RefreshCandidate()
        {
            if (interaction == null || data == null)
                return;

            string wanted = null;
            string verb = null;
            string obj = null;
            var hold = 0f;
            switch (Phase)
            {
                case BlueberriesPhase.ReadyToOpen:
                    wanted = OpenCandidateId; verb = data.openVerbKey; obj = data.outcropObjectKey; hold = data.openHoldSeconds;
                    break;
                case BlueberriesPhase.Soil:
                    wanted = SoilCandidateId; verb = data.soilVerbKey; obj = data.soilObjectKey; hold = Mathf.Max(0.01f, data.soilHoldSeconds - bankedSeconds);
                    break;
                case BlueberriesPhase.Rock:
                    wanted = RockCandidateId; verb = data.rockVerbKey; obj = data.rockObjectKey; hold = Mathf.Max(0.01f, data.rockHoldSeconds - bankedSeconds);
                    break;
            }

            if (wanted == null)
            {
                ClearCandidate();
                return;
            }

            if (holding)
                return;

            var distance = 0f;
            var inRange = true;
            if (operatorTransform != null && outcropAnchor != null)
            {
                distance = DistanceToOutcrop();
                inRange = distance <= data.reachDistanceMeters;
            }

            if (candidateRegistered && candidateId == wanted && lastAvailable == inRange && Mathf.Approximately(lastHold, hold))
                return;

            if (candidateRegistered && candidateId != wanted)
                ClearCandidate();

            candidateRegistered = true;
            candidateId = wanted;
            lastAvailable = inRange;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = wanted, Anchor = outcropAnchor, VerbKey = verb, ObjectKey = obj, Available = inRange,
                DistanceMeters = distance, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            if (interaction != null && candidateId != null)
                interaction.ClearCandidate(candidateId);

            candidateId = null;
            lastHold = -1f;
        }

        // ---------- Eventos do InteractionService ----------

        void OnHoldStarted(string id)
        {
            if ((id == SoilCandidateId && Phase == BlueberriesPhase.Soil) || (id == RockCandidateId && Phase == BlueberriesPhase.Rock))
            {
                holding = true;
                currentHoldDuration = Mathf.Max(0.01f, (Phase == BlueberriesPhase.Soil ? data.soilHoldSeconds : data.rockHoldSeconds) - bankedSeconds);
            }
        }

        void OnHoldProgress(string id, float progress)
        {
            if (!holding || !((id == SoilCandidateId && Phase == BlueberriesPhase.Soil) || (id == RockCandidateId && Phase == BlueberriesPhase.Rock)))
                return;

            WorkSeconds = bankedSeconds + progress * currentHoldDuration;
        }

        void OnHoldCanceled(string id)
        {
            if (id != SoilCandidateId && id != RockCandidateId)
                return;

            // Soltar E pausa: o trabalho fica e o próximo hold pede só o restante.
            holding = false;
            bankedSeconds = WorkSeconds;
        }

        void OnHoldCompleted(string id)
        {
            if (id == OpenCandidateId && Phase == BlueberriesPhase.ReadyToOpen)
            {
                ClearCandidate();
                SetEngaged(true);
                BeginSample(BlueberriesPhase.Soil, soilView);
            }
            else if (id == SoilCandidateId && Phase == BlueberriesPhase.Soil)
            {
                holding = false;
                ClearCandidate();
                SoilRecorded = true;
                ShowMark(soilMark, true);
                AddEvidence(data.ImagingRecord(true));
                Raise(MarsChapterFlow.Events.SpheresSoilRecorded);
                BeginSample(BlueberriesPhase.Rock, rockView);
            }
            else if (id == RockCandidateId && Phase == BlueberriesPhase.Rock)
            {
                holding = false;
                ClearCandidate();
                RockRecorded = true;
                ShowMark(rockMark, true);
                AddEvidence(data.ImagingRecord(false));
                Raise(MarsChapterFlow.Events.SpheresRockRecorded);

                // O resultado mineralógico é uma medição separada: só agora, depois das duas imagens.
                WorkSeconds = 0f;
                SetPhase(BlueberriesPhase.Result);
                MineralIdentified = true;
                AddEvidence(data.MineralRecord());
                Raise(MarsChapterFlow.Events.SpheresHematiteIdentified);
            }
        }

        void BeginSample(BlueberriesPhase phase, Transform view)
        {
            WorkSeconds = 0f;
            bankedSeconds = 0f;
            SetView(view);
            SetPhase(phase);
        }

        void AddEvidence(EvidenceRecord record)
        {
            if (evidence != null)
                evidence.Add(record);
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        void SetPhase(BlueberriesPhase phase)
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

        static void ShowMark(GameObject mark, bool visible)
        {
            if (mark != null)
                mark.SetActive(visible);
        }

        /// <summary>Dados, âncoras e chaves. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(BlueberriesActivity);
            if (data == null)
                ContractValidation.Report(errors, owner, "falta BlueberriesActivityData.");
            else
                errors.AddRange(data.Validate());

            if (outcropAnchor == null)
                ContractValidation.Report(errors, owner, "falta a âncora do afloramento.");

            if (soilView == null)
                ContractValidation.Report(errors, owner, "falta a macrovisão do solo.");

            if (rockView == null)
                ContractValidation.Report(errors, owner, "falta a macrovisão da rocha.");

            return errors;
        }
    }
}
