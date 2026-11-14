using System;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Lê tabelas JSON <c>{"texts":[{"key","pt","en"}]}</c> para um <see cref="DossierTextCatalog"/>. É o mesmo formato
    /// da seção <c>texts</c> de <c>MarsSubtitles.json</c>: campos desconhecidos (cues, notas) são ignorados.
    /// </summary>
    public static class DossierTextTables
    {
        [Serializable]
        public sealed class Row
        {
            public string key;
            public string pt;
            public string en;
        }

        [Serializable]
        public sealed class Table
        {
            public Row[] texts = new Row[0];
        }

        /// <summary>Acrescenta as linhas do JSON ao catálogo. Devolve quantas chaves entraram; JSON inválido devolve 0 e avisa.</summary>
        public static int Load(TextAsset asset, DossierTextCatalog into)
        {
            if (asset == null || into == null)
                return 0;

            return Parse(asset.text, into, asset.name);
        }

        public static int Parse(string json, DossierTextCatalog into, string sourceName = null)
        {
            if (string.IsNullOrWhiteSpace(json) || into == null)
                return 0;

            Table table;
            try
            {
                table = JsonUtility.FromJson<Table>(json);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[DossierTextTables] JSON inválido em '{sourceName}': {exception.Message}");
                return 0;
            }

            if (table == null || table.texts == null)
                return 0;

            var added = 0;
            foreach (var row in table.texts)
            {
                if (row != null && into.Add(row.key, row.pt, row.en))
                    added++;
            }

            return added;
        }
    }
}
