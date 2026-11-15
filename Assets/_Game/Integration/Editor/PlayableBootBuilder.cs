using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Integration.EditorTools
{
    /// <summary>
    /// INT-001 (teste local): monta <c>Boot.unity</c> (serviços do aplicativo, dossiê, HUD, pausa, legendas, áudio) e põe Boot e Marte nas Build
    /// Settings, para jogar Dossiê → Marte (M01–M32) → arquivo sem Preview nem harness. Idempotente.
    /// </summary>
    public static class PlayableBootBuilder
    {
        public const string BootPath = "Assets/_Game/Scenes/Boot.unity";
        public const string MarsPath = "Assets/_Game/Chapters/Mars/Scenes/Mars.unity";
        public const string OpportunityPath = "Assets/_Game/Chapters/Mars/Opportunity/Scenes/Opportunity.unity";

        [MenuItem("Abandoned Space Apps/INT-001/Montar Boot jogável (Dossiê → Marte)")]
        public static void BuildFromMenu() => Build();

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Asset ausente: " + path);
            return asset;
        }

        static List<TextAsset> TextTables()
        {
            var tables = new List<TextAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Game/Chapters/Mars", "Assets/_Game/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Texts.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith("MarsSubtitles.json", StringComparison.OrdinalIgnoreCase))
                    tables.Add(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            }

            return tables.OrderBy(t => t.name).ToList();
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("GameHost");
            var component = host.AddComponent<GameHost>();
            var so = new SerializedObject(component);
            so.FindProperty("catalog").objectReferenceValue = Load<ChapterCatalog>("Assets/_Game/Core/ChapterCatalog.asset");
            so.FindProperty("dossierPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Dossier/DossierScreen.prefab");
            so.FindProperty("missionHudPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Hud/MissionHUD.prefab");
            so.FindProperty("guidancePrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Guidance/Guidance.prefab");
            so.FindProperty("pauseMenuPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Pause/PauseMenu.prefab");
            so.FindProperty("audioDirectorPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/Shared/Audio/AudioDirector.prefab");
            so.FindProperty("audioCatalog").objectReferenceValue = Load<AudioCatalog>("Assets/_Game/Shared/Audio/AudioCatalog.asset");
            var tables = TextTables();
            var array = so.FindProperty("textTables");
            array.arraySize = tables.Count;
            for (var i = 0; i < tables.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = tables[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Câmera de fundo do dossiê; a de Marte (depth maior) a cobre durante o capítulo.
            var cameraObject = new GameObject("Boot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.045f);
            camera.depth = -50f;
            camera.cullingMask = 0;

            EditorSceneManager.SaveScene(scene, BootPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MarsPath, true),
                new EditorBuildSettingsScene(OpportunityPath, true),
            };
            Debug.Log($"[PlayableBootBuilder] {BootPath} montada com {tables.Count} tabelas de texto; Build Settings: Boot, Marte, Opportunity.");
        }
    }
}
