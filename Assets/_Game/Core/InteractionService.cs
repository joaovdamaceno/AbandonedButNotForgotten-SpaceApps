using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Implementação de <see cref="IInteractionService"/>: liga o <see cref="InputRouter"/> (E) aos candidatos
    /// oferecidos pelos instrumentos. A UI pergunta <see cref="CanBeginHold"/> para mostrar E e a execução usa a
    /// MESMA condição, então prompt e ação nunca discordam.
    /// <para>
    /// Garantias: um pressionamento de E inicia no máximo um hold e nunca atravessa para a atividade seguinte
    /// (rearme até soltar); E mantido ao entrar numa área ou depois de uma pausa não inicia nada; soltar E, pausar,
    /// trocar de contexto ou perder foco cancelam o hold uma única vez; eventos de progresso só saem quando o valor muda.
    /// </para>
    /// </summary>
    public sealed class InteractionService : MonoBehaviour, IInteractionService
    {
        [SerializeField] InputRouter router;
        [SerializeField] HoldMode holdMode = HoldMode.Hold;

        readonly Dictionary<string, InteractionCandidate> candidates = new Dictionary<string, InteractionCandidate>();
        readonly HoldAction hold = new HoldAction();
        IActiveTimeSource timeSource;
        bool bound;
        bool awaitingRelease;
        float lastReportedProgress = -1f;

        public bool AwaitingRelease => awaitingRelease;
        public string HoldingId => hold.IsActive ? hold.Id : null;

        /// <summary>Acessibilidade: pressionar para iniciar e de novo para parar. Mudar cancela um hold em andamento.</summary>
        public HoldMode Mode
        {
            get => holdMode;
            set
            {
                if (holdMode == value)
                    return;

                CancelHold();
                holdMode = value;
            }
        }

        public event Action<string> HoldStarted;
        public event Action<string, float> HoldProgress;
        public event Action<string> HoldCompleted;
        public event Action<string> HoldCanceled;

        /// <summary>
        /// Liga o serviço. Sem fonte de tempo ativo o hold não avança (congelado), que é o lado seguro.
        /// O SessionClock (CORE-005) será a fonte; até lá, quem integra passa uma implementação de IActiveTimeSource.
        /// </summary>
        public void Initialize(InputRouter inputRouter, IActiveTimeSource activeTime)
        {
            Unbind();
            router = inputRouter;
            timeSource = activeTime;
            Bind();
        }

        void OnEnable() => Bind();

        void OnDisable() => Unbind();

        void Update()
        {
            if (timeSource != null)
                Tick(timeSource.ActiveDeltaSeconds);
        }

        void Bind()
        {
            if (bound || router == null)
                return;

            bound = true;
            router.InteractDown += OnInteractDown;
            router.InteractUp += OnInteractUp;
            router.ContextChanged += OnContextChanged;
            router.InputCleared += OnInputCleared;
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            router.InteractDown -= OnInteractDown;
            router.InteractUp -= OnInteractUp;
            router.ContextChanged -= OnContextChanged;
            router.InputCleared -= OnInputCleared;
            CancelHold();
            awaitingRelease = false;
        }

        // ---- IInteractionService ----

        public bool TryGetActive(out InteractionCandidate candidate)
        {
            var found = false;
            candidate = default;
            foreach (var entry in candidates.Values)
            {
                if (!entry.Available)
                    continue;

                if (!found || IsBetter(entry, candidate))
                {
                    candidate = entry;
                    found = true;
                }
            }

            return found;
        }

        static bool IsBetter(InteractionCandidate challenger, InteractionCandidate current)
        {
            if (challenger.Priority != current.Priority)
                return challenger.Priority > current.Priority;

            if (!Mathf.Approximately(challenger.DistanceMeters, current.DistanceMeters))
                return challenger.DistanceMeters < current.DistanceMeters;

            return string.CompareOrdinal(challenger.Id, current.Id) < 0;
        }

        public void SetCandidate(InteractionCandidate candidate)
        {
            var errors = candidate.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Candidato de interação inválido:" + ContractValidation.Describe(errors));

            candidates[candidate.Id] = candidate;
            if (hold.IsActive && !(TryGetActive(out var active) && active.Id == hold.Id))
                CancelHold();
        }

        public void ClearCandidate(string id)
        {
            if (id == null)
                return;

            candidates.Remove(id);
            if (hold.IsActive && hold.Id == id)
                CancelHold();
        }

        public bool CanBeginHold(string id) =>
            !hold.IsActive && !awaitingRelease && (router == null || router.AllowsInteract)
            && TryGetActive(out var active) && active.Id == id;

        public bool BeginHold(string id)
        {
            if (!CanBeginHold(id))
                return false;

            hold.Begin(id, candidates[id].HoldSeconds);
            lastReportedProgress = -1f;
            HoldStarted?.Invoke(id);
            return true;
        }

        public void CancelHold()
        {
            if (!hold.IsActive)
                return;

            var id = hold.Id;
            hold.Reset();
            lastReportedProgress = -1f;
            HoldCanceled?.Invoke(id);
        }

        public void RequireRelease()
        {
            // Só há o que esperar se a tecla ainda está pressionada; senão o rearme ficaria preso até o próximo ciclo.
            if (router == null || router.IsInteractPhysicallyHeld)
                awaitingRelease = true;
        }

        public void NotifyInteractReleased()
        {
            awaitingRelease = false;
            CancelHold();
        }

        /// <summary>Avança o hold com tempo ATIVO (zero na pausa). Chamado por Update com o IActiveTimeSource.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!hold.IsActive)
                return;

            var id = hold.Id;
            var completed = hold.Advance(activeDeltaSeconds);
            var progress = completed ? 1f : hold.Progress;
            if (!Mathf.Approximately(progress, lastReportedProgress))
            {
                lastReportedProgress = progress;
                HoldProgress?.Invoke(id, progress);
            }

            if (!completed)
                return;

            hold.Reset();
            lastReportedProgress = -1f;
            RequireRelease();
            HoldCompleted?.Invoke(id);
        }

        // ---- Entrada ----

        void OnInteractDown()
        {
            if (holdMode == HoldMode.Toggle && hold.IsActive)
            {
                CancelHold();
                return;
            }

            if (TryGetActive(out var active))
                BeginHold(active.Id);
        }

        void OnInteractUp()
        {
            if (holdMode == HoldMode.Hold)
            {
                NotifyInteractReleased();
                return;
            }

            // Alternar: soltar a tecla apenas libera o rearme; o hold continua até o segundo pressionamento.
            awaitingRelease = false;
        }

        void OnContextChanged(InputContext context)
        {
            CancelHold();
            awaitingRelease = false;
        }

        void OnInputCleared()
        {
            CancelHold();
            awaitingRelease = false;
        }
    }
}
