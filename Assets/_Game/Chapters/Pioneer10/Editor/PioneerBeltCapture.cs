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
    /// PIO-005 · Prévia do painel do cinturão: renderiza o UXML real (PanelSettings da UI-001, textura de destino) em
    /// 1280×720 sobre fundo escuro, um quadro por situação, em Docs/Art/Pioneer10_Belt. Evidência de layout; o fundo é
    /// um substituto da cena 3D e os textos são as CHAVES (o resolvedor de idioma é da UI-004).
    /// Menu: Abandoned Space Apps → PIO-005 → Capturar painel do cinturão.
    /// </summary>
    public static class PioneerBeltCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Belt";
        const int Width = 1280;
        const int Height = 720;
        const int FramesBeforeCapture = 40;

        static readonly (string name, BeltViewState state)[] Frames =
        {
            ("01_mapa", new BeltViewState { Phase = BeltPhase.Map }),
            ("02_fantasia", Fantasy(new Vector2(0.28f, 0.50f), new Vector2(0.62f, 0.30f), new Vector2(0.78f, 0.66f), new Vector2(0.98f, 0.45f), false, 0)),
            ("03_fantasia_toc", Fantasy(new Vector2(0.55f, 0.32f), new Vector2(0.58f, 0.30f), new Vector2(0.80f, 0.70f), new Vector2(1.00f, 0.50f), true, 1)),
            ("04_registro_inicio", Record(0, 0f, false, 0, false)),
            ("05_registro_segurando", Record(2, 0.55f, true, 0, false)),
            ("06_registro_ajuda_6s", Record(1, 0f, false, 1, false)),
            ("07_registro_ajuda_12s", Record(1, 0f, false, 2, false)),
            ("08_registro_cinco", Record(5, 1f, false, 0, false)),
        };

        static BeltViewState Fantasy(Vector2 probe, Vector2 a, Vector2 b, Vector2 c, bool deformed, int arrow) => new BeltViewState
        {
            Phase = BeltPhase.Fantasy, SealVisible = true, Probe = probe, RockA = a, RockB = b, RockC = c, ProbeDeformed = deformed, ArrowDirection = arrow,
        };

        static BeltViewState Record(int recorded, float progress, bool acquiring, int help, bool assisted) => new BeltViewState
        {
            Phase = BeltPhase.Record, Recorded = recorded, Focus = recorded, FocusProgress = progress, Acquiring = acquiring, HelpLevel = help, Assisted = assisted,
        };

        static Queue<(string name, BeltViewState state)> pending;
        static (string name, BeltViewState state) current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture target;
        static BeltPanelView view;
        static int frames;
        static int written;
        static bool exitWhenDone;

        [MenuItem("Abandoned Space Apps/PIO-005/Capturar painel do cinturão")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            pending = new Queue<(string, BeltViewState)>(Frames);
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
                Debug.LogError("PIO-005 · captura falhou: " + exception);
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
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-005 capture" };
            target.Create();

            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            panel = Object.Instantiate(baseline);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0.04f, 0.05f, 0.08f, 1f);

            host = new GameObject("PIO-005 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerBeltBuilder.PanelLayoutPath);
            view = host.AddComponent<BeltPanelView>();
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
            Debug.Log($"[PIO-005] {written} prévia(s) do painel em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
