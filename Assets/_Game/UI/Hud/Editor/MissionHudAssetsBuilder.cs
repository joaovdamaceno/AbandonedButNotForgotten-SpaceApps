using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-003: cria ou completa, de forma idempotente, o prefab do HUD compartilhado e valida o conteúdo (textos em PT e EN,
    /// marcadores e UXML). Reexecutar reutiliza o mesmo caminho e GUID. Não toca cenas nem Build Settings (R1).
    /// </summary>
    public static class MissionHudAssetsBuilder
    {
        public const string HudRoot = "Assets/_Game/UI/Hud";
        public const string PrefabPath = HudRoot + "/MissionHUD.prefab";
        public const string UxmlPath = HudRoot + "/MissionHud.uxml";
        public const string TextsPath = HudRoot + "/HudTexts.json";

        /// <summary>Abaixo do dossiê (10) e das camadas modais futuras: o HUD é a camada mais baixa da interface.</summary>
        const float SortingOrder = 5f;

        [MenuItem("Abandoned Space Apps/UI-003/Reconstruir prefab do HUD", priority = 40)]
        public static void BuildFromMenu() => Build();

        [MenuItem("Abandoned Space Apps/UI-003/Validar textos e UXML do HUD", priority = 41)]
        public static void ValidateFromMenu()
        {
            var problems = Validate();
            if (problems.Count == 0)
                Debug.Log("[UI-003] Textos e UXML do HUD válidos.");
            else
                Debug.LogError("[UI-003] Divergências no HUD:\n- " + string.Join("\n- ", problems));
        }

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Build();
                var problems = Validate();
                if (problems.Count > 0)
                    throw new InvalidOperationException("Divergências no HUD:\n- " + string.Join("\n- ", problems));
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
            var uxml = Require<VisualTreeAsset>(UxmlPath, "MissionHud.uxml");
            var texts = Require<TextAsset>(TextsPath, "HudTexts.json");

            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("MissionHUD");
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
            Debug.Log("[UI-003] Prefab do HUD atualizado.");
        }

        /// <summary>Textos completos em PT e EN com seus marcadores, e UXML presente.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) == null)
                problems.Add($"UXML ausente em {UxmlPath}.");

            var texts = new DossierTextCatalog();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            if (asset == null)
                problems.Add($"textos ausentes em {TextsPath}.");
            else
                DossierTextTables.Load(asset, texts);

            problems.AddRange(HudContentCheck.Validate(texts));
            return problems;
        }

        static void Configure(GameObject root, PanelSettings panel, VisualTreeAsset uxml, TextAsset texts)
        {
            // UIDocument primeiro: MissionHUD exige o componente e a ordem fica igual à do prefab versionado.
            var document = root.GetComponent<UIDocument>();
            if (document == null)
                document = root.AddComponent<UIDocument>();

            document.panelSettings = panel;
            document.visualTreeAsset = uxml;
            document.sortingOrder = SortingOrder;

            if (root.GetComponent<ResponsiveLayoutClass>() == null)
                root.AddComponent<ResponsiveLayoutClass>();

            var hud = root.GetComponent<MissionHUD>();
            if (hud == null)
                hud = root.AddComponent<MissionHUD>();

            var serialized = new SerializedObject(hud);
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
