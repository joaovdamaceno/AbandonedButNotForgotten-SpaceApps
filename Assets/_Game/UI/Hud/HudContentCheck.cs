using System;
using System.Collections.Generic;
using System.Reflection;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Confere o conteúdo do HUD sem Unity: toda chave de <see cref="HudKeys"/> existe em PT e EN e os textos com marcador
    /// ({action}, {percent}, {seconds}) o mantêm nos dois idiomas, senão o prompt perderia o verbo, o progresso ou o tempo
    /// do comando em silêncio. Usado pelo teste de assets e pelo menu de validação do Editor.
    /// </summary>
    public static class HudContentCheck
    {
        static readonly KeyValuePair<string, string>[] RequiredPlaceholders =
        {
            new KeyValuePair<string, string>(HudKeys.PromptHold, HudFormat.ActionPlaceholder),
            new KeyValuePair<string, string>(HudKeys.PromptToggle, HudFormat.ActionPlaceholder),
            new KeyValuePair<string, string>(HudKeys.PromptToggleStop, HudFormat.ActionPlaceholder),
            new KeyValuePair<string, string>(HudKeys.PromptPress, HudFormat.ActionPlaceholder),
            new KeyValuePair<string, string>(HudKeys.StatusHolding, HudFormat.PercentPlaceholder),
            new KeyValuePair<string, string>(HudKeys.StatusPaused, HudFormat.PercentPlaceholder),
            new KeyValuePair<string, string>(HudKeys.CommandTransit, HudFormat.SecondsPlaceholder),
            new KeyValuePair<string, string>(HudKeys.CommandStatusTransit, HudFormat.PercentPlaceholder),
        };

        public static List<string> RequiredKeys()
        {
            var keys = new List<string>();
            foreach (var field in typeof(HudKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    keys.Add((string)field.GetRawConstantValue());
            }

            return keys;
        }

        /// <summary>Divergências em linguagem legível; vazia = conteúdo completo.</summary>
        public static List<string> Validate(DossierTextCatalog texts)
        {
            var problems = new List<string>();
            if (texts == null)
            {
                problems.Add("tabela de textos ausente.");
                return problems;
            }

            foreach (var key in RequiredKeys())
            {
                if (!texts.TryGet(key, DossierFlow.Portuguese, out _))
                    problems.Add($"chave '{key}' sem texto em PT.");
                if (!texts.TryGet(key, DossierFlow.English, out _))
                    problems.Add($"chave '{key}' sem texto em EN.");
            }

            foreach (var pair in RequiredPlaceholders)
            {
                foreach (var language in new[] { DossierFlow.Portuguese, DossierFlow.English })
                {
                    if (texts.TryGet(pair.Key, language, out var text) && text.IndexOf(pair.Value, StringComparison.Ordinal) < 0)
                        problems.Add($"chave '{pair.Key}' ({language}) perdeu o marcador {pair.Value}.");
                }
            }

            foreach (var key in texts.KeysMissingLanguage())
                problems.Add($"chave '{key}' existe mas está vazia em um dos idiomas.");

            return problems;
        }
    }
}
