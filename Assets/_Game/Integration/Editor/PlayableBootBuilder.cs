using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Integration.EditorTools
{
    /// <summary>
    /// INT-001: monta <c>Boot.unity</c> (serviços do aplicativo, dossiê, HUD, pausa, legendas, áudio), liga Pioneer 10 e Surveyor 3 ao catálogo
    /// do dossiê e grava as Build Settings do jogo: Boot, Marte, Opportunity, Pioneer10 e SurveyorChapter (nenhuma cena de prévia, de teste
    /// ou de entrada isolada). Não existe <c>Dossier.unity</c>: o dossiê é instanciado pelo <see cref="GameHost"/> dentro do Boot. Idempotente.
    /// </summary>
    public static class PlayableBootBuilder
    {
        public const string BootPath = "Assets/_Game/Scenes/Boot.unity";
        public const string MarsPath = "Assets/_Game/Chapters/Mars/Scenes/Mars.unity";
        public const string OpportunityPath = "Assets/_Game/Chapters/Mars/Opportunity/Scenes/Opportunity.unity";
        public const string PioneerPath = "Assets/_Game/Chapters/Pioneer10/Scenes/Pioneer10.unity";
        public const string SurveyorPath = "Assets/_Game/Chapters/Surveyor/Scenes/SurveyorChapter.unity";

        public const string CatalogPath = "Assets/_Game/Core/ChapterCatalog.asset";
        public const string PioneerChapterPath = "Assets/_Game/Chapters/Pioneer10/Data/PioneerChapter.asset";
        public const string SurveyorChapterPath = "Assets/_Game/Chapters/Surveyor/Chapter/SurveyorChapter.asset";
        public const string SurveyorTextsPath = "Assets/_Game/Chapters/Surveyor/Chapter/ChapterTexts.json";
        public const string PioneerTextsPath = "Assets/_Game/Shared/Localization/Pioneer10Texts.json";

        /// <summary>Crédito da capa do Surveyor (a mesma procedência que o arquivo isolado do capítulo registra).</summary>
        public const string SurveyorCoverCredit = "NASA · AS12-48-7134";

        /// <summary>As cenas do jogo, na ordem das Build Settings (Boot é o índice 0).</summary>
        public static readonly string[] GameScenes = { BootPath, MarsPath, OpportunityPath, PioneerPath, SurveyorPath };

        [MenuItem("Abandoned Space Apps/INT-001/Montar Boot jogável (Dossiê → três capítulos)")]
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

        /// <summary>
        /// Tabelas de texto entregues ao <see cref="GameHost"/>: verbos, objetos, objetivos e legendas de Marte, os textos da interface global e as
        /// legendas da Pioneer. O Surveyor carrega as próprias tabelas (<c>DocumentaryTexts.json</c>) no host do capítulo.
        /// </summary>
        static List<TextAsset> TextTables()
        {
            var tables = new List<TextAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Game/Chapters/Mars", "Assets/_Game/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Texts.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith("MarsSubtitles.json", StringComparison.OrdinalIgnoreCase))
                    tables.Add(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            }

            tables.Add(Load<TextAsset>(PioneerTextsPath));
            return tables.OrderBy(t => t.name).ToList();
        }

        public static void Build()
        {
            EnsureCatalogEntries();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Câmera de fundo do dossiê; a de cada capítulo (depth maior) a cobre. Leva o AudioListener do Boot: o GameHost o desliga assim que
            // um capítulo ativa o seu, para haver um único ouvinte por estado.
            var cameraObject = new GameObject("Boot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.045f);
            camera.depth = -50f;
            camera.cullingMask = 0;
            cameraObject.AddComponent<AudioListener>();

            var host = new GameObject("GameHost");
            var component = host.AddComponent<GameHost>();
            var so = new SerializedObject(component);
            so.FindProperty("catalog").objectReferenceValue = Load<ChapterCatalog>(CatalogPath);
            so.FindProperty("dossierPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Dossier/DossierScreen.prefab");
            so.FindProperty("missionHudPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Hud/MissionHUD.prefab");
            so.FindProperty("subtitleLayerPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/Shared/Localization/SubtitleLayer.prefab");
            so.FindProperty("guidancePrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Guidance/Guidance.prefab");
            so.FindProperty("pauseMenuPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/UI/Pause/PauseMenu.prefab");
            so.FindProperty("audioDirectorPrefab").objectReferenceValue = Load<GameObject>("Assets/_Game/Shared/Audio/AudioDirector.prefab");
            so.FindProperty("audioCatalog").objectReferenceValue = Load<AudioCatalog>("Assets/_Game/Shared/Audio/AudioCatalog.asset");
            so.FindProperty("bootCamera").objectReferenceValue = camera;
            var tables = TextTables();
            var array = so.FindProperty("textTables");
            array.arraySize = tables.Count;
            for (var i = 0; i < tables.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = tables[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootPath);
            WriteBuildSettings();

            var problems = DossierAssetsBuilder.Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException("Catálogo e textos do dossiê com divergências:\n- " + string.Join("\n- ", problems));

            Debug.Log($"[PlayableBootBuilder] {BootPath} montada com {tables.Count} tabelas de texto; catálogo com as três pastas; Build Settings: {string.Join(", ", GameScenes.Select(System.IO.Path.GetFileNameWithoutExtension))}.");
        }

        /// <summary>Grava as cinco cenas do jogo nas Build Settings (e nenhuma outra).</summary>
        public static void WriteBuildSettings()
        {
            foreach (var path in GameScenes)
                Load<SceneAsset>(path);

            EditorBuildSettings.scenes = GameScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        /// <summary>
        /// Atribui <c>PioneerChapter.asset</c> e <c>SurveyorChapter.asset</c> às pastas do dossiê (o <c>slotId</c> é igual ao <c>chapterId</c>) e
        /// preenche o texto de capa com o crédito. Preserva a pasta de Marte e qualquer entrada que outra pessoa tenha acrescentado.
        /// </summary>
        public static void EnsureCatalogEntries()
        {
            var catalog = Load<ChapterCatalog>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            SetEntry(entries, "pioneer10", Load<ChapterDefinition>(PioneerChapterPath), null, "sa-chapter--pioneer", string.Empty);
            SetEntry(entries, "surveyor3", Load<ChapterDefinition>(SurveyorChapterPath), Load<TextAsset>(SurveyorTextsPath), "sa-chapter--surveyor", SurveyorCoverCredit);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void SetEntry(SerializedProperty entries, string slotId, ChapterDefinition chapter, TextAsset texts, string accentClass, string coverCredit)
        {
            SerializedProperty element = null;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var candidate = entries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("slotId").stringValue == slotId)
                {
                    element = candidate;
                    break;
                }
            }

            if (element == null)
            {
                entries.arraySize++;
                element = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                element.FindPropertyRelative("slotId").stringValue = slotId;
                element.FindPropertyRelative("captureCursor").boolValue = false;
            }

            element.FindPropertyRelative("chapter").objectReferenceValue = chapter;
            element.FindPropertyRelative("chapterTexts").objectReferenceValue = texts;
            element.FindPropertyRelative("accentClass").stringValue = accentClass;
            element.FindPropertyRelative("coverCredit").stringValue = coverCredit;
        }
    }
}
