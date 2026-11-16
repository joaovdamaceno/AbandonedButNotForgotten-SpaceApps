using System;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>Montagem isolada de SUR-003; não registra cenas nem altera Boot/Dossier.</summary>
    public sealed class SurveyorLaunchBuilder : SceneBuilderModule
    {
        public const string Folder = "Assets/_Game/Chapters/Surveyor/Sequences";
        public const string Scene = Folder + "/Preview/SurveyorLaunchPreview.unity";
        public const string SequencePath = Folder + "/SurveyorLaunch.asset";
        public const string ChapterPath = Folder + "/Preview/SurveyorLaunchPreview.asset";
        public const string PrefabPath = Folder + "/SurveyorLaunch.prefab";
        public const string UiFolder = "Assets/_Game/Chapters/Surveyor/UI";
        public override string Id => "surveyor.launch";
        public override string Owner => "R6 · SUR-003";
        public override string ScenePath => Scene;
        public override string RootName => "[R6 SUR-003] Launch";
        public override string AssetFolder => Folder;

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-003 Reconstruir abertura")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new SurveyorLaunchBuilder());
            if (!result.Succeeded) throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public override void Build(SceneBuildContext context)
        {
            var sequence = Store(context, SequencePath, CreateSequence());
            var chapter = Store(context, ChapterPath, ChapterDefinition.Create("surveyor.launch.preview", "launch.mission",
                "launch.description", "launch.title", "SurveyorLaunchPreview", 28, sequence.SequenceId,
                new[] { new ChapterStep("Launch", false, "Cruise"), new ChapterStep("Cruise", false, "End"), new ChapterStep("End", true) }));
            var baseline = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset");
            if (baseline == null) throw new InvalidOperationException("UI-001 PanelSettings ausente.");
            var panel = context.Asset(Folder + "/LaunchPanelSettings.asset", () => Object.Instantiate(baseline));
            var audio = CreateBed();
            var prefab = context.Prefab(PrefabPath, root =>
            {
                var clock = Get<SessionClock>(root);
                var router = Get<InputRouter>(root);
                var interactions = Get<InteractionService>(root);
                var owner = Get<ChapterDirector>(root);
                var director = Get<CameraDirector>(root);
                var timeline = Get<SequenceDirector>(root);
                timeline.enabled = false;
                var flow = Get<Chapters.Surveyor.SurveyorLaunchController>(root);
                var view = Get<Chapters.Surveyor.LaunchTelemetryView>(root);
                var host = Get<Chapters.Surveyor.SurveyorLaunchPreviewHost>(root);
                var cameraTransform = root.transform.Find("Camera");
                if (cameraTransform == null)
                {
                    cameraTransform = new GameObject("Camera").transform;
                    cameraTransform.SetParent(root.transform, false);
                }
                cameraTransform.localPosition = new Vector3(0, 3, -10);
                cameraTransform.localRotation = Quaternion.identity;
                var camera = Get<Camera>(cameraTransform.gameObject);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f,.047f,.075f);
                camera.cullingMask = 0; camera.fieldOfView = 45;
                Get<AudioListener>(cameraTransform.gameObject);
                director.Initialize(camera);
                var document = Get<UIDocument>(root);
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder + "/LaunchTelemetry.uxml");
                Get<ResponsiveLayoutClass>(root);
                var source = Get<AudioSource>(root);
                source.clip = audio; source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.volume = 0;
                host.Configure(owner,clock,router,interactions,director,timeline,flow,view,document,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder + "/LaunchTexts.json"),chapter,sequence,source);
            });
            context.PrefabInstance(context.Root.transform, prefab, "SurveyorLaunch");
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath).Validate());
            errors.AddRange(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath).Validate());
            errors.AddRange(Chapters.Surveyor.SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder + "/LaunchTexts.json")).Validate());
            if (context.Root.GetComponentsInChildren<Camera>(true).Length != 1) errors.Add("A abertura precisa de uma câmera.");
            if (context.Root.GetComponentsInChildren<AudioListener>(true).Length != 1) errors.Add("AudioListener duplicado/ausente.");
        }

        public static SequenceDefinition CreateSequence()
        {
            ShotDefinition Shot(string id, float duration, params ShotEvent[] events) => new ShotDefinition(id,duration,CameraDirector.WorldRigId,
                new[] { new ShotKeyframe(0,new Vector3(0,3,-10),Vector3.zero,45), new ShotKeyframe(duration,new Vector3(0,3,-10),Vector3.zero,45) },events:events);
            return SequenceDefinition.Create("surveyor.seq.Launch", new[]
            {
                Shot("surveyor.launch.purpose",9),
                Shot("surveyor.launch.ascent",8,new ShotEvent(0,Chapters.Surveyor.SurveyorLaunchController.AscentEvent)),
                Shot("surveyor.launch.confirm",11,new ShotEvent(0,Chapters.Surveyor.SurveyorLaunchController.EnableCommandEvent),
                    new ShotEvent(5,Chapters.Surveyor.SurveyorLaunchController.SendCommandEvent),
                    new ShotEvent(6.3f,Chapters.Surveyor.SurveyorLaunchController.ExecuteCentaurEvent))
            },new[] { new SubtitleCue("SUR-S01","SUR-S01",0,8),new SubtitleCue("SUR-S02","SUR-S02",17,22),new SubtitleCue("SUR-S03","SUR-S03",22,28) });
        }

        static T Store<T>(SceneBuildContext context, string path, T template) where T : Object
        {
            var value = context.Asset(path, () => template);
            if (value != template) { EditorUtility.CopySerialized(template,value); Object.DestroyImmediate(template); }
            EditorUtility.SetDirty(value); return value;
        }
        static T Get<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }

        static AudioClip CreateBed()
        {
            const string path = Folder + "/Audio/LaunchBed.wav";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const int rate = 22050, samples = rate * 4;
                using (var w = new BinaryWriter(File.Create(path)))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
                    w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples*2);
                    for (int i=0;i<samples;i++)
                    {
                        double t=(double)i/rate;
                        double sound=.42*Math.Sin(2*Math.PI*55*t)+.16*Math.Sin(2*Math.PI*82.5*t)+.07*Math.Sin(2*Math.PI*110*t);
                        w.Write((short)(sound*short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;sample.compressionFormat = AudioCompressionFormat.Vorbis;sample.quality = .6f;
            importer.defaultSampleSettings = sample;importer.forceToMono = true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
