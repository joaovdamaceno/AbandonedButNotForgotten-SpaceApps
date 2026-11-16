using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Orientação para quem não joga (UI-005): tutorial por ações e assistência quando não há progresso. Cada instrução
    /// só some por uma ação REALMENTE observada (movimento pelo <see cref="InputRouter"/> ou por uma medida do capítulo;
    /// E pelo <c>HoldCompleted</c> do <see cref="IInteractionService"/>), nunca pelo tempo. Sem progresso: 12 s ativos
    /// reforçam a instrução e 30 s oferecem H, enquadramento ou o retorno ao corredor previsto. Nada é teleportado,
    /// resolvido ou concluído pela ajuda.
    /// <para>
    /// Não captura entrada: não pede contexto ao <see cref="InputRouter"/>, não chama BeginHold/CancelHold e só escuta
    /// eventos (H, hold concluído). O E do instrumento e o rearme continuam sendo do <see cref="InteractionService"/>. O
    /// relógio é o tempo ATIVO (<see cref="IActiveTimeSource"/>): pausa, dossiê e carregamento não contam.
    /// </para>
    /// <para>
    /// O tutorial pode ser repetido a qualquer momento (<see cref="Repeat"/>), mesmo marcado como visto: "visto" só decide
    /// se <see cref="BeginTutorial"/> sem <c>force</c> começa sozinho. Quem desenha é o <see cref="TutorialPresenter"/>.
    /// </para>
    /// Instalação (R1, INT-001): <c>Initialize(router, interactionService, sessionClock, chapterDirector, settingsStore)</c>;
    /// o capítulo chama <see cref="BeginTutorial"/> ou <see cref="BeginAssist"/> na etapa certa e <see cref="End"/> ao sair.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuidanceController : MonoBehaviour
    {
        public const string MoveStepId = "guide.move";
        public const string InteractStepId = "guide.interact";

        /// <summary>Entrada de movimento mínima (0–1) para contar como "mover" quando o capítulo não informa metros.</summary>
        const float MoveInputThreshold = 0.5f;

        [Header("Metas do tutorial")]
        [SerializeField, Min(0.1f), Tooltip("Segundos ativos de movimento pela entrada (WASD/setas) quando o capítulo não informa metros.")]
        float moveGoalSeconds = 0.5f;

        [SerializeField, Min(0.1f), Tooltip("Metros efetivos (colisão não conta) quando o capítulo informa SetMovementProbe.")]
        float moveGoalMeters = 1f;

        InputRouter router;
        IInteractionService interactions;
        IActiveTimeSource activeTime;
        ChapterDirector director;
        SettingsStore settings;
        Func<float> movementProbe;
        float lastProbeMeters;
        bool bound;

        GuidanceChecklist checklist;
        GuidanceOptions options = new GuidanceOptions();
        readonly GuidanceTimeline timeline = new GuidanceTimeline();
        ActionMeter moveSecondsMeter;
        ActionMeter moveMetersMeter;
        string sessionId;
        bool isTutorial;
        bool countsInPanel;
        bool running;
        GuidanceLevel level;

        /// <summary>Há orientação em andamento (instruções pendentes sendo observadas).</summary>
        public bool IsRunning => running;

        /// <summary>Todas as instruções da sessão atual foram cumpridas.</summary>
        public bool IsComplete => checklist != null && checklist.IsComplete;

        public GuidanceLevel Level => running ? level : GuidanceLevel.None;
        public string SessionId => sessionId;
        public bool IsTutorial => isTutorial;
        public bool CountsInPanel => countsInPanel;
        public float IdleSeconds => timeline.IdleSeconds;
        public float MoveGoalSeconds => moveGoalSeconds;
        public float MoveGoalMeters => moveGoalMeters;

        /// <summary>
        /// Estado para a interface. Depois de concluída a sessão continua visível por aqui (as instruções ficam cumpridas)
        /// até <see cref="End"/> ou uma nova sessão: assim o apresentador pode mostrar "feito ✓" antes de recolher.
        /// </summary>
        public GuidanceSnapshot Snapshot => new GuidanceSnapshot(checklist, Level, options.HelpAvailable, options.CorridorAvailable, isTutorial);

        /// <summary>Qualquer mudança que a interface precisa redesenhar: início, fim, instrução cumprida, nível.</summary>
        public event Action Changed;

        /// <summary>Uma instrução foi cumprida por uma ação observada. Argumento: ID da instrução.</summary>
        public event Action<string> StepCompleted;

        /// <summary>Todas as instruções foram cumpridas. Argumento: ID da sessão (do tutorial ou da atividade).</summary>
        public event Action<string> Completed;

        /// <summary>O nível subiu (reforço, oferta) ou voltou a normal por causa de uma ação.</summary>
        public event Action<GuidanceLevel> LevelChanged;

        /// <summary>H foi pressionado numa sessão em que o capítulo declarou tratá-lo.</summary>
        public event Action HelpUsed;

        /// <summary>
        /// Ligação por código (integração e testes). Só <paramref name="interactionService"/> é obrigatório. Sem
        /// <paramref name="activeTimeSource"/> o relógio não corre (nunca usar tempo de parede: a pausa contaria).
        /// </summary>
        public void Initialize(InputRouter inputRouter, IInteractionService interactionService, IActiveTimeSource activeTimeSource,
            ChapterDirector chapterDirector = null, SettingsStore settingsStore = null)
        {
            Unbind();
            router = inputRouter;
            interactions = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
            activeTime = activeTimeSource;
            director = chapterDirector;
            settings = settingsStore;
            if (isActiveAndEnabled)
                Bind();
        }

        /// <summary>
        /// Metros efetivos já percorridos (cumulativo, colisão não conta), ex.: <c>() => rover.DrivenMeters</c>. Sem sonda,
        /// "mover" mede segundos de entrada de movimento. Vale a partir da próxima leitura; nulo remove.
        /// </summary>
        public void SetMovementProbe(Func<float> cumulativeMeters)
        {
            movementProbe = cumulativeMeters;
            lastProbeMeters = cumulativeMeters != null ? cumulativeMeters() : 0f;
        }

        void OnEnable() => Bind();

        void OnDisable()
        {
            Unbind();
            End();
        }

        void Bind()
        {
            if (bound || interactions == null)
                return;

            bound = true;
            interactions.HoldStarted += OnHoldStarted;
            interactions.HoldCompleted += OnHoldCompleted;
            if (router != null)
                router.HelpDown += OnHelp;

            if (director != null)
            {
                director.StateChanged += OnStateChanged;
                director.ChapterUnloading += OnChapterUnloading;
            }
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            interactions.HoldStarted -= OnHoldStarted;
            interactions.HoldCompleted -= OnHoldCompleted;
            if (router != null)
                router.HelpDown -= OnHelp;

            if (director != null)
            {
                director.StateChanged -= OnStateChanged;
                director.ChapterUnloading -= OnChapterUnloading;
            }
        }

        // ---------- Início e fim ----------

        /// <summary>O tutorial <paramref name="tutorialId"/> ainda não foi visto (ou não há preferências para consultar).</summary>
        public bool ShouldAutoStart(string tutorialId) => settings == null || !settings.HasSeenTutorial(tutorialId);

        /// <summary>
        /// Começa o tutorial "mover e interagir". Sem <paramref name="force"/>, um tutorial já visto não começa (devolve
        /// false); com <paramref name="force"/> começa sempre. Recomeça do zero se já havia uma sessão.
        /// </summary>
        public bool BeginTutorial(string tutorialId, GuidanceOptions chapterOptions = null, bool force = false)
        {
            if (!ContractValidation.IsValidId(tutorialId))
                throw new ArgumentException($"ID de tutorial inválido: '{tutorialId}'.", nameof(tutorialId));

            if (!force && !ShouldAutoStart(tutorialId))
                return false;

            var steps = new[]
            {
                new GuidanceStep(MoveStepId, GuidanceKeys.GlyphMove, GuidanceKeys.StepMove, GuidanceKeys.StepMoveReinforce),
                new GuidanceStep(InteractStepId, GuidanceKeys.GlyphInteract, GuidanceKeys.StepInteractHold,
                    GuidanceKeys.StepInteractReinforceHold, GuidanceKeys.StepInteractPress, GuidanceKeys.StepInteractReinforcePress),
            };
            StartSession(new GuidanceChecklist(steps), tutorialId, chapterOptions, tutorial: true, inPanel: false);
            return true;
        }

        /// <summary>Repetição pedida pela pessoa (pausa, ajuda): igual a <see cref="BeginTutorial"/> com <c>force</c>, mesmo já visto.</summary>
        public void Repeat(string tutorialId, GuidanceOptions chapterOptions = null) =>
            BeginTutorial(tutorialId, chapterOptions, force: true);

        /// <summary>Começa a assistência de uma atividade (placa, sintonia ou varredura). Recomeça do zero se já havia uma sessão.</summary>
        public void BeginAssist(GuidanceTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            StartSession(new GuidanceChecklist(new[] { task.Step }), task.Step.Id, task.Options, tutorial: false, inPanel: task.CountsInPanel);
        }

        void StartSession(GuidanceChecklist list, string id, GuidanceOptions chapterOptions, bool tutorial, bool inPanel)
        {
            End();
            checklist = list;
            options = chapterOptions ?? new GuidanceOptions();
            sessionId = id;
            isTutorial = tutorial;
            countsInPanel = inPanel;
            timeline.NoteProgress();
            moveSecondsMeter = new ActionMeter(moveGoalSeconds);
            moveMetersMeter = new ActionMeter(moveGoalMeters);
            lastProbeMeters = movementProbe != null ? movementProbe() : 0f;
            level = GuidanceLevel.None;
            running = true;
            Changed?.Invoke();
        }

        /// <summary>Encerra a sessão e esconde a orientação. Idempotente; chamar ao sair da etapa ou do capítulo.</summary>
        public void End()
        {
            if (checklist == null && !running)
                return;

            running = false;
            checklist = null;
            sessionId = null;
            isTutorial = false;
            countsInPanel = false;
            options = new GuidanceOptions();
            level = GuidanceLevel.None;
            Changed?.Invoke();
        }

        // ---------- Sinais do capítulo ----------

        /// <summary>
        /// O capítulo observou uma ação real do jogador (ex.: mexeu o botão, escolheu uma peça). Zera a contagem de
        /// ociosidade. Não conclui nada.
        /// </summary>
        public void NoteProgress()
        {
            if (running)
                ProgressObserved();
        }

        /// <summary>
        /// Cumpre uma instrução porque o contrato do capítulo concluiu a ação (ex.: peça encaixada). Idempotente. A
        /// orientação nunca chama isto por conta própria para atividades: só quem tem o contrato sabe que concluiu.
        /// </summary>
        public bool CompleteStep(string stepId)
        {
            if (!running || checklist == null || !checklist.Complete(stepId))
                return false;

            timeline.NoteProgress();
            SetLevel(GuidanceLevel.None);
            StepCompleted?.Invoke(stepId);
            if (checklist != null && checklist.IsComplete)
                Finish();
            else
                Changed?.Invoke();

            return true;
        }

        void Finish()
        {
            // A sessão acaba, mas a checklist continua legível para o apresentador mostrar "feito ✓" antes de recolher.
            running = false;
            var id = sessionId;
            if (isTutorial && settings != null && ContractValidation.IsValidId(id))
                settings.MarkTutorialSeen(id);

            Changed?.Invoke();
            Completed?.Invoke(id);
        }

        // ---------- Observação ----------

        void Update() => Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);

        /// <summary>Mede as ações e a ociosidade com tempo ATIVO. Público para testes; Update chama com o relógio da sessão.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!running || !IsObserving())
                return;

            if (isTutorial && !checklist.IsDone(MoveStepId))
                ObserveMove(activeDeltaSeconds);

            if (!running)
                return;

            SetLevel(timeline.Advance(activeDeltaSeconds));
        }

        bool IsObserving()
        {
            if (director != null && (director.State != AppState.Playing || director.IsPaused))
                return false;

            if (router == null)
                return true;

            var context = router.Context;
            return context == InputContext.Gameplay || (countsInPanel && context == InputContext.Panel);
        }

        void ObserveMove(float activeDeltaSeconds)
        {
            if (movementProbe != null)
            {
                var total = movementProbe();
                var delta = total - lastProbeMeters;
                lastProbeMeters = total;
                if (delta > 0f)
                {
                    ProgressObserved();
                    if (moveMetersMeter.Add(delta))
                        CompleteStep(MoveStepId);
                }

                return;
            }

            if (router == null || router.Move.sqrMagnitude < MoveInputThreshold * MoveInputThreshold)
                return;

            ProgressObserved();
            if (moveSecondsMeter.Add(activeDeltaSeconds))
                CompleteStep(MoveStepId);
        }

        void ProgressObserved()
        {
            timeline.NoteProgress();
            SetLevel(GuidanceLevel.None);
        }

        void SetLevel(GuidanceLevel next)
        {
            if (next == level)
                return;

            level = next;
            LevelChanged?.Invoke(next);
            Changed?.Invoke();
        }

        void OnHoldStarted(string id)
        {
            // Segurar E no alvo é participação: a ajuda espera, sem concluir nada.
            if (running && IsObserving())
                ProgressObserved();
        }

        void OnHoldCompleted(string id)
        {
            if (!running || !IsObserving())
                return;

            if (isTutorial && checklist != null && !checklist.IsDone(InteractStepId)
                && (string.IsNullOrEmpty(options.InteractCandidateId) || options.InteractCandidateId == id))
            {
                CompleteStep(InteractStepId);
                return;
            }

            ProgressObserved();
        }

        void OnHelp()
        {
            // H já é tratado pelo capítulo (enquadrar o alvo). Aqui ele só conta como participação e recolhe a oferta.
            if (!running || !options.HelpAvailable)
                return;

            ProgressObserved();
            HelpUsed?.Invoke();
        }

        void OnStateChanged(AppState state)
        {
            if (state != AppState.Playing)
                End();
        }

        void OnChapterUnloading(string chapterId) => End();

        /// <summary>Ligações e metas. Vazio = pronto. Chamado pelo teste e pela integração.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(GuidanceController);
            if (interactions == null)
                ContractValidation.Report(errors, owner, "falta o IInteractionService (Initialize).");

            if (activeTime == null)
                ContractValidation.Report(errors, owner, "falta a fonte de tempo ativo (IActiveTimeSource): sem ela a ajuda nunca sobe.");

            if (router == null && movementProbe == null)
                ContractValidation.Report(errors, owner, "sem InputRouter nem sonda de movimento, 'mover' nunca é observado.");

            ContractValidation.RequirePositive(errors, owner, nameof(moveGoalSeconds), moveGoalSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(moveGoalMeters), moveGoalMeters);
            return errors;
        }
    }
}
