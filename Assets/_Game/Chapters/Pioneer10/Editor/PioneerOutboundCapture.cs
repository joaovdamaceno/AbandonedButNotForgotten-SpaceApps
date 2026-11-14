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
    /// PIO-010 · Prévia do diagrama orbital, da síntese de energia e do gráfico de 1998. Renderiza o UXML real (PanelSettings
    /// da UI-001) sobre um fundo CLARO de propósito (pior caso), em <c>Docs/Art/Pioneer10_Outbound/&lt;estado&gt;_&lt;L&gt;x&lt;A&gt;.png</c>,
    /// em 1280×720 e 1024×768 (classe <c>sa-layout--narrow</c>). Os textos saem RESOLVIDOS no português do capítulo (lidos de
    /// <c>Data/Localization</c>) para a revisão científica. Menu: Abandoned Space Apps → PIO-010 → Capturar caminho de saída.
    /// <b>Precisa da janela da Unity em primeiro plano</b>; sem foco o painel sai preto.
    /// </summary>
    public static class PioneerOutboundCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Outbound";
        const string LocalizationPath = "Data/Localization/Pioneer10.pt-BR.json";
        const int FramesBeforeCapture = 40;
        static readonly Color Backdrop = new Color(0.788f, 0.604f, 0.431f, 1f); // céu marciano #C99A6E (pior fundo do UI_SPEC)

        [Serializable] sealed class Locale { public Entry[] entries; }

        [Serializable] sealed class Entry { public string key, text; }

        struct Item
        {
            public string Name;
            public OutboundViewState State;
            public Vector2Int Size;
        }

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static RenderTexture target;
        static PanelSettings panel;
        static OutboundAtlasView view;
        static Dictionary<string, string> texts;
        static int frames, written;
        static bool exitWhenDone;

        static OutboundViewState State(OutboundPhase phase, int rings, float hold = 0f, OutboundHoldState holdState = OutboundHoldState.Available, bool confirmed = false) =>
            new OutboundViewState { Phase = phase, OrbitsCrossed = rings, HoldProgress = hold, Hold = holdState, PowerConfirmed = confirmed };

        [MenuItem("Abandoned Space Apps/PIO-010/Capturar caminho de saída")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png"))
                File.Delete(old);

            texts = new Dictionary<string, string>();
            foreach (var entry in JsonUtility.FromJson<Locale>(File.ReadAllText(LocalizationPath)).entries)
                texts[entry.key] = entry.text;

            var states = new (string name, OutboundViewState state)[]
            {
                ("01_1976_saturno", State(OutboundPhase.Saturn1976, 2)),
                ("02_1983_netuno", State(OutboundPhase.Neptune1983, OutboundAtlas.OrbitRings)),
                ("03_energia_hold_inicio", State(OutboundPhase.Power, OutboundAtlas.OrbitRings, 0.0f, OutboundHoldState.Available)),
                ("04_energia_hold_meio", State(OutboundPhase.Power, OutboundAtlas.OrbitRings, 0.5f, OutboundHoldState.Holding)),
                ("05_grafico_1998", State(OutboundPhase.Chart1998, OutboundAtlas.OrbitRings, 1f, OutboundHoldState.Done, true)),
            };

            var items = new List<Item>();
            foreach (var (name, state) in states)
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                    items.Add(new Item { Name = name, State = state, Size = size });
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
                Debug.LogError("PIO-010 · captura falhou: " + exception);
                Cleanup();
                pending.Clear();
                Finish(1);
            }
        }

        static string Resolve(string key) => texts.TryGetValue(key, out var text) ? text : key;

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            target = new RenderTexture(current.Size.x, current.Size.y, 24, RenderTextureFormat.ARGB32) { name = "PIO-010 capture" };
            target.Create();

            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"));
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Backdrop;

            host = new GameObject("PIO-010 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerOutboundBuilder.PanelLayoutPath);
            view = host.AddComponent<OutboundAtlasView>();
        }

        static void Apply()
        {
            view.Bind();
            var root = view.Root;
            var narrow = ResponsiveLayoutClass.IsNarrow(root.layout.width, ResponsiveLayoutClass.DefaultNarrowBelowLogicalWidth);
            root.parent?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            root.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            view.Render(current.State, Resolve);
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
            Debug.Log($"[PIO-010] {written} captura(s) em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
