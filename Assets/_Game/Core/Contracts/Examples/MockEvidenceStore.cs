using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.Examples
{
    /// <summary>MOCK de exemplo/teste de <see cref="IEvidenceStore"/>; o EvidenceStore real é da CORE-005.</summary>
    public sealed class MockEvidenceStore : IEvidenceStore
    {
        readonly List<EvidenceRecord> records = new List<EvidenceRecord>();
        readonly Dictionary<string, EvidenceRecord> byId = new Dictionary<string, EvidenceRecord>();

        public IReadOnlyList<EvidenceRecord> Records => records;

        public event Action<EvidenceRecord> Added;

        public bool Add(EvidenceRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var errors = record.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Evidência inválida:" + ContractValidation.Describe(errors), nameof(record));

            if (byId.ContainsKey(record.EvidenceId))
                return false;

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
            records.Clear();
            byId.Clear();
        }
    }
}
