using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abandoned.SpaceApps.UI;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-006: cria a cena de prévia <c>PausePreview</c> para testar a pausa à mão (Play). Idempotente: recria o arquivo.
    /// Não mexe em Build Settings (R1).
    /// </summary>
    public static class PausePreviewSceneBuilder
    {
        public const string ScenePath = PauseMenuAssetsBuilder.PauseRoot + "/Preview/PausePreview.unity";

        [MenuItem("Abandoned Space Apps/UI-006/Criar cena de prévia da pausa", priority = 62)]
        public static void BuildFromMenu() => Build();

        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuAssetsBuilder.PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Rode antes: Abandoned Space Apps > UI-006 > Reconstruir prefab da pausa.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 9f, -9f), Quaternion.Euler(45f, 0f, 0f));

            var lightObject = new GameObject("Sun");
            lightObject.AddComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            var mover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mover.name = "Mover (WASD)";
            mover.transform.position = new Vector3(0f, 0.5f, 0f);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "Indicador de E";
            indicator.transform.SetParent(mover.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            indicator.transform.localScale = Vector3.one * 0.6f;

            var eventSystem = new GameObject("EventSystem");
            var es = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI"); if (es != null) eventSystem.AddComponent(es);
            var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
                eventSystem.AddComponent(module); // o assembly do Editor não referencia o Input System: por nome

            var menu = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var host = new GameObject("PausePreviewHost");
            var hostComponent = host.AddComponent<PausePreviewHost>();
            var serialized = new SerializedObject(hostComponent);
            serialized.FindProperty("pauseMenu").objectReferenceValue = menu.GetComponent<PauseMenu>();
            serialized.FindProperty("mover").objectReferenceValue = mover.transform;
            serialized.FindProperty("interactIndicator").objectReferenceValue = indicator.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[UI-006] Cena de prévia criada em " + ScenePath + ". Abra-a e dê Play.");
        }
    }
}
