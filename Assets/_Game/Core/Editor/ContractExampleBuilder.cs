using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abandoned.SpaceApps.Examples;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Constrói a cena de teste e o asset de capítulo de exemplo de forma determinística. Reexecutar reutiliza
    /// os mesmos arquivos e objetos, sem trocar GUIDs nem fileIDs.
    /// </summary>
    public static class ContractExampleBuilder
    {
        public const string Folder = "Assets/_Game/Core/Contracts/Examples";
        public const string ChapterPath = Folder + "/ExampleChapter.asset";
        public const string ScenePath = Folder + "/ContractExample.unity";
        const string HarnessObjectName = "ContractExample";

        [MenuItem("Abandoned Space Apps/CORE-002/Criar cena de exemplo dos contratos", priority = 30)]
        public static void BuildFromMenu() => Build();

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Build();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Build()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            if (chapter == null)
            {
                var template = ContractExampleFlow.CreateExampleChapter();
                AssetDatabase.CreateAsset(template, ChapterPath);
                chapter = template;
            }

            var previous = SceneManager.GetActiveScene().path;
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == HarnessObjectName)
                    host = root;
            }

            host = host != null ? host : new GameObject(HarnessObjectName);
            var harness = host.GetComponent<ContractExampleHarness>();
            if (harness == null)
                harness = host.AddComponent<ContractExampleHarness>();

            var serialized = new SerializedObject(harness);
            serialized.FindProperty("chapter").objectReferenceValue = chapter;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
                EditorSceneManager.OpenScene(previous);

            Debug.Log("[CORE-002] Cena de exemplo dos contratos pronta em " + ScenePath);
        }
    }
}
