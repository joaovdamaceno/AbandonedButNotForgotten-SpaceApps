using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-006 · Prévia do painel da varredura IPP: renderiza o UXML real (PanelSettings da UI-001, textura de destino) em
    /// 1280×720 sobre fundo escuro, um quadro por situação, em Docs/Art/Pioneer10_Imaging. Evidência de layout; o fundo é
    /// um substituto da cena 3D e os textos são as CHAVES (o resolvedor de idioma é da UI-004).
    /// Menu: Abandoned Space Apps → PIO-006 → Capturar painel da varredura.
    /// </summary>
    public static class PioneerImagingCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Imaging";
        const int Width = 1280;
        const int Height = 720;
        const int FramesBeforeCapture = 40;

        static readonly (string name, ImagingViewState state)[] Frames =
        {
            ("01_aprender", State(ImagingPhase.Learn, 0, 0.10f, false, false, false, 0, false)),
            ("02_varredura_janela", State(ImagingPhase.Sweep, 2, 0.50f, true, true, false, 0, false)),
            ("03_varredura_fora_da_janela", State(ImagingPhase.Sweep, 3, 0.10f, true, false, false, 0, false)),
            ("04_passagem_perdida", State(ImagingPhase.Sweep, 3, 0.90f, false, false, false, 0, false, missed: true)),
            ("05_ajuda_6s", State(ImagingPhase.Sweep, 1, 0.10f, false, false, false, 1, false)),
            ("06_ajuda_12s_janela_ampla", State(ImagingPhase.Sweep, 1, 0.55f, false, true, false, 2, true)),
            ("07_interrompida_6_de_8", State(ImagingPhase.Interrupted, 6, 0.00f, false, false, true, 0, false)),
            ("08_azul_7_de_8", State(ImagingPhase.Sweep, 7, 0.40f, true, true, false, 0, false)),
            ("09_montada_8_de_8", State(ImagingPhase.Assembled, 8, 0.00f, false, false, false, 0, false)),
        };

        static ImagingViewState State(ImagingPhase phase, int captured, float progress, bool holding, bool inWindow, bool interrupted, int help, bool assisted, bool missed = false) => new ImagingViewState
        {
            Phase = phase, Captured = captured, PassProgress = progress, WindowStart = assisted ? 0f : PioneerImaging.WindowStart, WindowEnd = assisted ? 1f : PioneerImaging.WindowEnd,
            Holding = holding, InWindow = inWindow, Interrupted = interrupted, HelpLevel = help, Assisted = assisted, PassMissed = missed, Target = captured,
        };

        static Queue<(string name, ImagingViewState state)> pending;
        static (string name, ImagingViewState state) current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture target;
        static ImagingPanelView view;
        static int frames;
        static int written;
        static bool exitWhenDone;

        [MenuItem("Abandoned Space Apps/PIO-006/Capturar painel da varredura")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            pending = new Queue<(string, ImagingViewState)>(Frames);
            exitWhenDone = exitAfter;
            written = 0;
            host = null;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
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
                    view.Render(current.state, Key);

                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-006 · captura falhou: " + exception);
                Cleanup();
                pending.Clear();
                Finish(1);
            }
        }

        static string Key(string key) => key;

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-006 capture" };
            target.Create();

            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            panel = Object.Instantiate(baseline);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0.04f, 0.05f, 0.08f, 1f);

            host = new GameObject("PIO-006 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerImagingBuilder.PanelLayoutPath);
            view = host.AddComponent<ImagingPanelView>();
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputFolder, current.name + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            written++;
        }

        static void Cleanup()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            if (panel != null)
                Object.DestroyImmediate(panel);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }

            host = null;
            view = null;
        }

        static void Finish(int code = 0)
        {
            EditorApplication.update -= Tick;
            Debug.Log($"[PIO-006] {written} prévia(s) do painel em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
