using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-006: cria ou completa, de forma idempotente, o prefab do menu de pausa e valida o conteúdo (textos PT/EN e os
    /// elementos que o código exige no UXML). Reexecutar reutiliza o mesmo caminho e GUID. Não toca cenas nem Build Settings (R1).
    /// </summary>
    public static class PauseMenuAssetsBuilder
    {
        public const string PauseRoot = "Assets/_Game/UI/Pause";
        public const string PrefabPath = PauseRoot + "/PauseMenu.prefab";
        public const string UxmlPath = PauseRoot + "/Pause.uxml";
        public const string TextsPath = PauseRoot + "/PauseTexts.json";

        /// <summary>Acima do HUD (5), abaixo do dossiê (10): a pausa só existe durante um capítulo.</summary>
        const float SortingOrder = 8f;

        static readonly string[] RequiredElements =
        {
            "pause-root", "pause-corner", "pause-button", "pause-button-label", "pause-button-key", "pause-overlay",
            "pause-card", "pause-title", "pause-subtitle", "view-main", "btn-resume", "btn-settings",
            "btn-restart", "btn-dossier", "pause-hint", "view-settings", "settings-title", "settings-scroll", "btn-back",
            "pause-navigation",
        };

        [MenuItem("Abandoned Space Apps/UI-006/Reconstruir prefab da pausa", priority = 60)]
        public static void BuildFromMenu() => Build();

        [MenuItem("Abandoned Space Apps/UI-006/Validar textos e UXML da pausa", priority = 61)]
        public static void ValidateFromMenu()
        {
            var problems = Validate();
            if (problems.Count == 0)
                Debug.Log("[UI-006] Textos e UXML da pausa válidos.");
            else
                Debug.LogError("[UI-006] Divergências na pausa:\n- " + string.Join("\n- ", problems));
        }

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Build();
                var problems = Validate();
                if (problems.Count > 0)
                    throw new InvalidOperationException("Divergências na pausa:\n- " + string.Join("\n- ", problems));
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
            var panel = Require<PanelSettings>(UIDesignAssetsBuilder.PanelSettingsPath, "rode Abandoned Space Apps > UI-001 > Reconstruir assets de design");
            var uxml = Require<VisualTreeAsset>(UxmlPath, "Pause.uxml");
            var texts = Require<TextAsset>(TextsPath, "PauseTexts.json");

            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("PauseMenu");
            try
            {
                Configure(root, panel, uxml, texts);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UI-006] Prefab da pausa atualizado.");
        }

        /// <summary>Textos completos em PT e EN e UXML com todos os elementos que PauseMenu liga.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
                problems.Add($"UXML ausente em {UxmlPath}.");
            else
            {
                var tree = uxml.CloneTree();
                foreach (var name in RequiredElements)
                {
                    if (tree.Q<VisualElement>(name) == null)
                        problems.Add($"o UXML não tem o elemento '{name}'.");
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            if (asset == null)
            {
                problems.Add($"textos ausentes em {TextsPath}.");
                return problems;
            }

            var texts = new DossierTextCatalog();
            DossierTextTables.Load(asset, texts);
            foreach (var key in PauseKeys.All)
            {
                if (!texts.TryGet(key, SettingsData.Portuguese, out var pt) || string.IsNullOrWhiteSpace(pt))
                    problems.Add($"chave '{key}' sem texto em PT.");
                if (!texts.TryGet(key, SettingsData.English, out var en) || string.IsNullOrWhiteSpace(en))
                    problems.Add($"chave '{key}' sem texto em EN.");
            }

            return problems;
        }

        static void Configure(GameObject root, PanelSettings panel, VisualTreeAsset uxml, TextAsset texts)
        {
            var document = root.GetComponent<UIDocument>();
            if (document == null)
                document = root.AddComponent<UIDocument>();

            document.panelSettings = panel;
            document.visualTreeAsset = uxml;
            document.sortingOrder = SortingOrder;

            if (root.GetComponent<ResponsiveLayoutClass>() == null)
                root.AddComponent<ResponsiveLayoutClass>();

            var menu = root.GetComponent<PauseMenu>();
            if (menu == null)
                menu = root.AddComponent<PauseMenu>();

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("uiTexts").objectReferenceValue = texts;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Require<T>(string path, string hint) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Asset ausente: {path} ({hint}).");

            return asset;
        }
    }
}
