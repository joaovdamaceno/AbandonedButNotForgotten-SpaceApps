using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>As cinco partes da placa, na ordem das legendas PIO.05.02–06.</summary>
    public enum PlaquePiece
    {
        Hydrogen = 0,
        Pulsars = 1,
        SolarSystem = 2,
        PioneerScale = 3,
        People = 4,
    }

    public enum PlaquePhase
    {
        Idle = 0,
        /// <summary>0–9 s: a câmera chega à placa presa aos suportes; sem interação.</summary>
        Arrive = 1,
        /// <summary>9–38 s: cinco peças, cinco contornos.</summary>
        Fit = 2,
        /// <summary>5/5: a placa inteira; a revelação segue pelo relógio da sequência.</summary>
        Complete = 3,
        Done = 4,
    }

    /// <summary>Quem recebe as setas e o E: a escolha da peça ou a do contorno.</summary>
    public enum PlaqueMode
    {
        ChoosePiece = 0,
        ChooseOutline = 1,
    }

    /// <summary>Estado do prompt de E (UI_SPEC): vira UMA classe <c>sa-hold--*</c>; nenhum depende só de cor.</summary>
    public enum PlaqueHoldState
    {
        Available = 0,
        /// <summary>O E que abriu a atividade ainda está pressionado: nada começa até soltar.</summary>
        Rearm = 1,
        Holding = 2,
        /// <summary>Soltou E no meio da ajuda: os encaixes feitos continuam.</summary>
        Paused = 3,
        Done = 4,
    }

    public enum PlaqueResult
    {
        NotActive = 0,
        Placed = 1,
        WrongOutline = 2,
        NothingSelected = 3,
        AlreadyPlaced = 4,
        /// <summary>A peça foi selecionada (E ou clique); falta o contorno.</summary>
        Selected = 5,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct PlaqueViewState
    {
        public PlaquePhase Phase;
        /// <summary>Peças já encaixadas (0–5), para o progresso "n/5".</summary>
        public int Placed;
        /// <summary>Bit i = a peça i já está no seu contorno.</summary>
        public int PlacedBits;
        public PlaqueMode Mode;
        /// <summary>Peça selecionada (0–4) ou -1.</summary>
        public int Selected;
        /// <summary>Peça com o foco do teclado na bandeja (0–4) ou -1.</summary>
        public int FocusPiece;
        /// <summary>Contorno com o foco do teclado enquanto há peça selecionada (0–4) ou -1.</summary>
        public int FocusOutline;
        /// <summary>Contorno CORRETO da peça selecionada (pulsa) ou -1.</summary>
        public int HintOutline;
        /// <summary>Fase do pulso do contorno (alterna devagar; fica parada e ligada com movimento reduzido).</summary>
        public bool HintPulseOn;
        /// <summary>Mensagem neutra de contorno errado visível (a peça voltou; nada se perdeu).</summary>
        public bool RetryVisible;
        public int RetryOutline;
        /// <summary>Progresso da ajuda (segurar E, 0–1).</summary>
        public float AssistProgress;
        public PlaqueHoldState Hold;
        /// <summary>Versão só de cinema: o painel não mostra instruções de escolher peça nem de E.</summary>
        public bool Cinema;
    }

    /// <summary>
    /// PIO-009 · Estado PIO.Plaque (33 s): cinco peças da placa educativa e cinco contornos. Caminhos equivalentes e sem
    /// penalidade: clique peça→contorno, arrastar (snap de 96 px a 720p, decidido pelo painel), setas + E (setas percorrem as
    /// peças restantes, E seleciona, setas percorrem os contornos, E encaixa) e a ajuda de segurar E por 2 s, que encaixa a
    /// peça focada no contorno indicado (as cinco ≤ 30 s). O contorno correto da peça selecionada pulsa devagar; um contorno
    /// errado devolve a peça com uma mensagem neutra e nunca remove um encaixe. Cada encaixe aciona a legenda de 4 s da
    /// peça (<c>director.Trigger</c>: PIO.Plaque.*) e o próximo item já está disponível. A sequência espera 5/5 no marco de
    /// 37,5 s; nada conclui por tempo. A placa 3D continua presa à sonda: esta atividade não a move nem a oculta.
    /// </summary>
    public sealed class PioneerPlaqueActivity : MonoBehaviour
    {
        public const string StateId = "PIO.Plaque";
        public const string EventArrive = "PIO.Plaque.Arrive";
        public const string EventActivity = "PIO.Plaque.Activity";
        /// <summary>Em 37,5 s: a revelação (38 s) espera as cinco peças.</summary>
        public const string EventGate = "PIO.Plaque.Gate";
        public const string EventReveal = "PIO.Plaque.Reveal";
        public const string CandidateId = "PIO.Plaque.Place";
        public const string PromptKey = "PIO.UI.snap";
        public const int PieceCount = 5;
        /// <summary>Snap do arrastar: 96 px lógicos (a UI escala pela altura, então vale a 720p e nas outras resoluções).</summary>
        public const float SnapRadiusPixels = 96f;
        /// <summary>Segurar E por este tempo encaixa a peça focada (ajuda). Cinco peças ≤ 30 s.</summary>
        public const float AssistHoldSeconds = 2f;
        public const float MaxAssistedRunSeconds = 30f;
        /// <summary>Versão só de cinema: a primeira peça encaixa sozinha 1,5 s depois do início do encaixe, e as demais a cada 5,5 s (cada legenda dura 4 s).</summary>
        public const float CinemaFirstPieceSeconds = 0.4f;
        public const float CinemaPieceIntervalSeconds = 4f;
        /// <summary>Pressionar e soltar o E dentro deste tempo é um toque (seleciona ou encaixa).</summary>
        public const float TapMaxSeconds = 0.35f;
        public const float RetrySeconds = 3f;
        /// <summary>Período do pulso do contorno: 1,4 s (≈ 0,7 Hz, bem abaixo do limiar de flashes).</summary>
        public const float PulsePeriodSeconds = 1.4f;

        public static readonly string[] TriggerIds =
        {
            "PIO.Plaque.Hydrogen", "PIO.Plaque.Pulsars", "PIO.Plaque.SolarSystem", "PIO.Plaque.PioneerScale", "PIO.Plaque.People",
        };

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] PlaquePanelView panel;
        [SerializeField, Tooltip("Movimento reduzido: o contorno não pulsa, só fica destacado.")] bool reducedMotion;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        PlaquePhase phase;
        PlaqueMode mode;
        bool running, candidateOpen, holding, pausedByRelease, completedRaised, allRaised, revealRaised, skipped, navigateBound;
        string waitingGate;
        int placedBits, placed, selected = -1, focusPiece = -1, focusOutline = -1, wrongCount;
        float holdSeconds, pulseClock, retryTimer, cinemaClock;
        int retryOutline = -1;

        public event Action Completed;
        /// <summary>Peça encaixada: índice da peça e quantas já estão no lugar (1–5).</summary>
        public event Action<int, int> PieceFitted;
        /// <summary>Contorno errado: a peça voltou. Sem punição; só para a UI e o som.</summary>
        public event Action<int, int> WrongOutline;
        /// <summary>5/5: UMA vez por execução.</summary>
        public event Action AllPiecesFitted;
        /// <summary>Instante da revelação no relógio da sequência (38 s).</summary>
        public event Action RevealStarted;

        public SequenceDefinition Sequence => sequence;
        public PlaquePhase Phase => phase;
        public PlaqueMode Mode => mode;
        public bool IsRunning => running;
        public bool IsGateWaiting => waitingGate != null;
        public int PlacedCount => placed;
        public int SelectedPiece => selected;
        public int FocusPiece => focusPiece;
        public int FocusOutline => focusOutline;
        public int WrongAttempts => wrongCount;
        /// <summary>
        /// Roteiro v2 (cena 6 só de cinema): as cinco peças encaixam sozinhas, uma a cada <see cref="CinemaPieceIntervalSeconds"/>, sem
        /// pedir entrada e sem mostrar instruções de E. Ligado pelo controlador do capítulo; o padrão (falso) é a versão interativa.
        /// </summary>
        public bool CinemaMode { get; set; }

        public bool IsPlaced(int piece) => piece >= 0 && piece < PieceCount && (placedBits & (1 << piece)) != 0;
        public bool ReducedMotion => reducedMotion;
        public void SetReducedMotion(bool value) => reducedMotion = value;

        public void Initialize(SequenceDirector sequenceDirector, IInteractionService interactionService, InputRouter inputRouter,
            IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            CleanUp();
            director = sequenceDirector;
            interaction = interactionService;
            router = inputRouter;
            timeSource = activeTime;
            resolveText = textResolver;
            if (panel != null)
            {
                UnbindPanel();
                panel.PieceClicked += OnPieceClicked;
                panel.OutlineClicked += OnOutlineClicked;
                panel.PieceDropped += OnPieceDropped;
            }
        }

        void OnPieceClicked(int piece) => ClickPiece(piece);

        void OnOutlineClicked(int outline) => ClickOutline(outline);

        void OnPieceDropped(int piece, int outline) => DropPiece(piece, outline);

        void UnbindPanel()
        {
            if (panel == null)
                return;

            panel.PieceClicked -= OnPieceClicked;
            panel.OutlineClicked -= OnOutlineClicked;
            panel.PieceDropped -= OnPieceDropped;
        }

        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("PioneerPlaqueActivity sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            skipped = false;
            running = true;
            phase = PlaquePhase.Arrive;
            director.EventRaised += OnEventRaised;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor(StateId);
            }

            director.Play(sequence, skippable);
            if (!CinemaMode)
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
                case EventArrive:
                    if (phase == PlaquePhase.Idle)
                        phase = PlaquePhase.Arrive;
                    break;
                case EventActivity:
                    if (phase == PlaquePhase.Arrive)
                        StartFit();
                    break;
                case EventGate:
                    Gate(placed >= PieceCount);
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

        void Gate(bool satisfied)
        {
            if (satisfied || skipped || director == null || !director.IsPlaying)
                return;

            waitingGate = EventGate;
            director.Pause();
        }

        void ReleaseGate()
        {
            if (waitingGate == null)
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
            phase = completed ? PlaquePhase.Done : PlaquePhase.Idle;
            if (completed && !completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }
        }

        void StartFit()
        {
            phase = PlaquePhase.Fit;
            cinemaClock = 0f;
            mode = PlaqueMode.ChoosePiece;
            selected = -1;
            focusPiece = FirstRemaining(0);
            focusOutline = -1;
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

            if (CinemaMode && dt > 0f && phase == PlaquePhase.Fit && placed < PieceCount)
            {
                cinemaClock += dt;
                if (cinemaClock >= CinemaFirstPieceSeconds + placed * CinemaPieceIntervalSeconds)
                    AssistPlace();
            }

            if (dt > 0f)
            {
                if (holding)
                    holdSeconds += dt;
                retryTimer = Mathf.Max(0f, retryTimer - dt);
                if (!reducedMotion && selected >= 0)
                    pulseClock = (pulseClock + dt) % PulsePeriodSeconds;
            }

            PushView();
        }

        // ---------- Escolha e encaixe (cliques, arrastar, teclado) ----------

        int FirstRemaining(int from)
        {
            for (var i = 0; i < PieceCount; i++)
            {
                var piece = (from + i) % PieceCount;
                if (!IsPlaced(piece))
                    return piece;
            }

            return -1;
        }

        int NextRemaining(int current, int step)
        {
            for (var i = 1; i <= PieceCount; i++)
            {
                var piece = ((current + step * i) % PieceCount + PieceCount) % PieceCount;
                if (!IsPlaced(piece))
                    return piece;
            }

            return -1;
        }

        /// <summary>Seleciona a peça (clique, início do arrastar, E). Peça já encaixada ou fora da fase: falso.</summary>
        public bool SelectPiece(int piece)
        {
            if (phase != PlaquePhase.Fit || piece < 0 || piece >= PieceCount || IsPlaced(piece))
                return false;

            selected = piece;
            focusPiece = piece;
            mode = PlaqueMode.ChooseOutline;
            focusOutline = FirstRemaining(0);
            pulseClock = 0f;
            retryTimer = 0f;
            retryOutline = -1;
            PushView();
            return true;
        }

        /// <summary>Solta a seleção sem encaixar (clicar de novo na mesma peça).</summary>
        public void ClearSelection()
        {
            if (selected < 0)
                return;

            selected = -1;
            mode = PlaqueMode.ChoosePiece;
            focusOutline = -1;
            PushView();
        }

        /// <summary>Clique numa peça da bandeja: seleciona, ou solta a seleção se já era a escolhida.</summary>
        public void ClickPiece(int piece)
        {
            if (phase != PlaquePhase.Fit)
                return;

            if (selected == piece)
                ClearSelection();
            else
                SelectPiece(piece);
        }

        /// <summary>Clique num contorno: encaixa a peça selecionada (peça→contorno).</summary>
        public PlaqueResult ClickOutline(int outline) => PlaceOn(outline);

        /// <summary>
        /// Soltar um arrastar: <paramref name="outline"/> é o contorno a até 96 px (decidido pelo painel) ou -1 se solto
        /// longe de todos (a peça só volta, sem mensagem).
        /// </summary>
        public PlaqueResult DropPiece(int piece, int outline)
        {
            if (phase != PlaquePhase.Fit)
                return PlaqueResult.NotActive;
            if (!SelectPiece(piece))
                return IsPlaced(piece) ? PlaqueResult.AlreadyPlaced : PlaqueResult.NotActive;
            if (outline < 0)
            {
                ClearSelection();
                return PlaqueResult.NothingSelected;
            }

            return PlaceOn(outline);
        }

        /// <summary>Encaixa a peça selecionada no contorno. Errado: a peça volta e aparece a mensagem neutra, nada é perdido.</summary>
        public PlaqueResult PlaceOn(int outline)
        {
            if (phase != PlaquePhase.Fit)
                return PlaqueResult.NotActive;
            if (selected < 0)
                return PlaqueResult.NothingSelected;
            if (outline < 0 || outline >= PieceCount)
                return PlaqueResult.NothingSelected;
            if (IsPlaced(outline))
                return PlaqueResult.AlreadyPlaced;

            var piece = selected;
            if (outline != piece)
            {
                wrongCount++;
                retryTimer = RetrySeconds;
                retryOutline = outline;
                selected = -1;
                mode = PlaqueMode.ChoosePiece;
                focusPiece = piece;
                focusOutline = -1;
                WrongOutline?.Invoke(piece, outline);
                PushView();
                return PlaqueResult.WrongOutline;
            }

            Fit(piece);
            return PlaqueResult.Placed;
        }

        void Fit(int piece)
        {
            placedBits |= 1 << piece;
            placed++;
            selected = -1;
            mode = PlaqueMode.ChoosePiece;
            focusOutline = -1;
            retryTimer = 0f;
            retryOutline = -1;
            focusPiece = NextRemaining(piece, 1); // o próximo item já está disponível
            if (director != null && director.IsPlaying)
                director.Trigger(TriggerIds[piece]); // legenda de 4 s da peça (PIO.05.02–06)
            PieceFitted?.Invoke(piece, placed);
            if (placed >= PieceCount)
                FinishFit();
            PushView();
        }

        void FinishFit()
        {
            if (allRaised)
                return;

            allRaised = true;
            phase = PlaquePhase.Complete;
            focusPiece = -1;
            CloseInput();
            AllPiecesFitted?.Invoke();
            ReleaseGate();
        }

        // ---------- Teclado: setas + E ----------

        /// <summary>Seta (+1 = direita/baixo, -1 = esquerda/cima): percorre as peças restantes ou os contornos restantes.</summary>
        public void Navigate(int step)
        {
            if (phase != PlaquePhase.Fit || step == 0)
                return;

            if (mode == PlaqueMode.ChoosePiece)
            {
                focusPiece = focusPiece < 0 ? FirstRemaining(0) : NextRemaining(focusPiece, step > 0 ? 1 : -1);
            }
            else
            {
                focusOutline = focusOutline < 0 ? FirstRemaining(0) : NextRemaining(focusOutline, step > 0 ? 1 : -1);
            }

            PushView();
        }

        /// <summary>E (toque): seleciona a peça em foco; com peça selecionada, encaixa no contorno em foco.</summary>
        public PlaqueResult Confirm()
        {
            if (phase != PlaquePhase.Fit)
                return PlaqueResult.NotActive;

            if (mode == PlaqueMode.ChoosePiece)
                return SelectPiece(focusPiece) ? PlaqueResult.Selected : PlaqueResult.NotActive;
            return PlaceOn(focusOutline);
        }

        /// <summary>
        /// Ajuda (segurar E por 2 s): encaixa a peça selecionada, ou a focada, no contorno correto, o que já pulsa depois da
        /// seleção. Devolve falso fora da fase ou sem peça restante.
        /// </summary>
        public bool AssistPlace()
        {
            if (phase != PlaquePhase.Fit)
                return false;

            var piece = selected >= 0 ? selected : focusPiece;
            if (piece < 0 || IsPlaced(piece))
                return false;

            selected = piece;
            Fit(piece);
            return true;
        }

        void OnNavigate(Vector2Int direction)
        {
            var step = direction.x > 0 || direction.y < 0 ? 1 : direction.x < 0 || direction.y > 0 ? -1 : 0;
            Navigate(step);
        }

        // ---------- Entrada ----------

        void OpenInput()
        {
            if (inputLease == null && router != null)
                inputLease = router.Acquire(InputContext.Panel, "PIO.Plaque");
            if (router != null && !navigateBound)
            {
                navigateBound = true;
                router.NavigateDown += OnNavigate;
            }

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
                HoldSeconds = AssistHoldSeconds,
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id != CandidateId)
                return;

            holding = true;
            holdSeconds = 0f;
            pausedByRelease = false;
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId)
                return;

            var wasTap = holding && holdSeconds <= TapMaxSeconds;
            holding = false;
            if (wasTap)
            {
                Confirm();
                return;
            }

            pausedByRelease = placed > 0; // soltou no meio da ajuda: nada se perde
        }

        void OnHoldCompleted(string id)
        {
            if (id != CandidateId)
                return;

            holding = false;
            AssistPlace();
        }

        void CloseInput()
        {
            holding = false;
            if (navigateBound)
            {
                navigateBound = false;
                if (router != null)
                    router.NavigateDown -= OnNavigate;
            }

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
            running = pausedByRelease = allRaised = revealRaised = false;
            waitingGate = null;
            phase = PlaquePhase.Idle;
            mode = PlaqueMode.ChoosePiece;
            placedBits = placed = wrongCount = 0;
            selected = focusPiece = focusOutline = retryOutline = -1;
            holdSeconds = pulseClock = retryTimer = cinemaClock = 0f;
            PushView();
        }

        public PlaqueViewState ViewState()
        {
            return new PlaqueViewState
            {
                Phase = phase,
                Placed = placed,
                PlacedBits = placedBits,
                Mode = mode,
                Selected = selected,
                FocusPiece = focusPiece,
                FocusOutline = selected >= 0 ? focusOutline : -1,
                HintOutline = selected,
                HintPulseOn = reducedMotion || pulseClock < PulsePeriodSeconds * 0.5f,
                RetryVisible = retryTimer > 0f,
                RetryOutline = retryOutline,
                AssistProgress = holding ? Mathf.Clamp01(holdSeconds / AssistHoldSeconds) : 0f,
                Hold = HoldState(),
                Cinema = CinemaMode,
            };
        }

        /// <summary>
        /// Estado do prompt de E. O rearme vem do InputRouter, que já suprime o E pressionado quando o contexto muda:
        /// E fisicamente pressionado sem hold ativo = a atividade abriu com E pressionado, e nada começa até soltar.
        /// </summary>
        PlaqueHoldState HoldState()
        {
            if (phase == PlaquePhase.Complete || phase == PlaquePhase.Done)
                return PlaqueHoldState.Done;
            if (holding && holdSeconds > TapMaxSeconds)
                return PlaqueHoldState.Holding;
            if (holding)
                return PlaqueHoldState.Available; // ainda pode ser um toque
            if (router != null && router.IsInteractPhysicallyHeld)
                return PlaqueHoldState.Rearm;
            return pausedByRelease ? PlaqueHoldState.Paused : PlaqueHoldState.Available;
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): referências, sequência de 33 s com os marcos, as sete legendas (cinco
        /// por gatilho), anchor do palco e painel ligado ao UXML.
        /// </summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PioneerPlaqueActivity";
            if (sequence == null)
                errors.Add($"{owner}: sequence não atribuída.");
            else
            {
                errors.AddRange(sequence.Validate().ConvertAll(e => $"{owner}: {e}"));
                if (Mathf.Abs(sequence.TotalDurationSeconds - 33f) > 0.01f)
                    errors.Add($"{owner}: a sequência dura {sequence.TotalDurationSeconds} s; o roteiro v2 retemporizado pede 33 s.");

                var events = new HashSet<string>();
                foreach (var shot in sequence.Shots)
                {
                    if (shot.Events == null)
                        continue;
                    foreach (var shotEvent in shot.Events)
                        events.Add(shotEvent.eventId);
                }

                foreach (var required in new[] { EventArrive, EventActivity, EventGate, EventReveal })
                {
                    if (!events.Contains(required))
                        errors.Add($"{owner}: a sequência não dispara o evento '{required}'.");
                }

                var triggers = new HashSet<string>();
                var keys = new HashSet<string>();
                foreach (var cue in sequence.Cues)
                {
                    keys.Add(cue.TextKey);
                    if (cue.IsTriggered)
                        triggers.Add(cue.TriggerId);
                }

                for (var i = 0; i < PieceCount; i++)
                {
                    if (!triggers.Contains(TriggerIds[i]))
                        errors.Add($"{owner}: falta a legenda por gatilho '{TriggerIds[i]}' (PIO.05.0{i + 2}).");
                }

                foreach (var key in new[] { "PIO.05.01", "PIO.05.07" })
                {
                    if (!keys.Contains(key))
                        errors.Add($"{owner}: falta a legenda {key}.");
                }
            }

            if (presentation == null)
                errors.Add($"{owner}: presentation (palco da PIO-003) não atribuída.");
            else if (!presentation.TryGetAnchor(StateId, out _))
                errors.Add($"{owner}: o palco não tem o anchor {StateId}.");

            if (panel == null)
                errors.Add($"{owner}: panel (PlaquePanelView) não atribuído.");
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
        public void PreviewState(PlaqueViewState state)
        {
            if (panel != null)
                panel.Render(state, resolveText);
        }

        void OnDestroy()
        {
            CleanUp();
            UnbindPanel();
        }
    }
}
