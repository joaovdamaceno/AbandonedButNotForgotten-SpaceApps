using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Roteiro v2 · quadros dos dois jogos (a foto em ponto de vista e a sintonia do rádio): o palco 3D visto pela câmera do anchor,
    /// com o painel de UI Toolkit (tema da UI-001) desenhado por cima. Grava PNGs em <c>QA/PIO-036/</c>. Cada quadro monta o estado
    /// com valores fixos, sem tocar a sequência nem a entrada. Textos vêm de <c>Data/Localization/Pioneer10.pt-BR.json</c>.
    /// -executeMethod Abandoned.SpaceApps.EditorTools.PioneerMinigameCapture.CaptureFromCommandLine (sem -quit, com gráfico)
    /// </summary>
    public static class PioneerMinigameCapture
    {
        public const string OutputFolder = "QA/PIO-036";
        const int Width = 1280;
        const int Height = 720;
        const int FramesBeforeCapture = 40;

        sealed class Item
        {
            public string Name;
            public string AnchorId;
            public float YawDegrees;
            public Action<GameObject> BuildPanel;
            public Action<GameObject, Func<string, string>> RenderPanel;
            public Texture2D Stage;
        }

        static Queue<Item> pending;
        static Item current;
        static GameObject host;
        static PanelSettings panel;
        static RenderTexture uiTarget;
        static Texture2D background;
        static Dictionary<string, string> texts;
        static PioneerPresentation presentation;
        static int frames, written;
        static bool exitWhenDone;

        [MenuItem("Abandoned Space Apps/PIO-036/Capturar os jogos da foto e da sintonia")]
        public static void CaptureFromMenu() => Begin(false);

        public static void CaptureFromCommandLine() => Begin(true);

        static string Text(string key) => texts.TryGetValue(key, out var value) ? value : key;

        static SpinShotViewState Spin(SpinShotPhase phase, float angle01, int shots, float flash = 0f, float combine = 0f, bool outside = false)
        {
            var half = PioneerImaging.SpinWindowHalfDegrees / 360f;
            return new SpinShotViewState
            {
                Phase = phase,
                Angle01 = angle01,
                WindowStart = 0.5f - half,
                WindowEnd = 0.5f + half,
                InWindow = Mathf.Abs(angle01 - 0.5f) <= half,
                ShotTarget = PioneerImaging.SpinShotCount,
                Shots = shots,
                Flash = flash,
                CombineProgress = combine,
                PressedOutside = outside,
                Hold = ImagingHoldState.Available,
            };
        }

        static ListeningViewState Radar(int round, float knob, ListeningResult result = ListeningResult.None, float listen = 0f, bool attempt = false)
        {
            var start = PioneerListening.AutoZoneStarts[Mathf.Min(round, 2)];
            var end = start + PioneerListening.AutoZoneWidths[Mathf.Min(round, 2)];
            var distance = knob < start ? start - knob : knob > end ? knob - end : 0f;
            var proximity = attempt ? 0f : 1f - Mathf.Clamp01(distance / 0.6f);
            var signal = PioneerListening.AutoSignals[Mathf.Min(round, 2)];
            return new ListeningViewState
            {
                Phase = attempt ? ListeningPhase.Attempt4 : ListeningPhase.Contact1 + round,
                Knob = knob,
                ZoneVisible = !attempt,
                ZoneStart = attempt ? 0f : start,
                ZoneEnd = attempt ? 0f : end,
                Proximity = proximity,
                Direction = attempt || distance <= 0f ? 0 : knob < start ? 1 : -1,
                InZone = !attempt && distance <= 0f,
                Signal = attempt ? 0f : signal,
                Strength = attempt ? 0f : proximity * signal,
                Listen = listen,
                Result = result,
                AutoListen = true,
                Hold = ListeningHoldState.Available,
            };
        }

        static Item RadarItem(string name, ListeningViewState state) => new Item
        {
            Name = name,
            AnchorId = PioneerListening.StateId,
            BuildPanel = go => AddView<ListeningPanelView>(go, PioneerListeningBuilder.PanelLayoutPath),
            RenderPanel = (go, text) =>
            {
                var view = go.GetComponent<ListeningPanelView>();
                view.WavePhase = 1.7f;
                view.Render(state, text);
            },
        };

        static Item SpinItem(string name, SpinShotViewState state) => new Item
        {
            Name = name,
            AnchorId = PioneerImaging.PovAnchorId,
            YawDegrees = (state.Angle01 - 0.5f) * 360f,
            BuildPanel = go => AddView<SpinShotPanelView>(go, PioneerImagingBuilder.SpinPanelLayoutPath),
            RenderPanel = (go, text) => go.GetComponent<SpinShotPanelView>().Render(state, text),
        };

        static List<Item> Items()
        {
            var items = new List<Item>
            {
                SpinItem("foto_fora_da_mira", Spin(SpinShotPhase.Shoot, 0.5f - 38f / 360f, 1)),
                SpinItem("foto_na_mira", Spin(SpinShotPhase.Shoot, 0.5f + 2f / 360f, 2)),
                SpinItem("foto_clarao", Spin(SpinShotPhase.Shoot, 0.5f + 4f / 360f, 3, flash: 0.85f)),
                SpinItem("juncao_2_fotos", Spin(SpinShotPhase.Combine, 0.5f, 5, combine: 0.2f)),
                SpinItem("juncao_5_fotos", Spin(SpinShotPhase.Done, 0.5f, 5, combine: 1f)),
                RadarItem("radar_sintonizando_longe", Radar(0, 0.30f)),
                RadarItem("radar_na_zona", Radar(0, 0.66f, listen: 0.55f)),
                RadarItem("radar_contato1_sinal", Radar(0, 0.66f, ListeningResult.Signal, 1f)),
                RadarItem("radar_contato2_dados", Radar(1, 0.40f, ListeningResult.Data, 1f)),
                RadarItem("radar_rodada3_zona_estreita", Radar(2, 0.44f)),
                RadarItem("radar_contato3_portadora", Radar(2, 0.61f, ListeningResult.Carrier, 1f)),
                RadarItem("radar_sem_sinal", Radar(3, 0.5f, ListeningResult.NoSignal, 1f, attempt: true)),
            };
            return items;
        }

        static void Begin(bool exitAfter)
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder, "*.png"))
                File.Delete(old);

            texts = new Dictionary<string, string>();
            foreach (Match match in Regex.Matches(File.ReadAllText("Data/Localization/Pioneer10.pt-BR.json"), "\"key\": \"([^\"]+)\"[^\\n]*?\"text\": \"([^\"]*)\""))
                texts[match.Groups[1].Value] = match.Groups[2].Value;

            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            presentation.RegisterWithCameraDirector();
            presentation.CameraDirector.GroundProbe = _ => null;

            // O que o PioneerChapterController.Initialize faz na rota real: pendurar as silhuetas nos sockets das regiões.
            foreach (var listening in Object.FindObjectsByType<PioneerListening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                listening.AttachToStage();
            foreach (var launch in Object.FindObjectsByType<PioneerLaunchActivity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                launch.AttachToStage();

            var items = Items();
            foreach (var item in items)
            {
                current = item;
                item.Stage = RenderBackground(); // o palco é renderizado já, como na captura de quadros de desenvolvimento
            }

            pending = new Queue<Item>(items);
            exitWhenDone = exitAfter;
            written = 0;
            host = null;
            EditorApplication.update += Tick;
        }

        static void AddView<T>(GameObject go, string layoutPath) where T : MonoBehaviour
        {
            var document = go.GetComponent<UIDocument>();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(layoutPath);
            var view = go.AddComponent<T>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
                {
                    var view = host.GetComponent<MonoBehaviour>();
                    switch (view)
                    {
                        case SpinShotPanelView spin:
                            spin.Bind();
                            break;
                        case ListeningPanelView listeningView:
                            listeningView.Bind();
                            break;
                    }

                    current.RenderPanel(host, Text);
                }

                if (frames < FramesBeforeCapture)
                    return;

                Save();
                Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-036 · captura dos jogos falhou: " + exception);
                Cleanup();
                pending.Clear();
                Finish(1);
            }
        }

        static void StartNext()
        {
            current = pending.Dequeue();
            frames = 0;
            background = current.Stage;

            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-036 ui" };
            uiTarget.Create();
            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            panel = Object.Instantiate(baseline);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0f, 0f, 0f, 0f);

            host = new GameObject("PIO-036 ui") { hideFlags = HideFlags.DontSave };
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            current.BuildPanel(host);
        }

        static Texture2D RenderBackground()
        {
            Texture2D result;
            presentation.ShowAnchor(current.AnchorId);
            presentation.SetAnchorYaw(current.AnchorId, current.YawDegrees);
            presentation.ShowAnchor(current.AnchorId); // de novo: aplica a pose já girada e posiciona o céu
            var director = presentation.CameraDirector;
            var camera = director.TargetCamera;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var previousAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }

                RenderTexture.active = target;
                result = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                result.Apply();
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                UnityEditor.ShaderUtil.allowAsyncCompilation = previousAsync;
                target.Release();
                Object.DestroyImmediate(target);
            }

            return result;
        }

        static void Save()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = uiTarget;
            var ui = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            ui.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            ui.Apply();
            RenderTexture.active = previous;

            // O painel foi desenhado sobre transparente: alfa pré-multiplicado sobre o palco.
            var back = background.GetPixels32();
            var front = ui.GetPixels32();
            for (var i = 0; i < back.Length; i++)
            {
                var a = front[i].a / 255f;
                back[i] = new Color32(
                    (byte)Mathf.Clamp(front[i].r + back[i].r * (1f - a), 0f, 255f),
                    (byte)Mathf.Clamp(front[i].g + back[i].g * (1f - a), 0f, 255f),
                    (byte)Mathf.Clamp(front[i].b + back[i].b * (1f - a), 0f, 255f), 255);
            }

            var composed = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            composed.SetPixels32(back);
            composed.Apply();
            File.WriteAllBytes(Path.Combine(OutputFolder, current.Name + ".png"), composed.EncodeToPNG());
            Object.DestroyImmediate(ui);
            Object.DestroyImmediate(composed);
            written++;
        }

        static void Cleanup()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            host = null;
            if (panel != null)
                Object.DestroyImmediate(panel);
            if (background != null)
                Object.DestroyImmediate(background);
            if (uiTarget != null)
            {
                uiTarget.Release();
                Object.DestroyImmediate(uiTarget);
            }
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            presentation?.UnregisterFromCameraDirector();
            Debug.Log($"[minigame capture] {written} quadro(s) em {OutputFolder}");
            if (exitWhenDone)
                EditorApplication.Exit(code);
        }
    }
}
