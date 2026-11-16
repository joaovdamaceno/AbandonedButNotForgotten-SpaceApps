using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    [Serializable]
    public sealed class NarrationLine
    {
        [SerializeField] string textKey;
        [SerializeField] AudioClip clip;
        public string TextKey => textKey;
        public AudioClip Clip => clip;
        public NarrationLine(string key, AudioClip recording) { textKey = key; clip = recording; }
    }

    /// <summary>NAR-001: human English recordings, keyed by subtitle identity, independent of UI language.</summary>
    [CreateAssetMenu(menuName = "Abandoned Space Apps/English narration")]
    public sealed class NarrationCatalog : ScriptableObject
    {
        [SerializeField] string chapterId;
        [SerializeField] List<NarrationLine> lines = new List<NarrationLine>();
        readonly Dictionary<string, AudioClip> lookup = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        public string ChapterId => chapterId;
        public IReadOnlyList<NarrationLine> Lines => lines;

        public void Configure(string id, IEnumerable<NarrationLine> recordings)
        {
            chapterId = id; lines = new List<NarrationLine>(recordings); Rebuild();
        }
        void OnEnable() => Rebuild();
        void Rebuild()
        {
            lookup.Clear();
            foreach (var line in lines)
                if (line != null && !string.IsNullOrEmpty(line.TextKey) && line.Clip != null)
                    lookup[line.TextKey] = line.Clip;
        }
        public bool TryGet(string key, out AudioClip clip)
        {
            clip = null;
            return !string.IsNullOrEmpty(key) && lookup.TryGetValue(key, out clip) && clip != null;
        }
        public float Duration(string key) => TryGet(key, out var clip) ? clip.length : 0f;
        public List<string> Validate()
        {
            var errors = new List<string>(); var keys = new HashSet<string>();
            foreach (var line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.TextKey) || line.Clip == null)
                    errors.Add("Narration line missing key or clip.");
                else if (!keys.Add(line.TextKey)) errors.Add("Duplicate narration key: " + line.TextKey);
            }
            return errors;
        }
    }
}
