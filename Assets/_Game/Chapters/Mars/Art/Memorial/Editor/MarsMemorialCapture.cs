using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>MAR-019: quadros de revisão do memorial (M31–M32). Exige GPU. Evidência, não teste.</summary>
    public static class MarsMemorialCapture
    {
        public static void CaptureFromCommandLine()
        {
            try
            {
                var dir = Environment.GetEnvironmentVariable("MEMORIAL_CAPTURE_DIR");
                Directory.CreateDirectory(dir);
                EditorSceneManager.OpenScene(MarsMemorialBuilder.PreviewScenePath, OpenSceneMode.Single);
                var director = UnityEngine.Object.FindFirstObjectByType<MarsMemorialDirector>(FindObjectsInactive.Include);
                var cameraDirector = UnityEngine.Object.FindFirstObjectByType<CameraDirector>(FindObjectsInactive.Include);
                var sequenceDirector = UnityEngine.Object.FindFirstObjectByType<SequenceDirector>(FindObjectsInactive.Include);
                var main = Camera.main;
                cameraDirector.Initialize(main);
                sequenceDirector.Initialize(cameraDirector, null, null);
                director.Initialize(sequenceDirector, cameraDirector);
                director.SetMemory(null);
                var m31 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsMemorialBuilder.MainSequencePath);
                var m32 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsMemorialBuilder.TitleSequencePath);
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                foreach (var part in (Environment.GetEnvironmentVariable("MEMORIAL_TIMES") ?? "1,4,8,14,20,27,32").Split(','))
                {
                    var t = float.Parse(part, System.Globalization.CultureInfo.InvariantCulture);
                    if (t < MarsMemorialChoreography.MemorialSeconds) sequenceDirector.Preview(m31, t);
                    else sequenceDirector.Preview(m32, t - MarsMemorialChoreography.MemorialSeconds);
                    director.ApplyAt(t);
                    main.targetTexture = rt;
                    main.Render();
                    main.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, $"t{t:00.0}.png"), tex.EncodeToPNG());
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
