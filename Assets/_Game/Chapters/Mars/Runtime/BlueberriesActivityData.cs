using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-003: parâmetros e identificadores da observação das esferas (M08–M09). Tudo que é número de roteiro, chave de
    /// texto ou referência científica fica aqui, não no código. Nenhuma frase de jogo: só chaves.
    /// </summary>
    [CreateAssetMenu(fileName = "BlueberriesActivity", menuName = "Abandoned/Marte/Blueberries")]
    public sealed class BlueberriesActivityData : ScriptableObject
    {
        [Header("Aproximação (M08) e alcance do E (M09), em metros")]
        [Min(0.5f)] public float reachDistanceMeters = 4f;

        [Header("Tempo ATIVO de E, em segundos (roteiro M09: 4 + 4 + 4 de resultado)")]
        [Min(0f), Tooltip("E que ABRE a observação. Zero: conclui ao pressionar; é preciso soltar antes de operar.")] public float openHoldSeconds = 0f;
        [Min(0.1f)] public float soilHoldSeconds = 4f;
        [Min(0.1f)] public float rockHoldSeconds = 4f;
        [Min(0f), Tooltip("Resultado mineralógico na tela antes de concluir.")] public float resultSeconds = 4f;

        [Header("Círculo E (verbo + objeto)")]
        public string openVerbKey = "MAR.VERB.observe";
        public string soilVerbKey = "MAR.VERB.recordSoil";
        public string rockVerbKey = "MAR.VERB.recordRock";
        public string soilObjectKey = "MAR.OBJ.soil";
        public string rockObjectKey = "MAR.OBJ.rock";
        public string outcropObjectKey = "MAR.OBJ.outcrop";

        [Header("Evidências (EvidenceRecord) · MI mostra forma e distribuição; Mössbauer identifica o mineral")]
        public string soilEvidenceId = "mars.ev.spheres.soil";
        public string rockEvidenceId = "mars.ev.spheres.rock";
        public string mineralEvidenceId = "mars.ev.spheres.hematite";
        public string soilResultKey = "MAR.BLUE.mi.soil";
        public string rockResultKey = "MAR.BLUE.mi.rock";
        public string mineralResultKey = "MAR.RESULT01";
        [Tooltip("Matriz de fontes de 01_MARS_SOURCES.md.")] public string imagingScienceRef = "MS13";
        public string mineralScienceRef = "MS03";

        [Header("Amostras (conteúdo visual do prefab)")]
        [Min(1)] public int soilSphereCount = 14;
        [Min(1)] public int rockSphereCount = 10;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"BlueberriesActivityData '{name}'";
            ContractValidation.RequirePositive(errors, owner, nameof(reachDistanceMeters), reachDistanceMeters);
            ContractValidation.RequireNonNegative(errors, owner, nameof(openHoldSeconds), openHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(soilHoldSeconds), soilHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(rockHoldSeconds), rockHoldSeconds);
            ContractValidation.RequireNonNegative(errors, owner, nameof(resultSeconds), resultSeconds);
            if (Mathf.Abs(soilHoldSeconds + rockHoldSeconds + resultSeconds - 12f) > 4.01f)
                ContractValidation.Report(errors, owner, "M09 tem 12 s de referência (4 + 4 + 4); os tempos se afastam demais.");

            foreach (var (field, value) in new[]
            {
                (nameof(openVerbKey), openVerbKey), (nameof(soilVerbKey), soilVerbKey), (nameof(rockVerbKey), rockVerbKey),
                (nameof(soilObjectKey), soilObjectKey), (nameof(rockObjectKey), rockObjectKey), (nameof(outcropObjectKey), outcropObjectKey),
                (nameof(soilEvidenceId), soilEvidenceId), (nameof(rockEvidenceId), rockEvidenceId), (nameof(mineralEvidenceId), mineralEvidenceId),
                (nameof(soilResultKey), soilResultKey), (nameof(rockResultKey), rockResultKey), (nameof(mineralResultKey), mineralResultKey),
                (nameof(imagingScienceRef), imagingScienceRef), (nameof(mineralScienceRef), mineralScienceRef),
            })
            {
                ContractValidation.RequireId(errors, owner, field, value);
            }

            return errors;
        }

        /// <summary>Registro de uma macrovisão (MI): forma e distribuição, nunca composição. Não conta como foto do jogador.</summary>
        public EvidenceRecord ImagingRecord(bool soil) => new EvidenceRecord(
            soil ? soilEvidenceId : rockEvidenceId, EvidenceKind.Observation, soil ? soilResultKey : rockResultKey,
            imagingScienceRef, EvidenceAuthorship.Reconstruction);

        /// <summary>Resultado mineralógico (Mössbauer), apresentado à parte das imagens.</summary>
        public EvidenceRecord MineralRecord() => new EvidenceRecord(
            mineralEvidenceId, EvidenceKind.Composition, mineralResultKey, mineralScienceRef, EvidenceAuthorship.Reconstruction);
    }
}
