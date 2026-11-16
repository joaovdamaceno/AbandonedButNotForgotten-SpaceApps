using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Uma etapa cinemática do capítulo: a sequência, a região 3D que fica ativa, o tempo de referência e os pontos de corte documentados.</summary>
    [Serializable]
    public sealed class MarsChapterSequenceEntry
    {
        [SerializeField] string stepId;
        [SerializeField] SequenceDefinition sequence;
        [SerializeField, Tooltip("Região 3D ativa durante a etapa (as demais cinemáticas ficam desativadas).")] string regionId;
        [SerializeField, Tooltip("Segundos de referência da etapa em MarsChapterFlow (editorial).")] float referenceSeconds;
        [SerializeField, Tooltip("Instantes locais (s) em que há corte de plano, calculados dos planos.")] List<float> cutPoints = new List<float>();

        public MarsChapterSequenceEntry()
        {
        }

        public MarsChapterSequenceEntry(string stepId, SequenceDefinition sequence, string regionId, float referenceSeconds, IEnumerable<float> cutPoints)
        {
            this.stepId = stepId;
            this.sequence = sequence;
            this.regionId = regionId;
            this.referenceSeconds = referenceSeconds;
            this.cutPoints = new List<float>(cutPoints ?? new float[0]);
        }

        public string StepId => stepId;
        public SequenceDefinition Sequence => sequence;
        public string RegionId => regionId;
        public float ReferenceSeconds => referenceSeconds;
        public IReadOnlyList<float> CutPoints => cutPoints;

        /// <summary>Folga: duração da sequência menos a referência editorial (0 = exata).</summary>
        public float SlackSeconds => sequence != null ? sequence.TotalDurationSeconds - referenceSeconds : 0f;
    }
}
