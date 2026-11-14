using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps
{
    /// <summary>Contrato do <c>EvidenceStore</c> (implementação: CORE-005). Estado da sessão; nunca cruza capítulos.</summary>
    public interface IEvidenceStore
    {
        IReadOnlyList<EvidenceRecord> Records { get; }

        /// <summary>Disparado somente quando um registro novo entra.</summary>
        event Action<EvidenceRecord> Added;

        /// <summary>
        /// Idempotente por <see cref="EvidenceRecord.EvidenceId"/>: devolve true se entrou, false se o ID já existia
        /// (o primeiro registro é mantido, sem evento). Registro nulo ou inválido lança <see cref="ArgumentException"/>.
        /// </summary>
        bool Add(EvidenceRecord record);

        bool Contains(string evidenceId);

        bool TryGet(string evidenceId, out EvidenceRecord record);

        /// <summary>Descarta tudo (reinício ou saída do capítulo).</summary>
        void Clear();
    }
}
