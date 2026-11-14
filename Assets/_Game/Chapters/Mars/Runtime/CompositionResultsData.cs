using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-005: parâmetros, IDs e chaves de M13 (APXS e Mössbauer na cavidade da RAT). O APXS informa ELEMENTOS (enxofre
    /// abundante); o Mössbauer identifica MINERAIS de ferro (jarosita); a RAT só prepara a superfície e não mede água (MS10).
    /// Resultados qualitativos: nenhum espectro numérico é inventado. Só chaves, nenhuma frase de jogo.
    /// </summary>
    [CreateAssetMenu(fileName = "CompositionResults", menuName = "Abandoned/Marte/Composition results")]
    public sealed class CompositionResultsData : ScriptableObject
    {
        public const float MinimumClearanceMeters = 0.10f;

        [Header("Tempo ATIVO, em segundos (roteiro M13: 6 + 6 + 6 = 18)")]
        [Min(0.1f), Tooltip("E sustentado do APXS, dentro dos 6 s do bloco.")] public float apxsHoldSeconds = 4f;
        [Min(0.1f)] public float mossbauerHoldSeconds = 4f;
        [Min(0.1f), Tooltip("A ferramenta desce até o contato com a cavidade.")] public float approachSeconds = 0.75f;
        [Min(0.1f), Tooltip("A ferramenta sobe pela normal até a folga antes de girar.")] public float retractSeconds = 0.75f;
        [Min(0.1f), Tooltip("Giro do revólver para a outra ferramenta, só depois da folga.")] public float rotateSeconds = 0.5f;
        [Min(0f), Tooltip("Leitura e recolhimento final.")] public float finalSeconds = 6f;

        [Header("Trajetória (metros)")]
        [Min(MinimumClearanceMeters), Tooltip("Folga pela normal antes de girar o braço. Nunca menos de 10 cm.")] public float retractClearanceMeters = 0.12f;
        [Min(0f), Tooltip("Distância ferramenta-superfície no contato (o APXS fica rente, nunca dentro da cavidade).")] public float contactStandoffMeters = 0.015f;

        [Header("Círculo E (verbo + objeto)")]
        public string apxsVerbKey = "MAR.VERB.apxs";
        public string mossbauerVerbKey = "MAR.VERB.mossbauer";
        public string cavityObjectKey = "MAR.OBJ.cavity";

        [Header("Evidências · APXS = elemento, Mössbauer = mineral, depois a descoberta (MS10, MS11)")]
        public string apxsEvidenceId = "mars.ev.apxs";
        public string apxsResultKey = "MAR.COMP.apxs";
        public string mossbauerEvidenceId = "mars.ev.mossbauer";
        public string mossbauerResultKey = "MAR.COMP.mossbauer";
        public string discoveryEvidenceId = "mars.ev.water";
        public string discoveryResultKey = "MAR.RESULT03";
        public string noteKey = "MAR.COMP.note";
        public string scienceRef = "MS10";
        public string discoveryScienceRef = "MS11";

        public float BlockSeconds(bool apxs) => (apxs ? apxsHoldSeconds : mossbauerHoldSeconds) + approachSeconds + retractSeconds + rotateSeconds;
        public float TotalSeconds => BlockSeconds(true) + BlockSeconds(false) + finalSeconds;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"CompositionResultsData '{name}'";
            ContractValidation.RequirePositive(errors, owner, nameof(apxsHoldSeconds), apxsHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(mossbauerHoldSeconds), mossbauerHoldSeconds);
            if (retractClearanceMeters < MinimumClearanceMeters)
                ContractValidation.Report(errors, owner, $"a folga antes de girar não pode ser menor que {MinimumClearanceMeters} m.");

            if (Mathf.Abs(BlockSeconds(true) - 6f) > 0.51f || Mathf.Abs(BlockSeconds(false) - 6f) > 0.51f)
                ContractValidation.Report(errors, owner, "cada medição ocupa 6 s de referência (aproximar, E, recuar e girar).");

            if (Mathf.Abs(TotalSeconds - 18f) > 1.01f)
                ContractValidation.Report(errors, owner, $"M13 soma {TotalSeconds} s; o roteiro pede 18 s.");

            foreach (var (field, value) in new[]
            {
                (nameof(apxsVerbKey), apxsVerbKey), (nameof(mossbauerVerbKey), mossbauerVerbKey), (nameof(cavityObjectKey), cavityObjectKey),
                (nameof(apxsEvidenceId), apxsEvidenceId), (nameof(apxsResultKey), apxsResultKey),
                (nameof(mossbauerEvidenceId), mossbauerEvidenceId), (nameof(mossbauerResultKey), mossbauerResultKey),
                (nameof(discoveryEvidenceId), discoveryEvidenceId), (nameof(discoveryResultKey), discoveryResultKey),
                (nameof(noteKey), noteKey), (nameof(scienceRef), scienceRef), (nameof(discoveryScienceRef), discoveryScienceRef),
            })
            {
                ContractValidation.RequireId(errors, owner, field, value);
            }

            if (apxsVerbKey == mossbauerVerbKey)
                ContractValidation.Report(errors, owner, "APXS e Mössbauer precisam de verbos distintos (elemento × mineral).");

            return errors;
        }

        /// <summary>Elementos medidos pelo APXS (enxofre abundante): composição qualitativa, sem espectro numérico.</summary>
        public EvidenceRecord ApxsRecord() => new EvidenceRecord(apxsEvidenceId, EvidenceKind.Composition, apxsResultKey, scienceRef, EvidenceAuthorship.Reconstruction);

        /// <summary>Mineral identificado pelo Mössbauer (jarosita): outra evidência, não a mesma do APXS.</summary>
        public EvidenceRecord MossbauerRecord() => new EvidenceRecord(mossbauerEvidenceId, EvidenceKind.Composition, mossbauerResultKey, scienceRef, EvidenceAuthorship.Reconstruction);

        /// <summary>Descoberta: evidências de água ANTIGA (nunca água atual nem vida). Observação, não foto.</summary>
        public EvidenceRecord DiscoveryRecord() => new EvidenceRecord(discoveryEvidenceId, EvidenceKind.Observation, discoveryResultKey, discoveryScienceRef, EvidenceAuthorship.Reconstruction);
    }
}
