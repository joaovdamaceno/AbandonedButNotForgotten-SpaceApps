using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.Examples
{
    /// <summary>
    /// MOCK de exemplo/teste de <see cref="IChapterDirector"/>. A implementação real (CORE-005) acrescenta o ciclo
    /// do aplicativo e o SessionClock; a semântica de etapas abaixo é a mesma que ela deve manter.
    /// </summary>
    public sealed class MockChapterDirector : IChapterDirector
    {
        readonly HashSet<string> completed = new HashSet<string>();

        public ChapterDefinition Current { get; private set; }
        public string CurrentStepId { get; private set; }
        public bool IsFinished { get; private set; }

        public event Action<string> StepEntered;
        public event Action<string> ChapterFinished;

        public void Begin(ChapterDefinition chapter)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));

            var errors = chapter.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Capítulo inválido:" + ContractValidation.Describe(errors), nameof(chapter));

            Current = chapter;
            completed.Clear();
            IsFinished = false;
            Enter(chapter.InitialStepId);
        }

        public bool CompleteStep(string stepId)
        {
            if (!CanComplete(stepId, out var step))
                return false;

            if (step.IsTerminal)
            {
                FinishChapter();
                return true;
            }

            if (step.NextStepIds.Count != 1)
                return false;

            Advance(stepId, step.NextStepIds[0]);
            return true;
        }

        public bool CompleteStep(string stepId, string nextStepId)
        {
            if (!CanComplete(stepId, out var step) || step.IsTerminal || !step.AllowsNext(nextStepId))
                return false;

            Advance(stepId, nextStepId);
            return true;
        }

        public void FinishChapter()
        {
            if (Current == null || IsFinished || !Current.IsTerminalStep(CurrentStepId))
                return;

            completed.Add(CurrentStepId);
            IsFinished = true;
            CurrentStepId = null;
            ChapterFinished?.Invoke(Current.ChapterId);
        }

        public void RestartChapter()
        {
            if (Current != null)
                Begin(Current);
        }

        bool CanComplete(string stepId, out ChapterStep step)
        {
            step = null;
            return Current != null && !IsFinished && stepId == CurrentStepId && !completed.Contains(stepId)
                && Current.TryGetStep(stepId, out step);
        }

        void Advance(string fromStepId, string toStepId)
        {
            completed.Add(fromStepId);
            Enter(toStepId);
        }

        void Enter(string stepId)
        {
            CurrentStepId = stepId;
            StepEntered?.Invoke(stepId);
        }
    }
}
