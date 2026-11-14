using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-012 · Prévia e auditoria do painel da despedida. Renderiza o UXML real (PanelSettings da UI-001, textura de
    /// destino) sobre um fundo CLARO de propósito (pior caso de contraste, como no UI_SPEC), um quadro por situação, em
    /// <c>Docs/Art/Pioneer10_Farewell/&lt;estado&gt;_&lt;L&gt;x&lt;A&gt;.png</c>, nas três resoluções do UI_SPEC (1280×720,
    /// 960×600 e 1024×768 com a classe <c>sa-layout--narrow</c>). Além das imagens grava duas auditorias em QA/PIO-012:
    /// <c>contrast-audit.txt</c> (WCAG medido nos PIXELS renderizados: 4,5:1 para texto, 3:1 para contornos) e
    /// <c>layout-audit.txt</c> (elementos dentro do quadro). Textos aparecem como CHAVES (o resolvedor é da UI-004).
    /// Menu: Abandoned Space Apps → PIO-012 → Capturar painel da despedida.
    /// </summary>
    public static class PioneerFarewellCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Farewell";
        public const string ContrastAuditPath = "QA/PIO-012/contrast-audit.txt";
        public const string LayoutAuditPath = "QA/PIO-012/layout-audit.txt";
        const int FramesBeforeCapture = 40;
        const float TextContrast = 4.5f;
        const float UiContrast = 3f;
        static readonly Color Backdrop = new Color(0.788f, 0.604f, 0.431f, 1f); // céu marciano #C99A6E, o pior fundo do UI_SPEC

        static readonly Vector2Int Wide = new Vector2Int(1280, 720);
        static readonly Vector2Int Itch = new Vector2Int(960, 600);
        static readonly Vector2Int FourThree = new Vector2Int(1024, 768);

        struct Item
        {
            public string Name;
            public FarewellViewState State;
            public Vector2Int Size;
        }

        static readonly (string name, FarewellViewState state, bool allSizes)[] Frames =
        {
            ("01_plano_aberto_guia_de_Aldebaran", State(FarewellPhase.Playing, guide: true), true),
            ("02_preto_subindo", State(FarewellPhase.Playing, black: 0.5f), false),
            ("03_memorial_texto", State(FarewellPhase.Playing, black: 1f, ending: true), true),
            ("04_acoes_finais_foco_voltar", State(FarewellPhase.Memorial, black: 1f, ending: true, choices: true, focus: 0), true),
            ("05_acoes_finais_foco_repetir", State(FarewellPhase.Memorial, black: 1f, ending: true, choices: true, focus: 1), true),
        };

        static FarewellViewState State(FarewellPhase phase, float black = 0f, bool ending = false, bool guide = false, bool choices = false, int focus = 0) => new FarewellViewState
        {
            Phase = phase, BlackAlpha = black, EndingVisible = ending, GuideVisible = guide, ChoicesVisible = choices, Focus = focus,
        };

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture target;
        static FarewellPanelView view;
        static int frames;
        static int written;
        static bool exitWhenDone;
        static readonly List<string> contrast = new List<string>();
        static readonly List<string> layout = new List<string>();
        static int failures;

        [MenuItem("Abandoned Space Apps/PIO-012/Capturar painel da despedida")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ContrastAuditPath));
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png")) // substitui a rodada anterior por inteiro
                File.Delete(old);

            var items = new List<Item>();
            foreach (var (name, state, allSizes) in Frames)
            {
                items.Add(new Item { Name = name, State = state, Size = Wide });
                if (!allSizes)
                    continue;
                items.Add(new Item { Name = name, State = state, Size = Itch });
                items.Add(new Item { Name = name, State = state, Size = FourThree });
            }

            pending = new Queue<Item>(items);
            exitWhenDone = exitAfter;
            written = failures = 0;
            contrast.Clear();
            layout.Clear();
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
                {
                    view.Bind();
                    var root = view.Root;
                    var narrow = ResponsiveLayoutClass.IsNarrow(root.layout.width, ResponsiveLayoutClass.DefaultNarrowBelowLogicalWidth);
                    root.parent?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
                    root.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
                    view.Render(current.State, Key);
                }

                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-012 · captura falhou: " + exception);
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
            target = new RenderTexture(current.Size.x, current.Size.y, 24, RenderTextureFormat.ARGB32) { name = "PIO-012 capture" };
            target.Create();

            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            panel = Object.Instantiate(baseline);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Backdrop;

            host = new GameObject("PIO-012 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerFarewellBuilder.PanelLayoutPath);
            view = host.AddComponent<FarewellPanelView>();
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(current.Size.x, current.Size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, current.Size.x, current.Size.y), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var file = $"{current.Name}_{current.Size.x}x{current.Size.y}.png";
            File.WriteAllBytes(Path.Combine(OutputFolder, file), texture.EncodeToPNG());
            Audit(texture, file);
            Object.DestroyImmediate(texture);
            written++;
        }

        // ---------- Auditorias ----------

        static void Audit(Texture2D texture, string file)
        {
            var root = view.Root;
            var frame = root.Q("farewell-root");
            var scale = current.Size.y / 720f; // PanelSettings: escala pela altura (match = 1), altura lógica sempre 720

            // Texto: 4,5:1. Elementos visíveis neste estado; o fundo é o PIOR dos quatro cantos internos.
            var textElements = new List<(string, VisualElement)>
            {
                ("guia de Aldebaran", root.Q("guide-label")),
                ("título do memorial", root.Q("ending-title")),
                ("linha de situação", root.Q("ending-subtitle")),
                ("Voltar ao arquivo", root.Q("return-label")),
                ("Repetir capítulo", root.Q("replay-label")),
                ("tecla E", root.Q(className: "sa-keycap__glyph")),
            };
            foreach (var (label, element) in textElements)
            {
                if (element == null || !IsRendered(element) || element.worldBound.width < 1f)
                    continue;
                Measure(texture, file, label, element.worldBound, element.resolvedStyle.color, scale, TextContrast);
            }

            // Não-texto: 3:1. Borda dos botões contra o preto do memorial e contra o próprio botão.
            var blackColor = Over(new Color(0f, 0f, 0f, root.Q("black").resolvedStyle.opacity), Backdrop);
            foreach (var name in new[] { "return-button", "replay-button" })
            {
                var button = root.Q(name);
                if (button == null || !IsRendered(button))
                    continue;
                var buttonFill = Over(button.resolvedStyle.backgroundColor, blackColor);
                Compare(file, name + " borda × fundo", button.resolvedStyle.borderTopColor, blackColor, UiContrast);
                Compare(file, name + " borda × botão", button.resolvedStyle.borderTopColor, buttonFill, UiContrast);
            }

            // Layout: tudo dentro do quadro e o quadro dentro da tela.
            var screen = new Rect(0f, 0f, current.Size.x / scale, 720f);
            if (frame != null && frame.resolvedStyle.display != DisplayStyle.None)
            {
                if (!Contains(screen, frame.worldBound))
                    layout.Add($"FALHA {file}: o quadro sai da tela ({frame.worldBound} em {screen}).");
                foreach (var name in new[] { "guide", "memorial", "choices" })
                {
                    var element = root.Q(name);
                    if (element == null || element.resolvedStyle.display == DisplayStyle.None)
                        continue;
                    if (!Contains(frame.worldBound, element.worldBound))
                        layout.Add($"FALHA {file}: '{name}' vaza do quadro ({element.worldBound} fora de {frame.worldBound}).");
                }

                layout.Add($"ok    {file}: quadro {frame.worldBound.x:0}-{frame.worldBound.xMax:0} x {frame.worldBound.y:0}-{frame.worldBound.yMax:0} (tela lógica {screen.width:0}×720)");
            }
        }

        /// <summary>O elemento e todos os seus ancestrais estão com display ligado (resolvedStyle.display do próprio não basta).</summary>
        static bool IsRendered(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            return true;
        }

        static Color Over(Color top, Color bottom) => new Color(
            top.r * top.a + bottom.r * (1f - top.a), top.g * top.a + bottom.g * (1f - top.a), top.b * top.a + bottom.b * (1f - top.a), 1f);

        static void Compare(string file, string what, Color foreground, Color background, float minimum)
        {
            var ratio = Ratio(foreground, background);
            var ok = ratio >= minimum;
            if (!ok)
                failures++;
            contrast.Add($"{(ok ? "ok   " : "FALHA")} {file} · {what}: {ratio:0.00}:1 (mín. {minimum:0.0}:1) · {Hex(foreground)} sobre {Hex(background)} (composto)");
        }

        static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

        static void Measure(Texture2D texture, string file, string what, Rect logicalRect, Color foreground, float scale, float minimum)
        {
            // pontos de fundo: quatro cantos internos do retângulo (2 px dentro), no pior caso
            var worst = float.MaxValue;
            var worstColor = Color.black;
            var inset = 2f;
            foreach (var corner in new[]
            {
                new Vector2(logicalRect.xMin + inset, logicalRect.yMin + inset), new Vector2(logicalRect.xMax - inset, logicalRect.yMin + inset),
                new Vector2(logicalRect.xMin + inset, logicalRect.yMax - inset), new Vector2(logicalRect.xMax - inset, logicalRect.yMax - inset),
            })
            {
                var x = Mathf.Clamp(Mathf.RoundToInt(corner.x * scale), 0, texture.width - 1);
                var y = Mathf.Clamp(texture.height - 1 - Mathf.RoundToInt(corner.y * scale), 0, texture.height - 1);
                var pixel = texture.GetPixel(x, y);
                var ratio = Ratio(foreground, pixel);
                if (ratio < worst)
                {
                    worst = ratio;
                    worstColor = pixel;
                }
            }

            var ok = worst >= minimum;
            if (!ok)
                failures++;
            contrast.Add($"{(ok ? "ok   " : "FALHA")} {file} · {what}: {worst:0.00}:1 (mín. {minimum:0.0}:1) · texto/traço {Hex(foreground)} sobre {Hex(worstColor)}");
        }

        static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        static float Luminance(Color color)
        {
            static float Channel(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        static float Ratio(Color a, Color b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
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
            var header = $"PIO-012 · auditoria do painel da despedida · fundo claro #C99A6E (pior caso) · {written} captura(s)";
            File.WriteAllText(ContrastAuditPath, header + "\n" + $"Falhas de contraste: {failures}\n" + string.Join("\n", contrast) + "\n", new UTF8Encoding(false));
            var layoutFailures = layout.Count(l => l.StartsWith("FALHA", StringComparison.Ordinal));
            File.WriteAllText(LayoutAuditPath, header + "\n" + $"Falhas de layout: {layoutFailures}\n" + string.Join("\n", layout) + "\n", new UTF8Encoding(false));
            Debug.Log($"[PIO-012] {written} prévia(s) em {OutputFolder}; contraste: {failures} falha(s); layout: {layoutFailures} falha(s).");
            if (exitWhenDone)
                EditorApplication.Exit(code != 0 ? code : 0);
        }
    }
}
