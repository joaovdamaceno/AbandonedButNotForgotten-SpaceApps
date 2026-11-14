using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Bindings de edição do capítulo (MAR-018): cada uma das 14 etapas cinemáticas de <see cref="MarsChapterFlow"/> aponta para a sua
    /// <see cref="SequenceDefinition"/>, a região 3D ativa, os segundos de referência e os pontos de corte. As legendas vêm das próprias
    /// sequências (clock=sequence), nunca de C#. A MAR-009 consome este catálogo pelo <see cref="MarsSequenceConductor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "MarsChapterSequences", menuName = "Abandoned/Mars Chapter Sequences")]
    public sealed class MarsChapterSequenceCatalog : ScriptableObject
    {
        public const int ExpectedCinematicSteps = 14;
        public const float SlackToleranceSeconds = 0.01f;

        [SerializeField] List<MarsChapterSequenceEntry> entries = new List<MarsChapterSequenceEntry>();

        public IReadOnlyList<MarsChapterSequenceEntry> Entries => entries;

        public void SetEntries(IEnumerable<MarsChapterSequenceEntry> value) => entries = new List<MarsChapterSequenceEntry>(value);

        public MarsChapterSequenceEntry Find(string stepId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].StepId == stepId)
                    return entries[i];
            }

            return null;
        }

        /// <summary>Confere o catálogo contra as etapas cinemáticas reais do fluxo: ninguém sem sequência, nenhuma sobrando, durações iguais.</summary>
        public List<string> Validate(IReadOnlyList<MarsChapterFlow.StepBinding> bindings = null)
        {
            var errors = new List<string>();
            const string owner = nameof(MarsChapterSequenceCatalog);
            var steps = bindings ?? MarsChapterFlow.CreateDefaultBindings();
            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    ContractValidation.Report(errors, owner, "entrada nula.");
                    continue;
                }

                if (!seen.Add(entry.StepId))
                    ContractValidation.Report(errors, owner, $"etapa '{entry.StepId}' duplicada.");
                ContractValidation.RequireId(errors, owner, "regionId", entry.RegionId);
                if (entry.Sequence == null)
                {
                    ContractValidation.Report(errors, owner, $"etapa '{entry.StepId}' sem sequência.");
                    continue;
                }

                errors.AddRange(entry.Sequence.Validate());
                if (Math.Abs(entry.SlackSeconds) > SlackToleranceSeconds)
                    ContractValidation.Report(errors, owner, $"etapa '{entry.StepId}': sequência de {entry.Sequence.TotalDurationSeconds} s para referência de {entry.ReferenceSeconds} s.");
            }

            var cinematic = 0;
            foreach (var binding in steps)
            {
                if (!binding.IsCinematic)
                    continue;

                cinematic++;
                var entry = Find(binding.StepId);
                if (entry == null)
                {
                    ContractValidation.Report(errors, owner, $"etapa cinemática '{binding.StepId}' sem entrada.");
                    continue;
                }

                if (entry.Sequence != null && entry.Sequence.SequenceId != binding.SequenceId)
                    ContractValidation.Report(errors, owner, $"etapa '{binding.StepId}': sequência '{entry.Sequence.SequenceId}' difere de '{binding.SequenceId}'.");
                if (Math.Abs(entry.ReferenceSeconds - binding.ReferenceSeconds) > SlackToleranceSeconds)
                    ContractValidation.Report(errors, owner, $"etapa '{binding.StepId}': referência {entry.ReferenceSeconds} s difere de {binding.ReferenceSeconds} s do fluxo.");
            }

            if (entries.Count != cinematic)
                ContractValidation.Report(errors, owner, $"{entries.Count} entradas para {cinematic} etapas cinemáticas.");
            return errors;
        }
    }
}
