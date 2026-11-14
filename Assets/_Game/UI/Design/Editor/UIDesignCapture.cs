using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// UI-001: renderiza os layouts estáticos fora da tela (PanelSettings.targetTexture) em 960×600,
    /// 1280×720 e 1024×768 e grava PNGs em Docs/Art/UI_Previews. Evidência de layout, não de jogo:
    /// fundo liso no lugar do 3D e nenhuma lógica de UI-002/UI-003.
    /// </summary>
    public static class UIDesignCapture
    {
        public const string OutputFolder = "Docs/Art/UI_Previews";

        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(960, 600),
            new Vector2Int(1280, 720),
            new Vector2Int(1024, 768),
        };

        const int FramesBeforeCapture = 12;

        static Queue<(string name, string layout, Color backdrop, Vector2Int size)> pending;
        static (string name, string layout, Color backdrop, Vector2Int size) current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture target;
        static int frames;
        static bool exitWhenDone;
        static int written;

        [MenuItem("Abandoned Space Apps/UI-001/Capturar prévias PNG", priority = 21)]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            UIDesignAssetsBuilder.Build(false);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutputFolder);

            pending = new Queue<(string, string, Color, Vector2Int)>();
            foreach (var (name, layout, backdrop, _) in UIDesignAssetsBuilder.PreviewLayouts)
            foreach (var size in Resolutions)
                pending.Enqueue((name, layout, backdrop, size));

            exitWhenDone = exitAfter;
            written = 0;
            host = null;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (host == null)
            {
                if (pending.Count == 0)
                {
                    Finish();
                    return;
                }

                StartNext();
            }

            frames++;
            EditorApplication.QueuePlayerLoopUpdate();

            if (frames == FramesBeforeCapture / 2)
            {
                // Mesma regra do componente de runtime, aplicada de forma síncrona para a captura.
                var root = host.GetComponent<UIDocument>().rootVisualElement;
                root.EnableInClassList(ResponsiveLayoutClass.NarrowClass,
                    ResponsiveLayoutClass.IsNarrow(root.layout.width, ResponsiveLayoutClass.DefaultNarrowBelowLogicalWidth));
            }

            if (frames < FramesBeforeCapture)
                return;

            Save();
            Cleanup();
        }

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            target = new RenderTexture(current.size.x, current.size.y, 24, RenderTextureFormat.ARGB32) { name = "UI-001 capture" };
            target.Create();

            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>(UIDesignAssetsBuilder.PanelSettingsPath);
            panel = Object.Instantiate(baseline);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = current.backdrop;

            host = new GameObject("UI-001 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(current.layout);
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            var file = Path.GetFileNameWithoutExtension(current.layout) + $"_{current.size.x}x{current.size.y}.png";
            File.WriteAllBytes(Path.Combine(OutputFolder, file), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            written++;
        }

        static void Cleanup()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(panel);
            target.Release();
            Object.DestroyImmediate(target);
            host = null;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Debug.Log($"[UI-001] {written} prévias gravadas em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(0);
        }
    }
}
