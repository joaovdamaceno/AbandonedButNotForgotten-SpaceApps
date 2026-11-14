using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>
    /// Catálogo factual da SUR-001. Gerado no Editor a partir de ReferenceManifest.json;
    /// não busca páginas na rede, não contém telemetria simulada e não importa legendas (SUR-014).
    /// </summary>
    [CreateAssetMenu(fileName = "SurveyorScience", menuName = "Abandoned/Surveyor/Science Catalog")]
    public sealed class SurveyorScience : ScriptableObject
    {
        [SerializeField] int schemaVersion;
        [SerializeField] string chapterId;
        [SerializeField] string reviewedDate;
        [SerializeField] string crossReviewStatus;
        [SerializeField] Source[] sources = Array.Empty<Source>();
        [SerializeField] Fact[] facts = Array.Empty<Fact>();
        [SerializeField] CueLink[] cueLinks = Array.Empty<CueLink>();

        public int SchemaVersion => schemaVersion;
        public string ChapterId => chapterId;
        public string ReviewedDate => reviewedDate;
        public string CrossReviewStatus => crossReviewStatus;
        public IReadOnlyList<Source> Sources => sources;
        public IReadOnlyList<Fact> Facts => facts;
        public IReadOnlyList<CueLink> CueLinks => cueLinks;

        // DTOs de dados para JsonUtility. O catálogo não possui lógica de apresentação/entrada.
        [Serializable]
        public sealed class Source
        {
            public string id;
            public string title;
            public string publisher;
            public string url;
            public string contentUrl;
            public string locator;
            public string verification;
            public string rights;
            public string sha256;
            public string notes;
        }

        [Serializable]
        public sealed class Citation
        {
            public string sourceId;
            public string locator;
        }

        [Serializable]
        public sealed class Fact
        {
            public string id;
            public string treatment;
            public bool approvedForDisplay;
            public string summaryPt;
            public string summaryEn;
            public string decision;
            public Citation[] citations = Array.Empty<Citation>();
        }

        [Serializable]
        public sealed class CueLink
        {
            public string cueId;
            public string[] factIds = Array.Empty<string>();
        }

        public bool TryGetFact(string id, out Fact fact)
        {
            if (facts != null)
                foreach (var item in facts)
                    if (item != null && string.Equals(item.id, id, StringComparison.Ordinal))
                    {
                        fact = item;
                        return true;
                    }
            fact = null;
            return false;
        }

        /// <summary>Impede evidências órfãs, conflitos publicados e cues sem sustentação.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (schemaVersion != 1) errors.Add("schemaVersion deve ser 1.");
            if (chapterId != "surveyor3") errors.Add("chapterId deve ser surveyor3.");
            if (!DateTime.TryParseExact(reviewedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)) errors.Add("reviewedDate deve usar yyyy-MM-dd.");
            if (string.IsNullOrWhiteSpace(crossReviewStatus)) errors.Add("Estado da revisão cruzada ausente.");

            var sourceIndex = new Dictionary<string, Source>(StringComparer.Ordinal);
            foreach (var source in sources ?? Array.Empty<Source>())
            {
                if (source == null) { errors.Add("Fonte nula."); continue; }
                if (!ContractValidation.IsValidId(source.id)) errors.Add("ID de fonte inválido.");
                else if (sourceIndex.ContainsKey(source.id)) errors.Add("Fonte duplicada: " + source.id);
                else sourceIndex.Add(source.id, source);
                if (!IsHttps(source.url)) errors.Add("Fonte precisa de URL HTTPS: " + source.id);
                if (string.IsNullOrWhiteSpace(source.title) || string.IsNullOrWhiteSpace(source.publisher) ||
                    string.IsNullOrWhiteSpace(source.locator) || string.IsNullOrWhiteSpace(source.rights))
                    errors.Add("Metadados de fonte incompletos: " + source.id);
                if (source.verification != "reviewed" && source.verification != "unavailable")
                    errors.Add("Estado de verificação inválido: " + source.id);
            }

            var factIndex = new Dictionary<string, Fact>(StringComparer.Ordinal);
            foreach (var fact in facts ?? Array.Empty<Fact>())
            {
                if (fact == null) { errors.Add("Fato nulo."); continue; }
                if (!ContractValidation.IsValidId(fact.id)) errors.Add("ID de fato inválido.");
                else if (factIndex.ContainsKey(fact.id)) errors.Add("Fato duplicado: " + fact.id);
                else factIndex.Add(fact.id, fact);
                if (!IsTreatment(fact.treatment)) errors.Add("Tratamento inválido: " + fact.id);
                if (fact.approvedForDisplay && fact.treatment == "conflict")
                    errors.Add("Conflito não pode ser publicado: " + fact.id);
                if (string.IsNullOrWhiteSpace(fact.summaryPt) || string.IsNullOrWhiteSpace(fact.summaryEn) ||
                    string.IsNullOrWhiteSpace(fact.decision)) errors.Add("Decisão/texto factual incompleto: " + fact.id);
                if (fact.citations == null || fact.citations.Length == 0) errors.Add("Fato sem fonte: " + fact.id);
                foreach (var citation in fact.citations ?? Array.Empty<Citation>())
                {
                    if (citation == null || string.IsNullOrEmpty(citation.sourceId) ||
                        !sourceIndex.TryGetValue(citation.sourceId, out var source))
                        errors.Add("Fonte desconhecida no fato: " + fact.id);
                    else if (fact.approvedForDisplay && source.verification != "reviewed")
                        errors.Add("Fato publicado depende de fonte indisponível: " + fact.id);
                    if (citation == null || string.IsNullOrWhiteSpace(citation.locator))
                        errors.Add("Citação sem localização: " + fact.id);
                }
            }

            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in cueLinks ?? Array.Empty<CueLink>())
            {
                if (cue == null || string.IsNullOrEmpty(cue.cueId)) { errors.Add("Cue nulo ou sem ID."); continue; }
                if (!cueIds.Add(cue.cueId)) errors.Add("Cue duplicado: " + cue.cueId);
                if (cue.factIds == null || cue.factIds.Length == 0) errors.Add("Cue sem sustentação: " + cue.cueId);
                foreach (var id in cue.factIds ?? Array.Empty<string>())
                {
                    if (string.IsNullOrEmpty(id) || !factIndex.TryGetValue(id, out var fact))
                        errors.Add("Fato desconhecido no cue: " + cue.cueId);
                    else if (!fact.approvedForDisplay) errors.Add("Cue usa fato bloqueado: " + cue.cueId + " → " + id);
                }
            }
            if (cueIds.Count != 31) errors.Add("Mapear exatamente os 31 cues da SUR-001.");
            for (var i = 1; i <= 31; i++)
                if (!cueIds.Contains("SUR-S" + i.ToString("00", CultureInfo.InvariantCulture)))
                    errors.Add("Cue ausente: SUR-S" + i.ToString("00", CultureInfo.InvariantCulture));

            // O epílogo não pode passar por uma gravação histórica de uma câmera operacional.
            foreach (var cue in cueLinks ?? Array.Empty<CueLink>())
            {
                if (cue == null || cue.cueId != "SUR-S26") continue;
                var qualified = false;
                foreach (var id in cue.factIds ?? Array.Empty<string>())
                    qualified |= factIndex.TryGetValue(id ?? "", out var fact) && fact.treatment == "reconstruction";
                if (!qualified) errors.Add("SUR-S26 deve identificar a reconstrução imaginada de 1969.");
            }
            return errors;
        }

        static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https";
        static bool IsTreatment(string value) => value == "verified" || value == "qualified" ||
            value == "conflict" || value == "reconstruction" || value == "editorial";
    }
}
