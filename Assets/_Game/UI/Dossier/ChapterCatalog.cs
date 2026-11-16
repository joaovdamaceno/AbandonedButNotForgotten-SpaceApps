using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Catálogo das pastas do dossiê (UI-002). A ordem da lista é a ordem na tela e o numeral do capítulo
    /// (I, II, III), mas é só uma etiqueta: nenhum capítulo depende de outro. O asset fica em
    /// <c>Assets/_Game/Core/ChapterCatalog.asset</c>.
    /// <para>
    /// O catálogo guarda apenas referências leves (definição do capítulo, capa e tabela de textos): nenhum
    /// modelo 3D, cena ou áudio pesado entra no menu. Um capítulo ainda não integrado fica com
    /// <see cref="Entry.Chapter"/> vazio e a pasta aparece como "em preparação", sem botão ativo.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterCatalog", menuName = "Abandoned/Chapter Catalog")]
    public sealed class ChapterCatalog : ScriptableObject
    {
        /// <summary>Toda classe de identidade visual do capítulo (UI_SPEC): sa-chapter--mars, --pioneer, --surveyor.</summary>
        public const string AccentClassPrefix = "sa-chapter--";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("ID estável da pasta. Igual ao chapterId do ChapterDefinition (mars, pioneer10, surveyor3).")]
            string slotId;

            [SerializeField, Tooltip("Definição do capítulo. Deixe vazio enquanto o capítulo não foi integrado: a pasta aparece como em preparação.")]
            ChapterDefinition chapter;

            [SerializeField, Tooltip("Tabela JSON de textos do capítulo ({\"texts\":[{\"key\",\"pt\",\"en\"}]}) que resolve as chaves título/descrição/pergunta.")]
            TextAsset chapterTexts;

            [SerializeField, Tooltip("Classe de identidade visual, ex.: sa-chapter--mars.")]
            string accentClass;

            [SerializeField, Tooltip("Captura o cursor no clique em Iniciar. Desligado por padrão: o controle de câmera manual é opcional em todos os capítulos.")]
            bool captureCursor;

            [SerializeField, Tooltip("Crédito exibido sobre a capa. Obrigatório quando o ChapterDefinition tem capa.")]
            string coverCredit;

            public string SlotId => slotId;
            public ChapterDefinition Chapter => chapter;
            public TextAsset ChapterTexts => chapterTexts;
            public string AccentClass => accentClass;
            public bool CaptureCursor => captureCursor;
            public string CoverCredit => coverCredit;

            /// <summary>Definição presente e válida: só então o botão Iniciar fica ativo.</summary>
            public bool IsAvailable => chapter != null && chapter.Validate().Count == 0;

            public static Entry Create(string slotId, ChapterDefinition chapter, string accentClass, TextAsset chapterTexts = null,
                bool captureCursor = false, string coverCredit = null) =>
                new Entry
                {
                    slotId = slotId,
                    chapter = chapter,
                    accentClass = accentClass,
                    chapterTexts = chapterTexts,
                    captureCursor = captureCursor,
                    coverCredit = coverCredit,
                };
        }

        [SerializeField]
        List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Cria um catálogo em memória (testes e builders). Não valida: chame <see cref="Validate"/>.</summary>
        public static ChapterCatalog Create(IEnumerable<Entry> entries)
        {
            var catalog = CreateInstance<ChapterCatalog>();
            catalog.entries = new List<Entry>(entries ?? new List<Entry>());
            return catalog;
        }

        public Entry Find(string slotId)
        {
            if (slotId == null)
                return null;

            foreach (var entry in entries)
            {
                if (entry != null && entry.SlotId == slotId)
                    return entry;
            }

            return null;
        }

        /// <summary>Pasta cujo <c>ChapterDefinition.ChapterId</c> é o informado (evento <c>ChapterLoading</c> traz o chapterId).</summary>
        public Entry FindByChapterId(string chapterId)
        {
            if (chapterId == null)
                return null;

            foreach (var entry in entries)
            {
                if (entry != null && entry.Chapter != null && entry.Chapter.ChapterId == chapterId)
                    return entry;
            }

            return null;
        }

        /// <summary>Lista as divergências; vazia = válida. Capítulo ainda não integrado (definição vazia) é permitido.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"ChapterCatalog '{name}'";

            if (entries == null || entries.Count == 0)
            {
                ContractValidation.Report(errors, owner, "precisa de ao menos uma pasta.");
                return errors;
            }

            var slots = new HashSet<string>();
            var chapterIds = new HashSet<string>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    ContractValidation.Report(errors, owner, $"a entrada {i} é nula.");
                    continue;
                }

                var label = $"entrada {i} ('{entry.SlotId}')";
                if (!ContractValidation.IsValidId(entry.SlotId))
                    ContractValidation.Report(errors, owner, $"{label}: 'slotId' vazio ou inválido.");
                else if (!slots.Add(entry.SlotId))
                    ContractValidation.Report(errors, owner, $"{label}: 'slotId' duplicado.");

                if (string.IsNullOrEmpty(entry.AccentClass) || !entry.AccentClass.StartsWith(AccentClassPrefix, StringComparison.Ordinal)
                    || entry.AccentClass.Length == AccentClassPrefix.Length)
                    ContractValidation.Report(errors, owner, $"{label}: 'accentClass' deve começar com '{AccentClassPrefix}' (ex.: sa-chapter--mars).");

                if (entry.Chapter == null)
                    continue;

                foreach (var chapterError in entry.Chapter.Validate())
                    ContractValidation.Report(errors, owner, $"{label}: {chapterError}");

                if (entry.Chapter.ChapterId != entry.SlotId)
                    ContractValidation.Report(errors, owner, $"{label}: o ChapterDefinition tem chapterId '{entry.Chapter.ChapterId}', diferente do slotId.");
                else if (!chapterIds.Add(entry.Chapter.ChapterId))
                    ContractValidation.Report(errors, owner, $"{label}: o mesmo capítulo aparece em duas pastas.");

                if (entry.Chapter.Cover != null && string.IsNullOrWhiteSpace(entry.CoverCredit))
                    ContractValidation.Report(errors, owner, $"{label}: a capa precisa de 'coverCredit' (procedência registrada em ASSET_CREDITS.md).");
            }

            return errors;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var errors = Validate();
            if (errors.Count > 0)
                Debug.LogWarning($"[{nameof(ChapterCatalog)}] {name}:{ContractValidation.Describe(errors)}", this);
        }
#endif
    }
}
