using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Abandoned.SpaceApps.UI;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-004 · Cria <c>Shared/Localization/SubtitleLayer.prefab</c> (UIDocument com o <see cref="SubtitlePresenter"/>) para o caso sem
    /// HUD. Dentro do HUD o apresentador entra na coluna da base (<c>MissionHUD.BottomColumn</c>) e este prefab não é usado.
    /// Idempotente. Menu: Abandoned Space Apps → UI-004 → Criar prefab da camada de legendas.
    /// </summary>
    public static class SubtitleLayerSetup
    {
        public const string Folder = "Assets/_Game/Shared/Localization";
        public const string PrefabPath = Folder + "/SubtitleLayer.prefab";
        public const string LayoutPath = Folder + "/SubtitleLayer.uxml";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset";

        [MenuItem("Abandoned Space Apps/UI-004/Criar prefab da camada de legendas")]
        public static void CreateFromMenu()
        {
            var errors = Create();
            if (errors.Count == 0)
                Debug.Log("[UI-004] prefab da camada de legendas conforme.");
            else
                Debug.LogError("[UI-004] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> Create()
        {
            var errors = new List<string>();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
            {
                errors.Add("SubtitleLayer.uxml ou SpaceAppsPanelSettings ausente.");
                return errors;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = root != null ? (GameObject)PrefabUtility.InstantiatePrefab(root) : new GameObject("SubtitleLayer");
            var document = Get<UIDocument>(instance);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            document.sortingOrder = 20; // acima do HUD e dos painéis dos capítulos
            Get<SubtitlePresenter>(instance);
            Get<ResponsiveLayoutClass>(instance);
            var so = new SerializedObject(instance.GetComponent<SubtitlePresenter>());
            so.FindProperty("document").objectReferenceValue = document;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (saved == null || saved.GetComponent<SubtitlePresenter>() == null || saved.GetComponent<UIDocument>() == null)
                errors.Add("SubtitleLayer.prefab sem os componentes.");
            return errors;
        }

        static T Get<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
