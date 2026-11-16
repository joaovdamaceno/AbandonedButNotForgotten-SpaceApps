using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>
    /// SUR-004: uma ordem em trânsito. Só atrasa o efeito físico; não adquire entrada nem avança etapas.
    /// O relógio acumulado da sessão evita consumir o delta do próprio frame em que a ordem foi enviada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorCommandDelay : MonoBehaviour
    {
        public const float OneWayDelaySeconds = 1.3f;

        ChapterDirector owner;
        SessionClock clock;
        Action operation;
        string stepId;
        bool bound, dispatching;
        uint dispatchGeneration;
        float sentActiveSeconds, elapsedSeconds;
        ulong serial;
        SurveyorCommandReceipt receipt;

        public event Action Changed;
        public event Action<SurveyorCommandReceipt> Sent;
        public event Action<SurveyorCommandReceipt> Received;
        public CommandTransitStatus Status { get; private set; }
        public uint Generation { get; private set; }
        public SurveyorCommandReceipt LastReceipt => receipt;
        public float ElapsedSeconds => elapsedSeconds;
        public float RemainingSeconds => Mathf.Max(0, OneWayDelaySeconds - elapsedSeconds);
        public float Progress => elapsedSeconds / OneWayDelaySeconds;
        public bool IsPaused => owner != null && owner.IsPaused;
        public bool HasPending => Status == CommandTransitStatus.InTransit;
        public bool OwnsState => owner != null && clock != null && stepId != null &&
            owner.State == AppState.Playing && owner.CurrentStepId == stepId && clock.IsRunning;
        public bool CanSend => isActiveAndEnabled && OwnsState && !IsPaused && !HasPending &&
            (!dispatching || dispatchGeneration != Generation);

        public void Configure(ChapterDirector chapter, SessionClock session)
        {
            Detach();
            owner = chapter != null ? chapter : throw new ArgumentNullException(nameof(chapter));
            clock = session != null ? session : throw new ArgumentNullException(nameof(session));
            if (owner.Clock != clock) throw new ArgumentException("Use o SessionClock do ChapterDirector.");
            Bind();
        }

        /// <summary>Chamado pelo consumidor ao entrar numa etapa real. Invalida qualquer operação anterior.</summary>
        public bool BeginState(string expectedStepId)
        {
            if (owner == null || owner.State != AppState.Playing || owner.CurrentStepId != expectedStepId ||
                !ContractValidation.IsValidId(expectedStepId)) return false;
            Invalidate();
            stepId = expectedStepId;
            Status = CommandTransitStatus.Ready;
            Changed?.Invoke();
            return true;
        }

        /// <summary>O callback deve capturar o alvo por valor e iniciar o efeito, sem aguardar outra confirmação.</summary>
        public bool TrySend(string commandId, Action execute)
        {
            if (!CanSend || execute == null || !ContractValidation.IsValidId(commandId)) return false;
            receipt = new SurveyorCommandReceipt(commandId, Generation, ++serial, Time.frameCount);
            var accepted = receipt;
            operation = execute;
            sentActiveSeconds = clock.ActiveSeconds;
            elapsedSeconds = 0;
            Status = CommandTransitStatus.InTransit;
            // Binding de HUD e confirmação são síncronos; não esperam 1,3 s ou outro Update.
            Changed?.Invoke();
            if (IsCurrent(accepted) && HasPending) Sent?.Invoke(accepted);
            return true;
        }

        void Update() => Tick();

        public void Tick()
        {
            if (!HasPending) return;
            if (!OwnsState) { EndState(); return; }
            if (IsPaused) return;
            float elapsed = Mathf.Clamp(clock.ActiveSeconds - sentActiveSeconds, 0, OneWayDelaySeconds);
            if (elapsed < OneWayDelaySeconds - .0001f)
            {
                if (Mathf.Approximately(elapsed, elapsedSeconds)) return;
                elapsedSeconds = elapsed;
                Changed?.Invoke();
                return;
            }

            elapsedSeconds = OneWayDelaySeconds;
            var arrived = receipt;
            var execute = operation;
            operation = null; // Consome ANTES de chamar código do instrumento: reentrada não repete o efeito.
            Status = CommandTransitStatus.Received;
            dispatching = true;
            dispatchGeneration = Generation;
            try
            {
                execute?.Invoke();
            }
            catch
            {
                if (IsCurrent(arrived)) { Status = CommandTransitStatus.Faulted; Changed?.Invoke(); }
                throw; // Erro de programação visível; a ordem consumida não é repetida no quadro seguinte.
            }
            finally { dispatching = false; }
            if (!IsCurrent(arrived)) return;
            Changed?.Invoke();
            if (IsCurrent(arrived)) Received?.Invoke(arrived);
        }

        public void EndState()
        {
            Invalidate();
            Status = CommandTransitStatus.Inactive;
            Changed?.Invoke();
        }

        void Invalidate()
        {
            unchecked { Generation++; }
            operation = null;
            stepId = null;
            elapsedSeconds = 0;
            receipt = default;
        }

        bool IsCurrent(SurveyorCommandReceipt value) => value.Generation == Generation &&
            value.Serial == receipt.Serial && OwnsState;
        void OnStep(string id) { if (stepId != null && id != stepId) EndState(); }
        void OnUnloading(string id) => EndState();
        void OnState(AppState state) { if (state != AppState.Playing) EndState(); }
        void OnPaused(bool value) => Changed?.Invoke();

        void Bind()
        {
            if (bound || owner == null) return;
            bound = true;
            owner.StepEntered += OnStep;
            owner.ChapterUnloading += OnUnloading;
            owner.StateChanged += OnState;
            owner.PausedChanged += OnPaused;
        }
        void Detach()
        {
            EndState();
            if (!bound) return;
            bound = false;
            owner.StepEntered -= OnStep;
            owner.ChapterUnloading -= OnUnloading;
            owner.StateChanged -= OnState;
            owner.PausedChanged -= OnPaused;
        }
        void OnEnable() => Bind();
        void OnDisable() => Detach();
    }
}
