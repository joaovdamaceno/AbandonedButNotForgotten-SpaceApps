using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    /// <summary>
    /// SUR-009: cena isolada de preview (LunarSite + rig + Trench + ScoopRig), capturas no Editor e build WebGL só desta cena.
    /// Não altera Boot, Dossier nem BuildSettings (o build passa a cena em BuildPlayerOptions).
    /// </summary>
    public static class SurveyorScoopPreviewBuilder
    {
        public const string ScenePath = "Assets/_Game/Chapters/Surveyor/Scoop/Preview/SurveyorScoopPreview.unity";
        const string Art = SurveyorScoopBuilder.ArtPath;
        const string QaFolder = "QA/SUR-009";
        public const string WebOutput = "Builds/SUR-009-Web";

        static GameObject Instantiate(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Prefab ausente: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-009/Criar cena de preview")]
        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorScoopBuilder.TrenchPrefabPath) == null) SurveyorScoopBuilder.BuildAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("[R6 SUR-009] Preview");
            var site = Instantiate(Art + "/LunarSite.prefab", root.transform);
            var visible = Instantiate(Art + "/SurveyorVisibleRig.prefab", root.transform);
            var trench = Instantiate(SurveyorScoopBuilder.TrenchPrefabPath, root.transform);
            var rig = Instantiate(SurveyorScoopBuilder.RigPrefabPath, root.transform);

            var cameraObject = new GameObject("Preview Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.nearClipPlane = .02f; camera.farClipPlane = 360f; camera.allowHDR = false; camera.allowMSAA = false;
            cameraObject.AddComponent<AudioListener>();
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.renderShadows = true;

            var sunObject = new GameObject("Sun · single shadow light");
            sunObject.transform.SetParent(root.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(23f, 145f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, .965f, .91f); sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .93f; sun.shadowBias = .025f; sun.shadowNormalBias = .25f;
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.075f, .088f, .11f);
            RenderSettings.reflectionIntensity = .2f;

            var scoop = root.AddComponent<SurveyorScoop>();
            var preview = root.AddComponent<SurveyorScoopWebPreview>();
            preview.Configure(scoop, trench, rig, site, visible, camera);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SUR-009: cena de preview criada em " + ScenePath);
        }

        /// <summary>Capturas 960×540 do Editor: escavação e recolhimento a cada 10 %, ângulo baixo e câmera da TV.</summary>
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-009/Capturar poses no Editor")]
        public static void CaptureEditor()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var preview = UnityEngine.Object.FindFirstObjectByType<SurveyorScoopWebPreview>();
            var camera = Camera.main;
            string folder = Path.Combine(QaFolder, "Captures", "Editor");
            Directory.CreateDirectory(folder);
            var texture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var report = new System.Text.StringBuilder();
            try
            {
                foreach (var cam in new[] { "low", "tv" })
                    for (var i = 0; i <= 10; i++)
                        foreach (var retract in new[] { false, true })
                        {
                            float f = i / 10f;
                            preview.ApplyState(retract ? 1f : f, retract ? f : -1f, cam);
                            camera.targetTexture = texture; camera.Render();
                            RenderTexture.active = texture;
                            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                            string name = (retract ? "retract" : "dig") + "-" + (i * 10).ToString("000") + "-" + cam + ".png";
                            File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image);
                            report.AppendLine(name + " " + preview.Report);
                        }
            }
            finally { camera.targetTexture = null; RenderTexture.active = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            File.WriteAllText(Path.Combine(folder, "capture-report.txt"), report.ToString());
            Debug.Log("SUR-009: capturas em " + folder);
        }

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-009/Trocar plataforma para WebGL")]
        public static void SwitchToWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL) { Debug.Log("SUR-009: WebGL já está ativo."); return; }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        /// <summary>Build WebGL somente da cena de preview. Troque a plataforma antes e espere a importação terminar.</summary>
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-009/Build WebGL do preview")]
        public static void BuildWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Troque a plataforma para WebGL (menu SUR-009) e espere a importação antes do build.");
            if (!File.Exists(ScenePath)) BuildScene();
            Directory.CreateDirectory(WebOutput);
            var options = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = WebOutput, target = BuildTarget.WebGL, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Directory.CreateDirectory(QaFolder);
            File.WriteAllText(Path.Combine(QaFolder, "web-build.json"),
                "{\"result\":\"" + summary.result + "\",\"totalSizeBytes\":" + summary.totalSize + ",\"errors\":" + summary.totalErrors + ",\"warnings\":" + summary.totalWarnings +
                ",\"seconds\":" + summary.totalTime.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ",\"output\":\"" + WebOutput + "\"}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Build WebGL: " + summary.result);
            Debug.Log("SUR-009: build WebGL em " + WebOutput);
        }
    }
}
