using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-005: cria ou completa, de forma idempotente, o prefab <c>Guidance.prefab</c> (GuidanceController + TutorialPresenter
    /// ligados a GuidanceTexts.json e Guidance.uss) e valida os textos em PT e EN. Reexecutar reutiliza o mesmo caminho e
    /// GUID. Não toca cenas nem Build Settings (R1).
    /// </summary>
    public static class GuidanceAssetsBuilder
    {
        public const string Root = "Assets/_Game/UI/Guidance";
        public const string PrefabPath = Root + "/Guidance.prefab";
        public const string TextsPath = Root + "/GuidanceTexts.json";
        public const string StylePath = Root + "/Guidance.uss";

        [MenuItem("Abandoned Space Apps/UI-005/Reconstruir prefab da orientação", priority = 50)]
        public static void BuildFromMenu() => Build();

        [MenuItem("Abandoned Space Apps/UI-005/Validar textos da orientação", priority = 51)]
        public static void ValidateFromMenu()
        {
            var problems = Validate();
            if (problems.Count == 0)
                Debug.Log("[UI-005] Textos da orientação válidos.");
            else
                Debug.LogError("[UI-005] Divergências na orientação:\n- " + string.Join("\n- ", problems));
        }

        public static void Build()
        {
            var texts = Require<TextAsset>(TextsPath, "GuidanceTexts.json");
            var style = Require<StyleSheet>(StylePath, "Guidance.uss");

            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Guidance");
            try
            {
                Configure(root, texts, style);
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
            Debug.Log("[UI-005] Prefab da orientação atualizado.");
        }

        /// <summary>Textos completos em PT e EN e estilos presentes.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath) == null)
                problems.Add($"estilos ausentes em {StylePath}.");

            var texts = new DossierTextCatalog();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            if (asset == null)
                problems.Add($"textos ausentes em {TextsPath}.");
            else
                DossierTextTables.Load(asset, texts);

            problems.AddRange(GuidanceContentCheck.Validate(texts));
            return problems;
        }

        static void Configure(GameObject root, TextAsset texts, StyleSheet style)
        {
            if (root.GetComponent<GuidanceController>() == null)
                root.AddComponent<GuidanceController>();

            var presenter = root.GetComponent<TutorialPresenter>();
            if (presenter == null)
                presenter = root.AddComponent<TutorialPresenter>();

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("uiTexts").objectReferenceValue = texts;
            serialized.FindProperty("styleSheet").objectReferenceValue = style;
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
