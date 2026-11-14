using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-002: cria ou completa, de forma idempotente, o catálogo de capítulos e o prefab do dossiê, e valida o
    /// conteúdo. Reexecutar reutiliza os mesmos caminhos e GUIDs e nunca remove entradas que outra pessoa
    /// adicionou ao catálogo (INT-001 atribui as definições de Pioneer e Surveyor pelo Inspector).
    /// Não toca <c>Boot.unity</c>, <c>Dossier.unity</c> nem as Build Settings: são da R1.
    /// </summary>
    public static class DossierAssetsBuilder
    {
        public const string DossierRoot = "Assets/_Game/UI/Dossier";
        public const string CatalogPath = "Assets/_Game/Core/ChapterCatalog.asset";
        public const string PrefabPath = DossierRoot + "/DossierScreen.prefab";
        public const string ScreenUxmlPath = DossierRoot + "/DossierScreen.uxml";
        public const string FolderUxmlPath = DossierRoot + "/DossierFolder.uxml";
        public const string TextsPath = DossierRoot + "/DossierTexts.json";

        const string MarsChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string MarsTextsPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";

        /// <summary>As três pastas, na ordem do dossiê. Pioneer e Surveyor ficam sem definição até serem integrados.</summary>
        static readonly (string slotId, string chapterPath, string textsPath, string accentClass)[] Slots =
        {
            ("mars", MarsChapterPath, MarsTextsPath, "sa-chapter--mars"),
            ("pioneer10", null, null, "sa-chapter--pioneer"),
            ("surveyor3", null, null, "sa-chapter--surveyor"),
        };

        [MenuItem("Abandoned Space Apps/UI-002/Reconstruir catálogo e prefab do dossiê", priority = 30)]
        public static void BuildFromMenu() => Build();

        [MenuItem("Abandoned Space Apps/UI-002/Validar catálogo e textos do dossiê", priority = 31)]
        public static void ValidateFromMenu()
        {
            var problems = Validate();
            if (problems.Count == 0)
                Debug.Log("[UI-002] Catálogo e textos do dossiê válidos.");
            else
                Debug.LogError("[UI-002] Divergências no dossiê:\n- " + string.Join("\n- ", problems));
        }

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Build();
                var problems = Validate();
                if (problems.Count > 0)
                    throw new InvalidOperationException("Divergências no dossiê:\n- " + string.Join("\n- ", problems));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Build()
        {
            var catalog = EnsureCatalog();
            EnsurePrefab(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UI-002] Catálogo e prefab do dossiê atualizados.");
        }

        /// <summary>Catálogo válido + textos completos em PT e EN (inclui as tabelas dos capítulos do catálogo).</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<ChapterCatalog>(CatalogPath);
            if (catalog == null)
            {
                problems.Add($"catálogo ausente em {CatalogPath}.");
                return problems;
            }

            problems.AddRange(catalog.Validate());

            var texts = new DossierTextCatalog();
            var ui = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            if (ui == null)
                problems.Add($"textos ausentes em {TextsPath}.");
            else
                DossierTextTables.Load(ui, texts);

            var slotIds = new List<string>();
            foreach (var entry in catalog.Entries)
            {
                if (entry == null)
                    continue;

                slotIds.Add(entry.SlotId);
                DossierTextTables.Load(entry.ChapterTexts, texts);
            }

            problems.AddRange(DossierContentCheck.Validate(texts, slotIds));
            return problems;
        }

        static ChapterCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChapterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ChapterCatalog.Create(new ChapterCatalog.Entry[0]);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            foreach (var slot in Slots)
            {
                if (FindEntryIndex(entries, slot.slotId) >= 0)
                    continue;

                entries.arraySize++;
                var element = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                element.FindPropertyRelative("slotId").stringValue = slot.slotId;
                element.FindPropertyRelative("chapter").objectReferenceValue = Load<ChapterDefinition>(slot.chapterPath);
                element.FindPropertyRelative("chapterTexts").objectReferenceValue = Load<TextAsset>(slot.textsPath);
                element.FindPropertyRelative("accentClass").stringValue = slot.accentClass;
                element.FindPropertyRelative("captureCursor").boolValue = false;
                element.FindPropertyRelative("coverCredit").stringValue = string.Empty;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static int FindEntryIndex(SerializedProperty entries, string slotId)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("slotId").stringValue == slotId)
                    return i;
            }

            return -1;
        }

        static void EnsurePrefab(ChapterCatalog catalog)
        {
            var panel = Require<PanelSettings>(UIDesignAssetsBuilder.PanelSettingsPath, "rode Abandoned Space Apps > UI-001 > Reconstruir assets de design");
            var screenUxml = Require<VisualTreeAsset>(ScreenUxmlPath, "DossierScreen.uxml");
            var folderUxml = Require<VisualTreeAsset>(FolderUxmlPath, "DossierFolder.uxml");
            var texts = Require<TextAsset>(TextsPath, "DossierTexts.json");

            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("DossierScreen");
            try
            {
                Configure(root, catalog, panel, screenUxml, folderUxml, texts);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Configure(GameObject root, ChapterCatalog catalog, PanelSettings panel, VisualTreeAsset screenUxml,
            VisualTreeAsset folderUxml, TextAsset texts)
        {
            // UIDocument primeiro: DossierScreen exige o componente e a ordem fica igual à do prefab versionado.
            var document = root.GetComponent<UIDocument>();
            if (document == null)
                document = root.AddComponent<UIDocument>();

            document.panelSettings = panel;
            document.visualTreeAsset = screenUxml;
            document.sortingOrder = 10f;

            if (root.GetComponent<ResponsiveLayoutClass>() == null)
                root.AddComponent<ResponsiveLayoutClass>();

            var screen = root.GetComponent<DossierScreen>();
            if (screen == null)
                screen = root.AddComponent<DossierScreen>();

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("uiTexts").objectReferenceValue = texts;
            serialized.FindProperty("folderTemplate").objectReferenceValue = folderUxml;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Load<T>(string path) where T : UnityEngine.Object =>
            string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);

        static T Require<T>(string path, string hint) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Asset ausente: {path} ({hint}).");

            return asset;
        }
    }
}
