using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum PioneerCueTiming
    {
        /// <summary>Legenda por janela de tempo local do estado.</summary>
        Window = 0,
        /// <summary>Legenda aberta por um gatilho, com tempo mínimo de leitura.</summary>
        Trigger = 1,
    }

    [Serializable]
    public sealed class PioneerStateData
    {
        public string id;
        public int order;
        public int nominalSeconds;
    }

    [Serializable]
    public sealed class PioneerCueData
    {
        public string id;
        public string state;
        public string textKey;
        public PioneerCueTiming timing;
        public float startSeconds;
        public float endSeconds;
        public string triggerId;
        public float minimumReadSeconds;
        [Tooltip("Ação que a sequência espera antes de seguir (ex.: PIO.Belt.Gate). Vazio = nenhuma.")] public string gateId;
        [Tooltip("Se > 0, a animação pode alongar até este tempo para a leitura não ser cortada.")] public float readingStretchMaxSeconds;

        public float NominalSeconds => timing == PioneerCueTiming.Window ? endSeconds - startSeconds : minimumReadSeconds;
    }

    [Serializable]
    public sealed class PioneerTextData
    {
        public string key;
        public string group;
        public string ptText;
        public string enText;
        public string ptStatus;
        public string enStatus;
    }

    /// <summary>
    /// PIO-013 · Dados do capítulo para o jogo: estados, cues e textos PT/EN importados de <c>Data/</c> (PIO-001) por
    /// <c>PioneerDataBuilder</c>. O player não lê caminhos do repositório; texto narrativo nunca fica em C#. Até o
    /// <c>LocalizationService</c> (UI-004) existir, esta tabela do próprio capítulo resolve as chaves, como faz a Surveyor.
    /// </summary>
    public sealed class PioneerData : ScriptableObject
    {
        public const string Portuguese = "pt";
        public const string English = "en";

        /// <summary>
        /// Velocidade de leitura usada para estimar o tempo de uma legenda. ASSUNÇÃO de produção (não vem de fonte):
        /// confirmar com a leitura em 720p na build real (PIO-015) e ajustar aqui.
        /// </summary>
        public const float ReadingCharactersPerSecond = 16f;

        [SerializeField] List<PioneerStateData> states = new List<PioneerStateData>();
        [SerializeField] List<PioneerCueData> cues = new List<PioneerCueData>();
        [SerializeField] List<PioneerTextData> texts = new List<PioneerTextData>();

        Dictionary<string, PioneerTextData> textIndex;

        public IReadOnlyList<PioneerStateData> States => states;
        public IReadOnlyList<PioneerCueData> Cues => cues;
        public IReadOnlyList<PioneerTextData> Texts => texts;

        public static PioneerData Create(IEnumerable<PioneerStateData> states, IEnumerable<PioneerCueData> cues, IEnumerable<PioneerTextData> texts)
        {
            var data = CreateInstance<PioneerData>();
            data.states = new List<PioneerStateData>(states);
            data.cues = new List<PioneerCueData>(cues);
            data.texts = new List<PioneerTextData>(texts);
            return data;
        }

        void OnEnable() => textIndex = null;

        void OnValidate() => textIndex = null;

        /// <summary>Texto da chave no idioma ("pt", "pt-BR" ou "en"). Vazio se a chave ou o idioma não existem: a caixa de legenda some.</summary>
        public string GetText(string key, string languageCode)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (textIndex == null)
            {
                textIndex = new Dictionary<string, PioneerTextData>();
                foreach (var entry in texts)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.key))
                        textIndex[entry.key] = entry;
                }
            }

            if (!textIndex.TryGetValue(key, out var found))
                return string.Empty;

            switch (Normalize(languageCode))
            {
                case Portuguese: return found.ptText ?? string.Empty;
                case English: return found.enText ?? string.Empty;
                default: return string.Empty;
            }
        }

        public static string Normalize(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return string.Empty;

            var code = languageCode.ToLowerInvariant();
            return code.StartsWith("pt") ? Portuguese : code.StartsWith("en") ? English : string.Empty;
        }

        public bool TryGetCue(string cueId, out PioneerCueData cue)
        {
            cue = null;
            foreach (var candidate in cues)
            {
                if (candidate != null && candidate.id == cueId)
                {
                    cue = candidate;
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<PioneerCueData> CuesOf(string stateId)
        {
            foreach (var cue in cues)
            {
                if (cue != null && cue.state == stateId)
                    yield return cue;
            }
        }

        public bool TryGetState(string stateId, out PioneerStateData state)
        {
            state = null;
            foreach (var candidate in states)
            {
                if (candidate != null && candidate.id == stateId)
                {
                    state = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tempo que a legenda precisa para ser lida no idioma: nunca menos que o nominal e, quando o roteiro permite
        /// alongar (<c>readingStretchMaxSeconds</c>), até o máximo. Sem permissão, o nominal é o teto (a leitura não
        /// justifica ultrapassar o que o roteiro fixou). Nunca acelera a leitura para caber.
        /// </summary>
        public float ReadingSeconds(PioneerCueData cue, string languageCode)
        {
            var needed = GetText(cue.textKey, languageCode).Length / ReadingCharactersPerSecond;
            var nominal = cue.NominalSeconds;
            if (cue.readingStretchMaxSeconds > nominal)
                return Mathf.Clamp(needed, nominal, cue.readingStretchMaxSeconds);

            return nominal;
        }

        /// <summary>Verdadeiro se o texto, no idioma, precisa de mais que o tempo nominal da legenda.</summary>
        public bool NeedsStretch(PioneerCueData cue, string languageCode) =>
            GetText(cue.textKey, languageCode).Length / ReadingCharactersPerSecond > cue.NominalSeconds + 1e-4f;

        /// <summary>Erros dos dados. Lista vazia = conforme.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            const string owner = "PioneerData";
            if (states.Count != PioneerPresentation.StateIds.Length)
                ContractValidation.Report(errors, owner, $"{states.Count} estados; esperado {PioneerPresentation.StateIds.Length}.");

            for (var i = 0; i < states.Count && i < PioneerPresentation.StateIds.Length; i++)
            {
                if (states[i].id != PioneerPresentation.StateIds[i])
                    ContractValidation.Report(errors, owner, $"estado {i + 1} é '{states[i].id}'; esperado '{PioneerPresentation.StateIds[i]}'.");
                if (states[i].order != i + 1)
                    ContractValidation.Report(errors, owner, $"estado '{states[i].id}' com order {states[i].order}.");
            }

            var ids = new HashSet<string>();
            foreach (var cue in cues)
            {
                if (cue == null || !ids.Add(cue.id ?? string.Empty))
                {
                    ContractValidation.Report(errors, owner, $"cue nulo ou duplicado '{cue?.id}'.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "cue.id", cue.id);
                if (!TryGetState(cue.state, out _))
                    ContractValidation.Report(errors, owner, $"cue '{cue.id}' em estado inexistente '{cue.state}'.");
                foreach (var language in new[] { Portuguese, English })
                {
                    if (GetText(cue.textKey, language).Trim().Length == 0)
                        ContractValidation.Report(errors, owner, $"cue '{cue.id}' sem texto em {language}.");
                }

                if (cue.timing == PioneerCueTiming.Window && !(cue.startSeconds >= 0f && cue.endSeconds > cue.startSeconds))
                    ContractValidation.Report(errors, owner, $"cue '{cue.id}' com janela inválida {cue.startSeconds}–{cue.endSeconds}.");
                if (cue.timing == PioneerCueTiming.Trigger && (string.IsNullOrEmpty(cue.triggerId) || cue.minimumReadSeconds <= 0f))
                    ContractValidation.Report(errors, owner, $"cue '{cue.id}' sem gatilho ou sem tempo mínimo de leitura.");
            }

            foreach (var entry in texts)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    ContractValidation.Report(errors, owner, "texto sem chave.");
            }

            return errors;
        }
    }
}
