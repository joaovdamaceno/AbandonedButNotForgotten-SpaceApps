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
    /// PIO-011 · Prévia do painel da escuta (onda, sintonia, resultados e respiração). Renderiza o UXML real (PanelSettings
    /// da UI-001) sobre um fundo CLARO de propósito (pior caso), em <c>Docs/Art/Pioneer10_Listening/&lt;estado&gt;_&lt;L&gt;x&lt;A&gt;.png</c>,
    /// em 1280×720 e 1024×768 (classe <c>sa-layout--narrow</c>). Os textos saem RESOLVIDOS no português do capítulo (lidos de
    /// <c>Data/Localization</c>) para a revisão científica. Menu: Abandoned Space Apps → PIO-011 → Capturar escuta.
    /// <b>Precisa da janela da Unity em primeiro plano</b>; sem foco o painel sai preto.
    /// </summary>
    public static class PioneerListeningCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Listening";
        const string LocalizationPath = "Data/Localization/Pioneer10.pt-BR.json";
        const int FramesBeforeCapture = 40;
        static readonly Color Backdrop = new Color(0.788f, 0.604f, 0.431f, 1f); // céu marciano #C99A6E (pior fundo do UI_SPEC)

        [Serializable] sealed class Locale { public Entry[] entries; }

        [Serializable] sealed class Entry { public string key, text; }

        struct Item
        {
            public string Name;
            public ListeningViewState State;
            public Vector2Int Size;
        }

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static RenderTexture target;
        static PanelSettings panel;
        static ListeningPanelView view;
        static Dictionary<string, string> texts;
        static int frames, written;
        static bool exitWhenDone;

        static ListeningViewState S(ListeningPhase phase, int contact, float knob, float listen = 0f, ListeningResult result = ListeningResult.None,
            ListeningHoldState hold = ListeningHoldState.Available, bool assisting = false, bool collapsed = false)
        {
            var zone = phase >= ListeningPhase.Teach && phase <= ListeningPhase.Contact3;
            var start = zone ? PioneerListening.ZoneStarts[contact] : 0f;
            var end = start + PioneerListening.ZoneWidth;
            var distance = !zone ? 1f : knob < start ? start - knob : knob > end ? knob - end : 0f;
            return new ListeningViewState
            {
                Phase = phase, Knob = knob, ZoneVisible = zone, ZoneStart = start, ZoneEnd = zone ? end : 0f,
                Proximity = zone ? 1f - Mathf.Clamp01(distance / 0.6f) : 0f,
                Direction = !zone || distance <= 0f ? 0 : knob < start ? 1 : -1, InZone = zone && distance <= 0f,
                Signal = zone ? PioneerListening.Signals[contact] : 0f, Listen = listen, Hold = hold, Assisting = assisting,
                Result = result, ControlsCollapsed = collapsed, Silent = collapsed,
            };
        }

        [MenuItem("Abandoned Space Apps/PIO-011/Capturar escuta")]
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

            var states = new (string name, ListeningViewState state)[]
            {
                ("01_ensino_proximidade", S(ListeningPhase.Teach, 0, 0.43f)),
                ("02_contato1_varredura_assistida", S(ListeningPhase.Contact1, 0, 0.52f, 0f, ListeningResult.None, ListeningHoldState.Holding, true)),
                ("03_contato1_escutando_na_zona", S(ListeningPhase.Contact1, 0, 0.66f, 0.5f, ListeningResult.None, ListeningHoldState.Holding)),
                ("04_contato1_sinal_recebido", S(ListeningPhase.Contact1, 0, 0.66f, 1f, ListeningResult.Signal, ListeningHoldState.Done)),
                ("05_contato2_dados_telemetria", S(ListeningPhase.Contact2, 1, 0.55f, 1f, ListeningResult.Data, ListeningHoldState.Done)),
                ("06_contato3_portadora_fraca", S(ListeningPhase.Contact3, 2, 0.60f, 1f, ListeningResult.Carrier, ListeningHoldState.Done)),
                ("07_contato3_zona_legivel_sinal_fraco", S(ListeningPhase.Contact3, 2, 0.40f)),
                ("08_tentativa4_ouvir_sem_zona", S(ListeningPhase.Attempt4, 0, 0.60f, 0.4f, ListeningResult.None, ListeningHoldState.Holding)),
                ("09_tentativa4_nenhum_sinal", S(ListeningPhase.Attempt4, 0, 0.60f, 1f, ListeningResult.NoSignal, ListeningHoldState.Done)),
                ("10_respirar_silencio", S(ListeningPhase.Breathe, 0, 0.60f, 1f, ListeningResult.NoSignal, ListeningHoldState.Done, false, true)),
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
                Debug.LogError("PIO-011 · captura falhou: " + exception);
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
            target = new RenderTexture(current.Size.x, current.Size.y, 24, RenderTextureFormat.ARGB32) { name = "PIO-011 capture" };
            target.Create();

            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"));
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Backdrop;

            host = new GameObject("PIO-011 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerListeningBuilder.PanelLayoutPath);
            view = host.AddComponent<ListeningPanelView>();
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
            Debug.Log($"[PIO-011] {written} captura(s) em {OutputFolder}.");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
