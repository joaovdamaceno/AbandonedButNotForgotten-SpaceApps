using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>MAR-013: quadros de revisão de M01–M03 pelo mesmo caminho do jogo (SequenceDirector.Preview). Exige GPU. Evidência, não teste.</summary>
    public static class MarsOriginCapture
    {
        public static void CaptureFromCommandLine()
        {
            try
            {
                var dir = Environment.GetEnvironmentVariable("MARSORIGIN_CAPTURE_DIR");
                var times = new List<float>();
                foreach (var part in (Environment.GetEnvironmentVariable("MARSORIGIN_TIMES") ?? "1,6,9,13,18,21,24").Split(','))
                    times.Add(float.Parse(part, System.Globalization.CultureInfo.InvariantCulture));

                Directory.CreateDirectory(dir);
                EditorSceneManager.OpenScene(MarsOriginBuilder.PreviewScenePath, OpenSceneMode.Single);
                var director = UnityEngine.Object.FindFirstObjectByType<MarsOriginDirector>(FindObjectsInactive.Include);
                var cameraDirector = UnityEngine.Object.FindFirstObjectByType<CameraDirector>(FindObjectsInactive.Include);
                var sequenceDirector = UnityEngine.Object.FindFirstObjectByType<SequenceDirector>(FindObjectsInactive.Include);
                var main = Camera.main;
                cameraDirector.Initialize(main);
                sequenceDirector.Initialize(cameraDirector, null, null);
                director.Initialize(sequenceDirector, cameraDirector);
                var sequences = new[] { "M01", "M02", "M03" };
                var offsets = new[] { 0f, MarsOriginChoreography.MapStart, MarsOriginChoreography.LaunchStart };
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                foreach (var t in times)
                {
                    var i = t < offsets[1] ? 0 : t < offsets[2] ? 1 : 2;
                    var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsOriginBuilder.SequencePath(sequences[i]));
                    sequenceDirector.Preview(sequence, t - offsets[i]);
                    director.ApplyAt(t);
                    main.targetTexture = rt;
                    main.Render();
                    main.Render();
                    RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(dir, $"t{t:00.0}.png"), texture.EncodeToPNG());
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
