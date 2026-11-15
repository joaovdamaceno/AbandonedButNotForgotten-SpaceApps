using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Cria a cena de TESTE JOGÁVEL da Pioneer 10 (<c>Playtest/Pioneer10_Playtest.unity</c>): uma cena mínima com o
    /// <see cref="PioneerPlaytestHost"/>, que carrega <c>Pioneer10.unity</c> em modo aditivo. Não é a integração (INT-001) e a cena
    /// NÃO entra nas Build Settings. Idempotente. Menu: Abandoned Space Apps → PIO → Criar cena de teste jogável.
    /// </summary>
    public static class PioneerPlaytestBuilder
    {
        public const string ScenePath = "Assets/_Game/Chapters/Pioneer10/Playtest/Pioneer10_Playtest.unity";
        const string ChapterPath = "Assets/_Game/Chapters/Pioneer10/Data/PioneerChapter.asset";
        const string TextsPath = "Assets/_Game/Shared/Localization/Pioneer10Texts.json";
        const string AudioPrefabPath = "Assets/_Game/Shared/Audio/AudioDirector.prefab";
        const string SubtitlePrefabPath = "Assets/_Game/Shared/Localization/SubtitleLayer.prefab";

        [MenuItem("Abandoned Space Apps/PIO/Criar cena de teste jogável")]
        public static void CreateFromMenu()
        {
            var errors = Create();
            if (errors.Count == 0)
                Debug.Log("[PIO] cena de teste criada: " + ScenePath);
            else
                Debug.LogError("[PIO] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static System.Collections.Generic.List<string> Create()
        {
            var errors = new System.Collections.Generic.List<string>();
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var texts = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            var subtitle = AssetDatabase.LoadAssetAtPath<GameObject>(SubtitlePrefabPath);
            if (chapter == null)
                errors.Add(ChapterPath + " ausente (rode o builder pioneer10.zchapter).");
            if (texts == null)
                errors.Add(TextsPath + " ausente (python Tools/export_pioneer_texts.py).");
            if (subtitle == null)
                errors.Add(SubtitlePrefabPath + " ausente (menu UI-004).");
            if (errors.Count > 0)
                return errors;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Recarrega depois de NewScene: a troca de cena descarrega assets não usados e deixaria as referências antigas nulas.
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            texts = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            subtitle = AssetDatabase.LoadAssetAtPath<GameObject>(SubtitlePrefabPath);
            var audio = AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefabPath);
            var host = new GameObject("[PLAYTEST] Pioneer 10");
            var component = host.AddComponent<PioneerPlaytestHost>();
            var so = new SerializedObject(component);
            so.FindProperty("chapter").objectReferenceValue = chapter;
            so.FindProperty("subtitleLayer").objectReferenceValue = subtitle;
            so.FindProperty("audioDirectorPrefab").objectReferenceValue = audio;
            var tables = so.FindProperty("textTables");
            tables.arraySize = 1;
            tables.GetArrayElementAtIndex(0).objectReferenceValue = texts;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            return errors;
        }
    }
}
