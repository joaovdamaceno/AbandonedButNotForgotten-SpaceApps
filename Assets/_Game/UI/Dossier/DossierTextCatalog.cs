using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Tabela PT/EN por chave, em memória e sem Unity. O dossiê junta a sua própria (<c>DossierTexts.json</c>) com as
    /// tabelas dos capítulos referenciadas no catálogo, assim as chaves de <c>ChapterDefinition</c> (ex.: MAR.TITLE)
    /// são resolvidas sem frases em C#. O <c>LocalizationService</c> da UI-004 poderá substituir este resolvedor.
    /// </summary>
    public sealed class DossierTextCatalog
    {
        sealed class Row
        {
            public string Pt;
            public string En;
        }

        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        readonly List<string> order = new List<string>();

        public int Count => rows.Count;

        /// <summary>Adiciona ou substitui uma chave. Chave vazia é ignorada (devolve false).</summary>
        public bool Add(string key, string pt, string en)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (!rows.ContainsKey(key))
                order.Add(key);

            rows[key] = new Row { Pt = pt ?? string.Empty, En = en ?? string.Empty };
            return true;
        }

        /// <summary>Texto no idioma pedido. Falso se a chave não existe ou o texto desse idioma está vazio.</summary>
        public bool TryGet(string key, string language, out string text)
        {
            text = null;
            if (key == null || !rows.TryGetValue(key, out var row))
                return false;

            var value = language == DossierFlow.English ? row.En : row.Pt;
            if (string.IsNullOrEmpty(value))
                return false;

            text = value;
            return true;
        }

        /// <summary>Texto no idioma pedido ou <paramref name="fallback"/> (nunca nulo).</summary>
        public string Get(string key, string language, string fallback = "") =>
            TryGet(key, language, out var text) ? text : fallback ?? string.Empty;

        /// <summary>Chaves com o prefixo, na ordem em que foram adicionadas (ex.: linhas de créditos).</summary>
        public List<string> KeysWithPrefix(string prefix)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(prefix))
                return result;

            foreach (var key in order)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    result.Add(key);
            }

            return result;
        }

        /// <summary>Chaves que existem mas não têm texto em algum idioma (para o validador do Editor e testes).</summary>
        public List<string> KeysMissingLanguage()
        {
            var missing = new List<string>();
            foreach (var key in order)
            {
                var row = rows[key];
                if (string.IsNullOrEmpty(row.Pt) || string.IsNullOrEmpty(row.En))
                    missing.Add(key);
            }

            return missing;
        }
    }
}
