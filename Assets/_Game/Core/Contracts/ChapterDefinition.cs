using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Dados de um capítulo: identidade, textos (por chave de localização), cena, tempo-alvo e tabela de etapas.
    /// É a única definição que o dossiê (UI-002) e os três diretores de capítulo consomem.
    /// </summary>
    [CreateAssetMenu(fileName = "Chapter", menuName = "Abandoned/Chapter Definition")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        public const float MaxTargetDurationSeconds = 600f;

        [Header("Identidade (obrigatório)")]
        [SerializeField, Tooltip("ID estável. Ex.: mars, pioneer10, surveyor3.")]
        string chapterId;

        [SerializeField, Tooltip("Chave de localização do título.")]
        string titleKey;

        [SerializeField, Tooltip("Chave de localização da descrição curta (destino/época).")]
        string descriptionKey;

        [SerializeField, Tooltip("Chave de localização da pergunta exibida na pasta do dossiê.")]
        string questionKey;

        [SerializeField, Tooltip("Capa do dossiê. Opcional enquanto a foto com crédito não chegou.")]
        Sprite cover;

        [Header("Execução (obrigatório)")]
        [SerializeField, Tooltip("Nome da cena do capítulo, como consta nas Build Settings.")]
        string sceneName;

        [SerializeField, Tooltip("Tempo-alvo ativo em segundos (referência editorial, nunca um cronômetro que conclui etapas).")]
        float targetDurationSeconds = 360f;

        [SerializeField, Tooltip("ID da sequência inicial, executada ao iniciar.")]
        string initialSequenceId;

        [Header("Etapas (a primeira é a inicial)")]
        [SerializeField]
        List<ChapterStep> steps = new List<ChapterStep>();

        public string ChapterId => chapterId;
        public string TitleKey => titleKey;
        public string DescriptionKey => descriptionKey;
        public string QuestionKey => questionKey;
        public Sprite Cover => cover;
        public string SceneName => sceneName;
        public float TargetDurationSeconds => targetDurationSeconds;
        public string InitialSequenceId => initialSequenceId;
        public IReadOnlyList<ChapterStep> Steps => steps;
        public string InitialStepId => steps.Count > 0 ? steps[0].StepId : null;

        /// <summary>Cria uma definição em memória (builders determinísticos e testes). Não valida: chame <see cref="Validate"/>.</summary>
        public static ChapterDefinition Create(string chapterId, string titleKey, string descriptionKey, string questionKey,
            string sceneName, float targetDurationSeconds, string initialSequenceId, IEnumerable<ChapterStep> steps, Sprite cover = null)
        {
            var definition = CreateInstance<ChapterDefinition>();
            definition.chapterId = chapterId;
            definition.titleKey = titleKey;
            definition.descriptionKey = descriptionKey;
            definition.questionKey = questionKey;
            definition.sceneName = sceneName;
            definition.targetDurationSeconds = targetDurationSeconds;
            definition.initialSequenceId = initialSequenceId;
            definition.cover = cover;
            definition.steps = new List<ChapterStep>(steps ?? new List<ChapterStep>());
            return definition;
        }

        public bool TryGetStep(string stepId, out ChapterStep step)
        {
            foreach (var candidate in steps)
            {
                if (candidate != null && candidate.StepId == stepId)
                {
                    step = candidate;
                    return true;
                }
            }

            step = null;
            return false;
        }

        /// <summary>True somente se a transição está na tabela explícita.</summary>
        public bool CanTransition(string fromStepId, string toStepId) =>
            TryGetStep(fromStepId, out var from) && from.AllowsNext(toStepId);

        public bool IsTerminalStep(string stepId) => TryGetStep(stepId, out var step) && step.IsTerminal;

        /// <summary>Lista as divergências; vazia = válida. Nunca lança por dado ausente.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"ChapterDefinition '{(string.IsNullOrEmpty(chapterId) ? name : chapterId)}'";

            ContractValidation.RequireId(errors, owner, nameof(chapterId), chapterId);
            ContractValidation.RequireId(errors, owner, nameof(titleKey), titleKey);
            ContractValidation.RequireId(errors, owner, nameof(descriptionKey), descriptionKey);
            ContractValidation.RequireId(errors, owner, nameof(questionKey), questionKey);
            ContractValidation.RequireText(errors, owner, nameof(sceneName), sceneName);
            ContractValidation.RequireId(errors, owner, nameof(initialSequenceId), initialSequenceId);
            ContractValidation.RequirePositive(errors, owner, nameof(targetDurationSeconds), targetDurationSeconds);
            if (targetDurationSeconds > MaxTargetDurationSeconds)
                ContractValidation.Report(errors, owner, $"'{nameof(targetDurationSeconds)}' acima de {MaxTargetDurationSeconds} s.");

            ValidateSteps(errors, owner);
            return errors;
        }

        void ValidateSteps(List<string> errors, string owner)
        {
            if (steps == null || steps.Count == 0)
            {
                ContractValidation.Report(errors, owner, "precisa de ao menos uma etapa.");
                return;
            }

            var ids = new HashSet<string>();
            var hasTerminal = false;
            foreach (var step in steps)
            {
                if (step == null)
                {
                    ContractValidation.Report(errors, owner, "há uma etapa nula na lista.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "steps[].stepId", step.StepId);
                if (ContractValidation.IsValidId(step.StepId) && !ids.Add(step.StepId))
                    ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' duplicada.");

                hasTerminal |= step.IsTerminal;
                if (step.IsTerminal && step.NextStepIds.Count > 0)
                    ContractValidation.Report(errors, owner, $"etapa terminal '{step.StepId}' não pode ter sucessoras.");
                if (!step.IsTerminal && step.NextStepIds.Count == 0)
                    ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' não é terminal e não tem sucessora (capítulo travaria).");
            }

            foreach (var step in steps)
            {
                if (step == null)
                    continue;

                foreach (var next in step.NextStepIds)
                {
                    if (next == step.StepId)
                        ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' aponta para si mesma.");
                    else if (!ids.Contains(next))
                        ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' aponta para '{next}', que não existe.");
                }
            }

            if (!hasTerminal)
            {
                ContractValidation.Report(errors, owner, "nenhuma etapa terminal: o capítulo não teria fim.");
                return;
            }

            var reachable = CollectReachable();
            foreach (var step in steps)
            {
                if (step != null && ContractValidation.IsValidId(step.StepId) && !reachable.Contains(step.StepId))
                    ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' é inalcançável a partir de '{InitialStepId}'.");
            }

            var terminalReachable = false;
            foreach (var step in steps)
                terminalReachable |= step != null && step.IsTerminal && reachable.Contains(step.StepId);

            if (!terminalReachable)
                ContractValidation.Report(errors, owner, "nenhuma etapa terminal é alcançável a partir da inicial.");
        }

        HashSet<string> CollectReachable()
        {
            var reachable = new HashSet<string>();
            var pending = new Stack<string>();
            if (InitialStepId != null)
                pending.Push(InitialStepId);

            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!reachable.Add(id) || !TryGetStep(id, out var step))
                    continue;

                foreach (var next in step.NextStepIds)
                    pending.Push(next);
            }

            return reachable;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var errors = Validate();
            if (errors.Count > 0)
                Debug.LogWarning($"[{nameof(ChapterDefinition)}] {name}:{ContractValidation.Describe(errors)}", this);
        }
#endif
    }
}
