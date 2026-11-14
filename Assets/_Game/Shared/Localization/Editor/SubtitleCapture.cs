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
    /// UI-004 · Prévia e auditoria das legendas. Renderiza o <see cref="SubtitlePresenter"/> real (PanelSettings da UI-001) sobre fundo
    /// CLARO (pior caso) em <c>Docs/Art/Subtitles/&lt;estado&gt;_&lt;L&gt;x&lt;A&gt;.png</c> (1280×720 e 1024×768 com <c>sa-layout--narrow</c>),
    /// com as legendas MAIS LONGAS em PT e EN, o carimbo de data, o carimbo + corpo e o estado sem texto. Além das imagens mede, com
    /// as métricas reais da Outfit, TODAS as legendas das tabelas (narrativas da Pioneer e <c>MarsSubtitles.json</c>) nos dois idiomas
    /// na caixa de 880 px e grava <c>QA/UI-004/subtitle-audit.txt</c> (linhas por legenda; mais de 2 = falha).
    /// Menu: Abandoned Space Apps → UI-004 → Capturar e auditar legendas. <b>Precisa da janela da Unity em primeiro plano.</b>
    /// </summary>
    public static class SubtitleCapture
    {
        public const string OutputFolder = "Docs/Art/Subtitles";
        public const string AuditPath = "QA/UI-004/subtitle-audit.txt";
        const int FramesBeforeCapture = 40;
        const float PlateMaxWidth = 880f;
        const float PlatePadding = 24f * 2f; // --sa-space-5 de cada lado
        static readonly Color Backdrop = new Color(0.788f, 0.604f, 0.431f, 1f);

        struct Item
        {
            public string Name;
            public string Language;
            public string Key;
            public string StampKey;
            public Vector2Int Size;
        }

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static RenderTexture target;
        static PanelSettings panel;
        static SubtitlePresenter presenter;
        static LocalizationService service;
        static int frames, written;
        static bool exitWhenDone;
        static readonly StringBuilder audit = new StringBuilder();
        static int auditFailures;

        [MenuItem("Abandoned Space Apps/UI-004/Capturar e auditar legendas")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static LocalizationService LoadService()
        {
            var loaded = new LocalizationService();
            foreach (var file in new[]
            {
                "Assets/_Game/Shared/Localization/Pioneer10Texts.json",
                "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json",
            })
                loaded.AddTable(File.ReadAllText(file), Path.GetFileName(file));
            return loaded;
        }

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(AuditPath));
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png"))
                File.Delete(old);

            service = LoadService();
            var narrative = service.Keys.Where(k => service.GetGroup(k) != "card" && !k.StartsWith("PIO.UI") && !k.StartsWith("MAR.T") && !k.StartsWith("MAR.VERB")).ToList();
            string Longest(string language) => narrative.Where(k => service.TryGet(k, language, out _)).OrderByDescending(k => { service.TryGet(k, language, out var t); return t.Length; }).First();
            var longestPt = Longest("pt");
            var longestEn = Longest("en");
            const string card = "PIO.Card.jupiter";
            var states = new List<(string name, string language, string key, string stamp)>
            {
                ("01_pt_legenda_mais_longa", "pt", longestPt, null),
                ("02_en_legenda_mais_longa", "en", longestEn, null),
                ("03_pt_carimbo_de_data", "pt", card, null),
                ("04_en_carimbo_de_data", "en", card, null),
                ("05_pt_carimbo_e_corpo", "pt", "PIO.02.01", card),
                ("06_en_carimbo_e_corpo", "en", "PIO.02.01", card),
                ("07_sem_texto_nenhuma_caixa", "pt", "T.NAO_EXISTE", null),
            };
            var items = new List<Item>();
            foreach (var (name, language, key, stamp) in states)
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                    items.Add(new Item { Name = name, Language = language, Key = key, StampKey = stamp, Size = size });
            }

            pending = new Queue<Item>(items);
            exitWhenDone = exitAfter;
            written = auditFailures = 0;
            audit.Clear();
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
                        Finish(auditFailures > 0 ? 1 : 0);
                        return;
                    }

                    StartNext();
                }

                frames++;
                EditorApplication.QueuePlayerLoopUpdate();
                if (frames == FramesBeforeCapture / 2)
                    Apply();
                if (frames == FramesBeforeCapture - 4 && current.Size.x == 1280 && current.Name.StartsWith("01"))
                    AuditAll(presenter.Layer); // depois de os estilos da legenda serem resolvidos
                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("UI-004 · captura falhou: " + exception);
                Cleanup();
                pending.Clear();
                Finish(1);
            }
        }

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            target = new RenderTexture(current.Size.x, current.Size.y, 24, RenderTextureFormat.ARGB32) { name = "UI-004 capture" };
            target.Create();
            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"));
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = Backdrop;

            host = new GameObject("UI-004 capture") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SubtitleLayerSetup.LayoutPath);
            presenter = host.AddComponent<SubtitlePresenter>();
            service = LoadService();
            service.SetLanguage(current.Language);
        }

        static void Apply()
        {
            presenter.Initialize(service, null);
            var root = presenter.Layer;
            var narrow = ResponsiveLayoutClass.IsNarrow(root.panel.visualTree.layout.width, ResponsiveLayoutClass.DefaultNarrowBelowLogicalWidth);
            root.parent?.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            root.EnableInClassList(ResponsiveLayoutClass.NarrowClass, narrow);
            if (!string.IsNullOrEmpty(current.StampKey))
                presenter.SetStamp(current.StampKey);
            presenter.ShowCue(new SubtitleCue("CAPTURE", current.Key, 0f, 5f));
        }

        /// <summary>Mede cada legenda das tabelas nos dois idiomas na largura útil da caixa e conta as linhas.</summary>
        static void AuditAll(VisualElement root)
        {
            var label = root.Q<Label>("subtitle-text");
            var width = PlateMaxWidth - PlatePadding;
            var fontSize = label.resolvedStyle.fontSize;
            if (fontSize < 20f)
            {
                audit.AppendLine($"AUDITORIA INVÁLIDA: a fonte resolvida da legenda é {fontSize:0} px (esperado 28 px); os estilos não foram aplicados.");
                auditFailures++;
                return;
            }

            var lineHeight = label.MeasureTextSize("Ag", width, VisualElement.MeasureMode.AtMost, 0f, VisualElement.MeasureMode.Undefined).y;
            audit.AppendLine("UI-004 · auditoria das legendas (Outfit Regular, caixa de 880 px, padding de 24 px; linhas = altura medida / altura de uma linha)");
            audit.AppendLine($"fonte resolvida {fontSize:0} px; largura útil {width:0} px; altura de uma linha {lineHeight:0.0} px. Mais de 2 linhas = FALHA.");
            var all = LoadService();
            var keys = all.Keys.Where(k => all.GetGroup(k) != "card" && !k.StartsWith("PIO.UI") && !k.StartsWith("PIO.Card") && !k.StartsWith("MAR.T") && !k.StartsWith("MAR.VERB")).ToList();
            var counts = new Dictionary<string, int>();
            foreach (var language in new[] { "pt", "en" })
            {
                var rows = new List<(string key, int chars, int lines, float height)>();
                foreach (var key in keys)
                {
                    if (!all.TryGet(key, language, out var text))
                        continue;
                    var size = label.MeasureTextSize(text, width, VisualElement.MeasureMode.AtMost, 0f, VisualElement.MeasureMode.Undefined);
                    var lines = Mathf.Max(1, Mathf.RoundToInt(size.y / lineHeight));
                    rows.Add((key, text.Length, lines, size.y));
                }

                var over = rows.Where(r => r.lines > 2).ToList();
                auditFailures += over.Count;
                audit.AppendLine();
                audit.AppendLine($"[{language}] {rows.Count} legendas medidas; 1 linha: {rows.Count(r => r.lines == 1)}; 2 linhas: {rows.Count(r => r.lines == 2)}; mais de 2: {over.Count}");
                foreach (var row in rows.OrderByDescending(r => r.chars).Take(6))
                    audit.AppendLine($"  mais longas: {row.key} · {row.chars} caracteres · {row.lines} linha(s)");
                foreach (var row in over)
                    audit.AppendLine($"  FALHA {row.key} · {row.chars} caracteres · {row.lines} linhas");
                counts[language] = rows.Count;
            }

            audit.AppendLine();
            audit.AppendLine("Pior caso do inglês: ver as linhas 'mais longas' de [en]. Medição pelo motor de texto do UI Toolkit em um painel real.");
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
            File.WriteAllText(AuditPath, audit.ToString(), new UTF8Encoding(false));
            Debug.Log($"[UI-004] {written} captura(s) em {OutputFolder}; auditoria em {AuditPath} ({auditFailures} falha(s)).");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
