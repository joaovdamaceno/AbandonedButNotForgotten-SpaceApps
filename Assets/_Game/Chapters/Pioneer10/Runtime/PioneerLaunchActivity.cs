using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-004 · Estado PIO.Launch (40 s nominais): toca <c>PIO_Launch</c> (refletor → Atlas noturno → elipse lunar →
    /// sonda livre) e conduz o primeiro hold guiado de 3 s no RTG, em que o calor aparece nos geradores e chega como
    /// eletricidade ao compartimento. A sequência espera o hold (marca <c>hold</c> do roteiro), sem concluir sozinha.
    /// <para>
    /// Regras: o E que já estava pressionado antes do cartão não conta (rearme); soltar E pausa o progresso sem perdê-lo;
    /// pausa do shell congela sequência, hold e fumaça (tempo ativo); cancelar ou pular encerra partículas, hold, contexto
    /// de entrada e candidato; <see cref="Completed"/> dispara uma única vez (pular conta como concluído; cancelar não).
    /// </para>
    /// </summary>
    public sealed class PioneerLaunchActivity : MonoBehaviour
    {
        public const string StateId = "PIO.Launch";
        public const string EventIgnition = "PIO.Launch.Ignition";
        public const string EventLiftoff = "PIO.Launch.Liftoff";
        public const string EventRtgHold = "PIO.Launch.RtgHold";
        public const string CandidateId = "PIO.Launch.Rtg";
        /// <summary>Linha do HUD "Segure E · acompanhar a energia" (PIO.UI.rtg, roteiro). Verbo e objeto usam a mesma
        /// chave porque os textos da Pioneer são frases inteiras; o HUD (UI-002) decide a composição.</summary>
        public const string PromptKey = "PIO.UI.rtg";
        public const float HoldSeconds = 3f;
        public const string AtlasActorId = "PIO.Atlas";
        public const string AtlasLookTargetId = "PIO.AtlasLook";
        public const string TraceActorId = "PIO.Launch.TraceHead";
        /// <summary>Único plano em que o gráfico Terra–Lua existe (PIO01.C); fora dele fica desligado.</summary>
        public const string EllipseShotId = "PIO01.C";

        [Header("Dados")]
        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;

        [Header("Lançamento (LaunchSilhouette)")]
        [SerializeField, Tooltip("Raiz da silhueta; vai para LaunchSite/AtlasSocket ao conectar.")] Transform launchRoot;
        [SerializeField] Transform atlas;
        [SerializeField] Transform atlasLookPoint;
        [SerializeField] GameObject flame;
        [SerializeField] Light flameLight;
        [SerializeField, Min(0f)] float flameLightIntensity = 40f;
        [SerializeField] ParticleSystem smoke;

        [Header("Elipse lunar (gráfico)")]
        [SerializeField, Tooltip("Vai para DeepSpace/LaunchDiagramSocket ao conectar.")] Transform lunarDiagram;
        [SerializeField] Transform traceHead;

        [Header("Calor → eletricidade")]
        [SerializeField] Transform rtgGlowA;
        [SerializeField] Transform rtgGlowB;
        [SerializeField] Transform pulseA;
        [SerializeField] Transform pulseB;
        [SerializeField] Transform compartmentGlow;
        [SerializeField, Min(0.01f)] float rtgGlowScaleMeters = 0.26f;
        [SerializeField, Min(0.01f)] float compartmentGlowScaleMeters = 0.5f;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        IDisposable inputLease;

        bool running;
        bool ignited;
        bool pendingHold;
        bool cinemaHold;
        bool awaitingHold;
        bool holdDone;
        bool completedRaised;
        float acquiredSeconds;   // progresso do hold já adquirido (soltar não apaga)
        float holdProgress;      // 0–1 total, para o retorno visual
        float visualClock;
        Transform rtgA, rtgB, compartment;

        /// <summary>Estado concluído (sequência terminou ou foi pulada). Dispara uma vez por <see cref="Begin"/>.</summary>
        public event Action Completed;

        /// <summary>
        /// Roteiro v2 (cena 1 só de cinema): a demonstração dos geradores corre sozinha em <see cref="HoldSeconds"/> segundos, sem pedir
        /// E e sem pausar a sequência. Ligado pelo controlador do capítulo; o padrão (falso) é a versão interativa.
        /// </summary>
        public bool CinemaMode { get; set; }

        public SequenceDefinition Sequence => sequence;
        public bool IsRunning => running;
        public bool IsAwaitingHold => awaitingHold;
        public bool HoldDone => holdDone;
        public float HoldProgress => holdProgress;
        public bool Ignited => ignited;
        public Transform Atlas => atlas;
        public ParticleSystem Smoke => smoke;
        public GameObject Flame => flame;

        /// <summary>Serviços globais (Boot/CORE). Registra atores e alvo de enquadramento da sequência.</summary>
        public void Initialize(SequenceDirector sequenceDirector, IInteractionService interactionService, InputRouter inputRouter, IActiveTimeSource activeTime)
        {
            Unbind();
            director = sequenceDirector;
            interaction = interactionService;
            router = inputRouter;
            timeSource = activeTime;
            AttachToStage();
            if (director != null)
            {
                director.RegisterActor(AtlasActorId, atlas);
                director.RegisterActor(TraceActorId, traceHead);
                if (director.CameraDirector != null)
                    director.CameraDirector.RegisterLookTarget(AtlasLookTargetId, atlasLookPoint);
            }
        }

        /// <summary>Pendura a silhueta, o diagrama e o brilho dos RTGs nos sockets do palco (PIO-003). Idempotente.</summary>
        public void AttachToStage()
        {
            if (presentation == null)
                return;

            Pin(launchRoot, presentation.RegionRoot(PioneerStageRegion.LaunchSite), "AtlasSocket");
            Pin(lunarDiagram, presentation.RegionRoot(PioneerStageRegion.DeepSpace), "LaunchDiagramSocket");
            presentation.TryGetSocket("RtgBoomA", out rtgA);
            presentation.TryGetSocket("RtgBoomB", out rtgB);
            presentation.TryGetSocket("EquipmentCompartment", out compartment);
            Pin(rtgGlowA, rtgA, null);
            Pin(rtgGlowB, rtgB, null);
            Pin(compartmentGlow, compartment, null);
            Pin(pulseA, rtgA, null);
            Pin(pulseB, rtgB, null);
            ResetVisuals();
        }

        static void Pin(Transform item, Transform parent, string childName)
        {
            if (item == null || parent == null)
                return;

            var target = string.IsNullOrEmpty(childName) ? parent : parent.Find(childName);
            if (target == null)
                return;

            item.SetParent(target, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
        }

        /// <summary>Começa o estado. <paramref name="skippable"/> vem do shell (só ele autoriza pular).</summary>
        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("PioneerLaunchActivity sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            running = true;
            director.EventRaised += OnEventRaised;
            director.ShotStarted += OnShotStarted;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor("PIO.Launch.Reveal");
            }

            director.Play(sequence, skippable);
        }

        /// <summary>Encerra sem concluir (mudança de estado pelo shell): mesmo caminho de saída da sequência.</summary>
        public void Cancel()
        {
            if (running && director != null && director.IsPlaying)
                director.Cancel();
            else
                CleanUp();
        }

        void OnEventRaised(string eventId)
        {
            switch (eventId)
            {
                case EventIgnition:
                    SetIgnition(true);
                    break;
                case EventRtgHold:
                    // Adiado ao próximo Tick: ao pular, a sequência dispara os eventos restantes e termina antes dele.
                    if (CinemaMode)
                        cinemaHold = !holdDone;
                    else
                        pendingHold = !holdDone;
                    break;
            }
        }

        void OnShotStarted(string shotId) => SetDiagramVisible(shotId == EllipseShotId);

        void SetDiagramVisible(bool visible)
        {
            if (lunarDiagram != null)
                lunarDiagram.gameObject.SetActive(visible);
        }

        void OnFinished(SequenceResult result)
        {
            var completed = result.Reason != SequenceExitReason.Canceled;
            CleanUp();
            if (completed && !completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }
        }

        void Update()
        {
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança efeitos com tempo ATIVO (zero na pausa) e abre o hold pendente. Testes chamam direto.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!running)
                return;

            if (pendingHold)
            {
                pendingHold = false;
                if (director != null && director.IsPlaying)
                    OpenHold();
            }

            if (cinemaHold && activeDeltaSeconds > 0f)
            {
                holdProgress = Mathf.Clamp01(holdProgress + activeDeltaSeconds / HoldSeconds);
                if (holdProgress >= 1f)
                {
                    holdDone = true;
                    cinemaHold = false;
                }
            }

            if (activeDeltaSeconds > 0f)
            {
                visualClock += activeDeltaSeconds;
                if (smoke != null && ignited)
                    smoke.Simulate(activeDeltaSeconds, true, false, false);
                if (flame != null && ignited)
                {
                    var flicker = 1f + 0.06f * Mathf.Sin(visualClock * 23f) + 0.04f * Mathf.Sin(visualClock * 37f);
                    flame.transform.localScale = new Vector3(1f, flicker, 1f);
                }
            }

            UpdateEnergyVisuals();
        }

        void OpenHold()
        {
            director.Pause();
            awaitingHold = true;
            inputLease = router != null ? router.Acquire(InputContext.Panel, "PIO.Launch.Rtg") : null;
            if (interaction != null)
            {
                interaction.HoldProgress += OnHoldProgress;
                interaction.HoldCanceled += OnHoldCanceled;
                interaction.HoldCompleted += OnHoldCompleted;
                // Rearme: a troca de contexto (Cinematic → Panel) já faz o InputRouter ignorar um E mantido desde antes
                // até ser solto. Não chamar RequireRelease aqui: a soltura de uma tecla suprimida não gera InteractUp.
                OfferCandidate();
            }
        }

        void OfferCandidate()
        {
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                Anchor = rtgA,
                VerbKey = PromptKey,
                ObjectKey = PromptKey,
                Available = true,
                DistanceMeters = 0f,
                HoldSeconds = Mathf.Max(0.05f, HoldSeconds - acquiredSeconds),
                Priority = 10,
            });
        }

        void OnHoldProgress(string id, float progress)
        {
            if (id != CandidateId)
                return;

            var remaining = Mathf.Max(0.05f, HoldSeconds - acquiredSeconds);
            holdProgress = Mathf.Clamp01((acquiredSeconds + progress * remaining) / HoldSeconds);
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId || !awaitingHold)
                return;

            // Soltar pausa sem apagar: o que já foi feito fica, e o próximo hold só cobre o restante.
            acquiredSeconds = Mathf.Min(HoldSeconds - 0.05f, holdProgress * HoldSeconds);
            OfferCandidate();
        }

        void OnHoldCompleted(string id)
        {
            if (id != CandidateId || !awaitingHold)
                return;

            holdProgress = 1f;
            holdDone = true;
            CloseHold();
            if (director != null && director.IsPlaying)
                director.Resume();
        }

        void CloseHold()
        {
            awaitingHold = false;
            if (interaction != null)
            {
                interaction.HoldProgress -= OnHoldProgress;
                interaction.HoldCanceled -= OnHoldCanceled;
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.ClearCandidate(CandidateId);
            }

            inputLease?.Dispose();
            inputLease = null;
        }

        void CleanUp()
        {
            if (awaitingHold)
                CloseHold();

            if (director != null)
            {
                director.EventRaised -= OnEventRaised;
                director.ShotStarted -= OnShotStarted;
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            running = false;
            pendingHold = false;
            cinemaHold = false;
            holdDone = false;
            acquiredSeconds = 0f;
            holdProgress = 0f;
            SetIgnition(false);
            SetDiagramVisible(false);
            ResetVisuals();
        }

        void Unbind()
        {
            CleanUp();
            if (director != null)
            {
                director.UnregisterActor(AtlasActorId);
                director.UnregisterActor(TraceActorId);
                if (director.CameraDirector != null)
                    director.CameraDirector.UnregisterLookTarget(AtlasLookTargetId);
            }
        }

        void SetIgnition(bool on)
        {
            ignited = on;
            if (flame != null)
                flame.SetActive(on);
            if (flameLight != null)
            {
                flameLight.enabled = on;
                flameLight.intensity = on ? flameLightIntensity : 0f;
            }

            if (smoke != null)
            {
                var emission = smoke.emission;
                emission.enabled = on;
                if (on)
                    smoke.Simulate(0f, true, true, false); // inicia parado; depois só Simulate incremental pelo tempo ativo
                else
                    smoke.Clear(true);
            }
        }

        void ResetVisuals()
        {
            visualClock = 0f;
            UpdateEnergyVisuals();
        }

        /// <summary>Retorno do hold: calor nos RTGs (laranja), pulsos pela haste, eletricidade no compartimento (ciano).</summary>
        void UpdateEnergyVisuals()
        {
            var p = holdProgress;
            var active = awaitingHold || holdDone || cinemaHold;
            SetGlow(rtgGlowA, active, rtgGlowScaleMeters * (0.35f + 0.65f * Mathf.Clamp01(p * 1.6f)));
            SetGlow(rtgGlowB, active, rtgGlowScaleMeters * (0.35f + 0.65f * Mathf.Clamp01(p * 1.6f)));
            SetGlow(compartmentGlow, active && p > 0.25f, compartmentGlowScaleMeters * Mathf.Clamp01((p - 0.25f) / 0.75f));

            var pulsing = active && p > 0f && !holdDone;
            var phase = Mathf.Repeat(visualClock * 0.8f, 1f);
            MovePulse(pulseA, rtgA, pulsing, phase);
            MovePulse(pulseB, rtgB, pulsing, Mathf.Repeat(phase + 0.5f, 1f));
        }

        static void SetGlow(Transform glow, bool visible, float sizeMeters)
        {
            if (glow == null)
                return;

            glow.gameObject.SetActive(visible && sizeMeters > 0.001f);
            glow.localScale = Vector3.one * Mathf.Max(0.001f, sizeMeters);
        }

        void MovePulse(Transform pulse, Transform from, bool visible, float phase)
        {
            if (pulse == null)
                return;

            pulse.gameObject.SetActive(visible && from != null && compartment != null);
            if (!pulse.gameObject.activeSelf)
                return;

            pulse.position = Vector3.Lerp(from.position, compartment.position, phase);
        }

        /// <summary>Prévia de Editor: ignição, fumaça e retorno do hold num instante (não toca a sequência).</summary>
        public void PreviewEffects(float sequenceTimeSeconds, float ignitionTimeSeconds, float holdPreviewProgress)
        {
            AttachToStage();
            var ellipse = FindShot(EllipseShotId, out var ellipseStart);
            SetDiagramVisible(ellipse != null && sequenceTimeSeconds >= ellipseStart && sequenceTimeSeconds < ellipseStart + ellipse.DurationSeconds);
            SetIgnition(sequenceTimeSeconds >= ignitionTimeSeconds);
            if (ignited && smoke != null)
            {
                smoke.Simulate(Mathf.Max(0.01f, sequenceTimeSeconds - ignitionTimeSeconds), true, true, false);
            }

            holdProgress = Mathf.Clamp01(holdPreviewProgress);
            awaitingHold = holdPreviewProgress > 0f && holdPreviewProgress < 1f;
            holdDone = holdPreviewProgress >= 1f;
            visualClock = 0.3f;
            UpdateEnergyVisuals();
            awaitingHold = false;
            holdDone = false;
        }

        ShotDefinition FindShot(string shotId, out float startSeconds)
        {
            startSeconds = 0f;
            if (sequence == null)
                return null;

            foreach (var shot in sequence.Shots)
            {
                if (shot.ShotId == shotId)
                    return shot;
                startSeconds += shot.DurationSeconds;
            }

            return null;
        }

        void OnDestroy() => Unbind();
    }
}
