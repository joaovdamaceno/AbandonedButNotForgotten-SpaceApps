using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-008: parâmetros, IDs e chaves de M24–M30 (mirante, Sol, vale, antena e escolha da memória). Tamanhos de imagem,
    /// tempos e referências ficam aqui; nenhuma frase de jogo. Orçamento das fotos do jogador em
    /// <c>Docs/Production/MARS_EVIDENCE_BUDGET.md</c>: só o vale é foto do jogador; o Sol vira registro científico.
    /// </summary>
    [CreateAssetMenu(fileName = "FinalSurvey", menuName = "Abandoned/Marte/Final survey")]
    public sealed class FinalSurveyData : ScriptableObject
    {
        public const int MaxImageWidth = 960;
        public const int MaxImageHeight = 540;

        [Header("Mirante (M24) e alcance, em metros")]
        [Min(0.5f)] public float reachMeters = 3f;

        [Header("Tempo ATIVO de E, em segundos")]
        [Min(0.1f)] public float sunHoldSeconds = 4f;
        [Min(0.1f)] public float valleyHoldSeconds = 4f;
        [Min(0.1f), Tooltip("M29: E sustentado orienta só a antena.")] public float antennaHoldSeconds = 4f;

        [Header("Imagens (até 960×540). Cada captura do Sol é metade da comparação lado a lado")]
        [Range(64, 480)] public int sunWidth = 480;
        [Range(64, 270)] public int sunHeight = 270;
        [Range(64, MaxImageWidth)] public int valleyWidth = 960;
        [Range(64, MaxImageHeight)] public int valleyHeight = 540;

        [Header("Ajuste opcional do quadro do vale (graus) · nunca bloqueia o avanço")]
        [Range(0f, 30f)] public float maxFrameAdjustDegrees = 15f;

        [Header("Antena (M29)")]
        [Min(1f), Tooltip("Velocidade máxima de giro da articulação. Nunca instantâneo.")] public float antennaDegreesPerSecond = 60f;

        [Header("Círculo E (verbo + objeto)")]
        public string sunVerbKey = "MAR.VERB.captureSun";
        public string sunObjectKey = "MAR.OBJ.sun";
        public string valleyVerbKey = "MAR.VERB.captureValley";
        public string valleyObjectKey = "MAR.OBJ.valley";
        public string antennaVerbKey = "MAR.VERB.orientAntenna";
        public string antennaObjectKey = "MAR.OBJ.antenna";
        public string confirmVerbKey = "MAR.VERB.confirmMemory";
        public string confirmObjectKey = "MAR.OBJ.memory";

        [Header("Evidências")]
        public string sunReferenceId = "mars.ev.sun.reference";
        public string sunReferenceResultKey = "MAR.SUN.reference";
        public string sunDustId = "mars.ev.sun.dust";
        public string sunDustResultKey = "MAR.SUN.dust";
        public string sunComparisonId = "mars.ev.sun.comparison";
        public string sunComparisonResultKey = "MAR.SUN.comparison";
        public string valleyPhotoId = "mars.ev.valley.photo";
        public string valleyPhotoResultKey = "MAR.VALLEY.photo";
        public string sunScienceRef = "MS27";
        public string valleyScienceRef = "MS26";

        [Header("Opções da memória (M30) e nota da transmissão (chaves)")]
        public string memoryValleyKey = "MAR.MEMORY.valley";
        public string memoryScienceKey = "MAR.MEMORY.science";
        public string transmissionNoteKey = "MAR.TRANSMISSION.note";

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"FinalSurveyData '{name}'";
            ContractValidation.RequirePositive(errors, owner, nameof(reachMeters), reachMeters);
            ContractValidation.RequirePositive(errors, owner, nameof(sunHoldSeconds), sunHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(valleyHoldSeconds), valleyHoldSeconds);
            ContractValidation.RequirePositive(errors, owner, nameof(antennaHoldSeconds), antennaHoldSeconds);
            if (valleyWidth > MaxImageWidth || valleyHeight > MaxImageHeight)
                ContractValidation.Report(errors, owner, $"a foto do vale passa de {MaxImageWidth}×{MaxImageHeight}.");

            if (sunWidth * 2 > MaxImageWidth || sunHeight > MaxImageHeight)
                ContractValidation.Report(errors, owner, $"a comparação do Sol (duas capturas lado a lado) passa de {MaxImageWidth}×{MaxImageHeight}.");

            foreach (var (field, value) in new[]
            {
                (nameof(sunVerbKey), sunVerbKey), (nameof(sunObjectKey), sunObjectKey), (nameof(valleyVerbKey), valleyVerbKey),
                (nameof(valleyObjectKey), valleyObjectKey), (nameof(antennaVerbKey), antennaVerbKey), (nameof(antennaObjectKey), antennaObjectKey),
                (nameof(confirmVerbKey), confirmVerbKey), (nameof(confirmObjectKey), confirmObjectKey),
                (nameof(sunReferenceId), sunReferenceId), (nameof(sunReferenceResultKey), sunReferenceResultKey),
                (nameof(sunDustId), sunDustId), (nameof(sunDustResultKey), sunDustResultKey),
                (nameof(sunComparisonId), sunComparisonId), (nameof(sunComparisonResultKey), sunComparisonResultKey),
                (nameof(valleyPhotoId), valleyPhotoId), (nameof(valleyPhotoResultKey), valleyPhotoResultKey),
                (nameof(sunScienceRef), sunScienceRef), (nameof(valleyScienceRef), valleyScienceRef),
                (nameof(memoryValleyKey), memoryValleyKey), (nameof(memoryScienceKey), memoryScienceKey), (nameof(transmissionNoteKey), transmissionNoteKey),
            })
            {
                ContractValidation.RequireId(errors, owner, field, value);
            }

            return errors;
        }

        /// <summary>Captura do Sol: NÃO é foto do jogador (Observation de reconstrução); só existe como comparação.</summary>
        public EvidenceRecord SunRecord(bool dust, Sprite thumbnail) => new EvidenceRecord(dust ? sunDustId : sunReferenceId, EvidenceKind.Observation,
            dust ? sunDustResultKey : sunReferenceResultKey, sunScienceRef, EvidenceAuthorship.Reconstruction, thumbnail);

        /// <summary>O registro científico: as duas capturas do Sol lado a lado (miniatura até 960×540).</summary>
        public EvidenceRecord SunComparison(Sprite sideBySide) => new EvidenceRecord(sunComparisonId, EvidenceKind.Observation, sunComparisonResultKey,
            sunScienceRef, EvidenceAuthorship.Reconstruction, sideBySide);

        /// <summary>Foto do vale: a única foto do jogador deste trecho (a terceira vaga reservada).</summary>
        public EvidenceRecord ValleyPhoto(Sprite photo) => new EvidenceRecord(valleyPhotoId, EvidenceKind.Photo, valleyPhotoResultKey,
            valleyScienceRef, EvidenceAuthorship.PlayerCapture, photo);
    }
}
