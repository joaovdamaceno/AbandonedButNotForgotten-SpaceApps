using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Dados do pouso paralelo (MAR-014): durações de M04–M06, datas UTC dos pousos e do desembarque, letreiros por chave e a pose
    /// da câmera de condução para a entrega do controle. Datas conforme NASA GISS Mars24 (Spirit 4/1/2004, Opportunity 25/1/2004
    /// UTC) e JPL (desembarque da Opportunity em 31/1/2004). Tempo narrativo: o split compara datas diferentes, não é simultaneidade.
    /// </summary>
    [CreateAssetMenu(fileName = "MarsDualLanding", menuName = "Abandoned/Mars Dual Landing Data")]
    public sealed class MarsDualLandingData : ScriptableObject
    {
        [SerializeField] string splitSequenceId = "mars.seq.M04";
        [SerializeField] string eagleSequenceId = "mars.seq.M05";
        [SerializeField] string verificationSequenceId = "mars.seq.M06";
        [SerializeField, Min(1f)] float splitSeconds = MerLandingChoreography.SplitSeconds;
        [SerializeField, Min(1f)] float eagleSeconds = MerLandingChoreography.EagleSeconds;
        [SerializeField, Min(1f)] float verificationSeconds = MerLandingChoreography.VerificationSeconds;
        [SerializeField] Vector3Int spiritLandingUtc = new Vector3Int(2004, 1, 4);
        [SerializeField] Vector3Int opportunityLandingUtc = new Vector3Int(2004, 1, 25);
        [SerializeField] Vector3Int egressUtc = new Vector3Int(2004, 1, 31);
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();
        [SerializeField, Tooltip("Mesmos valores do MarsRoverController (followDistance, pivotHeight, defaultPitch) e do FOV da câmera de jogo.")] float gameplayFollowDistanceMeters = 5.5f;
        [SerializeField] float gameplayPivotHeightMeters = 0.9f;
        [SerializeField] float gameplayPitchDegrees = 16f;
        [SerializeField] float gameplayFieldOfViewDegrees = 60f;

        public string SplitSequenceId => splitSequenceId;
        public string EagleSequenceId => eagleSequenceId;
        public string VerificationSequenceId => verificationSequenceId;
        public float SplitSeconds => splitSeconds;
        public float EagleSeconds => eagleSeconds;
        public float VerificationSeconds => verificationSeconds;
        public float TotalSeconds => splitSeconds + eagleSeconds + verificationSeconds;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;
        public float GameplayFollowDistanceMeters => gameplayFollowDistanceMeters;
        public float GameplayPivotHeightMeters => gameplayPivotHeightMeters;
        public float GameplayPitchDegrees => gameplayPitchDegrees;
        public float GameplayFieldOfViewDegrees => gameplayFieldOfViewDegrees;

        public DateTime SpiritLanding => ToDate(spiritLandingUtc);
        public DateTime OpportunityLanding => ToDate(opportunityLandingUtc);
        public DateTime Egress => ToDate(egressUtc);
        public int DaysBetweenLandings => (int)(OpportunityLanding - SpiritLanding).TotalDays;

        static DateTime ToDate(Vector3Int ymd) => new DateTime(ymd.x, ymd.y, ymd.z, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Início global (segundos desde M04) da sequência, ou -1 se o ID não é do pouso.</summary>
        public float OffsetOf(string sequenceId)
        {
            if (sequenceId == splitSequenceId) return 0f;
            if (sequenceId == eagleSequenceId) return splitSeconds;
            if (sequenceId == verificationSequenceId) return splitSeconds + eagleSeconds;
            return -1f;
        }

        public void SetLabels(IEnumerable<MarsLandingLabel> value) => labels = new List<MarsLandingLabel>(value);

        public List<string> Validate()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsDualLandingData);
            ContractValidation.RequireId(errors, owner, nameof(splitSequenceId), splitSequenceId);
            ContractValidation.RequireId(errors, owner, nameof(eagleSequenceId), eagleSequenceId);
            ContractValidation.RequireId(errors, owner, nameof(verificationSequenceId), verificationSequenceId);
            if (Math.Abs(splitSeconds - MerLandingChoreography.SplitSeconds) > 1e-3f
                || Math.Abs(eagleSeconds - MerLandingChoreography.EagleSeconds) > 1e-3f
                || Math.Abs(verificationSeconds - MerLandingChoreography.VerificationSeconds) > 1e-3f)
                ContractValidation.Report(errors, owner, "as durações de M04–M06 devem ser 18/4/5 s (roteiro e coreografia).");
            if (DaysBetweenLandings != 21)
                ContractValidation.Report(errors, owner, $"intervalo entre os pousos {DaysBetweenLandings} dias; o roteiro e a NASA dizem 21.");
            if (SpiritLanding >= OpportunityLanding || Egress <= OpportunityLanding)
                ContractValidation.Report(errors, owner, "ordem de datas inválida (Spirit < Opportunity < desembarque).");
            if (gameplayFollowDistanceMeters <= 0f || gameplayFieldOfViewDegrees < ShotDefinition.MinFieldOfViewDegrees || gameplayFieldOfViewDegrees > ShotDefinition.MaxFieldOfViewDegrees)
                ContractValidation.Report(errors, owner, "parâmetros da câmera de condução inválidos.");

            foreach (var label in labels)
            {
                ContractValidation.RequireId(errors, owner, "labels[].textKey", label.TextKey);
                if (label.StartSeconds < 0f || label.EndSeconds <= label.StartSeconds || label.EndSeconds > TotalSeconds + 1e-3f)
                    ContractValidation.Report(errors, owner, $"letreiro '{label.TextKey}' fora de 0–{TotalSeconds} s.");
            }

            return errors;
        }
    }
}
