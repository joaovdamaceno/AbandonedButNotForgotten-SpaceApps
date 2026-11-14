using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Etapa de um capítulo e suas transições explícitas. Os diretores avançam por ID, nunca somando 1 a um enum.
    /// A primeira etapa da lista é a inicial. Etapa terminal não tem sucessoras.
    /// </summary>
    [Serializable]
    public sealed class ChapterStep
    {
        [SerializeField, Tooltip("ID estável da etapa (obrigatório). Ex.: Launch, Belt, FirstImage.")]
        string stepId;

        [SerializeField, Tooltip("Etapas que podem vir depois. Vazio somente se for terminal.")]
        List<string> nextStepIds = new List<string>();

        [SerializeField, Tooltip("Marca o fim do capítulo. Precisa existir ao menos uma etapa terminal alcançável.")]
        bool isTerminal;

        public ChapterStep()
        {
        }

        public ChapterStep(string stepId, bool isTerminal, params string[] nextStepIds)
        {
            this.stepId = stepId;
            this.isTerminal = isTerminal;
            this.nextStepIds = new List<string>(nextStepIds ?? Array.Empty<string>());
        }

        public string StepId => stepId;
        public bool IsTerminal => isTerminal;
        public IReadOnlyList<string> NextStepIds => nextStepIds;

        public bool AllowsNext(string candidateStepId) => nextStepIds.Contains(candidateStepId);
    }
}
