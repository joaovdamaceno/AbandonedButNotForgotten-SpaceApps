using System.Collections.Generic;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Confere o conteúdo da orientação sem Unity: toda chave de <see cref="GuidanceKeys.All"/> existe em PT e EN. Usado pelo
    /// teste de assets e pelo menu de validação do Editor.
    /// </summary>
    public static class GuidanceContentCheck
    {
        /// <summary>Divergências em linguagem legível; vazia = conteúdo completo.</summary>
        public static List<string> Validate(DossierTextCatalog texts)
        {
            var problems = new List<string>();
            if (texts == null)
            {
                problems.Add("tabela de textos ausente.");
                return problems;
            }

            var seen = new HashSet<string>();
            foreach (var key in GuidanceKeys.All)
            {
                if (!seen.Add(key))
                    problems.Add($"chave '{key}' repetida em GuidanceKeys.All.");

                if (!texts.TryGet(key, DossierFlow.Portuguese, out _))
                    problems.Add($"chave '{key}' sem texto em PT.");

                if (!texts.TryGet(key, DossierFlow.English, out _))
                    problems.Add($"chave '{key}' sem texto em EN.");
            }

            return problems;
        }
    }
}
