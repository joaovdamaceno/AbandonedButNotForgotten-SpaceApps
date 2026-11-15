using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>
    /// SUR-014 · Catálogo dos 31 cues SUR-S01–S31 e dos objetivos do capítulo. Guarda só a LIGAÇÃO (ato, estado, clip/sequência,
    /// janela local ou evento, leitura mínima, páginas para fonte ampliada); os textos PT/EN ficam nos dois JSONs
    /// (<c>Localization/surveyor.pt-BR.json</c> e <c>surveyor.en.json</c>), nenhuma frase em C#.
    /// <para>
    /// Tempo: janelas são em segundos LOCAIS da sequência do ato (o relógio do <c>SequenceDirector</c>, que para nas barreiras);
    /// gatilhos nomeiam o evento do dono do ato. Nenhum cue usa relógio absoluto da partida.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "SurveyorSubtitles", menuName = "Abandoned/Surveyor/Subtitle Catalog")]
    public sealed class SurveyorSubtitles : ScriptableObject
    {
        public const int CueCount = 31;
        public const string PageTriggerSuffix = ".next";
        /// <summary>Limite de leitura da UI-004 (caracteres por segundo) usado como alerta.</summary>
        public const float MaxCharactersPerSecond = 22f;
        /// <summary>Maior legenda que a UI-004 mediu em 2 linhas (Outfit 28 px, 832 px úteis). Proxy, não medição desta tabela.</summary>
        public const int TwoLineCharacterProxy = 92;
        public const float FinalActSeconds = 50f;

        public enum Anchor { Window = 0, Event = 1 }

        [Serializable]
        public sealed class CueBinding
        {
            public string cueId;
            public string textKey;
            [Tooltip("Ato do roteiro (1–6).")] public int act;
            [Tooltip("Estado local do roteiro (LaunchTelemetry, Observe, ImaginedVisit…).")] public string stateId;
            [Tooltip("Sequência/clip dono do cue. 'surveyor.runtime.*' = controlador do ato, sem SequenceDefinition.")] public string sequenceId;
            public Anchor anchor;
            [Tooltip("Janela: segundos locais da sequência.")] public float startSeconds;
            public float endSeconds;
            [Tooltip("Evento: ID que o dono do ato dispara.")] public string triggerId;
            [Tooltip("Leitura mínima do roteiro (s).")] public float minimumReadSeconds;
            [Tooltip("Cue que precisa ter cumprido o tempo mínimo antes deste (evita sobreposição).")] public string afterCueId;
            public int priority;
            [Tooltip("built = mesmo vínculo já montado pelo dono; proposed = ID/janela definidos aqui para o dono adotar.")] public string bindingStatus;
            [Tooltip("Duas telas para fonte ampliada (vazio = frase única, não divide).")] public string[] pageKeys = Array.Empty<string>();
            [Tooltip("Divisão do tempo mínimo entre as páginas; soma = minimumReadSeconds.")] public float[] pageMinimumSeconds = Array.Empty<float>();

            public bool IsEvent => anchor == Anchor.Event;
            public bool HasPages => pageKeys != null && pageKeys.Length == 2;
        }

        [Serializable]
        public sealed class ObjectiveBinding
        {
            public string textKey;
            public int act;
            public string stateId;
        }

        [SerializeField] int schemaVersion = 1;
        [SerializeField] string chapterId = "surveyor3";
        [SerializeField] TextAsset ptBrTable;
        [SerializeField] TextAsset enTable;
        [SerializeField, Tooltip("Objetivos ficam no canto, nunca na posição das legendas.")] string objectiveRegion = "corner";
        [SerializeField] CueBinding[] cues = Array.Empty<CueBinding>();
        [SerializeField] ObjectiveBinding[] objectives = Array.Empty<ObjectiveBinding>();

        public int SchemaVersion => schemaVersion;
        public string ChapterId => chapterId;
        public TextAsset PtBrTable => ptBrTable;
        public TextAsset EnTable => enTable;
        public string ObjectiveRegion => objectiveRegion;
        public IReadOnlyList<CueBinding> Cues => cues;
        public IReadOnlyList<ObjectiveBinding> Objectives => objectives;

        // ---------- Tabelas por idioma ----------

        [Serializable] public sealed class LanguageEntry { public string key, group, status, text; }

        [Serializable] sealed class LanguageFile { public int schemaVersion; public string chapterId, language, languageCode; public LanguageEntry[] entries; }

        [Serializable] sealed class MergedFile { public MergedRow[] texts; }

        [Serializable] sealed class MergedRow { public string key, group, pt, en; }

        /// <summary>Lê um arquivo de idioma. Devolve nulo se o JSON for inválido (erro em <paramref name="errors"/>).</summary>
        public static LanguageEntry[] ParseLanguage(string json, string name, List<string> errors)
        {
            try
            {
                var file = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<LanguageFile>(json);
                if (file?.entries != null)
                    return file.entries;
                errors.Add($"{name}: sem a lista \"entries\".");
            }
            catch (Exception exception)
            {
                errors.Add($"{name}: JSON inválido ({exception.Message}).");
            }

            return null;
        }

        /// <summary>
        /// Junta PT e EN no formato <c>{"texts":[{"key","group","pt","en"}]}</c> que o <c>LocalizationService.AddTable</c> consome.
        /// Chave faltante num idioma, duplicada ou com grupo diferente vira erro; a tabela só sai se não houver nenhum.
        /// </summary>
        public static string MergeTables(string ptJson, string enJson, List<string> errors)
        {
            var pt = ParseLanguage(ptJson, "surveyor.pt-BR.json", errors);
            var en = ParseLanguage(enJson, "surveyor.en.json", errors);
            if (pt == null || en == null)
                return null;

            var enByKey = new Dictionary<string, LanguageEntry>(StringComparer.Ordinal);
            foreach (var entry in en)
            {
                if (entry == null || !ContractValidation.IsValidId(entry.key))
                    errors.Add($"surveyor.en.json: chave inválida '{entry?.key ?? "<nula>"}'.");
                else if (enByKey.ContainsKey(entry.key))
                    errors.Add($"surveyor.en.json: chave duplicada '{entry.key}'.");
                else
                    enByKey[entry.key] = entry;
            }

            var rows = new List<MergedRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in pt)
            {
                if (entry == null || !ContractValidation.IsValidId(entry.key))
                {
                    errors.Add($"surveyor.pt-BR.json: chave inválida '{entry?.key ?? "<nula>"}'.");
                    continue;
                }

                if (!seen.Add(entry.key))
                {
                    errors.Add($"surveyor.pt-BR.json: chave duplicada '{entry.key}'.");
                    continue;
                }

                if (!enByKey.TryGetValue(entry.key, out var other))
                {
                    errors.Add($"'{entry.key}' existe em PT e falta em EN.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.text) || string.IsNullOrWhiteSpace(other.text))
                    errors.Add($"'{entry.key}' sem texto em {(string.IsNullOrWhiteSpace(entry.text) ? "PT" : "EN")}.");
                if (entry.group != other.group)
                    errors.Add($"'{entry.key}' tem grupo diferente em PT ('{entry.group}') e EN ('{other.group}').");

                rows.Add(new MergedRow { key = entry.key, group = entry.group, pt = entry.text, en = other.text });
            }

            foreach (var key in enByKey.Keys)
                if (!seen.Contains(key))
                    errors.Add($"'{key}' existe em EN e falta em PT.");

            return errors.Count > 0 ? null : JsonUtility.ToJson(new MergedFile { texts = rows.ToArray() });
        }

        /// <summary>Tabela unificada para <c>LocalizationService.AddTable(json, "surveyor")</c>; nula se houver erro.</summary>
        public string BuildTableJson(List<string> errors) =>
            ptBrTable == null || enTable == null
                ? NullTables(errors)
                : MergeTables(ptBrTable.text, enTable.text, errors);

        static string NullTables(List<string> errors)
        {
            errors.Add("SurveyorSubtitles sem as duas tabelas (ptBrTable/enTable).");
            return null;
        }

        // ---------- Cues ----------

        public bool TryGetCue(string cueId, out CueBinding binding)
        {
            foreach (var item in cues ?? Array.Empty<CueBinding>())
                if (item != null && string.Equals(item.cueId, cueId, StringComparison.Ordinal))
                {
                    binding = item;
                    return true;
                }

            binding = null;
            return false;
        }

        /// <summary>
        /// Cues de uma sequência, no contrato comum. Com <paramref name="paged"/> (fonte ampliada) cada frase de dois períodos vira duas
        /// telas que somam o mesmo tempo: janelas consecutivas, ou o gatilho seguido de <c>gatilho + ".next"</c> que o dono do ato
        /// dispara ao fim da leitura mínima da primeira tela.
        /// </summary>
        public List<SubtitleCue> BuildCues(string sequenceId, bool paged = false)
        {
            var result = new List<SubtitleCue>();
            foreach (var cue in cues ?? Array.Empty<CueBinding>())
            {
                if (cue == null || cue.sequenceId != sequenceId)
                    continue;

                if (!paged || !cue.HasPages)
                {
                    result.Add(cue.IsEvent
                        ? SubtitleCue.Triggered(cue.cueId, cue.textKey, cue.triggerId, cue.minimumReadSeconds, SubtitlePosition.Bottom, cue.priority)
                        : new SubtitleCue(cue.cueId, cue.textKey, cue.startSeconds, cue.endSeconds, SubtitlePosition.Bottom, cue.priority));
                    continue;
                }

                var first = cue.pageMinimumSeconds[0];
                if (cue.IsEvent)
                {
                    result.Add(SubtitleCue.Triggered(cue.cueId + ".p1", cue.pageKeys[0], cue.triggerId, first, SubtitlePosition.Bottom, cue.priority));
                    result.Add(SubtitleCue.Triggered(cue.cueId + ".p2", cue.pageKeys[1], cue.triggerId + PageTriggerSuffix,
                        cue.pageMinimumSeconds[1], SubtitlePosition.Bottom, cue.priority));
                }
                else
                {
                    var split = cue.startSeconds + (cue.endSeconds - cue.startSeconds) * first / cue.minimumReadSeconds;
                    result.Add(new SubtitleCue(cue.cueId + ".p1", cue.pageKeys[0], cue.startSeconds, split, SubtitlePosition.Bottom, cue.priority));
                    result.Add(new SubtitleCue(cue.cueId + ".p2", cue.pageKeys[1], split, cue.endSeconds, SubtitlePosition.Bottom, cue.priority));
                }
            }

            return result;
        }

        // ---------- Validação ----------

        /// <summary>
        /// Conferências do catálogo contra as duas tabelas: 31 cues únicos, mesmas chaves PT/EN, janelas e gatilhos exclusivos,
        /// leitura mínima respeitada, páginas que reconstroem o texto, sem sobreposição de janelas, ato final de 50 s e
        /// objetivos fora da posição das legendas. Lista vazia = conforme.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var merged = BuildTableJson(errors);
            if (merged == null)
                return errors;

            var table = new Dictionary<string, MergedRow>(StringComparer.Ordinal);
            foreach (var row in JsonUtility.FromJson<MergedFile>(merged).texts)
                table[row.key] = row;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in cues ?? Array.Empty<CueBinding>())
            {
                if (cue == null)
                {
                    errors.Add("há um cue nulo.");
                    continue;
                }

                var owner = $"cue '{cue.cueId}'";
                if (!ContractValidation.IsValidId(cue.cueId) || !ids.Add(cue.cueId))
                    errors.Add($"{owner}: id inválido ou duplicado.");
                if (cue.textKey != cue.cueId || !table.ContainsKey(cue.textKey ?? string.Empty))
                    errors.Add($"{owner}: textKey '{cue.textKey}' deve ser o próprio id e existir em PT e EN.");
                if (cue.act < 1 || cue.act > 6 || string.IsNullOrEmpty(cue.stateId) || string.IsNullOrEmpty(cue.sequenceId))
                    errors.Add($"{owner}: ato/estado/sequência incompletos.");
                if (cue.bindingStatus != "built" && cue.bindingStatus != "proposed")
                    errors.Add($"{owner}: bindingStatus deve ser built ou proposed.");
                if (!(cue.minimumReadSeconds > 0f))
                    errors.Add($"{owner}: leitura mínima deve ser positiva.");

                if (cue.IsEvent)
                {
                    if (!ContractValidation.IsValidId(cue.triggerId))
                        errors.Add($"{owner}: gatilho inválido.");
                    if (cue.startSeconds != 0f || cue.endSeconds != 0f)
                        errors.Add($"{owner}: janela e gatilho são mutuamente exclusivos.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(cue.triggerId))
                        errors.Add($"{owner}: janela não leva gatilho.");
                    if (cue.endSeconds - cue.startSeconds + 1e-3f < cue.minimumReadSeconds)
                        errors.Add($"{owner}: janela de {cue.endSeconds - cue.startSeconds} s é menor que a leitura mínima de {cue.minimumReadSeconds} s.");
                }

                if (!string.IsNullOrEmpty(cue.afterCueId) && !TryGetCue(cue.afterCueId, out _))
                    errors.Add($"{owner}: afterCueId '{cue.afterCueId}' não existe.");

                ValidateText(errors, owner, cue, table);
                ValidatePages(errors, owner, cue, table);
            }

            if (ids.Count != CueCount)
                errors.Add($"o catálogo tem {ids.Count} cues; o roteiro tem {CueCount}.");
            for (var i = 1; i <= CueCount; i++)
                if (!ids.Contains($"SUR-S{i:00}"))
                    errors.Add($"falta SUR-S{i:00}.");

            ValidateWindows(errors);
            ValidateFinalAct(errors);
            ValidateObjectives(errors, table);
            return errors;
        }

        void ValidateText(List<string> errors, string owner, CueBinding cue, Dictionary<string, MergedRow> table)
        {
            if (!table.TryGetValue(cue.textKey ?? string.Empty, out var row))
                return;

            var seconds = cue.minimumReadSeconds;
            if (!cue.IsEvent)
                seconds = Mathf.Max(seconds, cue.endSeconds - cue.startSeconds);
            foreach (var (language, text) in new[] { ("PT", row.pt), ("EN", row.en) })
            {
                if (text.Length > TwoLineCharacterProxy)
                    errors.Add($"{owner} {language}: {text.Length} caracteres, acima do proxy de duas linhas ({TwoLineCharacterProxy}).");
                if (text.Length / seconds > MaxCharactersPerSecond)
                    errors.Add($"{owner} {language}: {text.Length / seconds:0.0} cps acima de {MaxCharactersPerSecond} em {seconds} s.");
                if (text.Contains("\n"))
                    errors.Add($"{owner} {language}: quebra de linha manual; a legenda quebra sozinha.");
            }
        }

        void ValidatePages(List<string> errors, string owner, CueBinding cue, Dictionary<string, MergedRow> table)
        {
            if (cue.pageKeys == null || cue.pageKeys.Length == 0)
                return;

            if (cue.pageKeys.Length != 2 || cue.pageMinimumSeconds == null || cue.pageMinimumSeconds.Length != 2)
            {
                errors.Add($"{owner}: páginas devem ser exatamente duas, com dois tempos.");
                return;
            }

            if (Mathf.Abs(cue.pageMinimumSeconds[0] + cue.pageMinimumSeconds[1] - cue.minimumReadSeconds) > 1e-3f)
                errors.Add($"{owner}: os tempos das páginas não somam a leitura mínima.");
            if (!table.TryGetValue(cue.textKey, out var whole))
                return;

            for (var i = 0; i < 2; i++)
                if (!table.ContainsKey(cue.pageKeys[i]))
                    return;

            var a = table[cue.pageKeys[0]];
            var b = table[cue.pageKeys[1]];
            if (a.pt + " " + b.pt != whole.pt || a.en + " " + b.en != whole.en)
                errors.Add($"{owner}: as páginas não reconstroem o texto inteiro em PT e EN.");
            for (var i = 0; i < 2; i++)
            {
                var page = table[cue.pageKeys[i]];
                var seconds = cue.pageMinimumSeconds[i];
                if (Mathf.Max(page.pt.Length, page.en.Length) / seconds > MaxCharactersPerSecond)
                    errors.Add($"{owner}: página {i + 1} rápida demais ({seconds} s).");
            }
        }

        void ValidateWindows(List<string> errors)
        {
            var bySequence = new Dictionary<string, List<CueBinding>>(StringComparer.Ordinal);
            foreach (var cue in cues ?? Array.Empty<CueBinding>())
            {
                if (cue == null || cue.IsEvent || string.IsNullOrEmpty(cue.sequenceId))
                    continue;
                if (!bySequence.TryGetValue(cue.sequenceId, out var list))
                    bySequence[cue.sequenceId] = list = new List<CueBinding>();
                list.Add(cue);
            }

            foreach (var pair in bySequence)
            {
                pair.Value.Sort((x, y) => x.startSeconds.CompareTo(y.startSeconds));
                for (var i = 1; i < pair.Value.Count; i++)
                    if (pair.Value[i].startSeconds + 1e-3f < pair.Value[i - 1].endSeconds)
                        errors.Add($"'{pair.Key}': {pair.Value[i - 1].cueId} e {pair.Value[i].cueId} se sobrepõem.");
            }
        }

        void ValidateFinalAct(List<string> errors)
        {
            var end = 0f;
            var count = 0;
            foreach (var cue in cues ?? Array.Empty<CueBinding>())
            {
                if (cue == null || cue.act != 6)
                    continue;
                count++;
                if (cue.IsEvent)
                    errors.Add($"cue '{cue.cueId}': o ato final é uma montagem sem interação; use janela.");
                end = Mathf.Max(end, cue.endSeconds);
            }

            if (count != 6 || Mathf.Abs(end - FinalActSeconds) > 1e-3f)
                errors.Add($"o ato 6 deve ter 6 cues terminando em {FinalActSeconds} s (tem {count}, termina em {end} s).");
        }

        void ValidateObjectives(List<string> errors, Dictionary<string, MergedRow> table)
        {
            if (objectiveRegion != "corner")
                errors.Add("objetivos devem ficar no canto, fora da posição das legendas.");

            foreach (var objective in objectives ?? Array.Empty<ObjectiveBinding>())
            {
                if (objective == null || !table.ContainsKey(objective.textKey ?? string.Empty))
                    errors.Add($"objetivo '{objective?.textKey}' sem texto em PT e EN.");
                else if (table[objective.textKey].group != "objective")
                    errors.Add($"objetivo '{objective.textKey}' deve ser do grupo 'objective'.");
            }
        }
    }
}
