using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum EvidenceKind
    {
        Photo = 0,
        Composition = 1,
        Observation = 2,
    }

    public enum EvidenceAuthorship
    {
        /// <summary>Fotografia produzida pelo jogador nesta partida. Só Photo pode ter esta autoria.</summary>
        PlayerCapture = 0,

        /// <summary>Imagem ou dado histórico real: exige crédito e data.</summary>
        Archive = 1,

        /// <summary>Reconstrução do jogo; deve ser rotulada como tal na interface.</summary>
        Reconstruction = 2,
    }

    /// <summary>
    /// Registro de uma evidência. Nenhum resultado simulado vira telemetria histórica literal: o resultado é uma
    /// chave de texto, e a referência científica aponta para a matriz de fontes do capítulo (ex.: MS10, P-S06, S3-03).
    /// </summary>
    [Serializable]
    public sealed class EvidenceRecord
    {
        public const int MaxThumbnailWidth = 960;
        public const int MaxThumbnailHeight = 540;

        [SerializeField] string evidenceId;
        [SerializeField] EvidenceKind kind;
        [SerializeField, Tooltip("Chave de localização do resultado exibido.")] string resultKey;
        [SerializeField, Tooltip("ID da fonte na matriz científica do capítulo.")] string scienceRef;
        [SerializeField, Tooltip("Miniatura opcional, até 960×540.")] Sprite thumbnail;
        [SerializeField] EvidenceAuthorship authorship;
        [SerializeField, Tooltip("Chave de crédito e data. Obrigatória quando a autoria é Archive.")] string creditKey;

        public EvidenceRecord()
        {
        }

        public EvidenceRecord(string evidenceId, EvidenceKind kind, string resultKey, string scienceRef,
            EvidenceAuthorship authorship, Sprite thumbnail = null, string creditKey = null)
        {
            this.evidenceId = evidenceId;
            this.kind = kind;
            this.resultKey = resultKey;
            this.scienceRef = scienceRef;
            this.authorship = authorship;
            this.thumbnail = thumbnail;
            this.creditKey = creditKey;
        }

        public string EvidenceId => evidenceId;
        public EvidenceKind Kind => kind;
        public string ResultKey => resultKey;
        public string ScienceRef => scienceRef;
        public Sprite Thumbnail => thumbnail;
        public EvidenceAuthorship Authorship => authorship;
        public string CreditKey => creditKey;

        /// <summary>Só fotografias do jogador podem ser apresentadas como suas.</summary>
        public bool CanBePresentedAsPlayers => kind == EvidenceKind.Photo && authorship == EvidenceAuthorship.PlayerCapture;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"EvidenceRecord '{evidenceId ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(evidenceId), evidenceId);
            ContractValidation.RequireId(errors, owner, nameof(resultKey), resultKey);
            ContractValidation.RequireId(errors, owner, nameof(scienceRef), scienceRef);

            if (authorship == EvidenceAuthorship.PlayerCapture && kind != EvidenceKind.Photo)
                ContractValidation.Report(errors, owner, "autoria PlayerCapture só é válida para Photo.");

            if (authorship == EvidenceAuthorship.Archive)
                ContractValidation.RequireId(errors, owner, nameof(creditKey), creditKey);

            if (thumbnail != null && (thumbnail.rect.width > MaxThumbnailWidth || thumbnail.rect.height > MaxThumbnailHeight))
            {
                ContractValidation.Report(errors, owner,
                    $"miniatura {thumbnail.rect.width}×{thumbnail.rect.height} excede {MaxThumbnailWidth}×{MaxThumbnailHeight}.");
            }

            return errors;
        }
    }
}
