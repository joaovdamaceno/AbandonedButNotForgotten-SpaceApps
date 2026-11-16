using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// UI-004 · Serviço de localização PT/EN (<c>Docs/03_ARCHITECTURE.md</c>: <c>LocalizationService.Get(key)</c>). Junta as tabelas JSON
    /// do projeto, todas no mesmo formato <c>{"texts":[{"key","group","pt","en"}]}</c> (Marte, HUD, dossiê e a exportação da
    /// Pioneer), sem nenhuma frase em C#. Texto ausente ou vazio no idioma atual devolve VAZIO, para a legenda esconder também a
    /// placa; a chave nunca aparece como se fosse texto de jogo.
    /// <para>
    /// Idioma: <see cref="SetLanguage"/> troca na hora e é idempotente; quem chama (menu de preferências/pausa) o faz num estado
    /// seguro, e os apresentadores só redesenham o que já está na tela, sem tocar etapa nem tempo de leitura.
    /// Duplicatas: a mesma chave com o MESMO texto é só repetição (a primeira vale); com texto DIFERENTE é conflito, a primeira
    /// vale e o problema fica em <see cref="Problems"/>.
    /// </para>
    /// </summary>
    public sealed class LocalizationService
    {
        public const string Portuguese = SettingsData.Portuguese;
        public const string English = SettingsData.English;

        sealed class Row
        {
            public string Group = string.Empty;
            public string Pt = string.Empty;
            public string En = string.Empty;
            public string Source = string.Empty;
        }

        [Serializable] sealed class TableFile { public TableRow[] texts; }

        [Serializable] sealed class TableRow { public string key, group, pt, en; }

        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        readonly List<string> order = new List<string>();
        readonly List<string> problems = new List<string>();
        readonly HashSet<string> missing = new HashSet<string>();
        string language = Portuguese;

        /// <summary>Disparado depois de uma troca efetiva de idioma (nunca ao repetir o mesmo).</summary>
        public event Action<string> LanguageChanged;

        public string Language => language;
        public int Count => order.Count;
        public IReadOnlyList<string> Keys => order;
        /// <summary>Problemas encontrados ao juntar tabelas (JSON inválido, id inválido, conflitos).</summary>
        public IReadOnlyList<string> Problems => problems;
        /// <summary>Chaves pedidas em <see cref="Get"/> que não existem ou estão vazias no idioma atual (para o QA).</summary>
        public IReadOnlyCollection<string> MissingRequests => missing;

        /// <summary>Resolvedor para os painéis dos capítulos (<c>Func&lt;string,string&gt;</c>).</summary>
        public Func<string, string> Resolver => Get;

        /// <summary>Normaliza "pt-BR", "PT", "en-US" para <c>pt</c>/<c>en</c>; qualquer outro valor é inválido (nulo).</summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            var lower = code.Trim().ToLowerInvariant();
            if (lower == Portuguese || lower.StartsWith("pt-", StringComparison.Ordinal) || lower.StartsWith("pt_", StringComparison.Ordinal))
                return Portuguese;
            if (lower == English || lower.StartsWith("en-", StringComparison.Ordinal) || lower.StartsWith("en_", StringComparison.Ordinal))
                return English;
            return null;
        }

        /// <summary>Troca o idioma. Devolve verdadeiro só se mudou; código desconhecido não muda nada.</summary>
        public bool SetLanguage(string code)
        {
            var normalized = Normalize(code);
            if (normalized == null || normalized == language)
                return false;

            language = normalized;
            LanguageChanged?.Invoke(language);
            return true;
        }

        public bool ApplySettings(SettingsData settings) => settings != null && SetLanguage(settings.languageCode);

        /// <summary>
        /// Junta uma tabela JSON. Devolve os problemas DESTA tabela (também acumulados em <see cref="Problems"/>): JSON inválido, chave
        /// inválida, conflito de texto e idioma sem texto.
        /// </summary>
        public List<string> AddTable(string json, string source)
        {
            var errors = new List<string>();
            var name = string.IsNullOrEmpty(source) ? "<tabela>" : source;
            TableFile file = null;
            try
            {
                file = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<TableFile>(json);
            }
            catch (Exception exception)
            {
                errors.Add($"{name}: JSON inválido ({exception.Message}).");
            }

            if (file == null || file.texts == null)
            {
                if (errors.Count == 0)
                    errors.Add($"{name}: sem a lista \"texts\".");
                problems.AddRange(errors);
                return errors;
            }

            foreach (var row in file.texts)
            {
                if (row == null)
                    continue;

                if (!ContractValidation.IsValidId(row.key))
                {
                    errors.Add($"{name}: chave inválida '{row.key ?? "<nula>"}'.");
                    continue;
                }

                var pt = row.pt ?? string.Empty;
                var en = row.en ?? string.Empty;
                if (string.IsNullOrWhiteSpace(pt) || string.IsNullOrWhiteSpace(en))
                    errors.Add($"{name}: '{row.key}' sem texto em {(string.IsNullOrWhiteSpace(pt) ? "PT" : "EN")}.");

                if (rows.TryGetValue(row.key, out var existing))
                {
                    if (existing.Pt != pt || existing.En != en)
                        errors.Add($"{name}: '{row.key}' já existe em {existing.Source} com texto diferente (vale o primeiro).");
                    continue;
                }

                rows[row.key] = new Row { Group = row.group ?? string.Empty, Pt = pt, En = en, Source = name };
                order.Add(row.key);
            }

            problems.AddRange(errors);
            return errors;
        }

        public bool Contains(string key) => key != null && rows.ContainsKey(key);

        /// <summary>Texto no idioma atual. Falso se a chave não existe ou o texto desse idioma está vazio.</summary>
        public bool TryGet(string key, out string text) => TryGet(key, language, out text);

        public bool TryGet(string key, string languageCode, out string text)
        {
            text = null;
            var code = Normalize(languageCode);
            if (code == null || key == null || !rows.TryGetValue(key, out var row))
                return false;

            var value = code == English ? row.En : row.Pt;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            text = value;
            return true;
        }

        /// <summary>
        /// Texto no idioma atual, ou VAZIO (nunca a chave) se faltar. O pedido sem resposta fica em <see cref="MissingRequests"/>;
        /// o vazio faz a legenda esconder a placa inteira.
        /// </summary>
        public string Get(string key)
        {
            if (TryGet(key, out var text))
                return text;

            if (!string.IsNullOrEmpty(key))
                missing.Add(key);
            return string.Empty;
        }

        /// <summary>Grupo da chave (<c>narrative</c>, <c>hud</c>, <c>instrument</c>, <c>card</c>); vazio se não informado.</summary>
        public string GetGroup(string key) => key != null && rows.TryGetValue(key, out var row) ? row.Group : string.Empty;

        /// <summary>Cartões de data/nome usam a hierarquia de carimbo (Plex Mono), não a de legenda.</summary>
        public bool IsStamp(string key) => GetGroup(key) == "card";

        /// <summary>Chaves sem texto em algum idioma (lista vazia = conforme).</summary>
        public List<string> KeysMissingLanguage()
        {
            var result = new List<string>();
            foreach (var key in order)
            {
                var row = rows[key];
                if (string.IsNullOrWhiteSpace(row.Pt) || string.IsNullOrWhiteSpace(row.En))
                    result.Add(key);
            }

            return result;
        }

        /// <summary>Erros acumulados (lista vazia = conforme): problemas ao juntar tabelas e chaves sem idioma.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>(problems);
            foreach (var key in KeysMissingLanguage())
            {
                var message = $"chave '{key}' sem texto em PT ou EN.";
                if (!errors.Exists(e => e.Contains($"'{key}' sem texto")))
                    errors.Add(message);
            }

            return errors;
        }

        public void Clear()
        {
            rows.Clear();
            order.Clear();
            problems.Clear();
            missing.Clear();
        }
    }
}
