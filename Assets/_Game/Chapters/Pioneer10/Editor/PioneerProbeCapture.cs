using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-002 · Prévia do prefab da sonda: renderiza vistas fixas e poses do rig a 1280×720 em Docs/Art/Pioneer10_Probe
    /// (câmera e luz temporárias; a cena não é salva). Evidência de montagem e escala, não de qualidade final.
    /// Menu: Abandoned Space Apps → PIO-002 → Capturar sonda. Linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.PioneerProbeCapture.CaptureFromCommandLine
    /// </summary>
    public static class PioneerProbeCapture
    {
        public const string OutputFolder = "Docs/Art/Pioneer10_Probe";
        const int Width = 1280;
        const int Height = 720;

        struct Frame
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public float Fov;
            public float SpinDegrees;
            public float BoomDegrees;
        }

        static readonly Frame[] Frames =
        {
            new Frame { Name = "01_frente_boca_do_refletor", Position = new Vector3(0f, 0.4f, 9.5f), Target = new Vector3(0f, -0.3f, 0f), Fov = 38f },
            new Frame { Name = "02_lado", Position = new Vector3(9f, 0.2f, 0.4f), Target = new Vector3(0f, -0.3f, 0.3f), Fov = 38f },
            new Frame { Name = "03_tres_quartos", Position = new Vector3(5.5f, 3.2f, 7f), Target = new Vector3(0f, -0.6f, 0.4f), Fov = 40f },
            new Frame { Name = "04_atras_corpo_e_hastes", Position = new Vector3(-4.2f, 2.6f, -6.5f), Target = new Vector3(0f, -0.3f, 0f), Fov = 40f },
            new Frame { Name = "05_placa_close", Position = new Vector3(1.55f, -0.15f, -0.62f), Target = new Vector3(1.05f, 0f, 0.24f), Fov = 30f },
            new Frame { Name = "05b_placa_contexto", Position = new Vector3(3.4f, 1.4f, -2.6f), Target = new Vector3(0.9f, 0f, 0.1f), Fov = 36f },
            new Frame { Name = "06_haste_rtg_cruzeiro", Position = new Vector3(3.2f, 2.4f, -1.2f), Target = new Vector3(1.4f, 0.8f, 0f), Fov = 40f },
            new Frame { Name = "07_haste_rtg_45graus", Position = new Vector3(3.2f, 2.4f, -1.2f), Target = new Vector3(1.4f, 0.8f, 0f), Fov = 40f, BoomDegrees = 45f },
            new Frame { Name = "08_spin_90graus", Position = new Vector3(5.5f, 3.2f, 7f), Target = new Vector3(0f, -0.6f, 0.4f), Fov = 40f, SpinDegrees = 90f },
        };

        [MenuItem("Abandoned Space Apps/PIO-002/Capturar sonda")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorUtility.DisplayDialog("PIO-002", $"{Capture()} captura(s) em {OutputFolder}.", "OK");
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
                Debug.LogError("PIO-002 · captura falhou: " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static int Capture()
        {
            var scene = EditorSceneManager.OpenScene(PioneerProbeBuilder.PreviewScenePath, OpenSceneMode.Single);
            var rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerProbeRig>(true)).Single();

            var cameraObject = new GameObject("PIO-002 capture camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            var lightObject = new GameObject("PIO-002 capture light") { hideFlags = HideFlags.DontSave };
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
            var ambient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);

            Directory.CreateDirectory(OutputFolder);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PIO-002 capture" };
            var previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var written = 0;
            try
            {
                foreach (var frame in Frames)
                {
                    rig.SetCruise();
                    rig.SetSpinDegrees(frame.SpinDegrees);
                    if (frame.BoomDegrees > 0f)
                        rig.SetBoomDegrees(PioneerBoom.RtgBoomA, frame.BoomDegrees);

                    cameraObject.transform.position = frame.Position;
                    cameraObject.transform.rotation = Quaternion.LookRotation(frame.Target - frame.Position, Vector3.up);
                    camera.fieldOfView = frame.Fov;
                    var image = Render(camera, target);
                    File.WriteAllBytes(Path.Combine(OutputFolder, frame.Name + ".png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    written++;
                }

                File.WriteAllText(Path.Combine(OutputFolder, "PROBE_STATS.md"), Describe(rig), new System.Text.UTF8Encoding(false));
                written++;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                RenderSettings.ambientLight = ambient;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                EditorSceneManager.OpenScene(PioneerProbeBuilder.PreviewScenePath, OpenSceneMode.Single); // descarta as poses
            }

            Debug.Log($"PIO-002 · {written} arquivo(s) em {OutputFolder}.");
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

        static string Describe(PioneerProbeRig rig)
        {
            rig.SetCruise();
            rig.SetSpinDegrees(0f);
            var report = PioneerProbeChecks.Run(rig);
            var text = new System.Text.StringBuilder();
            text.AppendLine("# PIO-002 · Sonda Pioneer 10 — medições do prefab");
            text.AppendLine();
            text.AppendLine("Gerado por `PioneerProbeCapture` a partir de `Art/PioneerActors.prefab`. Medidas de caixa (AABB) em metros, no espaço do `Spin_Root`.");
            text.AppendLine();
            text.AppendLine("- " + PioneerProbeChecks.Describe(report));
            text.AppendLine("- Meta do LOD0: até 60 mil triângulos; materiais: até 3.");
            foreach (var line in report.Lines)
                text.AppendLine("- " + line);
            text.AppendLine();
            text.AppendLine(report.Errors.Count == 0 ? "Verificações: **conformes**." : "Verificações com erro:\n" + string.Join("\n", report.Errors.Select(e => "- " + e)));
            return text.ToString().Replace("\r\n", "\n");
        }
    }
}
