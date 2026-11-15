using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Roteiro v2 · Renderiza o <see cref="CinemaHudView"/> (UI Toolkit, tema da UI-001) em instantes escolhidos sobre fundo transparente e
    /// grava PNGs em <c>QA/PIO-024/hud/</c>, para conferir posição e leitura do medidor de radiação, da estática, da velocidade e das barras
    /// de distância. Textos vêm de <c>Data/Localization/Pioneer10.pt-BR.json</c>. Não mede contraste (ver o README da QA).
    /// -executeMethod Abandoned.SpaceApps.EditorTools.PioneerHudCapture.CaptureFromCommandLine (sem -quit, com gráfico)
    /// </summary>
    public static class PioneerHudCapture
    {
        public const string OutputFolder = "QA/PIO-024/hud";
        const int Width = 1280;
        const int Height = 720;
        const int FramesBeforeCapture = 40;

        static readonly (string name, float seconds)[] Frames =
        {
            ("radiacao_08s", 8f),
            ("radiacao_14s", 14f),
            ("distancias_27s", 27f),
            ("anos_saturno_t5", 5f),
            ("anos_netuno_t20", 20f),
            ("anos_energia_t30", 30f),
            ("anos_voyager_t48", 48f),
            ("estrelas_t22", 22f),
            ("estrelas_t29", 29f),
            ("barras_com_estrelas_t29", 29f),
        };
        // Quadros com este prefixo desenham o HUD da cena 7 (PioneerYearsHud); os demais, o da cena 5 (PioneerClosestHud).
        const string YearsPrefix = "anos_";

        static Queue<(string name, float seconds)> pending;
        static (string name, float seconds) current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture target;
        static CinemaHudView view;
        static Dictionary<string, string> texts;
        static int frames;
        static int written;

        [MenuItem("Abandoned Space Apps/PIO-024/Capturar o HUD de cinema")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static bool exitWhenDone;

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png"))
                File.Delete(old);

            texts = new Dictionary<string, string>();
            foreach (Match match in Regex.Matches(File.ReadAllText("Data/Localization/Pioneer10.pt-BR.json"), "\"key\": \"([^\"]+)\"[^\\n]*?\"text\": \"([^\"]*)\""))
                texts[match.Groups[1].Value] = match.Groups[2].Value;

            pending = new Queue<(string, float)>(Frames);
            exitWhenDone = exitAfter;
            written = 0;
            host = null;
            EditorApplication.update += Tick;
        }

        static string Text(string key) => texts.TryGetValue(key, out var value) ? value : key;

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
                {
                    view.Bind();
                    var t = current.seconds;
                    if (current.name.StartsWith("barras_", StringComparison.Ordinal))
                    {
                        view.SetLetterbox(1f, PioneerLetterbox.BarFraction);
                    }

                    if (current.name.StartsWith("estrelas_", StringComparison.Ordinal) || current.name.StartsWith("barras_com_estrelas", StringComparison.Ordinal))
                    {
                        var labels = new string[PioneerFirstsHud.StarCount];
                        var progress = new float[PioneerFirstsHud.StarCount];
                        for (var i = 0; i < labels.Length; i++)
                        {
                            progress[i] = PioneerFirstsHud.ProgressAt(i, t);
                            labels[i] = progress[i] > 0f ? Text(PioneerFirstsHud.LabelKeys[i]) : string.Empty;
                        }

                        view.SetFirsts(PioneerFirstsHud.VisibleAt(t), labels, progress);
                        return;
                    }

                    if (current.name.StartsWith(YearsPrefix, StringComparison.Ordinal))
                    {
                        view.SetYears(true, PioneerYearsHud.StateAt(t, Text));
                        return;
                    }

                    var radiation = PioneerClosestHud.RadiationVisibleAt(t);
                    view.SetRadiation(radiation, Text(CinemaHudView.RadiationKey), PioneerClosestHud.RadiationFillAt(t), PioneerClosestHud.StaticOpacityAt(t), PioneerClosestHud.StaticOffsetAt(t));
                    var distances = PioneerClosestHud.DistancesVisibleAt(t);
                    view.SetSpeed(distances, Text(CinemaHudView.SpeedKey));
                    view.SetDistances(distances, Text(CinemaHudView.MoonDistanceKey), Text(CinemaHudView.CloudsDistanceKey), PioneerClosestHud.MoonFillAt(t), PioneerClosestHud.CloudsFillAt(t));
                }

                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-024 · captura do HUD falhou: " + exception);
                Cleanup();
                pending.Clear();
                Finish(1);
            }
        }

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-024 hud" };
            target.Create();

            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            panel = Object.Instantiate(baseline);
            panel.targetTexture = target;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0f, 0f, 0f, 0f);

            host = new GameObject("PIO-024 hud") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PioneerCinemaHudBuilder.PanelLayoutPath);
            view = host.AddComponent<CinemaHudView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("staticTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerStageBuilder.ChapterFolder + "/UI/Textures/PIO_StaticNoise.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
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
            host = null;
            if (panel != null)
                Object.DestroyImmediate(panel);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Debug.Log($"[hud capture] {written} quadro(s) em {OutputFolder}");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
