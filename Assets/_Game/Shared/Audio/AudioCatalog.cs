using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Catálogo de cues do <see cref="AudioDirector"/>, por dados. Cada capítulo registra os seus IDs (os da planilha de cues);
    /// os arquivos só entram depois de adquiridos e creditados. Um cue sem arquivo é "pendente" e soa como silêncio.
    /// </summary>
    [CreateAssetMenu(menuName = "Abandoned Space Apps/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] List<AudioCueDefinition> cues = new List<AudioCueDefinition>();

        Dictionary<string, AudioCueDefinition> index;

        public IReadOnlyList<AudioCueDefinition> Cues => cues;

        public bool TryGet(string cueId, out AudioCueDefinition cue)
        {
            if (index == null)
                Rebuild();
            cue = null;
            return !string.IsNullOrEmpty(cueId) && index.TryGetValue(cueId, out cue);
        }

        public bool Contains(string cueId) => TryGet(cueId, out _);

        /// <summary>Cues que ainda não têm arquivo licenciado (esperado enquanto a AUD-001 não virar importação).</summary>
        public List<string> PendingCueIds()
        {
            var pending = new List<string>();
            foreach (var cue in cues)
            {
                if (cue != null && cue.IsPending)
                    pending.Add(cue.Id);
            }

            return pending;
        }

        public void Set(IEnumerable<AudioCueDefinition> definitions)
        {
            cues = new List<AudioCueDefinition>(definitions);
            index = null;
        }

        void OnValidate() => index = null;

        void Rebuild()
        {
            index = new Dictionary<string, AudioCueDefinition>();
            foreach (var cue in cues)
            {
                if (cue != null && !string.IsNullOrEmpty(cue.Id) && !index.ContainsKey(cue.Id))
                    index.Add(cue.Id, cue);
            }
        }

        /// <summary>Erros do catálogo (lista vazia = conforme): IDs válidos e únicos e cada cue válido. Cue pendente não é erro.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var cue in cues)
            {
                if (cue == null)
                {
                    errors.Add("AudioCatalog: há um cue nulo.");
                    continue;
                }

                errors.AddRange(cue.Validate());
                if (!string.IsNullOrEmpty(cue.Id) && !seen.Add(cue.Id))
                    errors.Add($"AudioCatalog: cue '{cue.Id}' duplicado.");
            }

            return errors;
        }
    }
}
