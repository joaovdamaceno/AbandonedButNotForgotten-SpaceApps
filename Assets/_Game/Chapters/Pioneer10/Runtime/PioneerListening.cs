using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum ListeningPhase
    {
        Idle = 0,
        /// <summary>0–7 s: o cartão ensina; o botão de sintonia já se move e o indicador de proximidade já aparece. E ainda não escuta.</summary>
        Teach = 1,
        Contact1 = 2,
        Contact2 = 3,
        Contact3 = 4,
        /// <summary>37–45 s: "Segure E para ouvir". Não há zona nem frequência correta.</summary>
        Attempt4 = 5,
        /// <summary>45–52 s: os controles recolhem e há silêncio real.</summary>
        Breathe = 6,
        Done = 7,
    }

    /// <summary>O que o último contato (ou a tentativa) trouxe: quatro coisas DIFERENTES.</summary>
    public enum ListeningResult
    {
        None = 0,
        /// <summary>3 MAR 2002: sinal recebido.</summary>
        Signal = 1,
        /// <summary>27 ABR 2002: últimos dados de telemetria.</summary>
        Data = 2,
        /// <summary>23 JAN 2003: portadora muito fraca, SEM dados novos (o último sinal).</summary>
        Carrier = 3,
        /// <summary>7 FEV 2003: nenhum sinal detectado.</summary>
        NoSignal = 4,
    }

    public enum ListeningHoldState
    {
        Available = 0,
        /// <summary>O E que abriu a etapa ainda está pressionado: nada começa até soltar.</summary>
        Rearm = 1,
        Holding = 2,
        /// <summary>Soltou E com progresso: ele fica.</summary>
        Paused = 3,
        Done = 4,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct ListeningViewState
    {
        public ListeningPhase Phase;
        /// <summary>Posição do botão na escala (0–1).</summary>
        public float Knob;
        public bool ZoneVisible;
        public float ZoneStart;
        public float ZoneEnd;
        /// <summary>0–1: quão perto o botão está da zona (indicador visual, equivalente ao som).</summary>
        public float Proximity;
        /// <summary>Para onde ir: -1 esquerda, +1 direita, 0 dentro da zona (ou sem zona).</summary>
        public int Direction;
        public bool InZone;
        /// <summary>0–1: força do sinal da rodada (a terceira é mais fraca, mas a zona continua legível).</summary>
        public float Signal;
        /// <summary>0–1: progresso de escuta (2 s dentro da zona; na tentativa 4, 2 s de E mantido).</summary>
        public float Listen;
        public ListeningHoldState Hold;
        /// <summary>A varredura assistida está movendo o botão (E mantido, fora da zona).</summary>
        public bool Assisting;
        public ListeningResult Result;
        public bool ControlsCollapsed;
        /// <summary>Silêncio real em curso (2,5 s): nada se anima nem toca.</summary>
        public bool Silent;
    }

    /// <summary>
    /// PIO-011 · Estado PIO.Listening (52 s): escuta na Terra (equipe no ponto de vista da antena). Três contatos com zona
    /// generosa de 30% da escala, resultados históricos DISTINTOS (sinal recebido em 3 MAR 2002, últimos dados de
    /// telemetria em 27 ABR 2002, portadora muito fraca sem dados novos em 23 JAN 2003) e uma quarta tentativa
    /// (7 FEV 2003) em que não há zona escondida nem frequência correta: nenhum sinal, sem culpar quem escuta, e 2,5 s de
    /// silêncio real.
    /// <para>
    /// Entrada: setas/WASD e mouse movem o botão (contexto Gameplay do InputRouter, único leitor); E mantido fora da zona
    /// executa uma varredura assistida lenta que para na primeira zona válida, e dentro dela escuta por 2 s (o progresso é
    /// preservado ao soltar ou sair). Concluir uma rodada exige soltar E antes da próxima. Nada depende de som nem de timing:
    /// o botão começa a no máximo 15% da borda da zona e o indicador visual substitui o som.
    /// </para>
    /// A sequência (<c>PIO_Listening</c>) marca a abertura de cada rodada (7, 16, 25 e 37 s) e a respiração (45 s); cada marco
    /// espera a rodada anterior, sem avanço por inatividade. Datas são de RECEPÇÃO na Terra em UTC. O atraso de 11 h 20 min é
    /// de um sentido no último sinal, não o tempo do minigame.
    /// </summary>
    public sealed class PioneerListening : MonoBehaviour
    {
        public const string StateId = "PIO.Listening";
        public const string EventTeach = "PIO.Listening.Teach";
        public const string EventOpen1 = "PIO.Listening.Open1";
        public const string EventOpen2 = "PIO.Listening.Open2";
        public const string EventOpen3 = "PIO.Listening.Open3";
        public const string EventOpen4 = "PIO.Listening.Open4";
        public const string EventBreathe = "PIO.Listening.Breathe";
        /// <summary>Eventos de ESTADO para o áudio/legendas (PioneerNarration.RaiseEvent); os IDs são os propostos na PIO-001/013.</summary>
        public const string StateEventTune = "PIO.Listening.Tune";
        public const string StateEventContact1 = "PIO.Listening.Contact1";
        public const string StateEventContact2 = "PIO.Listening.Contact2";
        public const string StateEventContact3 = "PIO.Listening.Contact3";
        public const string StateEventAttempt4 = "PIO.Listening.Attempt4";
        public const string CandidateId = "PIO.Listening.Hold";
        public const string TunePromptKey = "PIO.UI.tune";
        public const string ListenPromptKey = "PIO.UI.listen";
        public const string DsnSocketName = "DishSocket";

        /// <summary>Largura da zona: 30% da escala, igual nas três rodadas.</summary>
        public const float ZoneWidth = 0.30f;
        public const float ListenSeconds = 2f;
        public const float KnobSpeedPerSecond = 0.40f;
        /// <summary>Pixels de mouse (delta) → fração da escala.</summary>
        public const float MouseKnobPerPixel = 0.0015f;
        public const float AssistSpeedPerSecond = 0.12f;
        public const float SilenceSeconds = PioneerCueMap.ListeningSilenceSeconds;
        public const int ContactCount = 3;
        const float OpenHoldSeconds = 100000f;

        /// <summary>Início e fim da zona de cada contato (mesma largura, levemente deslocada).</summary>
        public static readonly float[] ZoneStarts = { 0.55f, 0.40f, 0.48f };

        /// <summary>Onde o botão começa: a no máximo 15% da borda da zona (0,12 / 0,10 / 0,14).</summary>
        public static readonly float[] StartKnobs = { 0.43f, 0.80f, 0.34f };

        /// <summary>Força do sinal: a terceira rodada é mais fraca (volume e sinal visual menores), com a zona ainda legível.</summary>
        public static readonly float[] Signals = { 1f, 0.85f, 0.45f };

        public static readonly ListeningResult[] Results = { ListeningResult.Signal, ListeningResult.Data, ListeningResult.Carrier };
        public static readonly string[] StateEvents = { StateEventContact1, StateEventContact2, StateEventContact3 };

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] ListeningPanelView panel;
        [SerializeField, Tooltip("Raiz da silhueta da antena; vai para GroundStation/DishSocket ao conectar.")] Transform dishRoot;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        ListeningPhase phase;
        bool running, candidateOpen, holding, completedRaised, assisting, skipped, pausePending;
        readonly bool[] done = new bool[ContactCount];
        bool attemptDone;
        int waitingFor = -1; // marco aguardando a rodada anterior (2, 3, 4 ou 5 = respirar); -1 = nenhum
        float knob, listen, silenceRemaining;
        ListeningResult result;

        /// <summary>Estado concluído (sequência terminou ou foi pulada). Dispara uma vez por <see cref="Begin"/>.</summary>
        public event Action Completed;
        /// <summary>Evento de estado para o áudio/legendas (ids <c>PIO.Listening.*</c>); a integração liga a <c>PioneerNarration.RaiseEvent</c>.</summary>
        public event Action<string> StateEvent;
        /// <summary>Um contato (1–3) foi concluído. UMA vez cada por execução.</summary>
        public event Action<int> ContactMade;
        /// <summary>A quarta tentativa terminou sem sinal. UMA vez por execução.</summary>
        public event Action NoSignalHeard;

        public SequenceDefinition Sequence => sequence;
        public ListeningPhase Phase => phase;
        public bool IsRunning => running;
        public float Knob => knob;
        public float ListenProgress => listen / ListenSeconds;
        public bool IsWaitingForPrevious => waitingFor >= 0;
        public int ContactsMade => (done[0] ? 1 : 0) + (done[1] ? 1 : 0) + (done[2] ? 1 : 0);
        public bool AttemptDone => attemptDone;
        public bool IsSilent => silenceRemaining > 0f;
        public float SilenceRemaining => silenceRemaining;

        public void Initialize(SequenceDirector sequenceDirector, IInteractionService interactionService, InputRouter inputRouter,
            IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            CleanUp();
            director = sequenceDirector;
            interaction = interactionService;
            router = inputRouter;
            timeSource = activeTime;
            resolveText = textResolver;
            AttachToStage();
        }

        /// <summary>Pendura a silhueta da antena no <c>DishSocket</c> do palco (PIO-003). Idempotente.</summary>
        public void AttachToStage()
        {
            if (presentation == null || dishRoot == null)
                return;

            var region = presentation.RegionRoot(PioneerStageRegion.GroundStation);
            var socket = region != null ? region.Find(DsnSocketName) : null;
            if (socket == null)
                return;

            dishRoot.SetParent(socket, false);
            dishRoot.localPosition = Vector3.zero;
            dishRoot.localRotation = Quaternion.identity;
        }

        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("PioneerListening sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = skipped = false;
            running = true;
            phase = ListeningPhase.Teach; // o primeiro quadro já mostra o cartão e o indicador de proximidade
            knob = StartKnobs[0];
            director.EventRaised += OnEventRaised;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor(StateId);
            }

            director.Play(sequence, skippable);
            // Depois do Play: o contexto Gameplay (setas e mouse) precisa ficar acima do Cinematic da sequência.
            inputLease = router != null ? router.Acquire(InputContext.Gameplay, "PIO.Listening") : null;
            if (interaction != null)
            {
                candidateOpen = true;
                interaction.HoldStarted += OnHoldStarted;
                interaction.HoldCanceled += OnHoldCanceled;
                OfferCandidate();
            }

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
                case EventTeach:
                    if (phase <= ListeningPhase.Teach)
                        phase = ListeningPhase.Teach;
                    break;
                case EventOpen1:
                    OpenContact(0);
                    break;
                case EventOpen2:
                    OpenAfter(1, EventOpen2);
                    break;
                case EventOpen3:
                    OpenAfter(2, EventOpen3);
                    break;
                case EventOpen4:
                    OpenAfter(3, EventOpen4);
                    break;
                case EventBreathe:
                    OpenAfter(4, EventBreathe);
                    break;
            }

            PushView();
        }

        /// <summary>O marco espera a etapa anterior: a sequência pára no marco, sem concluir nem avançar por inatividade.</summary>
        void OpenAfter(int next, string eventId)
        {
            var previousDone = next <= 3 ? done[next - 1] : attemptDone;
            if (previousDone)
            {
                Open(next);
                return;
            }

            // A pausa é adiada ao próximo Tick: ao pular, a sequência dispara os eventos restantes e termina antes dele.
            waitingFor = next;
            pausePending = true;
        }

        void OpenContact(int index) => Open(index);

        void Open(int step)
        {
            switch (step)
            {
                case 0:
                case 1:
                case 2:
                    phase = (ListeningPhase)((int)ListeningPhase.Contact1 + step);
                    // Mantém o que a pessoa já ajustou no ensino, desde que esteja a no máximo 15% da zona.
                    var start = ZoneStarts[step];
                    var gap = knob < start ? start - knob : knob > start + ZoneWidth ? knob - start - ZoneWidth : 0f;
                    if (step > 0 || gap > 0.15f)
                        knob = StartKnobs[step];
                    listen = 0f;
                    assisting = false;
                    result = ListeningResult.None;
                    StateEvent?.Invoke(StateEventTune);
                    break;
                case 3: // quarta tentativa: sem zona
                    phase = ListeningPhase.Attempt4;
                    listen = 0f;
                    assisting = false;
                    result = ListeningResult.None;
                    break;
                case 4: // respirar: controles recolhem; silêncio real
                    phase = ListeningPhase.Breathe;
                    CloseInput();
                    silenceRemaining = SilenceSeconds;
                    StateEvent?.Invoke(StateEventAttempt4);
                    break;
            }

            if (candidateOpen)
                OfferCandidate();
        }

        void ReleaseWait()
        {
            if (waitingFor < 0)
                return;

            var next = waitingFor;
            waitingFor = -1;
            var neverPaused = pausePending;
            pausePending = false;
            Open(next);
            if (!neverPaused && director != null && director.IsPlaying)
                director.Resume();
        }

        void OnFinished(SequenceResult sequenceResult)
        {
            var completed = sequenceResult.Reason != SequenceExitReason.Canceled;
            skipped = sequenceResult.Reason == SequenceExitReason.Skipped;
            CleanUp();
            phase = completed ? ListeningPhase.Done : ListeningPhase.Idle;
            if (completed && !completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }

            PushView();
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

            if (pausePending && waitingFor >= 0)
            {
                pausePending = false;
                if (director != null && director.IsPlaying)
                    director.Pause();
            }

            if (dt > 0f)
            {
                MoveKnob(dt);
                if (silenceRemaining > 0f)
                    silenceRemaining = Mathf.Max(0f, silenceRemaining - dt);
            }

            PushView();
        }

        bool ZoneActive => phase >= ListeningPhase.Teach && phase <= ListeningPhase.Contact3;
        int ContactIndex => phase == ListeningPhase.Teach ? 0 : (int)phase - (int)ListeningPhase.Contact1;
        float ZoneStart => ZoneStarts[ContactIndex];
        float ZoneEnd => ZoneStarts[ContactIndex] + ZoneWidth;
        bool CanListen => phase >= ListeningPhase.Contact1 && phase <= ListeningPhase.Attempt4 && !ThisStepDone();
        bool InZone => ZoneActive && knob >= ZoneStart && knob <= ZoneEnd;

        bool ThisStepDone() => phase == ListeningPhase.Attempt4 ? attemptDone : phase >= ListeningPhase.Contact1 && phase <= ListeningPhase.Contact3 && done[ContactIndex];

        void MoveKnob(float dt)
        {
            if (router == null || !ZoneActive && phase != ListeningPhase.Attempt4)
                return;

            var delta = router.Move.x * KnobSpeedPerSecond * dt + router.Look.x * MouseKnobPerPixel;
            knob = Mathf.Clamp01(knob + delta);

            assisting = false;
            if (CanListen && holding && ZoneActive)
            {
                if (!InZone)
                {
                    // Varredura assistida lenta até a primeira zona válida (a borda mais próxima), onde pára.
                    assisting = true;
                    var target = knob < ZoneStart ? ZoneStart + 0.02f : ZoneEnd - 0.02f;
                    knob = Mathf.MoveTowards(knob, target, AssistSpeedPerSecond * dt);
                    assisting = !InZone; // ao entrar na zona a varredura pára
                }
            }

            if (CanListen && holding && (InZone || phase == ListeningPhase.Attempt4))
            {
                listen = Mathf.Min(ListenSeconds, listen + dt);
                if (listen >= ListenSeconds)
                    FinishStep();
            }
        }

        void FinishStep()
        {
            assisting = false;
            listen = ListenSeconds;
            if (phase == ListeningPhase.Attempt4)
            {
                attemptDone = true;
                result = ListeningResult.NoSignal;
                NoSignalHeard?.Invoke();
            }
            else
            {
                var index = ContactIndex;
                done[index] = true;
                result = Results[index];
                StateEvent?.Invoke(StateEvents[index]);
                if (director != null)
                    director.Trigger(StateEvents[index]); // abre a legenda do resultado (PIO.07.02–04)
                ContactMade?.Invoke(index + 1);
            }

            // O E mantido não pode concluir também a rodada seguinte: exige soltar.
            holding = false;
            if (interaction != null)
            {
                interaction.CancelHold();
                interaction.RequireRelease();
            }

            if (candidateOpen)
                OfferCandidate();
            ReleaseWait();
        }

        // ---------- Entrada ----------

        void OfferCandidate()
        {
            var available = CanListen;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                VerbKey = phase == ListeningPhase.Attempt4 ? ListenPromptKey : TunePromptKey,
                ObjectKey = phase == ListeningPhase.Attempt4 ? ListenPromptKey : TunePromptKey,
                Available = available,
                HoldSeconds = OpenHoldSeconds,
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id == CandidateId)
                holding = true;
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId)
                return;

            holding = false; // soltar só pausa: o progresso de escuta fica
            assisting = false;
        }

        void CloseInput()
        {
            holding = assisting = false;
            if (candidateOpen)
            {
                candidateOpen = false;
                if (interaction != null)
                {
                    interaction.HoldStarted -= OnHoldStarted;
                    interaction.HoldCanceled -= OnHoldCanceled;
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
            running = false;
            Array.Clear(done, 0, done.Length);
            attemptDone = false;
            waitingFor = -1;
            pausePending = false;
            listen = silenceRemaining = 0f;
            result = ListeningResult.None;
            phase = ListeningPhase.Idle;
            PushView();
        }

        public ListeningViewState ViewState()
        {
            var zone = ZoneActive;
            var start = zone ? ZoneStart : 0f;
            var end = zone ? ZoneEnd : 0f;
            var distance = !zone ? 1f : knob < start ? start - knob : knob > end ? knob - end : 0f;
            var contact = phase == ListeningPhase.Teach || phase >= ListeningPhase.Contact1 && phase <= ListeningPhase.Contact3 ? ContactIndex : -1;
            return new ListeningViewState
            {
                Phase = phase,
                Knob = knob,
                ZoneVisible = zone,
                ZoneStart = start,
                ZoneEnd = end,
                Proximity = zone ? 1f - Mathf.Clamp01(distance / 0.6f) : 0f,
                Direction = !zone || distance <= 0f ? 0 : knob < start ? 1 : -1,
                InZone = zone && distance <= 0f,
                Signal = contact >= 0 ? Signals[contact] : 0f,
                Listen = Mathf.Clamp01(listen / ListenSeconds),
                Hold = HoldState(),
                Assisting = assisting,
                Result = result,
                ControlsCollapsed = phase == ListeningPhase.Breathe || phase == ListeningPhase.Done,
                Silent = silenceRemaining > 0f,
            };
        }

        ListeningHoldState HoldState()
        {
            if (ThisStepDone())
                return ListeningHoldState.Done;
            if (holding)
                return ListeningHoldState.Holding;
            if (running && interaction != null && interaction.AwaitingRelease)
                return ListeningHoldState.Rearm;
            return listen > 0f ? ListeningHoldState.Paused : ListeningHoldState.Available;
        }

        /// <summary>Erros de montagem (lista vazia = conforme).</summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "PioneerListening";
            if (sequence == null)
                errors.Add($"{owner}: sequence não atribuída.");
            else
            {
                errors.AddRange(sequence.Validate().ConvertAll(e => $"{owner}: {e}"));
                if (Mathf.Abs(sequence.TotalDurationSeconds - 52f) > 0.01f)
                    errors.Add($"{owner}: a sequência dura {sequence.TotalDurationSeconds} s; o roteiro pede 52 s.");

                var events = new System.Collections.Generic.HashSet<string>();
                foreach (var shot in sequence.Shots)
                {
                    if (shot.Events == null)
                        continue;
                    foreach (var shotEvent in shot.Events)
                        events.Add(shotEvent.eventId);
                }

                foreach (var required in new[] { EventTeach, EventOpen1, EventOpen2, EventOpen3, EventOpen4, EventBreathe })
                {
                    if (!events.Contains(required))
                        errors.Add($"{owner}: a sequência não dispara o evento '{required}'.");
                }

                var cueKeys = new System.Collections.Generic.HashSet<string>();
                foreach (var cue in sequence.Cues)
                    cueKeys.Add(cue.TextKey);
                for (var i = 1; i <= 6; i++)
                {
                    if (!cueKeys.Contains("PIO.07.0" + i))
                        errors.Add($"{owner}: falta a legenda PIO.07.0{i}.");
                }
            }

            if (presentation == null)
                errors.Add($"{owner}: presentation (palco da PIO-003) não atribuída.");
            else if (!presentation.TryGetAnchor(StateId, out _))
                errors.Add($"{owner}: o palco não tem o anchor {StateId}.");

            if (dishRoot == null)
                errors.Add($"{owner}: dishRoot (DsnSilhouette) não atribuída.");
            if (panel == null)
                errors.Add($"{owner}: panel (ListeningPanelView) não atribuído.");
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
        public void PreviewState(ListeningViewState state)
        {
            if (panel != null)
                panel.Render(state, resolveText);
        }

        void OnDestroy() => CleanUp();
    }
}
