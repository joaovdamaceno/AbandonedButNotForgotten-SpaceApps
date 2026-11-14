using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.ShieldFieldGeometry;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-015: capturas de revisão (frente, lado, ângulo baixo, topo, rocha) da cena de prévia, para conferir apoio e
    /// leitura do hardware contra PIA07402/PIA07324. Exige GPU (sem -nographics). Não é teste: é evidência para revisão humana.
    /// </summary>
    public static class ShieldFieldCapture
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
                var output = Environment.GetEnvironmentVariable("SHIELD_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("Defina SHIELD_CAPTURE_DIR.");

                Capture(output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture(string directory)
        {
            Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(ShieldFieldBuilder.PreviewScenePath, OpenSceneMode.Single);
            var anchors = UnityEngine.Object.FindFirstObjectByType<ShieldFieldAnchors>();
            var camera = Camera.main;
            var main = G.MainPose.Origin;
            var shieldCenter = new Vector3((G.MainCenter.x + G.FlankCenter.x) * 0.5f, 0.35f, (G.MainCenter.y + G.FlankCenter.y) * 0.5f);
            var rock = anchors.MeteoritePhotoTarget.position;
            var views = new[]
            {
                new View { Name = "01_estacao_escudo", Position = anchors.ShieldCameraAnchor.position, Target = anchors.ShieldPhotoTarget.position, Fov = 46f },
                new View { Name = "02_lado_escudo", Position = shieldCenter + new Vector3(7.5f, 1.0f, 0.5f), Target = shieldCenter, Fov = 36f },
                new View { Name = "03_angulo_baixo", Position = G.Ground(G.MainCenter.x - 0.6f, G.MainCenter.y - 3.2f) + Vector3.up * 0.22f, Target = shieldCenter, Fov = 50f },
                new View { Name = "04_topo", Position = shieldCenter + new Vector3(0.2f, 11f, -0.5f), Target = shieldCenter, Fov = 38f },
                new View { Name = "05_rocha_estacao", Position = anchors.MeteoriteCameraAnchor.position, Target = rock, Fov = 38f },
                new View { Name = "06_rocha_proxima", Position = rock + new Vector3(0.5f, 0.28f, -0.55f), Target = rock, Fov = 38f },
                new View { Name = "07_panorama_campo", Position = new Vector3(2.2f, 1.7f, -4.5f), Target = new Vector3(0.5f, 0.3f, 3.5f), Fov = 62f },
            };

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render(); // o primeiro quadro ainda não tem o mapa de sombra: descarta
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var view in views)
            {
                camera.transform.position = view.Position;
                camera.transform.rotation = Quaternion.LookRotation(view.Target - view.Position, Vector3.up);
                camera.fieldOfView = view.Fov;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, view.Name + ".png"), texture.EncodeToPNG());
            }

            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("[MAR-015] capturas em " + directory + " · origem do destroço principal " + main);
        }
    }
}
