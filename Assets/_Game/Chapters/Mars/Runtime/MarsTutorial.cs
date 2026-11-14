using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Itens do tutorial de M07: o jogador conclui cada um agindo de verdade, em qualquer ordem.</summary>
    public enum MarsTutorialItem
    {
        Move = 0,
        Look = 1,
        Interact = 2,
    }

    /// <summary>
    /// MAR-002 · M07: tutorial por ações reais. Cada item só conclui por uma ação medida:
    /// dirigir <c>moveGoalMeters</c> (2 m efetivos, colisão não conta), olhar alguns graus (mouse, ou H, que enquadra o
    /// alvo: quem não usa mouse não fica preso) e completar o E no alvo de inspeção já visível. Concluídos recolhem
    /// para o canto; só os pendentes ficam centrais (a UI lê <see cref="IsDone"/> e <see cref="PendingItems"/>).
    /// <para>
    /// Quando os três estão feitos, envia <c>mars.tutorial.done</c> ao <see cref="MarsChapterFlow"/> (uma vez por tentativa).
    /// O tutorial nunca conclui por tempo: 12 s sem progresso reforçam a indicação, 25 s oferecem a ajuda de
    /// enquadramento (H), sem fazer a ação pela pessoa. Pode ser repetido (<see cref="Begin"/>/<see cref="Repeat"/>), mesmo
    /// marcado como visto. O E é um candidato do <see cref="InteractionService"/>: prompt e execução usam a mesma distância.
    /// </para>
    /// Textos PT/EN estão em <c>MarsTutorialTexts.json</c> (chaves em <see cref="ItemKey"/>); nenhuma frase fica em C#.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsTutorial : MonoBehaviour
    {
        public const string TutorialStepId = "M07";
        public const string SeenKey = "mars.tutorial";
        public const string InspectCandidateId = "mars.tutorial.inspect";
        public const string InspectVerbKey = "MAR.VERB.inspect";
        public const string InspectObjectKey = "MAR.OBJ.module";

        public const string MoveKey = "MAR.TUT.move";
        public const string LookKey = "MAR.TUT.look";
        public const string InteractKey = "MAR.TUT.interact";
        public const string ReinforceKey = "MAR.TUT.reinforce";
        public const string HelpKey = "MAR.TUT.help";

        const int InteractPriority = 10;

        [Header("Ligações (preenchidas pela integração; testes usam Initialize)")]
        [SerializeField] MarsRoverController rover;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField, Tooltip("Alvo de inspeção já visível junto do módulo. Âncora do prompt de E.")] Transform inspectionTarget;

        [Header("Metas (metros, segundos, graus)")]
        [SerializeField, Min(0.5f)] float moveGoalMeters = 2f;
        [SerializeField, Min(1f)] float lookGoalDegrees = 8f;
        [SerializeField, Min(0.5f)] float interactRangeMeters = 3.5f;
        [SerializeField, Min(0f), Tooltip("Segurar E por este tempo ativo ensina o gesto antes da primeira amostra.")] float holdSeconds = 0.6f;

        readonly bool[] done = new bool[3];
        readonly MarsRoverController.AssistTimer assist = new MarsRoverController.AssistTimer();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        SettingsStore settings;
        bool bound;
        bool running;
        bool completedRaised;
        bool candidateAvailable;
        bool candidateRegistered;
        float baselineDriven;
        float baselineLooked;
        MarsAssistLevel lastLevel;

        /// <summary>A tentativa está em andamento (entre <see cref="Begin"/> e <see cref="End"/>).</summary>
        public bool IsRunning => running;

        /// <summary>Os três itens foram cumpridos nesta tentativa.</summary>
        public bool IsComplete => done[0] && done[1] && done[2];

        public MarsAssistLevel AssistLevel => running ? assist.Level : MarsAssistLevel.None;
        public float MoveGoalMeters => moveGoalMeters;
        public float LookGoalDegrees => lookGoalDegrees;
        public Transform InspectionTarget => inspectionTarget;

        public event Action<MarsTutorialItem> ItemCompleted;
        public event Action Completed;
        public event Action<MarsAssistLevel> AssistChanged;

        /// <summary>Ligação por código (testes, integração). O serviço de interação é a interface do contrato.</summary>
        public void Initialize(MarsRoverController roverController, MarsChapterFlow chapterFlow, IInteractionService interactionContract,
            IActiveTimeSource activeTimeSource, SettingsStore settingsStore = null)
        {
            Unbind();
            rover = roverController;
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            settings = settingsStore;
            if (isActiveAndEnabled)
                Bind();
        }

        public void SetInspectionTarget(Transform target) => inspectionTarget = target;

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
                interaction.HoldCompleted += OnHoldCompleted;

            if (rover != null)
                rover.FrameRequested += OnFrameRequested;

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
                interaction.HoldCompleted -= OnHoldCompleted;

            if (rover != null)
                rover.FrameRequested -= OnFrameRequested;

            if (flow != null)
            {
                flow.StepStarted -= OnStepStarted;
                flow.StepEnded -= OnStepEnded;
            }

            End();
        }

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding != null && binding.StepId == TutorialStepId)
                Begin();
        }

        void OnStepEnded(string stepId)
        {
            if (stepId == TutorialStepId)
                End();
        }

        // ---- Ciclo da tentativa ----

        /// <summary>Começa (ou recomeça) o tutorial do zero: itens pendentes, contagens a partir de agora, ajuda zerada.</summary>
        public void Begin()
        {
            End();
            if (rover == null)
                return;

            running = true;
            completedRaised = false;
            Array.Clear(done, 0, done.Length);
            baselineDriven = rover.DrivenMeters;
            baselineLooked = rover.LookedDegrees;
            assist.NoteProgress();
            lastLevel = MarsAssistLevel.None;
            candidateAvailable = false;
            if (inspectionTarget != null)
                rover.SetFrameTarget(inspectionTarget);
        }

        /// <summary>Repetição pedida pela pessoa (ajuda, menu): igual a <see cref="Begin"/>, mesmo com o tutorial já visto.</summary>
        public void Repeat() => Begin();

        /// <summary>Encerra a tentativa: remove o candidato de E e solta o alvo de enquadramento. Idempotente.</summary>
        public void End()
        {
            if (!running && !candidateRegistered)
                return;

            running = false;
            ClearCandidate();
            if (rover != null && rover.FrameTarget == inspectionTarget)
                rover.SetFrameTarget(null);

            if (lastLevel != MarsAssistLevel.None)
            {
                lastLevel = MarsAssistLevel.None;
                AssistChanged?.Invoke(MarsAssistLevel.None);
            }
        }

        public bool IsDone(MarsTutorialItem item) => done[(int)item];

        public IEnumerable<MarsTutorialItem> PendingItems()
        {
            for (var i = 0; i < done.Length; i++)
            {
                if (!done[i])
                    yield return (MarsTutorialItem)i;
            }
        }

        public static string ItemKey(MarsTutorialItem item)
        {
            switch (item)
            {
                case MarsTutorialItem.Move: return MoveKey;
                case MarsTutorialItem.Look: return LookKey;
                default: return InteractKey;
            }
        }

        // ---- Quadro ----

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Mede as ações e a ajuda com tempo ATIVO. Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!running || rover == null)
                return;

            if (!done[(int)MarsTutorialItem.Move] && rover.DrivenMeters - baselineDriven >= moveGoalMeters)
                Complete(MarsTutorialItem.Move);

            if (!done[(int)MarsTutorialItem.Look] && rover.LookedDegrees - baselineLooked >= lookGoalDegrees)
                Complete(MarsTutorialItem.Look);

            RefreshCandidate();

            if (!running)
                return;

            var level = assist.Advance(activeDeltaSeconds);
            if (level != lastLevel)
            {
                lastLevel = level;
                AssistChanged?.Invoke(level);
            }
        }

        void OnFrameRequested()
        {
            // H é o olhar sem mouse: enquadrar o alvo cumpre o item de olhar, e nada além disso.
            if (running && !done[(int)MarsTutorialItem.Look])
                Complete(MarsTutorialItem.Look);
        }

        void OnHoldCompleted(string id)
        {
            if (running && id == InspectCandidateId && !done[(int)MarsTutorialItem.Interact])
                Complete(MarsTutorialItem.Interact);
        }

        void Complete(MarsTutorialItem item)
        {
            if (done[(int)item])
                return;

            done[(int)item] = true;
            assist.NoteProgress();
            if (lastLevel != MarsAssistLevel.None)
            {
                lastLevel = MarsAssistLevel.None;
                AssistChanged?.Invoke(MarsAssistLevel.None);
            }

            if (item == MarsTutorialItem.Interact)
                ClearCandidate();

            ItemCompleted?.Invoke(item);
            if (!IsComplete || completedRaised)
                return;

            completedRaised = true;
            settings?.MarkTutorialSeen(SeenKey);
            Completed?.Invoke();
            if (flow != null)
                flow.Raise(MarsChapterFlow.Events.TutorialDone);
        }

        // ---- Candidato de E ----

        void RefreshCandidate()
        {
            if (interaction == null || inspectionTarget == null || done[(int)MarsTutorialItem.Interact])
                return;

            var delta = inspectionTarget.position - rover.transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            var available = distance <= interactRangeMeters;
            if (available == candidateAvailable && candidateRegistered)
                return;

            // SetCandidate valida e aloca: só quando a disponibilidade muda, nunca por quadro.
            candidateAvailable = available;
            candidateRegistered = true;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = InspectCandidateId,
                Anchor = inspectionTarget,
                VerbKey = InspectVerbKey,
                ObjectKey = InspectObjectKey,
                Available = available,
                DistanceMeters = distance,
                HoldSeconds = holdSeconds,
                Priority = InteractPriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            candidateAvailable = false;
            interaction?.ClearCandidate(InspectCandidateId);
        }

        /// <summary>Referências, metas e chaves de texto. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsTutorial);
            if (rover == null)
                ContractValidation.Report(errors, owner, "falta MarsRoverController.");

            if (inspectionTarget == null)
                ContractValidation.Report(errors, owner, "falta o alvo de inspeção (âncora visível junto do módulo).");

            ContractValidation.RequirePositive(errors, owner, nameof(moveGoalMeters), moveGoalMeters);
            ContractValidation.RequirePositive(errors, owner, nameof(lookGoalDegrees), lookGoalDegrees);
            ContractValidation.RequirePositive(errors, owner, nameof(interactRangeMeters), interactRangeMeters);
            ContractValidation.RequireNonNegative(errors, owner, nameof(holdSeconds), holdSeconds);
            foreach (var key in new[] { MoveKey, LookKey, InteractKey, ReinforceKey, HelpKey, InspectVerbKey, InspectObjectKey })
                ContractValidation.RequireId(errors, owner, "chave de texto", key);

            return errors;
        }
    }
}
