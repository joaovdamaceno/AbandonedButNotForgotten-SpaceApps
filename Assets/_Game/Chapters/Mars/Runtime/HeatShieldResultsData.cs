using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-006: parâmetros, IDs e imagens reais creditadas de M16–M19 (escudo descartado e Heat Shield Rock).
    /// O escudo é HARDWARE descartado (explica o pouso); a rocha é um meteorito de ferro e níquel INDEPENDENTE: cada um tem
    /// objeto, verbo, evidência e imagem próprios. Nenhuma frase de jogo: só chaves (textos em <c>MarsShieldTexts.json</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "HeatShieldResults", menuName = "Abandoned/Marte/Heat shield results")]
    public sealed class HeatShieldResultsData : ScriptableObject
    {
        [Header("Alcance, em metros")]
        [Min(0.5f), Tooltip("Distância horizontal até a estação para dar a etapa por alcançada (M16 e M18).")] public float reachMeters = 1.5f;
        [Min(0.5f), Tooltip("Distância máxima para o prompt de E aparecer (M17 e M19).")] public float promptMeters = 3f;

        [Header("Tempo ATIVO de E, em segundos (roteiro: 4 s no escudo; 6 s + 4 s de resultado no meteorito)")]
        [Min(0.1f)] public float shieldPhotoHoldSeconds = 4f;
        [Min(0.1f)] public float meteoriteMeasureHoldSeconds = 6f;
        [Min(0f)] public float meteoriteResultSeconds = 4f;

        [Header("Círculo E (verbo + objeto): mesmo círculo, verbos e objetos distintos")]
        public string shieldVerbKey = "MAR.VERB.photograph";
        public string shieldObjectKey = "MAR.OBJ.shield";
        public string meteoriteVerbKey = "MAR.VERB.measure";
        public string meteoriteObjectKey = "MAR.OBJ.metalRock";

        [Header("Escudo (MS15, MS16) · hardware descartado")]
        public string shieldPhotoId = "mars.ev.shield.photo";
        public string shieldPhotoResultKey = "MAR.SHIELD.photo";
        public string shieldArchiveId = "mars.ev.shield.archive";
        public string shieldArchiveResultKey = "MAR.SHIELD.archive";
        public string shieldArchiveCreditKey = "MAR.CREDIT.shield";
        [Tooltip("PIA07402 (NASA/JPL/Cornell), até 960×540.")] public Sprite shieldArchiveImage;
        public string shieldScienceRef = "MS15";

        [Header("Meteorito (MS14) · rocha espacial independente do escudo")]
        public string meteoriteCompositionId = "mars.ev.meteorite.composition";
        public string meteoriteCompositionResultKey = "MAR.RESULT04";
        public string meteoritePhotoId = "mars.ev.meteorite.photo";
        public string meteoritePhotoResultKey = "MAR.METEORITE.photo";
        public string meteoriteArchiveId = "mars.ev.meteorite.archive";
        public string meteoriteArchiveResultKey = "MAR.METEORITE.archive";
        public string meteoriteArchiveCreditKey = "MAR.CREDIT.meteorite";
        [Tooltip("Imagem JPL de 19/1/2005, até 960×540.")] public Sprite meteoriteArchiveImage;
        public string meteoriteScienceRef = "MS14";

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"HeatShieldResultsData '{name}'";
            ContractValidation.RequirePositive(errors, owner, nameof(reachMeters), reachMeters);
            ContractValidation.RequirePositive(errors, owner, nameof(promptMeters), promptMeters);
            ContractValidation.RequirePositive(errors, owner, nameof(shieldPhotoHoldSeconds), shieldPhotoHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(meteoriteMeasureHoldSeconds), meteoriteMeasureHoldSeconds);
            ContractValidation.RequireNonNegative(errors, owner, nameof(meteoriteResultSeconds), meteoriteResultSeconds);
            if (shieldObjectKey == meteoriteObjectKey || shieldVerbKey == meteoriteVerbKey)
                ContractValidation.Report(errors, owner, "escudo e meteorito precisam de verbo e objeto distintos.");

            foreach (var (field, value) in new[]
            {
                (nameof(shieldVerbKey), shieldVerbKey), (nameof(shieldObjectKey), shieldObjectKey),
                (nameof(meteoriteVerbKey), meteoriteVerbKey), (nameof(meteoriteObjectKey), meteoriteObjectKey),
                (nameof(shieldPhotoId), shieldPhotoId), (nameof(shieldPhotoResultKey), shieldPhotoResultKey),
                (nameof(shieldArchiveId), shieldArchiveId), (nameof(shieldArchiveResultKey), shieldArchiveResultKey),
                (nameof(shieldArchiveCreditKey), shieldArchiveCreditKey), (nameof(shieldScienceRef), shieldScienceRef),
                (nameof(meteoriteCompositionId), meteoriteCompositionId), (nameof(meteoriteCompositionResultKey), meteoriteCompositionResultKey),
                (nameof(meteoritePhotoId), meteoritePhotoId), (nameof(meteoritePhotoResultKey), meteoritePhotoResultKey),
                (nameof(meteoriteArchiveId), meteoriteArchiveId), (nameof(meteoriteArchiveResultKey), meteoriteArchiveResultKey),
                (nameof(meteoriteArchiveCreditKey), meteoriteArchiveCreditKey), (nameof(meteoriteScienceRef), meteoriteScienceRef),
            })
            {
                ContractValidation.RequireId(errors, owner, field, value);
            }

            if (shieldArchiveImage == null)
                ContractValidation.Report(errors, owner, "falta a imagem real creditada do escudo.");

            if (meteoriteArchiveImage == null)
                ContractValidation.Report(errors, owner, "falta a imagem real creditada do meteorito.");

            return errors;
        }

        /// <summary>Foto feita pelo jogador no jogo (conta nas 3 do capítulo). Sem miniatura: a UI captura do quadro.</summary>
        public EvidenceRecord ShieldPhoto() => new EvidenceRecord(shieldPhotoId, EvidenceKind.Photo, shieldPhotoResultKey, shieldScienceRef, EvidenceAuthorship.PlayerCapture);

        /// <summary>Imagem real de arquivo, com crédito e data: nunca apresentada como do jogador.</summary>
        public EvidenceRecord ShieldArchive() => new EvidenceRecord(shieldArchiveId, EvidenceKind.Photo, shieldArchiveResultKey, shieldScienceRef,
            EvidenceAuthorship.Archive, shieldArchiveImage, shieldArchiveCreditKey);

        /// <summary>Composição qualitativa (ferro e níquel) por espectrômetros: reconstrução, sem números inventados.</summary>
        public EvidenceRecord MeteoriteComposition() => new EvidenceRecord(meteoriteCompositionId, EvidenceKind.Composition, meteoriteCompositionResultKey,
            meteoriteScienceRef, EvidenceAuthorship.Reconstruction);

        public EvidenceRecord MeteoritePhoto() => new EvidenceRecord(meteoritePhotoId, EvidenceKind.Photo, meteoritePhotoResultKey, meteoriteScienceRef, EvidenceAuthorship.PlayerCapture);

        public EvidenceRecord MeteoriteArchive() => new EvidenceRecord(meteoriteArchiveId, EvidenceKind.Photo, meteoriteArchiveResultKey, meteoriteScienceRef,
            EvidenceAuthorship.Archive, meteoriteArchiveImage, meteoriteArchiveCreditKey);
    }
}
