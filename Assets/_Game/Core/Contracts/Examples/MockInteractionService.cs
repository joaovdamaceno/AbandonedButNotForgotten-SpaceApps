using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.Examples
{
    /// <summary>
    /// MOCK só para exemplos e testes de contrato (assembly de Editor, fora da distribuição). Implementa a
    /// semântica documentada em <see cref="IInteractionService"/> e NÃO substitui o InteractionService da CORE-003,
    /// que usa o SessionClock e o InputRouter. Não declarar a CORE-003 pronta por causa deste mock.
    /// </summary>
    public sealed class MockInteractionService : IInteractionService
    {
        readonly Dictionary<string, InteractionCandidate> candidates = new Dictionary<string, InteractionCandidate>();
        float elapsedSeconds;

        public bool AwaitingRelease { get; private set; }
        public string HoldingId { get; private set; }

        public event Action<string> HoldStarted;
        public event Action<string, float> HoldProgress;
        public event Action<string> HoldCompleted;
        public event Action<string> HoldCanceled;

        public bool TryGetActive(out InteractionCandidate candidate)
        {
            var found = false;
            candidate = default;
            foreach (var entry in candidates.Values)
            {
                if (!entry.Available)
                    continue;

                if (!found || entry.Priority > candidate.Priority
                    || (entry.Priority == candidate.Priority && entry.DistanceMeters < candidate.DistanceMeters))
                {
                    candidate = entry;
                    found = true;
                }
            }

            return found;
        }

        public void SetCandidate(InteractionCandidate candidate)
        {
            var errors = candidate.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Candidato de interação inválido:" + ContractValidation.Describe(errors));

            candidates[candidate.Id] = candidate;
            if (HoldingId != null && !(TryGetActive(out var active) && active.Id == HoldingId))
                CancelHold();
        }

        public void ClearCandidate(string id)
        {
            candidates.Remove(id);
            if (HoldingId == id)
                CancelHold();
        }

        public bool CanBeginHold(string id) =>
            HoldingId == null && !AwaitingRelease && TryGetActive(out var active) && active.Id == id;

        public bool BeginHold(string id)
        {
            if (!CanBeginHold(id))
                return false;

            HoldingId = id;
            elapsedSeconds = 0f;
            HoldStarted?.Invoke(id);
            return true;
        }

        public void CancelHold()
        {
            if (HoldingId == null)
                return;

            var id = HoldingId;
            HoldingId = null;
            elapsedSeconds = 0f;
            HoldCanceled?.Invoke(id);
        }

        public void RequireRelease() => AwaitingRelease = true;

        public void NotifyInteractReleased()
        {
            AwaitingRelease = false;
            CancelHold();
        }

        /// <summary>Avança o hold. A implementação real usa o SessionClock (tempo ativo, congelado na pausa).</summary>
        public void Tick(float deltaSeconds)
        {
            if (HoldingId == null)
                return;

            var id = HoldingId;
            elapsedSeconds += deltaSeconds;
            var holdSeconds = candidates[id].HoldSeconds;
            var progress = holdSeconds <= 0f ? 1f : Math.Min(1f, elapsedSeconds / holdSeconds);
            HoldProgress?.Invoke(id, progress);

            if (progress < 1f)
                return;

            HoldingId = null;
            elapsedSeconds = 0f;
            AwaitingRelease = true;
            HoldCompleted?.Invoke(id);
        }
    }
}
