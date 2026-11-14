using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Fase da RAT. M11 cobre WaitingToPlace e Approaching; M12 cobre Presenting até Done.</summary>
    public enum MarsRatPhase
    {
        Idle = 0,
        WaitingToPlace = 1,
        Approaching = 2,
        Placed = 3,
        Presenting = 4,
        Grinding = 5,
        ShowingInterior = 6,
        Retracting = 7,
        Done = 8,
    }

    /// <summary>
    /// MAR-004 · M11–M12: a RAT abre uma janela rasa na rocha com o mesmo E das demais atividades.
    /// <para>
    /// M11: segurar E por um instante aproxima o braço pela normal (nunca gira o rover) e envia
    /// <c>mars.arm.placed</c>. M12: 3 s de apresentação, 18 s ÚTEIS de E sustentado, 3 s mostrando o interior e 3 s
    /// recolhendo; <c>mars.rat.ground</c> chega ao fim do esmerilhamento e <c>mars.rat.retracted</c> ao fim do recolhimento,
    /// cada um uma só vez (os 27 s de referência). Sem E não há progresso; soltar E, pausar ou perder foco pausa e o
    /// trabalho já feito fica (o hold seguinte pede apenas o tempo restante). Nunca conclui por tempo, nunca há A/D,
    /// aquecimento, dano ou telemetria inventada: o trabalho é só tempo ativo de E.
    /// </para>
    /// <para>
    /// Resposta no mesmo quadro: o progresso físico vai para <see cref="RatCavitySurface"/> (borda, anel e centro descem em
    /// sequência), a ferramenta segue o piso (nunca atravessa), o giro acelera e desacelera, e poeira e som acompanham o
    /// giro. A pose do rover é da MAR-002/MAR-009: este componente só avisa por <see cref="EngagedChanged"/> quando o rover
    /// deve ficar parado (do começo da aproximação ao fim do recolhimento).
    /// </para>
    /// Textos PT/EN em <c>MarsRatTexts.json</c> (chaves <see cref="PlaceVerbKey"/>, <see cref="GrindVerbKey"/>, <see cref="RockObjectKey"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsRatActivity : MonoBehaviour
    {
        public const string PlaceStepId = "M11";
        public const string GrindStepId = "M12";
        public const string PlaceCandidateId = "mars.rat.place";
        public const string GrindCandidateId = "mars.rat.grind";
        public const string PlaceVerbKey = "MAR.VERB.approach";
        public const string GrindVerbKey = "MAR.VERB.grind";
        public const string RockObjectKey = "MAR.OBJ.rock";

        const int CandidatePriority = 10;
        const float FinishEpsilonSeconds = 1e-3f;
        const float FloorReleaseMeters = 0.02f;

        [Header("Ligações (preenchidas pela integração; testes usam Initialize)")]
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField] RatCavitySurface surface;
        [SerializeField, Tooltip("Cabeça da RAT, com o pivô na ponta que toca a rocha.")] Transform tool;
        [SerializeField, Tooltip("Parte que gira (pode ser a própria ferramenta ou um filho).")] Transform spinner;
        [SerializeField, Tooltip("Rover/câmera de quem opera. Opcional: sem ele o E vale em qualquer distância.")] Transform operatorTransform;
        [SerializeField, Tooltip("Poeira. Opcional.")] ParticleSystem dust;
        [SerializeField, Tooltip("Som de contato em loop. Opcional.")] AudioSource contactAudio;

        [Header("Tempos (segundos de tempo ATIVO) e medidas (metros)")]
        [SerializeField, Min(0f)] float placeHoldSeconds = 1f;
        [SerializeField, Min(0.1f)] float approachSeconds = 3f;
        [SerializeField, Min(0f)] float presentationSeconds = 3f;
        [SerializeField, Min(0.1f)] float grindSeconds = 18f;
        [SerializeField, Min(0f)] float interiorSeconds = 3f;
        [SerializeField, Min(0.1f)] float retractSeconds = 3f;
        [SerializeField, Min(0.05f)] float approachDistanceMeters = 0.35f;
        [SerializeField, Min(0.05f)] float retractDistanceMeters = 0.25f;
        [SerializeField, Min(0.5f)] float interactRangeMeters = 3.5f;

        [Header("Giro, poeira e som")]
        [SerializeField, Min(1f)] float maxSpinDegreesPerSecond = 540f;
        [SerializeField, Min(0.05f)] float spinUpSeconds = 0.4f;
        [SerializeField, Min(0.05f)] float spinDownSeconds = 0.8f;
        [SerializeField, Min(0f)] float dustRatePerSecond = 40f;
        [SerializeField, Range(0f, 1f), Tooltip("Fração da poeira com partículas reduzidas (acessibilidade e Web).")] float reducedDustFraction = 0.3f;
        [SerializeField, Range(0f, 1f)] float contactVolume = 0.6f;

        readonly HashSet<string> raised = new HashSet<string>();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        bool bound;
        bool holding;
        bool reducedParticles;
        bool candidateRegistered;
        string candidateId;
        float phaseSeconds;
        float bankedWorkSeconds;
        float currentHoldDuration;
        float spinFraction;
        float spinAngle;
        float lastDustRate = -1f;
        bool engaged;
        bool lastAvailable;
        float lastHold;

        public MarsRatPhase Phase { get; private set; }

        /// <summary>Segundos úteis de E acumulados no esmerilhamento (0 a <see cref="GrindSeconds"/>).</summary>
        public float WorkSeconds { get; private set; }

        public float GrindSeconds => grindSeconds;
        public float WorkFraction => Mathf.Clamp01(WorkSeconds / grindSeconds);
        public bool IsHolding => holding;
        public float SpinFraction => spinFraction;
        public RatCavitySurface Surface => surface;
        public Transform Tool => tool;

        /// <summary>O rover deve ficar parado: da aproximação ao fim do recolhimento.</summary>
        public bool IsEngaged => engaged;

        public event Action<MarsRatPhase> PhaseChanged;
        public event Action<bool> EngagedChanged;

        /// <summary>Ligação por código (testes, integração). <paramref name="operatorPosition"/> pode ser nulo.</summary>
        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            Transform operatorPosition = null)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            operatorTransform = operatorPosition;
            if (isActiveAndEnabled)
                Bind();
        }

        /// <summary>Partículas reduzidas (Web, acessibilidade). Não muda o progresso nem o tempo.</summary>
        public void SetReducedParticles(bool reduced) => reducedParticles = reduced;

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
            SetEngaged(false);
            holding = false;
        }

        // ---- Etapas ----

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            if (binding.StepId == PlaceStepId)
                BeginPlace();
            else if (binding.StepId == GrindStepId)
                BeginGrind();
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != PlaceStepId && stepId != GrindStepId)
                return;

            ClearCandidate();
            holding = false;

            // M11 conclui com o braço assentado e o rover ainda parado para M12; M12 conclui com tudo recolhido.
            // Qualquer outra saída (reinício no meio) descarta o trabalho parcial e solta o rover.
            var completed = stepId == PlaceStepId ? Phase == MarsRatPhase.Placed : Phase == MarsRatPhase.Done;
            if (!completed)
                ResetActivity();
            else if (stepId == GrindStepId)
                SetEngaged(false);
        }

        /// <summary>Zera atividade, cavidade e eventos já enviados (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            WorkSeconds = 0f;
            bankedWorkSeconds = 0f;
            currentHoldDuration = 0f;
            phaseSeconds = 0f;
            spinFraction = 0f;
            SetEngaged(false);
            if (surface != null)
                surface.ResetSurface();

            ApplyToolPose(approachDistanceMeters);
            SetPhase(MarsRatPhase.Idle);
            ApplyEffects();
        }

        void BeginPlace()
        {
            ResetActivity();
            SetPhase(MarsRatPhase.WaitingToPlace);
        }

        void BeginGrind()
        {
            if (Phase != MarsRatPhase.Placed)
            {
                // Entrou em M12 sem passar por M11 (reinício, teste): parte do braço já assentado, sem repetir a aproximação.
                ResetActivity();
                SetEngaged(true);
                ApplyToolPose(0f);
            }

            WorkSeconds = 0f;
            bankedWorkSeconds = 0f;
            phaseSeconds = 0f;
            SetPhase(MarsRatPhase.Presenting);
        }

        // ---- Quadro ----

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança a atividade com tempo ATIVO (zero na pausa). Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            if (dt > 0f)
                Advance(dt);

            RefreshCandidate();
            ApplyEffects();
        }

        void Advance(float dt)
        {
            switch (Phase)
            {
                case MarsRatPhase.Approaching:
                    phaseSeconds += dt;
                    ApplyToolPose(approachDistanceMeters * (1f - Smooth(phaseSeconds / approachSeconds)));
                    if (phaseSeconds >= approachSeconds - FinishEpsilonSeconds)
                    {
                        ApplyToolPose(0f);
                        SetPhase(MarsRatPhase.Placed);
                        Raise(MarsChapterFlow.Events.ArmPlaced);
                    }

                    break;

                case MarsRatPhase.Presenting:
                    phaseSeconds += dt;
                    SpinTowards(Mathf.Clamp01(phaseSeconds / Mathf.Max(0.01f, presentationSeconds)) * 0.4f, dt);
                    if (phaseSeconds >= presentationSeconds - FinishEpsilonSeconds)
                        SetPhase(MarsRatPhase.Grinding);

                    break;

                case MarsRatPhase.Grinding:
                    SpinTowards(holding ? 1f : 0f, dt);
                    ApplyToolPose(0f);
                    break;

                case MarsRatPhase.ShowingInterior:
                    phaseSeconds += dt;
                    SpinTowards(0f, dt);
                    if (phaseSeconds >= interiorSeconds - FinishEpsilonSeconds)
                        SetPhase(MarsRatPhase.Retracting);

                    break;

                case MarsRatPhase.Retracting:
                    phaseSeconds += dt;
                    SpinTowards(0f, dt);
                    ApplyToolPose(retractDistanceMeters * Smooth(phaseSeconds / retractSeconds));
                    if (phaseSeconds >= retractSeconds - FinishEpsilonSeconds)
                    {
                        ApplyToolPose(retractDistanceMeters);
                        SetPhase(MarsRatPhase.Done);
                        SetEngaged(false);
                        Raise(MarsChapterFlow.Events.RatRetracted);
                    }

                    break;

                default:
                    SpinTowards(0f, dt);
                    break;
            }
        }

        void SetPhase(MarsRatPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            phaseSeconds = 0f;
            PhaseChanged?.Invoke(phase);
        }

        void SpinTowards(float target, float dt)
        {
            var seconds = target > spinFraction ? spinUpSeconds : spinDownSeconds;
            spinFraction = Mathf.MoveTowards(spinFraction, target, dt / seconds);
            spinAngle = Mathf.Repeat(spinAngle + spinFraction * maxSpinDegreesPerSecond * dt, 360f);
        }

        // A ponta segue o piso: nunca passa abaixo da profundidade atual do centro (não atravessa a rocha).
        void ApplyToolPose(float offsetAlongNormalMeters)
        {
            if (tool == null || surface == null)
                return;

            // Contínuo: encostada ela repousa no piso; ao subir 2 cm já está acima da superfície. Nunca abaixo do piso.
            var floor = surface.DeepestMeters * (1f - Mathf.Clamp01(offsetAlongNormalMeters / FloorReleaseMeters));
            tool.position = surface.transform.position + surface.Normal * (offsetAlongNormalMeters - floor);
            tool.rotation = surface.transform.rotation;
            if (spinner != null)
                spinner.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
        }

        void ApplyEffects()
        {
            if (spinner != null)
                spinner.localRotation = Quaternion.Euler(0f, spinAngle, 0f);

            if (dust != null)
            {
                // Poeira acompanha o giro (desacelera ao soltar E) e só existe com a ferramenta em contato.
                var inContact = Phase == MarsRatPhase.Presenting || Phase == MarsRatPhase.Grinding;
                var rate = inContact ? dustRatePerSecond * spinFraction * (reducedParticles ? reducedDustFraction : 1f) : 0f;

                if (!Mathf.Approximately(rate, lastDustRate))
                {
                    lastDustRate = rate;
                    var emission = dust.emission;
                    emission.rateOverTime = rate;
                    if (rate > 0f && !dust.isPlaying)
                        dust.Play();
                    else if (rate <= 0f && dust.isPlaying)
                        dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (contactAudio != null && contactAudio.clip != null)
            {
                contactAudio.volume = contactVolume * spinFraction;
                contactAudio.pitch = 0.8f + 0.4f * spinFraction;
                if (spinFraction > 0.01f && !contactAudio.isPlaying)
                    contactAudio.Play();
                else if (spinFraction <= 0.01f && contactAudio.isPlaying)
                    contactAudio.Pause();
            }
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---- Candidato de E ----

        void RefreshCandidate()
        {
            if (interaction == null)
                return;

            string wanted = null;
            float hold = 0f;
            if (Phase == MarsRatPhase.WaitingToPlace)
            {
                wanted = PlaceCandidateId;
                hold = placeHoldSeconds;
            }
            else if (Phase == MarsRatPhase.Grinding && !raised.Contains(MarsChapterFlow.Events.RatGround))
            {
                wanted = GrindCandidateId;
                hold = Mathf.Max(0.01f, grindSeconds - WorkSeconds);
            }

            if (wanted == null)
            {
                ClearCandidate();
                return;
            }

            var distance = 0f;
            var inRange = true;
            if (operatorTransform != null && surface != null)
            {
                var delta = surface.transform.position - operatorTransform.position;
                delta.y = 0f;
                distance = delta.magnitude;
                inRange = distance <= interactRangeMeters;
            }

            // SetCandidate valida e aloca: só quando algo muda, nunca por quadro; e nunca no meio de um hold.
            if (holding)
                return;

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
                Id = wanted,
                Anchor = surface != null ? surface.transform : null,
                VerbKey = wanted == PlaceCandidateId ? PlaceVerbKey : GrindVerbKey,
                ObjectKey = RockObjectKey,
                Available = inRange,
                DistanceMeters = distance,
                HoldSeconds = hold,
                Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            interaction?.ClearCandidate(candidateId);
            candidateId = null;
        }

        // ---- Eventos do InteractionService ----

        void OnHoldStarted(string id)
        {
            if (id == GrindCandidateId && Phase == MarsRatPhase.Grinding)
            {
                holding = true;
                currentHoldDuration = Mathf.Max(0.01f, grindSeconds - bankedWorkSeconds);
            }
        }

        void OnHoldProgress(string id, float progress)
        {
            if (id != GrindCandidateId || Phase != MarsRatPhase.Grinding)
                return;

            WorkSeconds = Mathf.Min(grindSeconds, bankedWorkSeconds + progress * currentHoldDuration);
            if (surface != null)
                surface.SetProgress(WorkFraction);

            ApplyToolPose(0f);
        }

        void OnHoldCanceled(string id)
        {
            if (id != GrindCandidateId)
                return;

            // Soltar E pausa: o trabalho fica, e o próximo hold pede só o que falta.
            holding = false;
            bankedWorkSeconds = WorkSeconds;
        }

        void OnHoldCompleted(string id)
        {
            if (id == PlaceCandidateId && Phase == MarsRatPhase.WaitingToPlace)
            {
                ClearCandidate();
                SetEngaged(true);
                SetPhase(MarsRatPhase.Approaching);
            }
            else if (id == GrindCandidateId && Phase == MarsRatPhase.Grinding)
            {
                holding = false;
                WorkSeconds = grindSeconds;
                bankedWorkSeconds = grindSeconds;
                if (surface != null)
                    surface.SetProgress(1f);

                ApplyToolPose(0f);
                ClearCandidate();
                SetPhase(MarsRatPhase.ShowingInterior);
                Raise(MarsChapterFlow.Events.RatGround);
            }
        }

        void SetEngaged(bool value)
        {
            if (engaged == value)
                return;

            engaged = value;
            EngagedChanged?.Invoke(value);
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        /// <summary>Referências, tempos e medidas. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsRatActivity);
            if (surface == null)
                ContractValidation.Report(errors, owner, "falta RatCavitySurface (disco da cavidade).");
            else
                errors.AddRange(surface.ValidateSetup());

            if (tool == null)
                ContractValidation.Report(errors, owner, "falta a ferramenta (cabeça da RAT).");

            if (placeHoldSeconds + approachSeconds > 5.01f)
                ContractValidation.Report(errors, owner, "M11 passa dos 5 s de referência.");

            var total = presentationSeconds + grindSeconds + interiorSeconds + retractSeconds;
            if (Mathf.Abs(total - 27f) > 0.01f)
                ContractValidation.Report(errors, owner, $"M12 soma {total} s; o roteiro pede 27 s (3 + 18 + 3 + 3).");

            foreach (var key in new[] { PlaceVerbKey, GrindVerbKey, RockObjectKey })
                ContractValidation.RequireId(errors, owner, "chave de texto", key);

            return errors;
        }
    }
}
