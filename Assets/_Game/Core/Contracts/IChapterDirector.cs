using System;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Contrato do <c>ChapterDirector</c> (implementação: CORE-005). Scripts de capítulo pedem avanço por este
    /// contrato; nunca alteram estado global nem escrevem Time.timeScale.
    /// </summary>
    public interface IChapterDirector
    {
        /// <summary>Capítulo em execução; nulo antes de <see cref="Begin"/>.</summary>
        ChapterDefinition Current { get; }

        /// <summary>Etapa atual; nulo antes de Begin e depois de FinishChapter.</summary>
        string CurrentStepId { get; }

        bool IsFinished { get; }

        /// <summary>Disparado ao entrar numa etapa (inclusive a inicial). Argumento: stepId.</summary>
        event Action<string> StepEntered;

        /// <summary>Disparado uma única vez por execução, ao concluir a etapa terminal. Argumento: chapterId.</summary>
        event Action<string> ChapterFinished;

        /// <summary>Inicia o capítulo na etapa inicial. Falha com <see cref="ArgumentException"/> se a definição for inválida.</summary>
        void Begin(ChapterDefinition chapter);

        /// <summary>
        /// Conclui <paramref name="stepId"/> e avança pela tabela de transições quando há uma única sucessora.
        /// Idempotente: repetir o ID de uma etapa já concluída devolve false e não altera nada.
        /// Um ID que não é o atual nem foi concluído nesta execução devolve false (sem exceção).
        /// Com várias sucessoras use a sobrecarga com <c>nextStepId</c>.
        /// </summary>
        bool CompleteStep(string stepId);

        /// <summary>Como <see cref="CompleteStep(string)"/>, escolhendo a sucessora. Devolve false se a transição não está na tabela.</summary>
        bool CompleteStep(string stepId, string nextStepId);

        /// <summary>Encerra o capítulo se a etapa atual é terminal; em qualquer outra etapa é ignorado. Idempotente. Concluir a etapa terminal por CompleteStep tem o mesmo efeito.</summary>
        void FinishChapter();

        /// <summary>Descarta o estado da execução e volta à etapa inicial, como se Begin fosse chamado de novo.</summary>
        void RestartChapter();
    }
}
