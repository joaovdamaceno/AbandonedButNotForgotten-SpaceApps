using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Dados de M22: marcos com data, odômetro, créditos de imagem e letreiros por chave. Fontes: NASA Spirit (7,73 km; último contato 22/3/2010; fim das buscas 25/5/2011).</summary>
    [CreateAssetMenu(fileName = "SpiritInterlude", menuName = "Abandoned/Spirit Interlude Data")]
    public sealed class SpiritInterludeData : ScriptableObject
    {
        [SerializeField] string sequenceId = "mars.seq.M22";
        [SerializeField] float odometerKilometers = SpiritInterludeChoreography.OdometerKilometers;
        [SerializeField] Vector3Int lastContactUtc = new Vector3Int(2010, 3, 22);
        [SerializeField] Vector3Int searchEndedUtc = new Vector3Int(2011, 5, 25);
        [SerializeField] List<string> imageCredits = new List<string>();
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();

        public string SequenceId => sequenceId;
        public float OdometerKilometers => odometerKilometers;
        public DateTime LastContact => new DateTime(lastContactUtc.x, lastContactUtc.y, lastContactUtc.z, 0, 0, 0, DateTimeKind.Utc);
        public DateTime SearchEnded => new DateTime(searchEndedUtc.x, searchEndedUtc.y, searchEndedUtc.z, 0, 0, 0, DateTimeKind.Utc);
        public IReadOnlyList<string> ImageCredits => imageCredits;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;

        public void Configure(IEnumerable<string> credits, IEnumerable<MarsLandingLabel> value)
        {
            imageCredits = new List<string>(credits);
            labels = new List<MarsLandingLabel>(value);
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            const string owner = nameof(SpiritInterludeData);
            ContractValidation.RequireId(errors, owner, nameof(sequenceId), sequenceId);
            if (Math.Abs(odometerKilometers - 7.73f) > 1e-4f)
                ContractValidation.Report(errors, owner, "odômetro deve ser 7,73 km (NASA).");
            if (LastContact != new DateTime(2010, 3, 22, 0, 0, 0, DateTimeKind.Utc) || SearchEnded != new DateTime(2011, 5, 25, 0, 0, 0, DateTimeKind.Utc))
                ContractValidation.Report(errors, owner, "último contato 22/3/2010 e fim das buscas 25/5/2011.");
            if (imageCredits.Count != 5 || imageCredits.Exists(string.IsNullOrWhiteSpace))
                ContractValidation.Report(errors, owner, "são cinco imagens oficiais, cada uma com a linha de crédito.");
            foreach (var label in labels)
            {
                ContractValidation.RequireId(errors, owner, "labels[].textKey", label.TextKey);
                if (label.StartSeconds < 0f || label.EndSeconds <= label.StartSeconds || label.EndSeconds > SpiritInterludeChoreography.TotalSeconds + 1e-3f)
                    ContractValidation.Report(errors, owner, $"letreiro '{label.TextKey}' fora de 0–45 s.");
            }

            return errors;
        }
    }
}
