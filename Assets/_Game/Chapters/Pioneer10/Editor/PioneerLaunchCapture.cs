using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-004 · Prévia da sequência PIO_Launch: renderiza instantes-chave com <c>SequenceDirector.Preview</c> (mesmo
    /// relógio, sem eventos) e grava PNGs + folha de contato em Docs/Art/Pioneer10_Launch. Não substitui assistir aos
    /// 40 s no Play Mode nem no navegador.
    /// Menu: Abandoned Space Apps → PIO-004 → Capturar lançamento. Linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.PioneerLaunchCapture.CaptureFromCommandLine
    /// </summary>
    public static class PioneerLaunchCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Launch";
        const int Width = 1280;
        const int Height = 720;

        /// <summary>Instante (s) e progresso do hold mostrado (0 = antes, 0,5 = segurando, 1 = concluído).</summary>
        static readonly (float time, float hold, string label)[] Frames =
        {
            (2.5f, 0f, "A_refletor"), (8.5f, 0f, "B_plataforma"), (12.5f, 0f, "B_ignicao"), (15.8f, 0f, "B_subida"),
            (19f, 0f, "C_elipse_inicio"), (24.5f, 0f, "C_elipse_cruzamento"), (31f, 0f, "D_sonda"), (32.5f, 0.5f, "D_hold"), (36f, 1f, "D_energia"),
        };

        [MenuItem("Abandoned Space Apps/PIO-004/Capturar lançamento")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorUtility.DisplayDialog("PIO-004", $"{Capture()} captura(s) em {OutputFolder}.", "OK");
        }

        public static void CaptureFromCommandLine()
        {
            var code = 0;
            try
            {
                Capture();
            }
            catch (Exception exception)
            {
                Debug.LogError("PIO-004 · captura falhou: " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static int Capture()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var presentation = roots.SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            var activity = roots.SelectMany(r => r.GetComponentsInChildren<PioneerLaunchActivity>(true)).Single();
            var camera = presentation.CameraDirector.TargetCamera;

            var host = new GameObject("PIO-004 capture director");
            var director = host.AddComponent<SequenceDirector>();
            director.Initialize(presentation.CameraDirector, null, null);
            presentation.CameraDirector.GroundProbe = _ => null;
            presentation.RegisterWithCameraDirector();
            activity.Initialize(director, null, null, null);

            Directory.CreateDirectory(OutputFolder);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-004 capture" };
            var previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var images = new Texture2D[Frames.Length];
            var written = 0;
            try
            {
                for (var i = 0; i < Frames.Length; i++)
                {
                    var frame = Frames[i];
                    presentation.ShowShotAt(activity.Sequence, frame.time);
                    director.Preview(activity.Sequence, frame.time);
                    activity.PreviewEffects(frame.time, PioneerLaunchBuilder.IgnitionSeconds, frame.hold);
                    presentation.SyncToCamera();
                    images[i] = Render(camera, target);
                    File.WriteAllBytes(Path.Combine(OutputFolder, $"{i + 1:00}_{frame.label}_{frame.time:00.0}s.png".Replace(',', '.')), images[i].EncodeToPNG());
                    written++;
                }

                File.WriteAllBytes(Path.Combine(OutputFolder, "PIO-004_contact_sheet.png"), Sheet(images).EncodeToPNG());
                written++;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                Object.DestroyImmediate(target);
                foreach (var image in images)
                {
                    if (image != null)
                        Object.DestroyImmediate(image);
                }

                EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single); // descarta a prévia
            }

            Debug.Log($"PIO-004 · {written} arquivo(s) em {OutputFolder}.");
            return written;
        }

        static Texture2D Render(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        static Texture2D Sheet(Texture2D[] images)
        {
            const int columns = 3, w = 426, h = 240, gap = 6;
            var rows = Mathf.CeilToInt(images.Length / (float)columns);
            var sheet = new Texture2D(columns * (w + gap) + gap, rows * (h + gap) + gap, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color(0.12f, 0.12f, 0.13f), sheet.width * sheet.height).ToArray();
            sheet.SetPixels(fill);
            for (var i = 0; i < images.Length; i++)
            {
                var ox = gap + i % columns * (w + gap);
                var oy = gap + (rows - 1 - i / columns) * (h + gap);
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                        sheet.SetPixel(ox + x, oy + y, images[i].GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h));
                }
            }

            sheet.Apply();
            return sheet;
        }
    }
}
