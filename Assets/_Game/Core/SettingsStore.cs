using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Preferências persistidas. NÃO guarda progresso de partida: cada capítulo começa completo e jogável numa
    /// instalação nova. "Tutorial visto" é só uma informação; nunca impede repetir o tutorial.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;
        public const string Portuguese = "pt";
        public const string English = "en";

        public int version = CurrentVersion;
        public string languageCode = English;
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0f, 1f)] public float uiVolume = 0.9f;
        [Range(0.8f, 1.5f)] public float textScale = 1f;
        [Range(0.1f, 3f)] public float lookSensitivity = 1f;
        public int qualityLevel;
        public HoldMode holdMode = HoldMode.Hold;
        public bool reducedMotion;
        public bool extraAssistance;
        public List<string> tutorialsSeen = new List<string>();

        public SettingsData Clone() => JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(this));

        /// <summary>Corrige valores fora da faixa ou ausentes (dados antigos ou editados à mão).</summary>
        public void Sanitize()
        {
            version = CurrentVersion;
            languageCode = English; // o jogo só tem inglês: um arquivo salvo com "pt" volta para inglês

            masterVolume = Clamp(masterVolume, 0f, 1f, 1f);
            musicVolume = Clamp(musicVolume, 0f, 1f, 0.8f);
            sfxVolume = Clamp(sfxVolume, 0f, 1f, 0.9f);
            uiVolume = Clamp(uiVolume, 0f, 1f, 0.9f);
            textScale = Clamp(textScale, 0.8f, 1.5f, 1f);
            lookSensitivity = Clamp(lookSensitivity, 0.1f, 3f, 1f);
            qualityLevel = Math.Max(0, qualityLevel);
            if (!Enum.IsDefined(typeof(HoldMode), holdMode))
                holdMode = HoldMode.Hold;

            var unique = new List<string>();
            foreach (var id in tutorialsSeen ?? new List<string>())
            {
                if (ContractValidation.IsValidId(id) && !unique.Contains(id))
                    unique.Add(id);
            }

            tutorialsSeen = unique;
        }

        static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(max, Math.Max(min, value));
    }

    /// <summary>
    /// Lê e grava <see cref="SettingsData"/> num único registro JSON versionado (PlayerPrefs por padrão, que no WebGL
    /// usa o armazenamento do navegador). Dado ausente, corrompido ou fora de faixa volta ao padrão sem exceção.
    /// </summary>
    public sealed class SettingsStore
    {
        public const string StorageKey = "abandoned.spaceapps.settings";

        /// <summary>Onde o JSON fica. Permite testes sem tocar nos PlayerPrefs reais.</summary>
        public interface IStorage
        {
            string Read(string key);
            void Write(string key, string value);
            void Delete(string key);
        }

        readonly IStorage storage;
        SettingsData data = new SettingsData();

        public SettingsStore() : this(new PlayerPrefsStorage())
        {
        }

        public SettingsStore(IStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Cópia das preferências atuais (alterar a cópia não muda nada: use <see cref="Apply"/>).</summary>
        public SettingsData Current => data.Clone();

        /// <summary>Disparado após cada mudança aplicada e gravada, com uma cópia dos dados.</summary>
        public event Action<SettingsData> Changed;

        /// <summary>Carrega; devolve false se usou os padrões (sem dado ou dado inválido).</summary>
        public bool Load()
        {
            data = new SettingsData();
            var json = storage.Read(StorageKey);
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                var loaded = JsonUtility.FromJson<SettingsData>(json);
                if (loaded == null)
                    return false;

                loaded.Sanitize();
                data = loaded;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Altera, corrige faixas, grava e avisa. Ex.: <c>store.Apply(s => s.musicVolume = 0.5f)</c>.</summary>
        public void Apply(Action<SettingsData> change)
        {
            if (change == null)
                throw new ArgumentNullException(nameof(change));

            var next = data.Clone();
            change(next);
            next.Sanitize();
            data = next;
            Save();
            Changed?.Invoke(data.Clone());
        }

        public void ResetToDefaults()
        {
            data = new SettingsData();
            storage.Delete(StorageKey);
            Changed?.Invoke(data.Clone());
        }

        public bool HasSeenTutorial(string chapterId) => chapterId != null && data.tutorialsSeen.Contains(chapterId);

        public void MarkTutorialSeen(string chapterId)
        {
            if (!ContractValidation.IsValidId(chapterId))
                throw new ArgumentException($"ID de capítulo inválido: '{chapterId}'.", nameof(chapterId));

            if (!HasSeenTutorial(chapterId))
                Apply(s => s.tutorialsSeen.Add(chapterId));
        }

        void Save() => storage.Write(StorageKey, JsonUtility.ToJson(data));

        sealed class PlayerPrefsStorage : IStorage
        {
            public string Read(string key) => PlayerPrefs.GetString(key, null);

            public void Write(string key, string value)
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save(); // no WebGL, só Save() garante a gravação
            }

            public void Delete(string key)
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
