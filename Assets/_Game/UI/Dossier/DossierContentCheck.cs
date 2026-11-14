using System;
using System.Collections.Generic;
using System.Reflection;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Confere o conteúdo do dossiê sem Unity: toda chave de <see cref="DossierKeys"/> e todo texto de pasta
    /// (<c>slot.&lt;id&gt;.*</c>) precisam existir em PT e EN, e os créditos precisam ter ao menos uma linha.
    /// Usado pelo teste de assets e pelo menu de validação do Editor.
    /// </summary>
    public static class DossierContentCheck
    {
        static readonly string[] SlotFields =
        {
            DossierKeys.SlotTitle, DossierKeys.SlotMeta, DossierKeys.SlotQuestion, DossierKeys.SlotDetail,
        };

        /// <summary>Chaves de texto exigidas: as constantes de <see cref="DossierKeys"/> e os campos de cada pasta.</summary>
        public static List<string> RequiredKeys(IEnumerable<string> slotIds)
        {
            var keys = new List<string>();
            foreach (var field in typeof(DossierKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!field.IsLiteral || field.IsInitOnly || field.FieldType != typeof(string))
                    continue;

                // SlotTitle/SlotMeta/... são nomes de campo e CreditsLinePrefix é um prefixo: não são chaves.
                if (field.Name.StartsWith("Slot", StringComparison.Ordinal) || field.Name == nameof(DossierKeys.CreditsLinePrefix))
                    continue;

                keys.Add((string)field.GetRawConstantValue());
            }

            foreach (var slotId in slotIds ?? new string[0])
            {
                foreach (var field in SlotFields)
                    keys.Add(DossierKeys.Slot(slotId, field));
            }

            return keys;
        }

        /// <summary>Divergências em linguagem legível; vazia = conteúdo completo.</summary>
        public static List<string> Validate(DossierTextCatalog texts, IEnumerable<string> slotIds)
        {
            var problems = new List<string>();
            if (texts == null)
            {
                problems.Add("tabela de textos ausente.");
                return problems;
            }

            foreach (var key in RequiredKeys(slotIds))
            {
                if (!texts.TryGet(key, DossierFlow.Portuguese, out _))
                    problems.Add($"chave '{key}' sem texto em PT.");
                if (!texts.TryGet(key, DossierFlow.English, out _))
                    problems.Add($"chave '{key}' sem texto em EN.");
            }

            if (texts.KeysWithPrefix(DossierKeys.CreditsLinePrefix).Count == 0)
                problems.Add("os créditos precisam de ao menos uma linha 'credits.line.*'.");

            foreach (var key in texts.KeysMissingLanguage())
                problems.Add($"chave '{key}' existe mas está vazia em um dos idiomas.");

            return problems;
        }
    }
}
