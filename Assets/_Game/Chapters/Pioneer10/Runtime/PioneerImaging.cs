using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum ImagingPhase
    {
        Idle = 0,
        /// <summary>0–16 s: o cursor de fase demonstra a passagem; nada é registrado.</summary>
        Learn = 1,
        /// <summary>16–48 s: oito passagens, uma faixa por passagem, E mantido registra a próxima janela válida.</summary>
        Sweep = 2,
        /// <summary>Depois da faixa 6: a aquisição fica parada, com as seis faixas intactas, até <see cref="PioneerImaging.ResumeAfterRecovery"/>.</summary>
        Interrupted = 3,
        /// <summary>8/8: a reconstrução está montada; a revelação histórica pertence à PIO-008.</summary>
        Assembled = 4,
        Done = 5,
    }

    /// <summary>
    /// Roteiro v2 (cena 4): a foto em TRÊS passos. 1/3 luz vermelha (segurar E, girar); 2/3 trocar para azul (um toque de E) e
    /// girar de novo; 3/3 juntar as duas imagens (um toque de E). Só existe com <see cref="PioneerImaging.ThreeStepMode"/>.
    /// </summary>
    public enum ImagingStep
    {
        None = 0,
        /// <summary>Passo 1/3: E mantido gira a sonda e as quatro faixas vermelhas aparecem.</summary>
        Red = 1,
        /// <summary>Passo 2/3 (primeira parte): um toque de E troca o filtro para azul.</summary>
        SwitchToBlue = 2,
        /// <summary>Passo 2/3 (segunda parte): E mantido gira a sonda e as quatro faixas azuis aparecem.</summary>
        Blue = 3,
        /// <summary>Passo 3/3: um toque de E junta as duas imagens.</summary>
        Combine = 4,
    }

    /// <summary>Canal que a faixa mede: as quatro primeiras são vermelhas, as quatro últimas azuis.</summary>
    public enum ImagingChannel
    {
        Red = 0,
        Blue = 1,
    }

    /// <summary>Estado do prompt de E (UI_SPEC): vira UMA classe <c>sa-hold--*</c>; nenhum depende só de cor.</summary>
    public enum ImagingHoldState
    {
        /// <summary>Candidato ativo; pressionar E segura.</summary>
        Available = 0,
        /// <summary>O E que abriu a atividade ainda está pressionado: nada começa até soltar.</summary>
        Rearm = 1,
        Holding = 2,
        /// <summary>Soltou E (ou a interrupção parou a aquisição) com faixas já registradas: o progresso está preservado.</summary>
        Paused = 3,
        /// <summary>8/8.</summary>
        Done = 4,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct ImagingViewState
    {
        public ImagingPhase Phase;
        /// <summary>Faixas registradas (0–8).</summary>
        public int Captured;
        /// <summary>Posição do cursor de fase na passagem atual (0–1).</summary>
        public float PassProgress;
        public float WindowStart;
        public float WindowEnd;
        /// <summary>E está mantido (ou foi tocado neste quadro).</summary>
        public bool Holding;
        public bool InWindow;
        /// <summary>A passagem atual já registrou a sua faixa.</summary>
        public bool PassCaptured;
        /// <summary>Passou uma janela sem registro e outra volta está em curso (informativo, sem punição).</summary>
        public bool PassMissed;
        /// <summary>0 normal, 1 após 6 s sem registro (reforça o controle), 2 após 12 s (janela ampliada).</summary>
        public int HelpLevel;
        public bool Assisted;
        public bool Interrupted;
        /// <summary>Faixa alvo da passagem (0–7); 8 quando completo.</summary>
        public int Target;
        public ImagingHoldState Hold;
        /// <summary>Versão de três passos (roteiro v2): o painel esconde o cursor de fase e mostra o passo.</summary>
        public bool StepsMode;
        public ImagingStep Step;
        /// <summary>Passo 1, 2 ou 3 (0 fora da varredura).</summary>
        public int StepNumber;
        /// <summary>Progresso do giro dentro da faixa em curso (0–1), só na versão de três passos.</summary>
        public float SpinProgress;
    }

    /// <summary>
    /// PIO-006 · Estado PIO.Imaging (64 s): varredura IPP em oito faixas e dois canais. A rotação da sonda varre o planeta;
    /// cada passagem do cursor de fase pela janela larga registra UMA faixa (um conjunto de linhas, síntese de muitos
    /// registros e não oito linhas históricas) se E estiver mantido. Um hold contínuo é aceito, inclusive iniciado antes
    /// da janela, e não há penalidade nem exigência de ritmo: perder uma passagem só oferece outra volta e jamais
    /// apaga faixas. Sem registro por 12 s, a janela cobre toda a passagem sobre o planeta (assistido).
    /// <para>
    /// Contrato com a PIO-008 (recuperação): ao completar a faixa 6 a passagem termina, a aquisição pára e
    /// <see cref="RadiationInterruption"/> dispara UMA vez. A PIO-008 apresenta ruído/aviso/botão e chama
    /// <see cref="ResumeAfterRecovery"/> (idempotente) para continuar com as duas últimas faixas, no mesmo progresso.
    /// Em 8/8, <see cref="AllStripsCaptured"/> dispara UMA vez e, no tempo da sequência, <see cref="RevealStarted"/>.
    /// </para>
    /// </summary>
    public sealed class PioneerImaging : MonoBehaviour
    {
        public const string StateId = "PIO.Imaging";
        public const string EventLearn = "PIO.Imaging.Learn";
        public const string EventSweep = "PIO.Imaging.Sweep";
        /// <summary>Em 28 s: a legenda dos canais só começa depois da faixa 4.</summary>
        public const string EventGateChannels = "PIO.Imaging.Gate4";
        /// <summary>Em 34 s: a legenda de radiação só começa depois da interrupção (faixa 6).</summary>
        public const string EventGateInterruption = "PIO.Imaging.Gate6";
        /// <summary>Em 40 s: a legenda de recuperação espera a recuperação.</summary>
        public const string EventGateRecovery = "PIO.Imaging.GateRecovery";
        /// <summary>Em 46 s: a revelação (PIO.04.06 em 48 s) espera 8/8.</summary>
        public const string EventGateComplete = "PIO.Imaging.Gate8";
        public const string EventReveal = "PIO.Imaging.Reveal";
        public const string CandidateId = "PIO.Imaging.Scan";
        public const string PromptKey = "PIO.UI.scan";
        public const int StripCount = 8;
        public const int ChannelStripCount = 4;
        /// <summary>Faixa depois da qual a radiação interrompe o instrumento.</summary>
        public const int InterruptionStrip = 6;
        public const float PassSeconds = 3f;
        /// <summary>Versão de três passos: segundos de E mantido para cada faixa (quatro faixas = 8 s por cor).</summary>
        public const float SpinSecondsPerStrip = 2f;
        public const float WindowStart = 0.25f;
        public const float WindowEnd = 0.75f;
        public const float HelpPulseSeconds = 6f;
        public const float HelpAssistSeconds = 12f;
        /// <summary>Candidato de interação sem fim prático: a atividade só usa "E está mantido", nunca um hold concluído.</summary>
        const float OpenHoldSeconds = 100000f;

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] ImagingPanelView panel;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        ImagingPhase phase;
        bool running, candidateOpen, holding, tapPending, assisted, passCaptured, passMissed, completedRaised;
        bool interruptionRaised, recoveryDone, allRaised, revealRaised, skipped, pausedByRelease;
        string waitingGate;
        ImagingStep step;
        bool awaitRelease;
        float spinSeconds;
        float passClock, idleSeconds, missedTimer;
        int captured, missedPasses;

        public event Action Completed;
        /// <summary>Faixa registrada (1–8), com o canal que mediu.</summary>
        public event Action<int, ImagingChannel> StripCaptured;
        /// <summary>Uma janela passou sem registro; outra volta começa (sem punição).</summary>
        public event Action PassMissed;
        /// <summary>6/8: a radiação interrompe o instrumento. UMA vez por execução (consumida pela PIO-008).</summary>
        public event Action RadiationInterruption;
        /// <summary>A aquisição foi retomada depois da recuperação. UMA vez por execução.</summary>
        public event Action Recovered;
        /// <summary>8/8: as oito faixas estão registradas. UMA vez por execução.</summary>
        public event Action AllStripsCaptured;
        /// <summary>Instante da revelação no relógio da sequência (48 s); a PIO-008 apresenta as imagens históricas.</summary>
        public event Action RevealStarted;

        /// <summary>
        /// Roteiro v2 (cena 4): a foto em três passos (<see cref="ImagingStep"/>), sem janela de fase, sem interrupção por radiação
        /// e sem recuperação. Ligado pelo controlador do capítulo; o padrão (falso) é a versão de oito passagens.
        /// </summary>
        public bool ThreeStepMode { get; set; }

        public ImagingStep Step => step;
        public SequenceDefinition Sequence => sequence;
        public ImagingPhase Phase => phase;
        public bool IsRunning => running;
        public bool IsInterrupted => phase == ImagingPhase.Interrupted;
        public bool IsGateWaiting => waitingGate != null;
        public string WaitingGate => waitingGate;
        public int CapturedCount => captured;
        public int MissedPasses => missedPasses;
        public bool Assisted => assisted;
        public bool RecoveryDone => recoveryDone;
        public float PassProgress => passClock / PassSeconds;

        public static ImagingChannel ChannelOf(int stripIndex) => stripIndex < ChannelStripCount ? ImagingChannel.Red : ImagingChannel.Blue;

        public void Initialize(SequenceDirector sequenceDirector, IInteractionService interactionService, InputRouter inputRouter,
            IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            CleanUp();
            director = sequenceDirector;
            interaction = interactionService;
            router = inputRouter;
            timeSource = activeTime;
            resolveText = textResolver;
        }

        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("PioneerImaging sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            skipped = false;
            running = true;
            phase = ImagingPhase.Learn; // o primeiro quadro já mostra a demonstração, antes do evento Learn
            director.EventRaised += OnEventRaised;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor(StateId);
            }

            director.Play(sequence, skippable);
            OpenInput(); // depois do Play: o lease Panel precisa ficar acima do lease Cinematic da sequência
            PushView();
        }

        public void Cancel()
        {
            if (running && director != null && director.IsPlaying)
                director.Cancel();
            else
                CleanUp();
        }

        // ---------- Eventos da sequência ----------

        void OnEventRaised(string eventId)
        {
            switch (eventId)
            {
                case EventLearn:
                    if (phase == ImagingPhase.Idle || phase == ImagingPhase.Learn)
                        phase = ImagingPhase.Learn;
                    break;
                case EventSweep:
                    if (phase == ImagingPhase.Learn)
                        StartSweep();
                    break;
                case EventGateChannels:
                    Gate(eventId, captured >= ChannelStripCount || phase > ImagingPhase.Sweep);
                    break;
                case EventGateInterruption:
                    Gate(eventId, interruptionRaised);
                    break;
                case EventGateRecovery:
                    Gate(eventId, recoveryDone);
                    break;
                case EventGateComplete:
                    Gate(eventId, allRaised);
                    break;
                case EventReveal:
                    if (!revealRaised)
                    {
                        revealRaised = true;
                        RevealStarted?.Invoke();
                    }

                    break;
            }

            PushView();
        }

        /// <summary>Segura a sequência no marco até a condição ser verdadeira (sem avanço por inatividade).</summary>
        void Gate(string eventId, bool satisfied)
        {
            if (satisfied || skipped || director == null || !director.IsPlaying)
                return;

            waitingGate = eventId;
            director.Pause();
        }

        void ReleaseGateIf(string eventId)
        {
            if (waitingGate != eventId)
                return;

            waitingGate = null;
            if (director != null && director.IsPlaying)
                director.Resume();
        }

        void OnFinished(SequenceResult result)
        {
            var completed = result.Reason != SequenceExitReason.Canceled;
            skipped = result.Reason == SequenceExitReason.Skipped;
            CleanUp();
            phase = completed ? ImagingPhase.Done : ImagingPhase.Idle;
            if (completed && !completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }
        }

        // ---------- Tempo ativo ----------

        void Update()
        {
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Testes chamam direto.</summary>
        public void Tick(float dt)
        {
            if (!running)
                return;

            // Amostra a entrada uma vez por quadro: E mantido, ou tocado entre dois quadros.
            var pressed = holding || tapPending;
            tapPending = false;
            if (dt > 0f)
            {
                missedTimer = Mathf.Max(0f, missedTimer - dt);
                if (phase == ImagingPhase.Learn)
                    Advance(dt, pressed, false);
                else if (phase == ImagingPhase.Sweep)
                {
                    if (ThreeStepMode)
                        TickSteps(dt);
                    else
                        Advance(dt, pressed, true);
                }
            }

            PushView();
        }

        float CurrentWindowStart => assisted ? 0f : WindowStart;
        float CurrentWindowEnd => assisted ? 1f : WindowEnd;

        void StartSweep()
        {
            phase = ImagingPhase.Sweep;
            passClock = 0f;
            passCaptured = false;
            passMissed = false;
            idleSeconds = 0f;
            if (ThreeStepMode)
            {
                step = ImagingStep.Red;
                spinSeconds = 0f;
                awaitRelease = false;
                // Sem radiação nesta cena (ela é da cena 5): os marcos de interrupção e de recuperação já estão satisfeitos.
                interruptionRaised = true;
                recoveryDone = true;
            }
        }

        /// <summary>Versão de três passos: E mantido gira (2 s por faixa); os toques trocam o filtro e juntam as imagens.</summary>
        void TickSteps(float dt)
        {
            if (step != ImagingStep.Red && step != ImagingStep.Blue)
                return;

            idleSeconds += dt;
            if (!assisted && idleSeconds >= HelpAssistSeconds)
                assisted = true; // sem E por 12 s, a ajuda gira sozinha (sem punição)
            if (!(holding || tapPending || assisted))
                return;

            if (holding || tapPending)
                idleSeconds = 0f;
            spinSeconds += dt;
            var guard = 0;
            while (spinSeconds >= SpinSecondsPerStrip - 1e-6f && captured < StripCount && guard++ < 8)
            {
                spinSeconds -= SpinSecondsPerStrip;
                Capture();
                if (step != ImagingStep.Red && step != ImagingStep.Blue)
                    break;
            }
        }

        /// <summary>
        /// Divide o quadro nos limites da passagem para que a janela nunca seja pulada por um quadro longo
        /// (resultado idêntico a 15, 30 ou 60 FPS).
        /// </summary>
        void Advance(float dt, bool pressed, bool capture)
        {
            var guard = 0;
            while (dt > 1e-6f && running && (phase == ImagingPhase.Sweep || phase == ImagingPhase.Learn) && guard++ < 64)
            {
                var step = Mathf.Min(dt, PassSeconds - passClock);
                var from = passClock;
                passClock += step;
                dt -= step;

                if (capture)
                {
                    idleSeconds += step;
                    if (!passCaptured && pressed && captured < StripCount && from < CurrentWindowEnd * PassSeconds - 1e-6f
                        && passClock > CurrentWindowStart * PassSeconds + 1e-6f)
                        Capture();

                    if (!assisted && idleSeconds >= HelpAssistSeconds)
                        assisted = true; // a ajuda amplia a tolerância e fica até o fim, sem alternar
                }

                if (passClock >= PassSeconds - 1e-6f)
                {
                    EndPass(capture);
                    if (phase != ImagingPhase.Sweep && phase != ImagingPhase.Learn)
                        break;
                }
            }
        }

        void Capture()
        {
            passCaptured = true;
            passMissed = false;
            idleSeconds = 0f;
            var index = captured;
            captured++;
            StripCaptured?.Invoke(captured, ChannelOf(index));
            if (captured == ChannelStripCount)
                ReleaseGateIf(EventGateChannels);
            if (ThreeStepMode)
            {
                if (captured == ChannelStripCount)
                    EnterStep(ImagingStep.SwitchToBlue);
                else if (captured >= StripCount)
                    EnterStep(ImagingStep.Combine);
                return;
            }

            if (captured >= StripCount)
                FinishSweep();
        }

        /// <summary>Muda de passo; o E que já estava mantido não vale como o toque do passo novo (é preciso soltar e tocar).</summary>
        void EnterStep(ImagingStep next)
        {
            step = next;
            spinSeconds = 0f;
            idleSeconds = 0f;
            assisted = false;
            awaitRelease = next == ImagingStep.SwitchToBlue || next == ImagingStep.Combine ? holding || tapPending : false;
        }

        void EndPass(bool capture)
        {
            passClock = 0f;
            if (!capture)
                return; // demonstração: o cursor só dá outra volta

            if (!passCaptured)
            {
                missedPasses++;
                passMissed = true;
                missedTimer = PassSeconds;
                PassMissed?.Invoke();
            }

            passCaptured = false;
            if (captured >= InterruptionStrip && !interruptionRaised && phase == ImagingPhase.Sweep)
            {
                interruptionRaised = true;
                phase = ImagingPhase.Interrupted;
                RadiationInterruption?.Invoke();
                ReleaseGateIf(EventGateInterruption);
            }
        }

        void FinishSweep()
        {
            if (allRaised)
                return;

            allRaised = true;
            phase = ImagingPhase.Assembled;
            CloseInput();
            AllStripsCaptured?.Invoke();
            ReleaseGateIf(EventGateComplete);
        }

        /// <summary>
        /// Chamado pela PIO-008 depois da recuperação. IDEMPOTENTE: só a primeira chamada durante a interrupção retoma;
        /// as demais devolvem falso e não alteram nada (faixas, passagem ou sequência).
        /// </summary>
        public bool ResumeAfterRecovery()
        {
            if (!running || phase != ImagingPhase.Interrupted || recoveryDone)
                return false;

            recoveryDone = true;
            phase = ImagingPhase.Sweep;
            passClock = 0f;
            passCaptured = false;
            idleSeconds = 0f; // as duas últimas faixas começam com a ajuda zerada
            Recovered?.Invoke();
            ReleaseGateIf(EventGateRecovery);
            PushView();
            return true;
        }

        // ---------- Entrada ----------

        void OpenInput()
        {
            if (inputLease == null && router != null)
                inputLease = router.Acquire(InputContext.Panel, "PIO.Imaging");
            if (interaction == null || candidateOpen)
                return;

            candidateOpen = true;
            interaction.HoldStarted += OnHoldStarted;
            interaction.HoldCanceled += OnHoldCanceled;
            interaction.HoldCompleted += OnHoldCompleted;
            OfferCandidate();
        }

        void OfferCandidate()
        {
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                VerbKey = PromptKey,
                ObjectKey = PromptKey,
                Available = true,
                HoldSeconds = OpenHoldSeconds,
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id != CandidateId)
                return;

            holding = true;
            tapPending = true;
            pausedByRelease = false;
            if (ThreeStepMode && phase == ImagingPhase.Sweep && !awaitRelease)
            {
                if (step == ImagingStep.SwitchToBlue)
                    EnterStep(ImagingStep.Blue);
                else if (step == ImagingStep.Combine)
                    FinishSweep();
            }
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId)
                return;

            holding = false; // soltar só pausa: as faixas registradas permanecem
            awaitRelease = false;
            pausedByRelease = captured > 0;
        }

        void OnHoldCompleted(string id)
        {
            if (id != CandidateId)
                return;

            holding = false;
            if (candidateOpen && interaction != null)
                OfferCandidate();
        }

        void CloseInput()
        {
            holding = tapPending = false;
            if (candidateOpen)
            {
                candidateOpen = false;
                if (interaction != null)
                {
                    interaction.HoldStarted -= OnHoldStarted;
                    interaction.HoldCanceled -= OnHoldCanceled;
                    interaction.HoldCompleted -= OnHoldCompleted;
                    interaction.ClearCandidate(CandidateId);
                }
            }

            inputLease?.Dispose();
            inputLease = null;
        }

        // ---------- Saída ----------

        void CleanUp()
        {
            CloseInput();
            if (director != null)
            {
                director.EventRaised -= OnEventRaised;
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            running = assisted = passCaptured = passMissed = pausedByRelease = false;
            interruptionRaised = recoveryDone = allRaised = revealRaised = false;
            waitingGate = null;
            step = ImagingStep.None;
            awaitRelease = false;
            spinSeconds = 0f;
            phase = ImagingPhase.Idle;
            passClock = idleSeconds = missedTimer = 0f;
            captured = missedPasses = 0;
            PushView();
        }

        public ImagingViewState ViewState()
        {
            var windowStart = CurrentWindowStart;
            var windowEnd = CurrentWindowEnd;
            var progress = passClock / PassSeconds;
            var level = assisted ? 2 : idleSeconds >= HelpPulseSeconds ? 1 : 0;
            return new ImagingViewState
            {
                Phase = phase,
                Captured = captured,
                PassProgress = progress,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                Holding = holding || tapPending,
                InWindow = progress >= windowStart && progress <= windowEnd,
                PassCaptured = passCaptured,
                PassMissed = passMissed && missedTimer > 0f,
                HelpLevel = phase == ImagingPhase.Sweep || phase == ImagingPhase.Interrupted ? level : 0,
                Assisted = assisted,
                Interrupted = phase == ImagingPhase.Interrupted,
                Target = captured,
                Hold = HoldState(),
                StepsMode = ThreeStepMode,
                Step = step,
                StepNumber = step == ImagingStep.Red ? 1 : step == ImagingStep.SwitchToBlue || step == ImagingStep.Blue ? 2 : step == ImagingStep.Combine ? 3 : 0,
                SpinProgress = Mathf.Clamp01(spinSeconds / SpinSecondsPerStrip),
            };
        }

        /// <summary>
        /// Estado do prompt de E. O rearme vem do InputRouter, que já suprime o E pressionado quando o contexto muda:
        /// E fisicamente pressionado sem hold ativo = a atividade abriu com E pressionado, e nada começa até soltar.
        /// </summary>
        ImagingHoldState HoldState()
        {
            if (phase == ImagingPhase.Assembled || phase == ImagingPhase.Done)
                return ImagingHoldState.Done;
            if (phase == ImagingPhase.Interrupted)
                return ImagingHoldState.Paused; // a aquisição parou com as faixas preservadas
            if (holding)
                return ImagingHoldState.Holding;
            if (router != null && router.IsInteractPhysicallyHeld)
                return ImagingHoldState.Rearm;
            return pausedByRelease ? ImagingHoldState.Paused : ImagingHoldState.Available;
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): referências, sequência de 64 s com os marcos e as 9 legendas,
        /// anchor com quadro de UI e painel ligado ao UXML. Usado pelo builder e pelo Inspector.
        /// </summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "PioneerImaging";
            if (sequence == null)
                errors.Add($"{owner}: sequence não atribuída.");
            else
            {
                errors.AddRange(sequence.Validate().ConvertAll(e => $"{owner}: {e}"));
                if (Mathf.Abs(sequence.TotalDurationSeconds - 64f) > 0.01f)
                    errors.Add($"{owner}: a sequência dura {sequence.TotalDurationSeconds} s; o roteiro v2 retemporizado pede 64 s.");

                var events = new System.Collections.Generic.HashSet<string>();
                foreach (var shot in sequence.Shots)
                {
                    if (shot.Events == null)
                        continue;
                    foreach (var shotEvent in shot.Events)
                        events.Add(shotEvent.eventId);
                }

                foreach (var required in new[] { EventLearn, EventSweep, EventGateChannels, EventGateInterruption, EventGateRecovery, EventGateComplete, EventReveal })
                {
                    if (!events.Contains(required))
                        errors.Add($"{owner}: a sequência não dispara o evento '{required}'.");
                }

                var cueKeys = new System.Collections.Generic.HashSet<string>();
                foreach (var cue in sequence.Cues)
                    cueKeys.Add(cue.TextKey);
                for (var i = 1; i <= 9; i++)
                {
                    var key = "PIO.04.0" + i;
                    if (!cueKeys.Contains(key))
                        errors.Add($"{owner}: falta a legenda {key}.");
                }
            }

            if (presentation == null)
                errors.Add($"{owner}: presentation (palco da PIO-003) não atribuída.");
            else
            {
                if (!presentation.TryGetAnchor(StateId, out _))
                    errors.Add($"{owner}: o palco não tem o anchor {StateId}.");
                else if (!presentation.TryGetUiFrame(StateId, out _))
                    errors.Add($"{owner}: o anchor {StateId} não reserva quadro de UI.");
            }

            if (panel == null)
                errors.Add($"{owner}: panel (ImagingPanelView) não atribuído.");
            else
                errors.AddRange(panel.ValidateSetup());

            return errors;
        }

        void PushView()
        {
            if (panel != null)
                panel.Render(ViewState(), resolveText);
        }

        /// <summary>Prévia de Editor: mostra uma situação com valores fixos, sem tocar a sequência.</summary>
        public void PreviewState(ImagingViewState state)
        {
            if (panel != null)
                panel.Render(state, resolveText);
        }

        void OnDestroy() => CleanUp();
    }
}
