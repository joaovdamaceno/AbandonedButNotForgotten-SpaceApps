using System.IO;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-001: gera de forma determinística os assets derivados do sistema visual — tema, PanelSettings e a
    /// cena de prévia. As fontes são referenciadas como .ttf no USS: o sistema de texto avançado da Unity 6.6
    /// rasteriza a partir da fonte (FontAssets estáticos não são aceitos nele), sem atlas versionado.
    /// Reexecutar reutiliza os mesmos caminhos e GUIDs. Não toca arquivos de outros donos.
    /// </summary>
    public static class UIDesignAssetsBuilder
    {
        public const string DesignRoot = "Assets/_Game/UI/Design";
        public const string LayoutsFolder = DesignRoot + "/Layouts";
        public const string ThemePath = DesignRoot + "/SpaceAppsTheme.tss";
        public const string PanelSettingsPath = DesignRoot + "/SpaceAppsPanelSettings.asset";
        public const string PreviewScenePath = DesignRoot + "/Preview/UIDesignPreview.unity";

        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        public static readonly (string name, string layout, Color backdrop, bool active)[] PreviewLayouts =
        {
            ("Dossiê · PT", LayoutsFolder + "/DossierLayout_PT.uxml", new Color32(0xE4, 0xD8, 0xC2, 0xFF), true),
            ("Dossiê · EN (estresse)", LayoutsFolder + "/DossierLayout_EN.uxml", new Color32(0xE4, 0xD8, 0xC2, 0xFF), false),
            ("HUD · PT (Marte, céu claro)", LayoutsFolder + "/HudLayout_PT.uxml", new Color32(0xC9, 0x9A, 0x6E, 0xFF), false),
            ("HUD · EN (Surveyor, estresse)", LayoutsFolder + "/HudLayout_EN.uxml", new Color32(0x8C, 0x8C, 0x8C, 0xFF), false),
            ("Instrumento · PT", LayoutsFolder + "/InstrumentPanelLayout_PT.uxml", new Color32(0x6E, 0x6E, 0x6E, 0xFF), false),
            ("Folha de componentes", LayoutsFolder + "/ComponentSheet_PT.uxml", new Color32(0x2A, 0x27, 0x23, 0xFF), false),
        };

        [MenuItem("Abandoned Space Apps/UI-001/Reconstruir assets de design", priority = 20)]
        public static void BuildFromMenu() => Build(true);

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Build(true);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Build(bool includePreviewScene)
        {
            EnsureTheme();
            EnsurePanelSettings();

            if (includePreviewScene)
                BuildPreviewScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[UI-001] Assets de design reconstruídos.");
        }

        static void EnsureTheme()
        {
            if (File.Exists(ThemePath))
                return;

            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath);
        }

        static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            ConfigurePanel(panel);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        /// <summary>Escala pela altura: 720 px lógicos sempre; largura lógica = 720 × proporção da tela.</summary>
        public static void ConfigurePanel(PanelSettings panel)
        {
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.textSettings = null; // padrão da Unity; fontes vêm do USS (.ttf)
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
        }

        static void BuildPreviewScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewScenePath) ?? DesignRoot);
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Câmera de prévia");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0xC9, 0x9A, 0x6E, 0xFF);

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var order = 0;
            foreach (var (name, layout, _, active) in PreviewLayouts)
            {
                var go = new GameObject(name);
                var document = go.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(layout);
                document.sortingOrder = order++;
                go.AddComponent<ResponsiveLayoutClass>();
                go.SetActive(active);
            }

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            if (!string.IsNullOrEmpty(previous) && previous != PreviewScenePath)
                EditorSceneManager.OpenScene(previous);
        }
    }
}
