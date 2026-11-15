using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum OutboundPhase
    {
        Idle = 0,
        /// <summary>0–12 s: diagrama orbital com o arco de saída e o cartão 1976 (distância orbital de Saturno).</summary>
        Saturn1976 = 1,
        /// <summary>12–24 s: cartão 13 JUN 1983 (distância orbital de Netuno): além das órbitas dos planetas.</summary>
        Neptune1983 = 2,
        /// <summary>24 s: a sequência espera o hold de 3 s que prioriza a comunicação.</summary>
        Power = 3,
        /// <summary>Depois do hold: o calendário chega a 1998 e o gráfico compara a distância ao Sol de duas rotas.</summary>
        Chart1998 = 4,
        Done = 5,
    }

    /// <summary>Estado do prompt de E (UI_SPEC): vira UMA classe <c>sa-hold--*</c>; nenhum depende só de cor.</summary>
    public enum OutboundHoldState
    {
        Available = 0,
        /// <summary>O E que abriu a atividade ainda está pressionado: nada começa até soltar.</summary>
        Rearm = 1,
        Holding = 2,
        /// <summary>Soltou E com progresso adquirido: ele fica.</summary>
        Paused = 3,
        Done = 4,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct OutboundViewState
    {
        public OutboundPhase Phase;
        /// <summary>0–1: progresso do hold de 3 s.</summary>
        public float HoldProgress;
        public OutboundHoldState Hold;
        /// <summary>A comunicação foi priorizada (hold concluído): economia confirmada.</summary>
        public bool PowerConfirmed;
        /// <summary>Quantos anéis de órbita a sonda já ultrapassou em distância (0–4: Júpiter, Saturno, Urano, Netuno).</summary>
        public int OrbitsCrossed;
        /// <summary>Versão só de cinema: o painel não mostra o prompt de E.</summary>
        public bool Cinema;
    }

    /// <summary>
    /// PIO-010 · Estado PIO.Outbound (55 s): o caminho continua. Toca <c>PIO_Outbound</c> (diagrama orbital 2D com cartões de
    /// 1976 e 13 JUN 1983, síntese da economia de energia e gráfico de distâncias de 1998) e conduz o hold de 3 s que
    /// prioriza a comunicação. A sequência espera o hold (marco <see cref="EventPower"/>), sem concluir sozinha.
    /// <para>
    /// Limites científicos: cruzar a DISTÂNCIA orbital de Saturno ou de Netuno não é visitá-los (nenhum planeta é colocado
    /// junto à sonda) e ir além das órbitas não é sair do Sistema Solar nem cruzar a heliopausa. A Voyager 1 ultrapassa a
    /// DISTÂNCIA AO SOL da Pioneer em 1998, sem encontro e sem segunda sonda 3D. As barras de energia são relativas, sem
    /// números, e a economia é uma síntese dos comandos da equipe ao longo dos anos, não uma lista de desligamentos.
    /// </para>
    /// Regras de entrada (as mesmas da PIO-004): o E já pressionado antes do cartão não conta; soltar E pausa sem perder o
    /// progresso; pausa do shell congela tudo; cancelar ou pular encerra hold, contexto e candidato;
    /// <see cref="Completed"/> dispara uma vez (pular conta; cancelar não). Errar não existe: não há falha possível.
    /// </summary>
    public sealed class OutboundAtlas : MonoBehaviour
    {
        public const string StateId = "PIO.Outbound";
        public const string EventCard1976 = "PIO.Outbound.Card1976";
        public const string EventCard1983 = "PIO.Outbound.Card1983";
        public const string EventPower = "PIO.Outbound.Power";
        public const string EventChart = "PIO.Outbound.Chart";
        public const string CandidateId = "PIO.Outbound.Power";
        /// <summary>Linha do HUD "Segure E · priorizar a comunicação" (PIO.UI.power, roteiro).</summary>
        public const string PromptKey = "PIO.UI.power";
        public const float HoldSeconds = 3f;
        /// <summary>Anéis do esquema (Júpiter, Saturno, Urano, Netuno). Esquemático, sem escala.</summary>
        public const int OrbitRings = 4;

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] OutboundAtlasView panel;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        OutboundPhase phase;
        bool running, pendingHold, cinemaHold, awaitingHold, holdDone, completedRaised, holding;
        float acquiredSeconds;
        float holdProgress;

        /// <summary>Estado concluído (sequência terminou ou foi pulada). Dispara uma vez por <see cref="Begin"/>.</summary>
        public event Action Completed;
        /// <summary>A comunicação foi priorizada (hold concluído). UMA vez por execução.</summary>
        public event Action PowerConfirmed;

        public SequenceDefinition Sequence => sequence;
        public OutboundPhase Phase => phase;
        public bool IsRunning => running;
        /// <summary>
        /// Roteiro v2 (cena 7 só de cinema): a priorização da comunicação corre sozinha em <see cref="HoldSeconds"/> segundos, sem pedir E
        /// e sem pausar a sequência. Ligado pelo controlador do capítulo; o padrão (falso) é a versão interativa.
        /// </summary>
        public bool CinemaMode { get; set; }

        /// <summary>
        /// Roteiro v2 (cena 7 como "Os anos"): o painel do atlas fica escondido e o HUD de cinema (<see cref="PioneerYearsHud"/>) desenha o
        /// ano, a linha do tempo, os instrumentos e o medidor de energia sobre a sonda em 3D. A lógica (fases, hold, conclusão) segue igual.
        /// Ligado pelo controlador do capítulo; o padrão (falso) mostra o atlas.
        /// </summary>
        public bool YearsMode { get; set; }

        public bool IsAwaitingHold => awaitingHold;
        public bool HoldDone => holdDone;
        public float HoldProgress => holdProgress;

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
                throw new InvalidOperationException("OutboundAtlas sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            running = true;
            phase = OutboundPhase.Saturn1976; // o primeiro quadro já mostra o cartão de 1976
            director.EventRaised += OnEventRaised;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor(StateId);
            }

            director.Play(sequence, skippable);
            PushView();
        }

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
                case EventCard1976:
                    if (phase == OutboundPhase.Idle || phase == OutboundPhase.Saturn1976)
                        phase = OutboundPhase.Saturn1976;
                    break;
                case EventCard1983:
                    if (phase <= OutboundPhase.Neptune1983)
                        phase = OutboundPhase.Neptune1983;
                    break;
                case EventPower:
                    // Adiado ao próximo Tick: ao pular, a sequência dispara os eventos restantes e termina antes dele.
                    if (CinemaMode)
                        cinemaHold = !holdDone;
                    else
                        pendingHold = !holdDone;
                    if (phase < OutboundPhase.Power)
                        phase = OutboundPhase.Power;
                    break;
                case EventChart:
                    if (phase < OutboundPhase.Chart1998)
                        phase = OutboundPhase.Chart1998;
                    break;
            }

            PushView();
        }

        void OnFinished(SequenceResult result)
        {
            var completed = result.Reason != SequenceExitReason.Canceled;
            CleanUp();
            phase = completed ? OutboundPhase.Done : OutboundPhase.Idle;
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

        /// <summary>Abre o hold pendente e atualiza a UI. O hold avança no InteractionService (tempo ativo). Testes chamam direto.</summary>
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
                    PowerConfirmed?.Invoke();
                }
            }

            PushView();
        }

        void OpenHold()
        {
            director.Pause();
            awaitingHold = true;
            inputLease = router != null ? router.Acquire(InputContext.Panel, "PIO.Outbound.Power") : null;
            if (interaction != null)
            {
                interaction.HoldStarted += OnHoldStarted;
                interaction.HoldProgress += OnHoldProgress;
                interaction.HoldCanceled += OnHoldCanceled;
                interaction.HoldCompleted += OnHoldCompleted;
                // Rearme: a troca de contexto (Cinematic → Panel) já faz o InputRouter ignorar um E mantido desde antes.
                OfferCandidate();
            }
        }

        void OfferCandidate()
        {
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                VerbKey = PromptKey,
                ObjectKey = PromptKey,
                Available = true,
                HoldSeconds = Mathf.Max(0.05f, HoldSeconds - acquiredSeconds),
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id == CandidateId)
                holding = true;
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

            holding = false;
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
            holding = false;
            CloseHold();
            PowerConfirmed?.Invoke();
            if (director != null && director.IsPlaying)
                director.Resume();
            PushView();
        }

        void CloseHold()
        {
            awaitingHold = false;
            if (interaction != null)
            {
                interaction.HoldStarted -= OnHoldStarted;
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
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            running = pendingHold = cinemaHold = holdDone = holding = false;
            acquiredSeconds = holdProgress = 0f;
            phase = OutboundPhase.Idle;
            PushView();
        }

        public OutboundViewState ViewState() => new OutboundViewState
        {
            Phase = phase,
            HoldProgress = holdProgress,
            Hold = HoldState(),
            PowerConfirmed = holdDone,
            Cinema = CinemaMode,
            OrbitsCrossed = phase switch
            {
                OutboundPhase.Saturn1976 => 2,
                OutboundPhase.Idle => 0,
                _ => OrbitRings,
            },
        };

        OutboundHoldState HoldState()
        {
            if (holdDone)
                return OutboundHoldState.Done;
            if (holding || cinemaHold)
                return OutboundHoldState.Holding;
            if (awaitingHold && router != null && router.IsInteractPhysicallyHeld)
                return OutboundHoldState.Rearm;
            return holdProgress > 0f ? OutboundHoldState.Paused : OutboundHoldState.Available;
        }

        /// <summary>Erros de montagem (lista vazia = conforme).</summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "OutboundAtlas";
            if (sequence == null)
                errors.Add($"{owner}: sequence não atribuída.");
            else
            {
                errors.AddRange(sequence.Validate().ConvertAll(e => $"{owner}: {e}"));
                if (Mathf.Abs(sequence.TotalDurationSeconds - 55f) > 0.01f)
                    errors.Add($"{owner}: a sequência dura {sequence.TotalDurationSeconds} s; o roteiro v2 retemporizado pede 55 s.");

                var events = new System.Collections.Generic.HashSet<string>();
                foreach (var shot in sequence.Shots)
                {
                    if (shot.Events == null)
                        continue;
                    foreach (var shotEvent in shot.Events)
                        events.Add(shotEvent.eventId);
                }

                foreach (var required in new[] { EventCard1976, EventCard1983, EventPower, EventChart })
                {
                    if (!events.Contains(required))
                        errors.Add($"{owner}: a sequência não dispara o evento '{required}'.");
                }

                var cueKeys = new System.Collections.Generic.HashSet<string>();
                foreach (var cue in sequence.Cues)
                    cueKeys.Add(cue.TextKey);
                for (var i = 1; i <= 5; i++)
                {
                    if (!cueKeys.Contains("PIO.06.0" + i))
                        errors.Add($"{owner}: falta a legenda PIO.06.0{i}.");
                }
            }

            if (presentation == null)
                errors.Add($"{owner}: presentation (palco da PIO-003) não atribuída.");
            else if (!presentation.TryGetAnchor(StateId, out _))
                errors.Add($"{owner}: o palco não tem o anchor {StateId}.");

            if (panel == null)
                errors.Add($"{owner}: panel (OutboundAtlasView) não atribuído.");
            else
                errors.AddRange(panel.ValidateSetup());

            return errors;
        }

        void PushView()
        {
            if (panel != null)
                panel.Render(YearsMode ? new OutboundViewState { Phase = OutboundPhase.Idle } : ViewState(), resolveText);
        }

        /// <summary>Prévia de Editor: mostra uma situação com valores fixos, sem tocar a sequência.</summary>
        public void PreviewState(OutboundViewState state)
        {
            if (panel != null)
                panel.Render(state, resolveText);
        }

        void OnDestroy() => CleanUp();
    }
}
