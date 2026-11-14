using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Marca de um ator num instante da sequência (mesmo relógio dos planos). Metros e graus.</summary>
    [Serializable]
    public struct ActorMark
    {
        public string actorId;
        public float timeSeconds;
        public Vector3 positionMeters;
        public Vector3 eulerAnglesDegrees;

        public ActorMark(string actorId, float timeSeconds, Vector3 positionMeters, Vector3 eulerAnglesDegrees)
        {
            this.actorId = actorId;
            this.timeSeconds = timeSeconds;
            this.positionMeters = positionMeters;
            this.eulerAnglesDegrees = eulerAnglesDegrees;
        }
    }

    /// <summary>
    /// Sequência: planos em ordem, legendas e marcas de atores num ÚNICO relógio (<see cref="TotalDurationSeconds"/>).
    /// Só uma sequência controla a câmera e a entrada a cada instante (SequenceDirector, CORE-004).
    /// </summary>
    [CreateAssetMenu(fileName = "Sequence", menuName = "Abandoned/Sequence Definition")]
    public sealed class SequenceDefinition : ScriptableObject
    {
        [SerializeField] string sequenceId;
        [SerializeField] List<ShotDefinition> shots = new List<ShotDefinition>();
        [SerializeField] List<SubtitleCue> cues = new List<SubtitleCue>();
        [SerializeField] List<ActorMark> actorMarks = new List<ActorMark>();

        public string SequenceId => sequenceId;
        public IReadOnlyList<ShotDefinition> Shots => shots;
        public IReadOnlyList<SubtitleCue> Cues => cues;
        public IReadOnlyList<ActorMark> ActorMarks => actorMarks;

        public float TotalDurationSeconds
        {
            get
            {
                var total = 0f;
                foreach (var shot in shots)
                    total += shot?.DurationSeconds ?? 0f;
                return total;
            }
        }

        public static SequenceDefinition Create(string sequenceId, IEnumerable<ShotDefinition> shots,
            IEnumerable<SubtitleCue> cues = null, IEnumerable<ActorMark> actorMarks = null)
        {
            var definition = CreateInstance<SequenceDefinition>();
            definition.sequenceId = sequenceId;
            definition.shots = new List<ShotDefinition>(shots ?? new List<ShotDefinition>());
            definition.cues = new List<SubtitleCue>(cues ?? new List<SubtitleCue>());
            definition.actorMarks = new List<ActorMark>(actorMarks ?? new List<ActorMark>());
            return definition;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"SequenceDefinition '{(string.IsNullOrEmpty(sequenceId) ? name : sequenceId)}'";
            ContractValidation.RequireId(errors, owner, nameof(sequenceId), sequenceId);

            if (shots == null || shots.Count == 0)
                ContractValidation.Report(errors, owner, "precisa de ao menos um plano.");

            var shotIds = new HashSet<string>();
            foreach (var shot in shots ?? new List<ShotDefinition>())
            {
                if (shot == null)
                {
                    ContractValidation.Report(errors, owner, "há um plano nulo na lista.");
                    continue;
                }

                errors.AddRange(shot.Validate());
                if (ContractValidation.IsValidId(shot.ShotId) && !shotIds.Add(shot.ShotId))
                    ContractValidation.Report(errors, owner, $"plano '{shot.ShotId}' duplicado.");
            }

            var total = TotalDurationSeconds;
            var cueIds = new HashSet<string>();
            foreach (var cue in cues ?? new List<SubtitleCue>())
            {
                if (cue == null)
                {
                    ContractValidation.Report(errors, owner, "há uma legenda nula na lista.");
                    continue;
                }

                errors.AddRange(cue.Validate());
                if (ContractValidation.IsValidId(cue.CueId) && !cueIds.Add(cue.CueId))
                    ContractValidation.Report(errors, owner, $"legenda '{cue.CueId}' duplicada.");
                if (!cue.IsTriggered && cue.EndSeconds > total + ContractValidation.TimeToleranceSeconds)
                    ContractValidation.Report(errors, owner, $"legenda '{cue.CueId}' termina em {cue.EndSeconds} s, depois do fim da sequência ({total} s).");
            }

            foreach (var mark in actorMarks ?? new List<ActorMark>())
            {
                ContractValidation.RequireId(errors, owner, "actorMarks[].actorId", mark.actorId);
                if (mark.timeSeconds < 0f || mark.timeSeconds > total + ContractValidation.TimeToleranceSeconds)
                    ContractValidation.Report(errors, owner, $"marca do ator '{mark.actorId}' em {mark.timeSeconds} s está fora da sequência (0–{total} s).");
            }

            return errors;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var errors = Validate();
            if (errors.Count > 0)
                Debug.LogWarning($"[{nameof(SequenceDefinition)}] {name}:{ContractValidation.Describe(errors)}", this);
        }
#endif
    }
}
