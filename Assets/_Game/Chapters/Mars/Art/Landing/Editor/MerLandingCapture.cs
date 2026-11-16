using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-014: quadros de revisão do pouso (split Spirit em cima, Opportunity embaixo) pelo mesmo caminho do jogo: SequenceDirector.Preview
    /// para a câmera principal e a câmera secundária do diretor para o Spirit. Exige GPU (sem -nographics). Evidência, não teste.
    /// </summary>
    public static class MerLandingCapture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds => 0f;
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("MERLANDING_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("Defina MERLANDING_CAPTURE_DIR.");

                var timesText = Environment.GetEnvironmentVariable("MERLANDING_TIMES");
                var times = new List<float>();
                foreach (var part in (string.IsNullOrEmpty(timesText) ? "1.5,4.5,7,9,10.8,12.4,13.5,14.4,15.4,16.9,17.8,19.5,21.5,23,24.6,25.8,26.6,27" : timesText).Split(','))
                    times.Add(float.Parse(part, System.Globalization.CultureInfo.InvariantCulture));

                Capture(output, times);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static Texture2D Render(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var rect = camera.rect;
            var previous = camera.targetTexture;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previous;
            camera.rect = rect;
            UnityEngine.Object.DestroyImmediate(rt);
            return texture;
        }

        public static void Capture(string directory, List<float> times)
        {
            Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(MerLandingBuilder.PreviewScenePath, OpenSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<MarsDualLandingDirector>(FindObjectsInactive.Include);
            var cameraDirector = UnityEngine.Object.FindFirstObjectByType<CameraDirector>(FindObjectsInactive.Include);
            var sequenceDirector = UnityEngine.Object.FindFirstObjectByType<SequenceDirector>(FindObjectsInactive.Include);
            var main = Camera.main;
            cameraDirector.Initialize(main);
            sequenceDirector.Initialize(cameraDirector, null, new FakeTime());
            director.Initialize(sequenceDirector, cameraDirector);
            var data = director.Data;
            var sequences = new[]
            {
                AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M04")),
                AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M05")),
                AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M06")),
            };

            foreach (var t in times)
            {
                var index = t < data.SplitSeconds ? 0 : t < data.SplitSeconds + data.EagleSeconds ? 1 : 2;
                var local = t - data.OffsetOf(sequences[index].SequenceId);
                sequenceDirector.Preview(sequences[index], local);
                director.ApplyAt(t);

                var share = director.OpportunityWidth;
                var right = Render(main, Mathf.RoundToInt(1280 * share), 720);
                Texture2D left = null;
                if (director.SpiritCamera.enabled)
                    left = Render(director.SpiritCamera, Mathf.Max(8, Mathf.RoundToInt(1280 * (1f - share))), 720);

                var composed = new Texture2D(right.width + (left != null ? left.width : 0), 720, TextureFormat.RGB24, false);
                if (left != null)
                    composed.SetPixels(0, 0, left.width, 720, left.GetPixels());
                composed.SetPixels(left != null ? left.width : 0, 0, right.width, 720, right.GetPixels());
                if (left != null)
                {
                    // Linha divisória como a UI a desenha (2 px).
                    for (var y = 0; y < 720; y++)
                        for (var dx = -1; dx <= 0; dx++)
                            composed.SetPixel(left.width + dx, y, new Color(0.96f, 0.94f, 0.9f));
                }

                File.WriteAllBytes(Path.Combine(directory, $"t{t:00.0}.png"), composed.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(right);
                if (left != null) UnityEngine.Object.DestroyImmediate(left);
                UnityEngine.Object.DestroyImmediate(composed);
            }
        }
    }
}
