using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>MAR-017: quadros de revisão de M22 (Spirit em cima, Opportunity embaixo). Exige GPU. Evidência, não teste.</summary>
    public static class SpiritInterludeCapture
    {
        static Texture2D Render(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var rect = camera.rect;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            camera.rect = rect;
            UnityEngine.Object.DestroyImmediate(rt);
            return texture;
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                var dir = Environment.GetEnvironmentVariable("SPIRIT_CAPTURE_DIR");
                Directory.CreateDirectory(dir);
                EditorSceneManager.OpenScene(SpiritInterludeBuilder.PreviewScenePath, OpenSceneMode.Single);
                var director = UnityEngine.Object.FindFirstObjectByType<SpiritInterludeDirector>(FindObjectsInactive.Include);
                var cameraDirector = UnityEngine.Object.FindFirstObjectByType<CameraDirector>(FindObjectsInactive.Include);
                var sequenceDirector = UnityEngine.Object.FindFirstObjectByType<SequenceDirector>(FindObjectsInactive.Include);
                var main = Camera.main;
                cameraDirector.Initialize(main);
                sequenceDirector.Initialize(cameraDirector, null, null);
                director.Initialize(sequenceDirector, cameraDirector);
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SpiritInterludeBuilder.MainSequencePath);
                foreach (var part in (Environment.GetEnvironmentVariable("SPIRIT_TIMES") ?? "2,8,13,19,24,30,35,41,44").Split(','))
                {
                    var t = float.Parse(part, System.Globalization.CultureInfo.InvariantCulture);
                    sequenceDirector.Preview(sequence, t);
                    director.ApplyAt(t);
                    var h = director.OpportunityHeight;
                    var bottom = Render(main, 1280, Mathf.RoundToInt(720 * h));
                    Texture2D top = director.SpiritCamera.enabled ? Render(director.SpiritCamera, 1280, Mathf.Max(8, Mathf.RoundToInt(720 * (1f - h)))) : null;
                    var composed = new Texture2D(1280, bottom.height + (top != null ? top.height : 0), TextureFormat.RGB24, false);
                    composed.SetPixels(0, 0, 1280, bottom.height, bottom.GetPixels());
                    if (top != null) composed.SetPixels(0, bottom.height, 1280, top.height, top.GetPixels());
                    File.WriteAllBytes(Path.Combine(dir, $"t{t:00.0}.png"), composed.EncodeToPNG());
                }

                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
