using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-003 · Prévia dos anchors: renderiza cada anchor a 1280×720 pela câmera única, desenha o contorno do quadro de UI
    /// reservado e grava PNGs + folha de contato + STAGE_STATS.md em Docs/Art/Pioneer10_Stage. As contagens são
    /// ESTIMATIVAS por renderer visível no frustum (não substituem o profiler na build Web).
    /// Menu: Abandoned Space Apps → PIO-003 → Capturar anchors. Linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.PioneerStageCapture.CaptureFromCommandLine
    /// </summary>
    public static class PioneerStageCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Stage";
        const int Width = 1280;
        const int Height = 720;
        static readonly Color FrameColor = new Color(0.31f, 0.82f, 0.88f);

        [MenuItem("Abandoned Space Apps/PIO-003/Capturar anchors")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var written = Capture();
            EditorUtility.DisplayDialog("PIO-003", $"{written} captura(s) em {OutputFolder}.", "OK");
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
                Debug.LogError("PIO-003 · captura falhou: " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public sealed class AnchorStats
        {
            public string AnchorId;
            public int Renderers;
            public int Triangles;
            public int DrawCallsEstimate;
            public int ActiveLights;
            public int ShadowedLights;
        }

        /// <summary>Abre a cena, captura todos os anchors e devolve quantos arquivos gravou. Não salva a cena.</summary>
        public static int Capture()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (presentation == null)
                throw new InvalidOperationException("PioneerPresentation não encontrada em " + PioneerStageBuilder.SceneAssetPath);

            // O que o PioneerChapterController.Initialize faz na rota real: as silhuetas da antena DSN (PIO-011) e do Atlas
            // (PIO-004) só vão para os sockets das regiões quando o capítulo inicia; sem isto elas aparecem na origem, na
            // frente da sonda, e poluem os planos do espaço. A cena é reaberta no fim, então nada disto é salvo.
            foreach (var listening in Object.FindObjectsByType<PioneerListening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                listening.AttachToStage();
            foreach (var launch in Object.FindObjectsByType<PioneerLaunchActivity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                launch.AttachToStage();

            Directory.CreateDirectory(OutputFolder);
            var camera = presentation.CameraDirector.TargetCamera;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-003 capture", antiAliasing = 1 };
            var previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var stats = new List<AnchorStats>();
            var thumbnails = new List<Texture2D>();
            var written = 0;
            try
            {
                foreach (var anchor in presentation.Anchors)
                {
                    presentation.ShowAnchor(anchor.anchorId);
                    var image = Render(camera, target);
                    if (anchor.HasUiFrame)
                        DrawFrame(image, anchor.uiFrameViewport);

                    File.WriteAllBytes(Path.Combine(OutputFolder, anchor.anchorId + ".png"), image.EncodeToPNG());
                    written++;
                    stats.Add(Measure(anchor.anchorId, camera, presentation));
                    thumbnails.Add(image);
                }

                File.WriteAllBytes(Path.Combine(OutputFolder, "PIO-003_contact_sheet.png"), ContactSheet(thumbnails, presentation.Anchors).EncodeToPNG());
                written++;
                var checks = PioneerStageChecks.Run(presentation);
                File.WriteAllText(Path.Combine(OutputFolder, "STAGE_STATS.md"), Describe(presentation, stats, checks), new UTF8Encoding(false));
                written++;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                foreach (var texture in thumbnails)
                    Object.DestroyImmediate(texture);

                // Descarta as mudanças de prévia (regiões, luzes, rotação): a cena salva continua a do builder.
                EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            }

            Debug.Log($"PIO-003 · {written} arquivo(s) em {OutputFolder}.");
            return written;
        }

        static Texture2D Render(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
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

        static void DrawFrame(Texture2D image, Rect viewport)
        {
            var x0 = Mathf.RoundToInt(viewport.xMin * Width);
            var x1 = Mathf.RoundToInt(viewport.xMax * Width) - 1;
            var y0 = Mathf.RoundToInt(viewport.yMin * Height);
            var y1 = Mathf.RoundToInt(viewport.yMax * Height) - 1;
            for (var t = 0; t < 3; t++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    image.SetPixel(x, y0 + t, FrameColor);
                    image.SetPixel(x, y1 - t, FrameColor);
                }

                for (var y = y0; y <= y1; y++)
                {
                    image.SetPixel(x0 + t, y, FrameColor);
                    image.SetPixel(x1 - t, y, FrameColor);
                }
            }

            image.Apply();
        }

        static AnchorStats Measure(string anchorId, Camera camera, PioneerPresentation presentation)
        {
            camera.fieldOfView = presentation.Current.fieldOfViewDegrees;
            camera.aspect = Width / (float)Height;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var stats = new AnchorStats { AnchorId = anchorId };
            foreach (var renderer in presentation.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.enabled || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;

                stats.Renderers++;
                stats.Triangles += (int)(mesh.GetIndexCount(0) / 3);
                for (var s = 1; s < mesh.subMeshCount; s++)
                    stats.Triangles += (int)(mesh.GetIndexCount(s) / 3);
                stats.DrawCallsEstimate += Mathf.Max(1, renderer.sharedMaterials.Length);
            }

            foreach (var light in presentation.GetComponentsInChildren<Light>(false))
            {
                if (!light.enabled || light.intensity <= 0f)
                    continue;

                stats.ActiveLights++;
                if (light.shadows != LightShadows.None)
                    stats.ShadowedLights++;
            }

            return stats;
        }

        static Texture2D ContactSheet(List<Texture2D> images, IReadOnlyList<PioneerStageAnchor> anchors)
        {
            const int columns = 5;
            const int thumbWidth = 384;
            const int thumbHeight = 216;
            const int gap = 6;
            var rows = Mathf.CeilToInt(images.Count / (float)columns);
            var sheet = new Texture2D(columns * (thumbWidth + gap) + gap, rows * (thumbHeight + gap) + gap, TextureFormat.RGB24, false);
            var fill = new Color[sheet.width * sheet.height];
            for (var i = 0; i < fill.Length; i++)
                fill[i] = new Color(0.12f, 0.12f, 0.13f);
            sheet.SetPixels(fill);

            for (var i = 0; i < images.Count; i++)
            {
                var column = i % columns;
                var row = rows - 1 - i / columns; // primeira linha no topo
                var ox = gap + column * (thumbWidth + gap);
                var oy = gap + row * (thumbHeight + gap);
                for (var y = 0; y < thumbHeight; y++)
                {
                    for (var x = 0; x < thumbWidth; x++)
                        sheet.SetPixel(ox + x, oy + y, images[i].GetPixelBilinear((x + 0.5f) / thumbWidth, (y + 0.5f) / thumbHeight));
                }

                // Marca de anchor de estado (faixa clara no topo) x plano extra (faixa escura).
                var stateAnchor = anchors[i].IsStateAnchor;
                for (var x = 0; x < thumbWidth; x++)
                {
                    for (var t = 0; t < 4; t++)
                        sheet.SetPixel(ox + x, oy + thumbHeight - 1 - t, stateAnchor ? FrameColor : new Color(0.35f, 0.35f, 0.38f));
                }
            }

            sheet.Apply();
            return sheet;
        }

        static string Describe(PioneerPresentation presentation, List<AnchorStats> stats, PioneerStageChecks.Report checks)
        {
            var c = CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.AppendLine("# PIO-003 · Palco espacial da Pioneer 10 — medições da prévia");
            text.AppendLine();
            text.AppendLine("Gerado por `PioneerStageCapture` (Editor, batchmode). **Estimativas** por renderer cujo AABB intersecta o frustum,");
            text.AppendLine("sem batching: não são números do profiler nem da build Web. Metas do roteiro: < 120 draw calls de conteúdo e");
            text.AppendLine("≤ 150 mil triângulos visíveis nos planos de Júpiter; uma luz principal sem sombra + um fill sem sombra.");
            text.AppendLine();
            text.AppendLine("| Anchor | Estado | Região | FOV | Renderers | Triângulos | Draw calls (est.) | Luzes ativas | Com sombra | Folga da sonda |");
            text.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var s in stats)
            {
                presentation.TryGetAnchor(s.AnchorId, out var anchor);
                var clearance = checks.ProbeClearance.TryGetValue(s.AnchorId, out var value) ? value.ToString("0.00", c) + " m" : "—";
                text.AppendLine($"| `{s.AnchorId}` | {anchor.stateId} | {anchor.region} | {anchor.fieldOfViewDegrees.ToString("0", c)}° | {s.Renderers} | {s.Triangles} | {s.DrawCallsEstimate} | {s.ActiveLights} | {s.ShadowedLights} | {clearance} |");
            }

            text.AppendLine();
            text.AppendLine($"Maior coordenada de anchor: {checks.LargestCoordinateMeters.ToString("0", c)} m (limite {PioneerStageChecks.MaxCoordinateMeters.ToString("0", c)} m).");
            text.AppendLine($"Rotação visual de cruzeiro: {presentation.SpinDegreesPerSecond.ToString("0.#", c)}°/s (valor de direção, não telemetria).");
            text.AppendLine();
            text.AppendLine("## Continuidade entre planos consecutivos");
            text.AppendLine();
            foreach (var line in checks.Lines)
                text.AppendLine("- " + line);
            text.AppendLine();
            text.AppendLine(checks.Errors.Count == 0 ? "Verificações geométricas: **conformes**." : "Verificações geométricas com erro:\n" + string.Join("\n", checks.Errors.Select(e => "- " + e)));
            return text.ToString().Replace("\r\n", "\n");
        }
    }
}
