using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Adapta o hold comum a uma ordem. Soltar/pausar conserva preparação; sair da etapa a descarta.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorCommandInteraction : MonoBehaviour
    {
        SurveyorCommandDelay queue;
        InteractionService interactions;
        InputRouter router;
        InteractionCandidate candidate;
        Func<Action> captureOperation;
        string commandId;
        uint generation;
        bool offered, bound;
        float preparedSeconds, attemptStart, totalSeconds;

        public event Action Changed;
        public float PreparationProgress => totalSeconds > 0 ? Mathf.Clamp01(preparedSeconds / totalSeconds) : 0;
        public bool IsHolding => offered && interactions.HoldingId == candidate.Id;
        public bool CanConfirm => offered && queue.CanSend && interactions.CanBeginHold(candidate.Id);
        public bool NeedsRelease => offered && interactions.AwaitingRelease;

        public void Configure(SurveyorCommandDelay delay, InteractionService service, InputRouter input)
        {
            Detach();
            queue = delay != null ? delay : throw new ArgumentNullException(nameof(delay));
            interactions = service != null ? service : throw new ArgumentNullException(nameof(service));
            router = input != null ? input : throw new ArgumentNullException(nameof(input));
            generation = queue.Generation;
            Bind();
        }

        /// <summary>Uma ação por vez. A fábrica captura a orientação atual somente quando a preparação termina.</summary>
        public bool Offer(InteractionCandidate action, Func<Action> capture)
        {
            if (offered || queue == null || !queue.CanSend || capture == null) return false;
            if (action.Validate().Count > 0 || action.Id.Length > 40) return false;
            commandId = action.Id;
            generation = queue.Generation;
            candidate = action;
            candidate.Id = "sur.cmd." + generation + "." + commandId;
            totalSeconds = action.HoldSeconds;
            preparedSeconds = 0;
            captureOperation = capture;
            offered = true;
            RegisterCandidate();
            Changed?.Invoke();
            return true;
        }

        void RegisterCandidate()
        {
            if (!offered) return;
            candidate.HoldSeconds = Mathf.Max(0, totalSeconds - preparedSeconds);
            interactions.SetCandidate(candidate);
            if (router.IsInteractPhysicallyHeld) interactions.RequireRelease();
        }
        public bool BeginConfirmation()
        {
            if (IsHolding && interactions.Mode == HoldMode.Toggle) { interactions.CancelHold(); return true; }
            return CanConfirm && interactions.BeginHold(candidate.Id);
        }
        public void ReleaseConfirmation()
        {
            if (interactions != null && interactions.Mode == HoldMode.Hold && IsHolding) interactions.CancelHold();
        }
        public void ClearAction() => ClearAction(false);
        void ClearAction(bool keepProgress)
        {
            bool wasOffered = offered;
            offered = false;
            captureOperation = null;
            if (wasOffered) interactions.ClearCandidate(candidate.Id);
            if (!keepProgress) { preparedSeconds = 0; totalSeconds = 0; }
            Changed?.Invoke();
        }

        bool Matches(string id) => offered && id == candidate.Id && generation == queue.Generation;
        void OnStarted(string id) { if (Matches(id)) { attemptStart = preparedSeconds; Changed?.Invoke(); } }
        void OnProgress(string id, float value)
        {
            if (!Matches(id)) return;
            preparedSeconds = attemptStart + (totalSeconds - attemptStart) * Mathf.Clamp01(value);
            Changed?.Invoke();
        }
        void OnCanceled(string id) { if (Matches(id)) { RegisterCandidate(); Changed?.Invoke(); } }
        void OnCompleted(string id)
        {
            if (!Matches(id)) return;
            preparedSeconds = totalSeconds;
            var capture = captureOperation;
            string acceptedId = commandId;
            uint token = generation;
            var execute = capture();
            if (token != queue.Generation) return;
            if (!queue.TrySend(acceptedId, execute)) RegisterCandidate();
            Changed?.Invoke();
        }
        void OnQueueChanged()
        {
            if (generation != queue.Generation)
            {
                generation = queue.Generation;
                ClearAction();
            }
            else if (queue.HasPending) ClearAction(true);
            else Changed?.Invoke();
        }

        void Bind()
        {
            if (bound || queue == null || interactions == null) return;
            bound = true;
            queue.Changed += OnQueueChanged;
            interactions.HoldStarted += OnStarted;
            interactions.HoldProgress += OnProgress;
            interactions.HoldCanceled += OnCanceled;
            interactions.HoldCompleted += OnCompleted;
        }
        void Detach()
        {
            ClearAction();
            if (!bound) return;
            bound = false;
            queue.Changed -= OnQueueChanged;
            interactions.HoldStarted -= OnStarted;
            interactions.HoldProgress -= OnProgress;
            interactions.HoldCanceled -= OnCanceled;
            interactions.HoldCompleted -= OnCompleted;
        }
        void OnEnable() => Bind();
        void OnDisable() => Detach();
    }
}
