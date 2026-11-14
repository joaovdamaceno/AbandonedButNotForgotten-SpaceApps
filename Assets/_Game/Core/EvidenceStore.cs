using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Evidências da execução atual de UM capítulo (implementação de <see cref="IEvidenceStore"/>). Nunca cruza capítulos:
    /// o ChapterDirector limpa ao reiniciar, trocar ou voltar ao dossiê. <see cref="Clear"/> também destrói as fotografias
    /// do jogador (textura e sprite criados em runtime), liberando memória antes da próxima cena.
    /// </summary>
    public sealed class EvidenceStore : IEvidenceStore
    {
        /// <summary>Limite de fotografias do jogador por capítulo (Docs/04_WEB_AND_QA.md: até 3 de 960×540).</summary>
        public const int MaxPlayerPhotos = 3;

        readonly List<EvidenceRecord> records = new List<EvidenceRecord>();
        readonly Dictionary<string, EvidenceRecord> byId = new Dictionary<string, EvidenceRecord>();

        public IReadOnlyList<EvidenceRecord> Records => records;

        public int PlayerPhotoCount
        {
            get
            {
                var count = 0;
                foreach (var record in records)
                {
                    if (record.CanBePresentedAsPlayers)
                        count++;
                }

                return count;
            }
        }

        public event Action<EvidenceRecord> Added;

        /// <summary>Disparado depois de limpar (reinício, troca de capítulo, volta ao dossiê).</summary>
        public event Action Cleared;

        public bool Add(EvidenceRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var errors = record.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Evidência inválida:" + ContractValidation.Describe(errors), nameof(record));

            if (byId.ContainsKey(record.EvidenceId))
                return false;

            if (record.CanBePresentedAsPlayers && PlayerPhotoCount >= MaxPlayerPhotos)
            {
                throw new InvalidOperationException(
                    $"Limite de {MaxPlayerPhotos} fotografias do jogador por capítulo atingido; '{record.EvidenceId}' não foi registrada.");
            }

            byId.Add(record.EvidenceId, record);
            records.Add(record);
            Added?.Invoke(record);
            return true;
        }

        public bool Contains(string evidenceId) => evidenceId != null && byId.ContainsKey(evidenceId);

        public bool TryGet(string evidenceId, out EvidenceRecord record)
        {
            record = null;
            return evidenceId != null && byId.TryGetValue(evidenceId, out record);
        }

        public void Clear()
        {
            foreach (var record in records)
            {
                if (record.Authorship != EvidenceAuthorship.PlayerCapture || record.Thumbnail == null)
                    continue;

                var sprite = record.Thumbnail;
                DestroyRuntimeObject(sprite.texture);
                DestroyRuntimeObject(sprite);
            }

            records.Clear();
            byId.Clear();
            Cleared?.Invoke();
        }

        /// <summary>Só destrói objetos criados em runtime: assets do projeto nunca são apagados por engano.</summary>
        static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.Contains(target))
                return;
#endif
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
