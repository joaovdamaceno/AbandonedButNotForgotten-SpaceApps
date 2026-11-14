using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.EagleGeometry;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-012: capturas de revisão (plano elevado, rota, as duas estações, macros dos alvos e vistas baixas sob os afloramentos)
    /// para comparar com as imagens NASA citadas em Docs/Production/MARS_EAGLE.md. Exige GPU (sem -nographics). Não é teste:
    /// é evidência para revisão humana.
    /// </summary>
    public static class EagleCapture
    {
        struct View
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public float Fov;
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("EAGLE_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("Defina EAGLE_CAPTURE_DIR.");

                Capture(output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static Vector3 Eye(Vector2 xz, float height) => G.Ground(xz) + Vector3.up * height;

        public static void Capture(string directory)
        {
            Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(EagleBuilder.PreviewScenePath, OpenSceneMode.Single);
            var anchors = UnityEngine.Object.FindFirstObjectByType<EagleAnchors>();
            var camera = Camera.main;
            var ledge = G.BlueberriesLedge();
            var plan = G.RoutePlan;
            var curve = plan[Mathf.Min(plan.Count - 1, G.BlueberriesStationIndex + 6)];

            var views = new List<View>
            {
                new View { Name = "01_plano_elevado_M05", Position = anchors.OverviewCamera.position, Target = anchors.OverviewTarget.position, Fov = 52f },
                new View { Name = "02_partida_olhando_esferas", Position = Eye(G.Start, 1.5f), Target = anchors.Blueberries.PromptAnchor.position + Vector3.up * 0.4f, Fov = 60f },
                new View { Name = "03_estacao_esferas", Position = anchors.Blueberries.StationAnchor.position + Vector3.up * 1.5f, Target = anchors.Blueberries.PromptAnchor.position + Vector3.up * 0.3f, Fov = 55f },
                new View { Name = "04_macro_solo", Position = anchors.Blueberries.PrimaryView.position, Target = anchors.Blueberries.PrimaryView.position + anchors.Blueberries.PrimaryView.forward, Fov = 38f },
                new View { Name = "05_macro_rocha", Position = anchors.Blueberries.SecondaryView.position, Target = anchors.Blueberries.SecondaryView.position + anchors.Blueberries.SecondaryView.forward, Fov = 38f },
                new View { Name = "06_curva_antes_de_revelar", Position = Eye(curve, 1.5f), Target = Eye(G.McKittrick, 0.4f), Fov = 60f },
                new View { Name = "07_estacao_mckittrick", Position = anchors.McKittrick.StationAnchor.position + Vector3.up * 1.5f, Target = anchors.McKittrick.PromptAnchor.position, Fov = 55f },
                new View { Name = "08_macro_mckittrick", Position = anchors.McKittrick.PrimaryView.position, Target = anchors.McKittrick.PrimaryView.position + anchors.McKittrick.PrimaryView.forward, Fov = 38f },
                new View { Name = "09_angulo_baixo_afloramento", Position = G.Ground(G.BlueberriesToe - ledge.Normal * 2.4f + ledge.Tangent * 2f) + Vector3.up * 0.12f, Target = G.Ground(G.BlueberriesToe + ledge.Tangent * -1f) + Vector3.up * 0.3f, Fov = 60f },
                new View { Name = "10_angulo_baixo_rochas", Position = Eye(G.Start, 0.15f), Target = Eye(new Vector2(-3f, 7f), 0.3f), Fov = 70f },
                new View { Name = "11_topo", Position = new Vector3(0f, 20f, 0.1f) + Vector3.down * 2.9f, Target = G.Ground(Vector2.zero), Fov = 60f },
            };

            const int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render(); // o primeiro quadro ainda não tem o mapa de sombra: descarta
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            foreach (var view in views)
            {
                camera.transform.position = view.Position;
                camera.transform.rotation = Quaternion.LookRotation(view.Target - view.Position, Vector3.up);
                camera.fieldOfView = view.Fov;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, view.Name + ".png"), texture.EncodeToPNG());
            }

            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("[MAR-012] capturas em " + directory);
        }
    }
}
