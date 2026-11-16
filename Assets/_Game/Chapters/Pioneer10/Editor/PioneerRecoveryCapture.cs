using System;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-008 · Prévia da interrupção, da recuperação e da revelação histórica. Renderiza os UXML reais (PanelSettings da
    /// UI-001) sobre um fundo CLARO de propósito (pior caso), em <c>Docs/Art/Pioneer10_Recovery/&lt;estado&gt;_&lt;L&gt;x&lt;A&gt;.png</c>,
    /// em 1280×720 e 1024×768 (classe <c>sa-layout--narrow</c>). Textos aparecem como CHAVES (o resolvedor é da UI-004).
    /// Menu: Abandoned Space Apps → PIO-008 → Capturar recuperação e revelação. <b>Precisa da janela da Unity em primeiro
    /// plano</b>; sem foco o painel sai preto.
    /// </summary>
    public static class PioneerRecoveryCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Recovery";
        const int FramesBeforeCapture = 40;
        static readonly Color Backdrop = new Color(0.788f, 0.604f, 0.431f, 1f); // céu marciano #C99A6E

        struct Item
        {
            public string Name;
            public ImagingViewState Imaging;
            public RecoveryPhase Recovery;
            public float RecoveryProgress;
            public float RevealSeconds; // < 0 = sem revelação
            public Vector2Int Size;
        }

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static RenderTexture target;
        static PanelSettings panel;
        static ImagingPanelView imagingView;
        static ImagingRecovery recovery;
        static HistoricalImageReveal reveal;
        static int frames, written;
        static bool exitWhenDone;

        static ImagingViewState Imaging(ImagingPhase phase, int captured, bool interrupted, ImagingHoldState hold) => new ImagingViewState
        {
            Phase = phase, Captured = captured, Interrupted = interrupted, Hold = hold, Target = captured, WindowStart = 0.25f, WindowEnd = 0.75f,
        };

        [MenuItem("Abandoned Space Apps/PIO-008/Capturar recuperação e revelação")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png"))
                File.Delete(old);

            var states = new (string name, ImagingViewState imaging, RecoveryPhase recovery, float progress, float reveal)[]
            {
                ("01_interrompida_aguardando_E", Imaging(ImagingPhase.Interrupted, 6, true, ImagingHoldState.Paused), RecoveryPhase.Awaiting, 0f, -1f),
                ("02_pacote_enviado_meio", Imaging(ImagingPhase.Interrupted, 6, true, ImagingHoldState.Paused), RecoveryPhase.Sending, 0.5f, -1f),
                ("03_montada_8_de_8", Imaging(ImagingPhase.Assembled, 8, false, ImagingHoldState.Done), RecoveryPhase.Idle, 0f, -1f),
                ("04_crossfade_para_jupiter", Imaging(ImagingPhase.Assembled, 8, false, ImagingHoldState.Done), RecoveryPhase.Idle, 0f, HistoricalImageReveal.JupiterAtSeconds + 0.5f),
                ("05_jupiter_io_perdida", Imaging(ImagingPhase.Assembled, 8, false, ImagingHoldState.Done), RecoveryPhase.Idle, 0f, HistoricalImageReveal.JupiterAtSeconds + 2f),
                ("06_ganimedes", Imaging(ImagingPhase.Assembled, 8, false, ImagingHoldState.Done), RecoveryPhase.Idle, 0f, HistoricalImageReveal.GanymedeAtSeconds + 2f),
            };

            var items = new List<Item>();
            foreach (var (name, imaging, recoveryPhase, progress, revealSeconds) in states)
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                    items.Add(new Item { Name = name, Imaging = imaging, Recovery = recoveryPhase, RecoveryProgress = progress, RevealSeconds = revealSeconds, Size = size });
            }

            pending = new Queue<Item>(items);
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
                        Finish(0);
                        return;
                    }

                    StartNext();
                }

                frames++;
                EditorApplication.QueuePlayerLoopUpdate();
                if (frames == FramesBeforeCapture / 2)
                    Apply();
                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-008 · captura falhou: " + exception);
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
            target = new RenderTexture(current.Size.x, current.Size.y, 24, RenderTextureFormat.ARGB32) { name = "PIO-008 capture" };
            target.Create();

            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"));
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Backdrop;

            host = new GameObject("PIO-008 capture") { hideFlags = HideFlags.DontSave };
            var imagingDocument = host.AddComponent<UIDocument>();
            imagingDocument.panelSettings = panel;
            imagingDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerImagingBuilder.PanelLayoutPath);
            imagingDocument.sortingOrder = 10;
            imagingView = host.AddComponent<ImagingPanelView>();

            var revealHost = new GameObject("reveal") { hideFlags = HideFlags.DontSave };
            revealHost.transform.SetParent(host.transform, false);
            var revealDocument = revealHost.AddComponent<UIDocument>();
            revealDocument.panelSettings = panel;
            revealDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerRecoveryBuilder.RevealLayoutPath);
            revealDocument.sortingOrder = 11;
            reveal = revealHost.AddComponent<HistoricalImageReveal>();
            var revealObject = new SerializedObject(reveal);
            revealObject.FindProperty("jupiterTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[1]);
            revealObject.FindProperty("ganymedeTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[0]);
            revealObject.ApplyModifiedPropertiesWithoutUndo();

            recovery = host.AddComponent<ImagingRecovery>();
            var recoveryObject = new SerializedObject(recovery);
            recoveryObject.FindProperty("panel").objectReferenceValue = imagingView;
            recoveryObject.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(PioneerRecoveryBuilder.RecoveryStylePath);
            recoveryObject.ApplyModifiedPropertiesWithoutUndo();
            recovery.Initialize(null, null, Key);
            reveal.Initialize(null, Key);
        }

        static void Apply()
        {
            imagingView.Bind();
            var root = imagingView.Root;
            var narrow = ResponsiveLayoutClass.IsNarrow(root.layout.width, ResponsiveLayoutClass.DefaultNarrowBelowLogicalWidth);
            root.parent?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            root.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            imagingView.Render(current.Imaging, Key);
            recovery.PreviewState(current.Recovery, current.RecoveryProgress);

            var revealRoot = reveal.GetComponent<UIDocument>().rootVisualElement;
            revealRoot?.parent?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            revealRoot?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            if (current.RevealSeconds >= 0f)
                reveal.PreviewTime(current.RevealSeconds);
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(current.Size.x, current.Size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, current.Size.x, current.Size.y), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputFolder, $"{current.Name}_{current.Size.x}x{current.Size.y}.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            written++;
        }

        static void Cleanup()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            host = null;
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }

            if (panel != null)
                Object.DestroyImmediate(panel);
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Debug.Log($"[PIO-008] {written} captura(s) em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
