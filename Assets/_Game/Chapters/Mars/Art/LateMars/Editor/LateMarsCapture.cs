using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-016: capturas de revisão dos dois palcos com o rover real, limpo × denso nos dois perfis, para conferir que a névoa
    /// integra o rover (sem silhueta imune), que o Sol continua legível e que a luz recupera em 2007 e termina sem sinal em 2018.
    /// Exige GPU (sem -nographics). Evidência para revisão humana, não teste.
    /// </summary>
    public static class LateMarsCapture
    {
        const string RoverPath = "Assets/_Game/Chapters/Mars/Prefabs/OpportunityVisual.prefab";

        public static void CaptureFromCommandLine()
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("LATEMARS_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("Defina LATEMARS_CAPTURE_DIR.");

                Capture(output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Shot(Camera camera, RenderTexture rt, Texture2D texture, string path, Vector3 position, Quaternion rotation, float fov)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }

        public static void Capture(string directory)
        {
            Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(LateMarsBuilder.PreviewScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var rover = AssetDatabase.LoadAssetAtPath<GameObject>(RoverPath);
            var controllers = UnityEngine.Object.FindObjectsByType<MarsAtmosphereController>(FindObjectsInactive.Include);
            var created = new System.Collections.Generic.List<GameObject>();
            try
            {
                foreach (var controller in controllers)
                {
                    var valley = controller.EventId == MarsStormProfileData.Valley2018;
                    foreach (var other in controllers)
                        other.enabled = other == controller;

                    var anchors = controller.GetComponent<LateMarsStageAnchors>();
                    var start = anchors.RoverStart;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(rover);
                    instance.transform.SetPositionAndRotation(start.position, start.rotation);
                    created.Add(instance);

                    var levels = valley ? new[] { 0.12f, 0.7f, 1f } : new[] { 0f, 0.5f, 0.85f };
                    var prefix = valley ? "valley2018" : "victoria2007";
                    var back = start.position - start.forward * 6f + Vector3.up * 2.3f;
                    var look = Quaternion.LookRotation((start.position + start.forward * 8f + Vector3.up * 0.4f) - back);
                    foreach (var dust in levels)
                    {
                        controller.Snap(dust);
                        Shot(camera, rt, texture, Path.Combine(directory, $"{prefix}_rover_poeira_{(int)(dust * 100):000}.png"), back, look, 50f);
                    }

                    // Vistas amplas: a cratera de Victoria à frente; o vale desce à frente do mirante.
                    controller.Snap(levels[0]);
                    if (valley)
                    {
                        Shot(camera, rt, texture, Path.Combine(directory, "valley2018_vista_do_vale_limpo.png"), anchors.ValleyView.position, anchors.ValleyView.rotation, 55f);
                        Shot(camera, rt, texture, Path.Combine(directory, "valley2018_vista_do_sol_limpo.png"), anchors.SunView.position, anchors.SunView.rotation, 50f);
                        controller.Snap(0.7f);
                        Shot(camera, rt, texture, Path.Combine(directory, "valley2018_vista_do_sol_poeira_070.png"), anchors.SunView.position, anchors.SunView.rotation, 50f);
                    }
                    else
                    {
                        var high = start.position + Vector3.up * 3f;
                        Shot(camera, rt, texture, Path.Combine(directory, "victoria2007_borda_da_cratera.png"), high, Quaternion.LookRotation(new Vector3(LateMarsGeometry.VictoriaCenter.x, -8f, LateMarsGeometry.VictoriaCenter.y)), 60f);
                        // Junto ao promontório: o penhasco cai para a cratera e a borda oposta recortada fecha o horizonte.
                        var edge = start.position + start.forward * (LateMarsGeometry.RimDistanceFromRover - 3f) + Vector3.up * 1.7f;
                        Shot(camera, rt, texture, Path.Combine(directory, "victoria2007_penhasco.png"), edge, Quaternion.LookRotation(start.forward + Vector3.down * 0.18f), 70f);
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(texture);
                foreach (var go in created)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log("[MAR-016] capturas em " + directory);
        }
    }
}
